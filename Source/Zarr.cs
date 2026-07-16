using AForge;
using BioLib;
using ZarrNET.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZarrNET;
using ZarrNET.Core;
using ZarrNET.Core.OmeZarr.Metadata;
using ZarrNET.Core.OmeZarr.Coordinates;
using static BioLib.ROI;

namespace BioLib
{
    /// <summary>
    /// Bridges BioLib's BioImage to the OmeZarrWriter, exporting a full 5D
    /// image as an OME-Zarr v3 dataset on local disk.
    ///
    /// XY planes are written in tile-sized chunks so that even whole-slide
    /// images never require a full plane to be resident in memory at once.
    /// Peak allocation is bounded to a single tile (tileW × tileH × bytesPerSample).
    ///
    /// RGB-interleaved buffers are deinterleaved into separate C planes so
    /// the output array shape is always (T, C, Z, Y, X) with scalar pixels.
    ///
    /// All resolution levels present in b.Resolutions are written, producing
    /// a proper OME-NGFF multiscale pyramid.
    /// </summary>
    public static class Zarr
    {
        private static int s_debugTileLogs = 0;

        private static void Log(string msg)
        {
            try
            {
                File.AppendAllText(@"log.txt", msg + Environment.NewLine);
            }
            catch
            {
            }
        }

        private static string SampleBytes(byte[] data, int count = 16)
        {
            if (data == null || data.Length == 0)
                return "<empty>";

            int n = Math.Min(count, data.Length);
            var parts = new List<string>(n);
            for (int i = 0; i < n; i++)
                parts.Add(data[i].ToString(CultureInfo.InvariantCulture));
            return string.Join(",", parts);
        }

        private static string SampleU16(byte[] data, int count = 8)
        {
            if (data == null || data.Length < 2)
                return "<empty>";

            int available = data.Length / 2;
            int n = Math.Min(count, available);
            var parts = new List<string>(n);
            for (int i = 0; i < n; i++)
            {
                ushort v = BitConverter.ToUInt16(data, i * 2);
                parts.Add(v.ToString(CultureInfo.InvariantCulture));
            }
            return string.Join(",", parts);
        }

        private static string MinMaxU16(byte[] data)
        {
            if (data == null || data.Length < 2)
                return "<empty>";

            int available = data.Length / 2;
            ushort min = ushort.MaxValue;
            ushort max = ushort.MinValue;
            for (int i = 0; i < available; i++)
            {
                ushort v = BitConverter.ToUInt16(data, i * 2);
                if (v < min) min = v;
                if (v > max) max = v;
            }
            return $"{min}-{max}";
        }

        private static string MinMaxU8(byte[] data)
        {
            if (data == null || data.Length == 0)
                return "<empty>";

            byte min = byte.MaxValue;
            byte max = byte.MinValue;
            for (int i = 0; i < data.Length; i++)
            {
                byte v = data[i];
                if (v < min) min = v;
                if (v > max) max = v;
            }
            return $"{min}-{max}";
        }

        // =====================================================================
        // Public entry point
        // =====================================================================

        /// <summary>
        /// Saves the full 5D content of a <see cref="BioImage"/> as an
        /// OME-Zarr v3 dataset at <paramref name="outputDir"/>.
        /// All pyramid resolution levels in <c>b.Resolutions</c> are written.
        /// </summary>
        public static async void SaveZarr(BioImage b, string outputDir)
        {
            // ------------------------------------------------------------------
            // 1. Resolve dimensions and data type from the BioImage
            // ------------------------------------------------------------------

            // Guard against zero-size dimensions which cause ZarrNET to throw
            // "regionStart[0] = 0 is out of bounds [0, 0)".
            if (b.SizeX <= 0 || b.SizeY <= 0 || b.SizeZ <= 0 || b.SizeC <= 0 || b.SizeT <= 0)
                throw new InvalidOperationException(
                    $"BioImage has invalid dimensions: X={b.SizeX} Y={b.SizeY} Z={b.SizeZ} C={b.SizeC} T={b.SizeT}. " +
                    "Ensure the image is fully loaded before saving as Zarr.");

            // Derive the saved scalar bit depth from BioImage metadata instead of
            // the current display buffer.  The display buffer may be 8-bit even
            // when the source image is 16-bit, which would incorrectly write the
            // Zarr as uint8 and make it appear nearly black on reopen.
            int bitsPerSample = DetermineSourceBitsPerSample(b);
            var sourcePixelFormat = DetermineSourcePixelFormat(b);
            bool sourceIsInterleavedRgb = IsInterleavedRgbFormat(sourcePixelFormat);
            int sourceChannelCount = sourceIsInterleavedRgb ? BioImage.GetBands(sourcePixelFormat) : 1;
            bool sourceIsBGRA = IsBGRAFormat(sourcePixelFormat);
            int exportChannelCount = sourceIsInterleavedRgb && b.SizeC == 1
                ? Math.Min(3, sourceChannelCount)
                : b.SizeC;

            var bytesPerSample = bitsPerSample / 8;
            var zarrDataType   = MapDataType(bitsPerSample);
            var totalChannels  = exportChannelCount;

            int tileW = 512;
            int tileH = 512;

            s_debugTileLogs = 0;
            Log($"[SaveZarr] file={b.Filename} out={outputDir} size={b.SizeX}x{b.SizeY} zct={b.SizeZ}/{b.SizeC}/{b.SizeT} " +
                $"bits={bitsPerSample} bytesPerSample={bytesPerSample} srcPixFmt={sourcePixelFormat} " +
                $"rgb={sourceIsInterleavedRgb} bands={sourceChannelCount}");
            BioImage.Progress = 0;
            BioImage.Status = "Preparing Zarr writer";

            // ------------------------------------------------------------------
            // 2. Build per-level descriptors from b.Resolutions
            //    Level 0 is always the full-resolution image (b.SizeX / b.SizeY).
            //    Subsequent levels are taken directly from b.Resolutions[1..N].
            // ------------------------------------------------------------------

            var levelDescriptors = new List<ResolutionLevelDescriptor>();
            int lastSizeX = int.MaxValue;
            int lastSizeY = int.MaxValue;
            bool haveLast = false;
            var basePixelFormat = b.Resolutions.Count > 0 ? b.Resolutions[0].PixelFormat : DetermineSourcePixelFormat(b);

            for (int lvl = 0; lvl < b.Resolutions.Count; lvl++)
            {
                var res = b.Resolutions[lvl];
                if (res.SizeX <= 0 || res.SizeY <= 0)
                    continue;

                if (haveLast && res.PixelFormat != basePixelFormat)
                    break;

                if (haveLast && (res.SizeX > lastSizeX || res.SizeY > lastSizeY))
                    break;

                if (haveLast && res.SizeX == lastSizeX && res.SizeY == lastSizeY)
                    continue;

                double dsample = b.GetLevelDownsample(lvl);
                if (!double.IsFinite(dsample) || dsample <= 0)
                    dsample = 1.0;

                levelDescriptors.Add(new ResolutionLevelDescriptor(res.SizeX, res.SizeY, dsample));
                Log($"[SaveZarr] level={lvl} size={res.SizeX}x{res.SizeY} downsample={dsample} fmt={res.PixelFormat}");
                lastSizeX = res.SizeX;
                lastSizeY = res.SizeY;
                haveLast = true;
            }

            // Fallback: if there is somehow no Resolutions list, use the image dims.
            var coord = new ZarrNET.Core.ZCT(b.SizeZ, totalChannels, b.SizeT);

            var baseDescriptor = new BioImageDescriptor(b.SizeX, b.SizeY, coord)
            {
                Name          = Path.GetFileNameWithoutExtension(b.Filename),
                DataType      = zarrDataType,
                PhysicalSizeX = b.PhysicalSizeX,
                PhysicalSizeY = b.PhysicalSizeY,
                PhysicalSizeZ = b.PhysicalSizeZ,
                ChunkY        = tileH,
                ChunkX        = tileW,
            };

            // ------------------------------------------------------------------
            // 4. Create the writer (bootstraps zarr.json metadata on disk for
            //    all levels at once)
            // ------------------------------------------------------------------

            var writer = OmeZarrWriter.CreateAsync(outputDir, baseDescriptor, levelDescriptors).Result;

            try
            {
                // ------------------------------------------------------------------
                // 5. Stream tiles into each resolution level
                // ------------------------------------------------------------------
                long totalPlanes = Math.Max(1L, (long)levelDescriptors.Count * b.SizeT * b.SizeZ * b.SizeC);
                long writtenPlanes = 0;

                for (int levelIndex = 0; levelIndex < levelDescriptors.Count; levelIndex++)
                {
                    var lvlDesc = levelDescriptors[levelIndex];
                    BioImage.Status = $"Writing resolution level {levelIndex + 1}/{levelDescriptors.Count}";

                    for (int t = 0; t < b.SizeT; t++)
                    {
                        for (int z = 0; z < b.SizeZ; z++)
                        {
                            for (int c = 0; c < exportChannelCount; c++)
                            {
                                BioImage.Status = $"Writing level {levelIndex + 1}/{levelDescriptors.Count} plane T{t + 1}/{b.SizeT} Z{z + 1}/{b.SizeZ} C{c + 1}/{b.SizeC}";
                                if (sourceIsInterleavedRgb)
                                {
                                    await WritePlaneInTiles(
                                        writer, b, lvlDesc,
                                        t, c, z,
                                        bytesPerSample, tileW, tileH,
                                        levelIndex: levelIndex,
                                        rgbChannelIndex: c,
                                        srcChannelCount: sourceChannelCount,
                                        isBGRA: sourceIsBGRA,
                                        logicalC: 0);
                                }
                                else
                                {
                                    await WritePlaneInTiles(
                                        writer, b, lvlDesc,
                                        t, c, z,
                                        bytesPerSample, tileW, tileH,
                                        levelIndex: levelIndex);
                                }

                                writtenPlanes++;
                                BioImage.Progress = (float)(writtenPlanes * 100.0 / totalPlanes);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            try
            {
                BioImage.Status = "Verifying saved Zarr";
                PostSaveReadback(outputDir).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Log($"[PostSaveReadback] EXCEPTION: {e.GetType().Name}: {e.Message}");
            }

            Recorder.Record($"Zarr.SaveZarr({b}, \"{outputDir}\");");
            BioImage.Progress = 100;
        }

        /// <summary>
        /// Saves already-processed pyramid levels as an OME-Zarr multiscale
        /// dataset. Each entry in <paramref name="levels"/> is written as one
        /// resolution level in the output pyramid.
        /// </summary>
        public static async Task SavePyramidalZarr(BioImage[] levels, string outputDir)
        {
            if (levels == null || levels.Length == 0)
                throw new ArgumentException("No pyramid levels were provided.", nameof(levels));

            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
            if (File.Exists(outputDir))
                File.Delete(outputDir);

            var root = levels[0];
            if (root == null)
                throw new ArgumentNullException(nameof(levels));

            if (root.SizeX <= 0 || root.SizeY <= 0 || root.SizeZ <= 0 || root.SizeC <= 0 || root.SizeT <= 0)
                throw new InvalidOperationException(
                    $"Invalid pyramid root dimensions: X={root.SizeX} Y={root.SizeY} Z={root.SizeZ} C={root.SizeC} T={root.SizeT}.");

            int bitsPerSample = DetermineSourceBitsPerSample(root);
            var sourcePixelFormat = DetermineSourcePixelFormat(root);
            bool sourceIsInterleavedRgb = IsInterleavedRgbFormat(sourcePixelFormat);
            int sourceChannelCount = sourceIsInterleavedRgb ? BioImage.GetBands(sourcePixelFormat) : 1;
            bool sourceIsBGRA = IsBGRAFormat(sourcePixelFormat);
            int bytesPerSample = bitsPerSample / 8;
            int tileW = 512;
            int tileH = 512;

            BioImage.Progress = 0;
            BioImage.Status = "Preparing pyramid Zarr writer";

            var levelDescriptors = new List<ResolutionLevelDescriptor>();
            int lastSizeX = int.MaxValue;
            int lastSizeY = int.MaxValue;
            bool haveLast = false;
            var basePixelFormat = DetermineSourcePixelFormat(root);

            for (int lvl = 0; lvl < levels.Length; lvl++)
            {
                BioImage levelImage = levels[lvl];
                if (levelImage == null)
                    continue;

                int levelWidth =
                    levelImage.Resolutions.Count > 0 && levelImage.Resolutions[0].SizeX > 0
                        ? levelImage.Resolutions[0].SizeX
                        : levelImage.SizeX;
                int levelHeight =
                    levelImage.Resolutions.Count > 0 && levelImage.Resolutions[0].SizeY > 0
                        ? levelImage.Resolutions[0].SizeY
                        : levelImage.SizeY;
                if (levelWidth <= 0 || levelHeight <= 0)
                    continue;

                var levelPixelFormat = DetermineSourcePixelFormat(levelImage);
                if (haveLast && levelPixelFormat != basePixelFormat)
                    break;

                if (haveLast && (levelWidth > lastSizeX || levelHeight > lastSizeY))
                    break;

                if (haveLast && levelWidth == lastSizeX && levelHeight == lastSizeY)
                    continue;

                double dsample = root.SizeX > 0 ? (double)root.SizeX / levelWidth : 1.0;
                if (!double.IsFinite(dsample) || dsample <= 0)
                    dsample = 1.0;

                levelDescriptors.Add(new ResolutionLevelDescriptor(levelWidth, levelHeight, dsample));
                Log($"[SaveZarr] level={lvl} size={levelWidth}x{levelHeight} downsample={dsample} fmt={levelPixelFormat}");
                lastSizeX = levelWidth;
                lastSizeY = levelHeight;
                haveLast = true;
            }

            if (levelDescriptors.Count == 0)
                levelDescriptors.Add(new ResolutionLevelDescriptor(root.SizeX, root.SizeY, 1.0));

            // Fallback: if there is somehow no Resolutions list, use the image dims.
            var coord = new ZarrNET.Core.ZCT(root.SizeZ, root.SizeC, root.SizeT);
            var baseDescriptor = new BioImageDescriptor(root.SizeX, root.SizeY, coord)
            {
                Name = Path.GetFileNameWithoutExtension(root.Filename),
                DataType = MapDataType(bitsPerSample),
                PhysicalSizeX = root.PhysicalSizeX,
                PhysicalSizeY = root.PhysicalSizeY,
                PhysicalSizeZ = root.PhysicalSizeZ,
                ChunkY = tileH,
                ChunkX = tileW,
            };

            var writer = await OmeZarrWriter.CreateAsync(outputDir, baseDescriptor, levelDescriptors).ConfigureAwait(false);
            try
            {
                long totalPlanes = Math.Max(1L, (long)levelDescriptors.Count * root.SizeT * root.SizeZ * root.SizeC);
                long writtenPlanes = 0;

                for (int levelIndex = 0; levelIndex < levels.Length; levelIndex++)
                {
                    BioImage levelImage = levels[levelIndex];
                    if (levelImage == null)
                        continue;

                    ResolutionLevelDescriptor lvlDesc = levelDescriptors[Math.Min(levelIndex, levelDescriptors.Count - 1)];
                    int levelBitsPerSample = DetermineSourceBitsPerSample(levelImage);
                    int levelBytesPerSample = levelBitsPerSample / 8;
                    var levelPixelFormat = DetermineSourcePixelFormat(levelImage);
                    bool levelIsInterleavedRgb = IsInterleavedRgbFormat(levelPixelFormat);
                    int levelChannelCount = levelIsInterleavedRgb ? BioImage.GetBands(levelPixelFormat) : 1;
                    bool levelIsBGRA = IsBGRAFormat(levelPixelFormat);

                    BioImage.Status = $"Writing pyramid level {levelIndex + 1}/{levels.Length}";

                    for (int t = 0; t < levelImage.SizeT; t++)
                    {
                        for (int z = 0; z < levelImage.SizeZ; z++)
                        {
                            for (int c = 0; c < levelImage.SizeC; c++)
                            {
                                if (levelIsInterleavedRgb)
                                {
                                    await WritePlaneInTiles(
                                        writer, levelImage, lvlDesc,
                                        t, c, z,
                                        levelBytesPerSample, tileW, tileH,
                                        levelIndex: levelIndex,
                                        rgbChannelIndex: c,
                                        srcChannelCount: levelChannelCount,
                                        isBGRA: levelIsBGRA,
                                        logicalC: 0).ConfigureAwait(false);
                                }
                                else
                                {
                                    // Pyramid results are full-frame level images. Their
                                    // PyramidalOrigin reflects viewport state, not where the
                                    // level pixels should be placed in the output pyramid.
                                    int originX = 0;
                                    int originY = 0;

                                    await WritePlaneFromBuffer(
                                        writer, levelImage, lvlDesc,
                                        t, c, z,
                                        levelBytesPerSample, tileW, tileH,
                                        levelIndex: levelIndex,
                                        originX: originX,
                                        originY: originY,
                                        sourceLevelIndex: 0).ConfigureAwait(false);
                                }

                                writtenPlanes++;
                                BioImage.Progress = (float)(writtenPlanes * 100.0 / totalPlanes);
                            }
                        }
                    }
                }
            }
            finally
            {
                try
                {
                    await writer.DisposeAsync().ConfigureAwait(false);
                }
                catch
                {
                }
            }

            Recorder.Record($"Zarr.SavePyramidalZarr(new BioImage[] {{ {string.Join(", ", levels.Select(level => level.ToString()))} }}, \"{outputDir}\");");
            BioImage.Progress = 100;
        }

        private static bool IsAssociatedResolution(BioImage image, int sourceIndex)
        {
            if (image == null)
                return false;

            return (image.MacroResolution.HasValue && image.MacroResolution.Value == sourceIndex) ||
                   (image.LabelResolution.HasValue && image.LabelResolution.Value == sourceIndex);
        }

        // =====================================================================
        // Tiled plane writer
        // =====================================================================

        /// <summary>
        /// Walks the XY extent of a single (t, c, z) plane in tile-sized
        /// chunks at the given resolution level, fetching each tile via
        /// BioImage.GetTile and writing it as a sub-region into the Zarr array.
        /// </summary>
        private static async Task WritePlaneInTiles(
            OmeZarrWriter              writer,
            BioImage                   b,
            ResolutionLevelDescriptor  lvlDesc,
            int t, int c, int z,
            int bytesPerSample,
            int tileW, int tileH,
            int levelIndex      = 0,
            int sourceLevelIndex = -1,
            int originX = 0,
            int originY = 0,
            int rgbChannelIndex = -1,
            int srcChannelCount = 1,
            bool isBGRA         = false,
            int logicalC        = -1)
        {
            bool needsDeinterleave = rgbChannelIndex >= 0;
            int  srcC              = needsDeinterleave ? logicalC : c;
            int  tileLevel         = sourceLevelIndex >= 0 ? sourceLevelIndex : levelIndex;

            for (int tileY = 0; tileY < lvlDesc.SizeY; tileY += tileH)
            {
                for (int tileX = 0; tileX < lvlDesc.SizeX; tileX += tileW)
                {
                    // Clamp to image bounds — edge tiles may be smaller.
                    int actualW = Math.Min(tileW, lvlDesc.SizeX - tileX);
                    int actualH = Math.Min(tileH, lvlDesc.SizeY - tileY);
                    int pixelCount         = actualW * actualH;
                    int interleavedBytes   = pixelCount * srcChannelCount * bytesPerSample;
                    int singleChannelBytes = pixelCount * bytesPerSample;

                    // Fetch raw tile bytes from BioLib at the requested pyramid level.
                    byte[] pixelBytes;
                    try
                    {
                        pixelBytes = await b.GetTileBytesRaw(
                            b.GetFrameIndex(z, srcC, t), tileLevel, tileX, tileY, actualW, actualH,
                            new AForge.ZCT(z, srcC, t)).ConfigureAwait(false);
                    }
                    catch
                    {
                        pixelBytes = null;
                    }

                    int expectedBytes = needsDeinterleave ? interleavedBytes : singleChannelBytes;
                    if (pixelBytes == null || pixelBytes.Length == 0)
                    {
                        pixelBytes = new byte[expectedBytes];
                    }

                    if (needsDeinterleave)
                    {
                        int inferredChannelCount = srcChannelCount;
                        if (pixelCount > 0 && pixelBytes.Length > 0)
                        {
                            int inferred = pixelBytes.Length / (pixelCount * bytesPerSample);
                            if (inferred >= 1 && inferred <= 4)
                                inferredChannelCount = inferred;
                        }

                        // Ensure the buffer is the full interleaved size before
                        // extracting a single channel from it.
                        int expectedInterleavedBytes = pixelCount * inferredChannelCount * bytesPerSample;
                        if (pixelBytes.Length != expectedInterleavedBytes)
                        {
                            var trimmed = new byte[expectedInterleavedBytes];
                            Buffer.BlockCopy(pixelBytes, 0, trimmed, 0,
                                Math.Min(pixelBytes.Length, expectedInterleavedBytes));
                            pixelBytes = trimmed;
                        }

                        // For BGRA buffers the byte order is [B, G, R, A].
                        // OpenSlide SVS tiles commonly arrive as BGRA even when the
                        // nominal source pixel format is reported as 24bpp RGB.
                        bool actualIsBGRA = isBGRA || inferredChannelCount == 4;
                        int srcByteIndex = actualIsBGRA ? (2 - rgbChannelIndex) : rgbChannelIndex;

                        pixelBytes = DeinterleaveChannel(
                            pixelBytes, srcByteIndex, inferredChannelCount,
                            bytesPerSample, pixelCount);
                    }
                    else
                    {
                        // Single-channel path: trim/pad to the exact scalar size.
                        if (pixelBytes.Length != singleChannelBytes)
                        {
                            var trimmed = new byte[singleChannelBytes];
                            Buffer.BlockCopy(pixelBytes, 0, trimmed, 0,
                                Math.Min(pixelBytes.Length, singleChannelBytes));
                            pixelBytes = trimmed;
                        }
                    }

                    if (s_debugTileLogs < 12)
                    {
                        string valueSummary = bytesPerSample == 2
                            ? $"u16mm={MinMaxU16(pixelBytes)} u16sample={SampleU16(pixelBytes)}"
                            : $"u8mm={MinMaxU8(pixelBytes)}";
                        Log($"[WritePlaneInTiles] lvl={levelIndex} t={t} c={c} z={z} srcC={srcC} x={tileX} y={tileY} " +
                            $"w={actualW} h={actualH} rawLen={pixelBytes.Length} expSingle={singleChannelBytes} " +
                            $"expInterleaved={interleavedBytes} needsDeinterleave={needsDeinterleave} " +
                            $"srcBands={srcChannelCount} bytesPerSample={bytesPerSample} sample={SampleBytes(pixelBytes)} {valueSummary}");
                        s_debugTileLogs++;
                    }

                    await writer.WriteRegionAsync(
                        t, c, z,
                        tileY, tileX,
                        actualH, actualW,
                        pixelBytes,
                        levelIndex: levelIndex).ConfigureAwait(false);

                    if (s_debugTileLogs <= 12)
                        Log($"[WritePlaneInTiles] wrote lvl={levelIndex} t={t} c={c} z={z} x={tileX} y={tileY} len={pixelBytes.Length}");
                }
            }
        }

        private static async Task WritePlaneFromBuffer(
            OmeZarrWriter              writer,
            BioImage                   b,
            ResolutionLevelDescriptor  lvlDesc,
            int t, int c, int z,
            int bytesPerSample,
            int tileW, int tileH,
            int levelIndex      = 0,
            int sourceLevelIndex = -1,
            int originX = 0,
            int originY = 0,
            int rgbChannelIndex = -1,
            int srcChannelCount = 1,
            bool isBGRA         = false,
            int logicalC        = -1)
        {
            bool needsDeinterleave = rgbChannelIndex >= 0;
            int srcC = needsDeinterleave ? logicalC : c;
            int index = b.GetFrameIndex(z, srcC, t);
            if (index < 0 || index >= b.Buffers.Count || b.Buffers[index] == null)
            {
                byte[] empty = new byte[Math.Max(0, lvlDesc.SizeX * lvlDesc.SizeY * bytesPerSample)];
                await writer.WriteRegionAsync(t, c, z, 0, 0, lvlDesc.SizeY, lvlDesc.SizeX, empty, levelIndex: levelIndex).ConfigureAwait(false);
                return;
            }

            Bitmap buffer = b.Buffers[index];
            int planeW = buffer.SizeX > 0 ? buffer.SizeX : lvlDesc.SizeX;
            int planeH = buffer.SizeY > 0 ? buffer.SizeY : lvlDesc.SizeY;
            int bufferBytesPerPixel = needsDeinterleave
                ? Math.Max(1, srcChannelCount * bytesPerSample)
                : Math.Max(1, bytesPerSample);
            byte[] planeBytes = ExtractRawPixels(buffer, planeW, planeH, bufferBytesPerPixel);

            if (needsDeinterleave)
            {
                int pixelCount = planeW * planeH;
                if (pixelCount <= 0)
                    return;

                int inferredChannelCount = srcChannelCount;
                if (planeBytes.Length > 0)
                {
                    int inferred = planeBytes.Length / (pixelCount * bytesPerSample);
                    if (inferred >= 1 && inferred <= 4)
                        inferredChannelCount = inferred;
                }

                int srcByteIndex = (isBGRA || inferredChannelCount == 4) ? (2 - rgbChannelIndex) : rgbChannelIndex;
                planeBytes = DeinterleaveChannel(
                    planeBytes, srcByteIndex, inferredChannelCount,
                    bytesPerSample, pixelCount);
            }
            else
            {
                if (planeBytes.Length != planeW * planeH * bytesPerSample)
                {
                    var trimmed = new byte[planeW * planeH * bytesPerSample];
                    Buffer.BlockCopy(planeBytes, 0, trimmed, 0, Math.Min(planeBytes.Length, trimmed.Length));
                    planeBytes = trimmed;
                }

                if (!buffer.LittleEndian && (buffer.PixelFormat == PixelFormat.Format16bppGrayScale || buffer.PixelFormat == PixelFormat.Format48bppRgb))
                    SwapBytePairsInPlace(planeBytes);
            }

            int writeX = Math.Max(0, originX);
            int writeY = Math.Max(0, originY);
            int writeW = Math.Min(planeW, Math.Max(0, lvlDesc.SizeX - writeX));
            int writeH = Math.Min(planeH, Math.Max(0, lvlDesc.SizeY - writeY));
            if (writeW <= 0 || writeH <= 0)
                return;

            if (writeW != planeW || writeH != planeH || writeX != 0 || writeY != 0)
            {
                byte[] canvas = new byte[lvlDesc.SizeX * lvlDesc.SizeY * bytesPerSample];
                int srcRowBytes = planeW * bytesPerSample;
                int dstRowBytes = lvlDesc.SizeX * bytesPerSample;
                for (int row = 0; row < writeH; row++)
                {
                    Buffer.BlockCopy(planeBytes, row * srcRowBytes, canvas, (writeY + row) * dstRowBytes + writeX * bytesPerSample, writeW * bytesPerSample);
                }
                planeBytes = canvas;
                planeW = lvlDesc.SizeX;
                planeH = lvlDesc.SizeY;
            }

            await writer.WriteRegionAsync(t, c, z, 0, 0, planeH, planeW, planeBytes, levelIndex: levelIndex).ConfigureAwait(false);
        }

        internal static async Task WriteTileFromBuffer(
            OmeZarrWriter              writer,
            Bitmap                     buffer,
            ResolutionLevelDescriptor  lvlDesc,
            int t, int c, int z,
            int tileX, int tileY,
            int bytesPerSample,
            int levelIndex      = 0,
            int rgbChannelIndex = -1,
            int srcChannelCount = 1,
            bool isBGRA         = false,
            int logicalC        = -1)
        {
            if (buffer == null || buffer.Bytes == null || buffer.Bytes.Length == 0)
                return;

            bool needsDeinterleave = rgbChannelIndex >= 0;
            int bufferBytesPerPixel = needsDeinterleave
                ? Math.Max(1, srcChannelCount * bytesPerSample)
                : Math.Max(1, bytesPerSample);

            byte[] pixelBytes = ExtractRawPixels(buffer, buffer.SizeX, buffer.SizeY, bufferBytesPerPixel);
            if (needsDeinterleave)
            {
                int pixelCount = buffer.SizeX * buffer.SizeY;
                if (pixelCount <= 0)
                    return;

                int srcByteIndex = isBGRA ? (2 - rgbChannelIndex) : rgbChannelIndex;
                pixelBytes = DeinterleaveChannel(
                    pixelBytes, srcByteIndex, srcChannelCount,
                    bytesPerSample, pixelCount);
            }
            else
            {
                if (pixelBytes.Length != buffer.SizeX * buffer.SizeY * bytesPerSample)
                {
                    var trimmed = new byte[buffer.SizeX * buffer.SizeY * bytesPerSample];
                    Buffer.BlockCopy(pixelBytes, 0, trimmed, 0, Math.Min(pixelBytes.Length, trimmed.Length));
                    pixelBytes = trimmed;
                }

                if (!buffer.LittleEndian && (buffer.PixelFormat == PixelFormat.Format16bppGrayScale || buffer.PixelFormat == PixelFormat.Format48bppRgb))
                    SwapBytePairsInPlace(pixelBytes);
            }

            await writer.WriteRegionAsync(
                t, c, z,
                tileY, tileX,
                buffer.SizeY, buffer.SizeX,
                pixelBytes,
                levelIndex: levelIndex).ConfigureAwait(false);
        }

        /// <summary>
        /// Copies exactly width × height × bytesPerSample from the bitmap's
        /// buffer, stripping any stride padding the bitmap format may include.
        /// </summary>
        private static byte[] ExtractRawPixels(
            Bitmap tileBitmap, int width, int height, int bytesPerSample)
        {
            int rowBytes = width * bytesPerSample;
            int totalBytes = rowBytes * height;
            var sourceBytes = tileBitmap.Bytes;

            if (sourceBytes.Length == totalBytes)
                return sourceBytes;

            var output = new byte[totalBytes];
            int sourceStride = tileBitmap.Stride;

            for (int row = 0; row < height; row++)
            {
                Buffer.BlockCopy(sourceBytes, row * sourceStride, output, row * rowBytes, rowBytes);
            }

            return output;
        }

        private static void SwapBytePairsInPlace(byte[] data)
        {
            if (data == null)
                return;

            for (int i = 0; i + 1 < data.Length; i += 2)
            {
                byte b0 = data[i];
                data[i] = data[i + 1];
                data[i + 1] = b0;
            }
        }

        private static async Task PostSaveReadback(string outputDir)
        {
            var reader = await OmeZarrReader.OpenAsync(outputDir).ConfigureAwait(false);
            var ms     = await reader.AsMultiscaleImageAsync().ConfigureAwait(false);
            var lvl0   = await ms.OpenResolutionLevelAsync(0, 0).ConfigureAwait(false);
            var shape  = lvl0.Shape;

            int tileW = (int)Math.Min(512, shape.Length >= 5 ? shape[4] : 512);
            int tileH = (int)Math.Min(512, shape.Length >= 5 ? shape[3] : 512);
            var tile  = await lvl0.ReadTileAsync(0, 0, tileW, tileH, t: 0, c: 0, z: 0).ConfigureAwait(false);

            string sample = tile.DataType == "uint16"
                ? $"raw={SampleBytes(tile.Data)} u16mm={MinMaxU16(tile.Data)} u16sample={SampleU16(tile.Data)}"
                : $"u8mm={MinMaxU8(tile.Data)}";

            Log($"[PostSaveReadback] out={outputDir} level=0 x=0 y=0 w={tileW} h={tileH} " +
                $"type={tile.DataType} len={tile.Data.Length} shape={string.Join("x", tile.Shape)} {sample}");
        }

        // =====================================================================
        // Shared helpers
        // =====================================================================

        /// <summary>
        /// Maps BioLib's per-sample bit depth to a Zarr v3 data_type string.
        /// </summary>
        private static string MapDataType(int bitsPerSample)
        {
            return bitsPerSample switch
            {
                8  => "uint8",
                16 => "uint16",
                32 => "float32",
                _  => "uint16"
            };
        }

        private static int DetermineSourceBitsPerSample(BioImage b)
        {
            if (b.Resolutions.Count > 0)
            {
                return b.Resolutions[0].PixelFormat switch
                {
                    AForge.PixelFormat.Format8bppIndexed => 8,
                    AForge.PixelFormat.Format24bppRgb => 8,
                    AForge.PixelFormat.Format32bppArgb => 8,
                    AForge.PixelFormat.Format32bppRgb => 8,
                    AForge.PixelFormat.Format16bppGrayScale => 16,
                    AForge.PixelFormat.Format48bppRgb => 16,
                    AForge.PixelFormat.Format64bppArgb => 16,
                    AForge.PixelFormat.Format64bppPArgb => 16,
                    _ => Math.Max(8, b.bitsPerPixel)
                };
            }

            return Math.Max(8, b.bitsPerPixel);
        }

        private static AForge.PixelFormat DetermineSourcePixelFormat(BioImage image)
        {
            if (image.Resolutions.Count > 0)
                return image.Resolutions[0].PixelFormat;

            if (image.Buffers.Count > 0)
                return image.Buffers[0].PixelFormat;

            return AForge.PixelFormat.Format16bppGrayScale;
        }

        private static bool IsInterleavedRgbFormat(AForge.PixelFormat format)
        {
            return format == AForge.PixelFormat.Format24bppRgb ||
                   format == AForge.PixelFormat.Format32bppArgb ||
                   format == AForge.PixelFormat.Format32bppPArgb ||
                   format == AForge.PixelFormat.Format32bppRgb ||
                   format == AForge.PixelFormat.Format48bppRgb ||
                   format == AForge.PixelFormat.Format64bppArgb ||
                   format == AForge.PixelFormat.Format64bppPArgb;
        }

        private static bool IsBGRAFormat(AForge.PixelFormat format)
        {
            return format == AForge.PixelFormat.Format32bppArgb ||
                   format == AForge.PixelFormat.Format32bppPArgb ||
                   format == AForge.PixelFormat.Format32bppRgb ||
                   format == AForge.PixelFormat.Format64bppArgb ||
                   format == AForge.PixelFormat.Format64bppPArgb;
        }

        /// <summary>
        // =====================================================================
        // ROI persistence — JSON sidecar at <outputDir>/rois.json
        // =====================================================================

        /// <summary>
        /// The file name used for the ROI sidecar that sits beside the Zarr dataset.
        /// </summary>
        public const string RoiFileName = "rois.json";

        /// <summary>
        /// Plain-old-data transfer object written to / read from <c>rois.json</c>.
        /// Every field maps 1-to-1 with the columns in BioImage's CSV format so
        /// that the two representations stay in sync.
        /// </summary>
        private sealed class RoiDto
        {
            public string RoiId        { get; set; } = "";
            public string RoiName      { get; set; } = "";
            public string Type         { get; set; } = "";
            public string Id           { get; set; } = "";
            public int    ShapeIndex   { get; set; }
            public string Text         { get; set; } = "";
            public int    Serie        { get; set; }
            public int    Z            { get; set; }
            public int    C            { get; set; }
            public int    T            { get; set; }
            public double X            { get; set; }
            public double Y            { get; set; }
            public double W            { get; set; }
            public double H            { get; set; }
            /// <summary>Points encoded as "x0,y0 x1,y1 …" (invariant culture).</summary>
            public string Points       { get; set; } = "";
            /// <summary>Stroke colour as "A,R,G,B".</summary>
            public string StrokeColor  { get; set; } = "";
            public double StrokeWidth  { get; set; }
            /// <summary>Fill colour as "A,R,G,B".</summary>
            public string FillColor    { get; set; } = "";
            public float  FontSize     { get; set; }
        }

        /// <summary>
        /// Writes all annotations on <paramref name="b"/> as a JSON sidecar
        /// file at <c>&lt;outputDir&gt;/rois.json</c>.
        ///
        /// Call this after <see cref="SaveZarr"/> so that both image data and
        /// ROIs live in the same directory and can be round-tripped together.
        /// </summary>
        public static void SaveROIs(BioImage b, string outputDir)
        {
            var dtos = new List<RoiDto>(b.Annotations.Count);

            foreach (ROI an in b.Annotations)
            {
                // Encode points as "x,y x,y …" (invariant culture) — same
                // format used by BioImage.ROIToString / stringToPoints.
                PointD[] pts  = an.GetPoints();
                var      ptSb = new System.Text.StringBuilder();
                for (int i = 0; i < pts.Length; i++)
                {
                    if (i > 0) ptSb.Append(' ');
                    ptSb.Append(pts[i].X.ToString(CultureInfo.InvariantCulture));
                    ptSb.Append(',');
                    ptSb.Append(pts[i].Y.ToString(CultureInfo.InvariantCulture));
                }

                dtos.Add(new RoiDto
                {
                    RoiId       = an.roiID,
                    RoiName     = an.roiName,
                    Type        = an.type.ToString(),
                    Id          = an.id,
                    ShapeIndex  = an.shapeIndex,
                    Text        = an.Text,
                    Serie       = an.serie,
                    Z           = an.coord.Z,
                    C           = an.coord.C,
                    T           = an.coord.T,
                    X           = an.X,
                    Y           = an.Y,
                    W           = an.W,
                    H           = an.H,
                    Points      = ptSb.ToString(),
                    StrokeColor = $"{an.strokeColor.A},{an.strokeColor.R},{an.strokeColor.G},{an.strokeColor.B}",
                    StrokeWidth = an.strokeWidth,
                    FillColor   = $"{an.fillColor.A},{an.fillColor.R},{an.fillColor.G},{an.fillColor.B}",
                    FontSize    = an.fontSize,
                });
            }

            var opts = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(dtos, opts);
            File.WriteAllText(Path.Combine(outputDir, RoiFileName), json);

            Recorder.Record($"Zarr.SaveROIs({b}, \"{outputDir}\");");
        }

        /// <summary>
        /// Reads <c>&lt;zarrDir&gt;/rois.json</c> and returns the deserialized
        /// list of <see cref="ROI"/> objects.  Returns an empty list when no
        /// sidecar file exists.
        /// </summary>
        public static List<ROI> LoadROIs(string zarrDir)
        {
            var result = new List<ROI>();
            string path = Path.Combine(zarrDir, RoiFileName);

            if (!File.Exists(path))
                return result;

            string json = File.ReadAllText(path);
            var dtos = JsonSerializer.Deserialize<List<RoiDto>>(json);
            if (dtos == null)
                return result;

            foreach (var dto in dtos)
            {
                var an = new ROI
                {
                    roiID      = dto.RoiId,
                    roiName    = dto.RoiName,
                    type       = (ROI.Type)Enum.Parse(typeof(ROI.Type), dto.Type),
                    id         = dto.Id,
                    shapeIndex = dto.ShapeIndex,
                    Text       = dto.Text,
                    serie      = dto.Serie,
                    coord      = new AForge.ZCT(dto.Z, dto.C, dto.T),
                    strokeWidth= dto.StrokeWidth,
                    fontSize   = dto.FontSize,
                };

                // Closed shapes
                if (an.type == ROI.Type.Freeform || an.type == ROI.Type.Polygon)
                    an.closed = true;

                // Restore colours
                if (!string.IsNullOrEmpty(dto.StrokeColor))
                {
                    var sc = dto.StrokeColor.Split(',');
                    an.strokeColor = AForge.Color.FromArgb(
                        int.Parse(sc[0]), int.Parse(sc[1]),
                        int.Parse(sc[2]), int.Parse(sc[3]));
                }
                if (!string.IsNullOrEmpty(dto.FillColor))
                {
                    var fc = dto.FillColor.Split(',');
                    an.fillColor = AForge.Color.FromArgb(
                        int.Parse(fc[0]), int.Parse(fc[1]),
                        int.Parse(fc[2]), int.Parse(fc[3]));
                }

                // Restore points then bounding box
                if (!string.IsNullOrEmpty(dto.Points))
                    an.AddPoints(an.stringToPoints(dto.Points));

                an.BoundingBox = new AForge.RectangleD(dto.X, dto.Y, dto.W, dto.H);

                result.Add(an);
            }

            Recorder.Record($"Zarr.LoadROIs(\"{zarrDir}\");");
            return result;
        }

        // =====================================================================
        // Labels → ROI conversion
        // =====================================================================

        /// <summary>
        /// Reads every label image stored under <c>&lt;zarrDir&gt;/labels/</c>
        /// according to the OME-NGFF labels spec and converts each unique
        /// non-zero integer label ID into a <see cref="ROI"/> of type
        /// <see cref="ROI.Type.Mask"/>.
        ///
        /// For each label sub-store the finest resolution level (index 0) is
        /// read one plane at a time (iterating Z, C, T from the parent
        /// <paramref name="b"/>).  Every distinct pixel value > 0 in a plane
        /// becomes a separate <c>ROI.Mask</c> whose float array contains 1.0
        /// wherever that label is present and 0.0 elsewhere.
        ///
        /// The method is a no-op when no <c>labels/</c> sub-directory exists,
        /// so it is safe to call unconditionally after opening any Zarr store.
        /// </summary>
        /// <param name="b">The parent <see cref="BioImage"/> that supplies
        /// physical-size calibration and ZCT extents.</param>
        /// <param name="zarrDir">Root path of the Zarr dataset on local disk
        /// (the directory that contains <c>zarr.json</c>).</param>
        /// <returns>
        /// List of <see cref="ROI"/> objects (one per unique label ID per
        /// plane).  Returns an empty list when no labels are found.
        /// </returns>
        public static List<ROI> LoadLabelsAsROIs(BioImage b, string zarrDir)
        {
            var result = new List<ROI>();

            string labelsDir = Path.Combine(zarrDir, "labels");
            if (!Directory.Exists(labelsDir))
                return result;

            try
            {
                File.AppendAllText(@"log.txt",
                    "[BioLib.LoadLabelsAsROIs] zarrDir=" + zarrDir +
                    " labelsDirExists=True\n");
            }
            catch { }

            // Each sub-directory of labels/ is one label image.
            foreach (string labelPath in Directory.GetDirectories(labelsDir))
            {
                string labelName = Path.GetFileName(labelPath);
                try
                {
                    try
                    {
                        File.AppendAllText(@"log.txt",
                            "[BioLib.LoadLabelsAsROIs] labelPath=" + labelPath +
                            " labelName=" + labelName + "\n");
                    }
                    catch { }
                    result.AddRange(ReadOneLabelStore(b, labelPath, labelName));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Zarr.LoadLabelsAsROIs] Skipping label '{labelName}': {ex.Message}");
                    try
                    {
                        File.AppendAllText(@"log.txt",
                            "[BioLib.LoadLabelsAsROIs] EXCEPTION labelName=" + labelName +
                            " type=" + ex.GetType().Name +
                            " message=" + ex.Message + "\n");
                    }
                    catch { }
                }
            }

            Recorder.Record($"Zarr.LoadLabelsAsROIs({b}, \"{zarrDir}\");");
            return result;
        }

        /// <summary>
        /// Reads a single label store (one entry under <c>labels/</c>) and
        /// returns ROIs for every unique non-zero label ID found in the data.
        /// </summary>
        private static List<ROI> ReadOneLabelStore(BioImage b, string labelPath, string labelName)
        {
            var result = new List<ROI>();

            try
            {
                File.AppendAllText(@"log.txt",
                    "[BioLib.ReadOneLabelStore] enter labelPath=" + labelPath +
                    " labelName=" + labelName + "\n");
            }
            catch { }

            // Open the label store with the same ZarrNET reader used for the
            // parent image so we get the same axis/shape metadata.
            var reader = OmeZarrReader.OpenAsync(labelPath).Result;
            var root   = reader.OpenRoot();
            var ms     = root as ZarrNET.Core.Nodes.MultiscaleNode;
            if (ms == null)
                throw new InvalidOperationException($"Label store '{labelPath}' is not a multiscale label image.");
            var levels = ms.OpenAllResolutionLevelsAsync().Result.ToList();

            try
            {
                File.AppendAllText(@"log.txt",
                    "[BioLib.ReadOneLabelStore] labelPath=" + labelPath +
                    " levels=" + levels.Count + "\n");
            }
            catch { }

            if (levels.Count == 0)
                return result;

            // Always use the finest (full-resolution) level.
            var level = levels[0];
            var axes      = level.EffectiveAxes;
            var rawShape  = level.Shape;
            var shape     = rawShape;
            bool hasLeadingSingletonT = shape.Length == axes.Length + 1 && shape[0] == 1;
            if (hasLeadingSingletonT)
            {
                // Local BioGTK label exports include a leading singleton t
                // dimension while the label axes describe only c/z/y/x.
                // Collapse it so older saved datasets remain readable.
                shape = shape.Skip(1).ToArray();
            }

            int sizeX = GetAxisSizeStatic(axes, shape, "x");
            int sizeY = GetAxisSizeStatic(axes, shape, "y");
            int sizeZ = GetAxisSizeStatic(axes, shape, "z", 1);
            int sizeC = GetAxisSizeStatic(axes, shape, "c", 1);
            int sizeT = GetAxisSizeStatic(axes, shape, "t", 1);

            try
            {
                File.AppendAllText(@"log.txt",
                    "[BioLib.ReadOneLabelStore] axes=" + string.Join("", axes.Select(a => a.Name)) +
                    " shape=" + string.Join("x", shape) +
                    " sizeZ=" + sizeZ +
                    " sizeC=" + sizeC +
                    " sizeT=" + sizeT +
                    " sizeX=" + sizeX +
                    " sizeY=" + sizeY + "\n");
            }
            catch { }

            if (sizeX == 0 || sizeY == 0)
                return result;

            int baseWidth = b.Resolutions.Count > 0 ? b.Resolutions[0].SizeX : b.SizeX;
            int baseHeight = b.Resolutions.Count > 0 ? b.Resolutions[0].SizeY : b.SizeY;
            double physX = b.PhysicalSizeX > 0 ? b.PhysicalSizeX : 1.0;
            double physY = b.PhysicalSizeY > 0 ? b.PhysicalSizeY : 1.0;
            physX *= sizeX > 0 ? baseWidth / (double)sizeX : 1.0;
            physY *= sizeY > 0 ? baseHeight / (double)sizeY : 1.0;

            int shapeIdx = 0;

            for (int t = 0; t < sizeT; t++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    for (int c = 0; c < sizeC; c++)
                    {
                        // Build start/end arrays covering the full XY plane for
                        // this (t, c, z) coordinate.
                        long[] start = hasLeadingSingletonT
                            ? new long[] { t, c, z, 0, 0 }
                            : BuildStart(axes, shape, t, c, z);
                        long[] end = hasLeadingSingletonT
                            ? new long[] { t + 1, c + 1, z + 1, sizeY, sizeX }
                            : BuildEnd(axes, shape, t, c, z, sizeX, sizeY);

                        var region     = new PixelRegion(start, end);
                        var regionData = level.ReadPixelRegionAsync(region).Result;
                        byte[] raw     = regionData.Data;

                        try
                        {
                            File.AppendAllText(@"log.txt",
                                "[BioLib.ReadOneLabelStore] plane t=" + t +
                                " c=" + c +
                                " z=" + z +
                                " raw=" + (raw?.Length ?? 0) + "\n");
                        }
                        catch { }

                        // Determine bytes-per-sample from raw buffer length.
                        int totalPixels   = sizeX * sizeY;
                        int bytesPerPixel = raw.Length / totalPixels;

                        // Collect unique non-zero label IDs and their pixel masks.
                        var labelMasks = SplitLabelPlane(raw, sizeX, sizeY, bytesPerPixel);
                        try
                        {
                            File.AppendAllText(@"log.txt",
                                "[BioLib.ReadOneLabelStore] plane labels=" + labelMasks.Count + "\n");
                        }
                        catch { }

                        var zct = new AForge.ZCT(z, c, t);

                        foreach (var kv in labelMasks)
                        {
                            int    labelId   = kv.Key;
                            byte[] maskData = kv.Value;

                            var mask = new Mask(maskData, sizeX, sizeY, physX, physY, 0, 0);

                            var roi = new ROI
                            {
                                type        = ROI.Type.Mask,
                                roiID       = $"{labelName}_{labelId}",
                                roiName     = labelName,
                                Text        = labelId.ToString(),
                                coord       = zct,
                                shapeIndex  = shapeIdx++,
                                strokeColor = AForge.Color.FromArgb(255, 0, 200, 83),
                                fillColor   = AForge.Color.FromArgb(80,  0, 200, 83),
                                strokeWidth = 1,
                                roiMask     = mask,
                            };
                            roi.UpdateBoundingBox();
                            result.Add(roi);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Splits a raw label plane into per-label float masks.
        /// Each entry in the returned dictionary maps a non-zero label ID to a
        /// float[] of length <paramref name="w"/> × <paramref name="h"/> where
        /// pixels belonging to that label are 1.0 and all others are 0.0.
        /// </summary>
        private static Dictionary<int, byte[]> SplitLabelPlane(
            byte[] raw, int w, int h, int bytesPerPixel)
        {
            int total  = w * h;
            var result = new Dictionary<int, byte[]>();

            for (int i = 0; i < total; i++)
            {
                int labelId = ReadLabelId(raw, i, bytesPerPixel);
                if (labelId == 0)
                    continue;

                if (!result.TryGetValue(labelId, out byte[]? mask))
                {
                    mask = new byte[total];
                    result[labelId] = mask;
                }
                mask[i] = 255;
            }

            return result;
        }

        /// <summary>
        /// Reads a single label integer from a raw byte buffer at pixel index
        /// <paramref name="pixelIndex"/>, handling 1-, 2-, and 4-byte labels
        /// (uint8, uint16, int32/uint32).  Little-endian byte order assumed.
        /// </summary>
        private static int ReadLabelId(byte[] raw, int pixelIndex, int bytesPerPixel)
        {
            int byteOffset = pixelIndex * bytesPerPixel;
            return bytesPerPixel switch
            {
                1 => raw[byteOffset],
                2 => BitConverter.ToUInt16(raw, byteOffset),
                4 => BitConverter.ToInt32(raw, byteOffset),
                _ => raw[byteOffset],
            };
        }

        /// <summary>
        /// Builds the <c>start</c> index array for a <see cref="PixelRegion"/>
        /// covering the full XY plane at the given (t, c, z) coordinate.
        /// Axes not present in the label store are skipped.
        /// </summary>
        private static long[] BuildStart(AxisMetadata[] axes, long[] shape, int t, int c, int z)
        {
            long[] start = new long[axes.Length];
            for (int i = 0; i < axes.Length; i++)
            {
                start[i] = axes[i].Name.ToLowerInvariant() switch
                {
                    "t" => t,
                    "c" => c,
                    "z" => z,
                    _   => 0,
                };
            }
            return start;
        }

        /// <summary>
        /// Builds the <c>end</c> index array for a <see cref="PixelRegion"/>
        /// covering the full XY plane at the given (t, c, z) coordinate.
        /// </summary>
        private static long[] BuildEnd(
            AxisMetadata[] axes, long[] shape,
            int t, int c, int z, int sizeX, int sizeY)
        {
            long[] end = new long[axes.Length];
            for (int i = 0; i < axes.Length; i++)
            {
                end[i] = axes[i].Name.ToLowerInvariant() switch
                {
                    "t" => t + 1,
                    "c" => c + 1,
                    "z" => z + 1,
                    "x" => sizeX,
                    "y" => sizeY,
                    _   => shape[i],
                };
            }
            return end;
        }

        /// <summary>
        /// Returns the size of a named axis from a shape array, or
        /// <paramref name="fallback"/> when the axis is not present.
        /// Static version used inside the labels reader (no instance required).
        /// </summary>
        private static int GetAxisSizeStatic(
            AxisMetadata[] axes, long[] shape, string name, int fallback = 0)
        {
            for (int i = 0; i < axes.Length; i++)
            {
                if (axes[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return (int)shape[i];
            }
            return fallback;
        }

        // =====================================================================
        // Channel-extraction helper
        // =====================================================================

        /// Extracts one colour channel from an interleaved buffer.
        ///
        /// Given pixels stored as [C0 C1 C2 C0 C1 C2 ...] with
        /// <paramref name="bytesPerSample"/> bytes per component, returns
        /// a contiguous single-channel plane for <paramref name="channelIndex"/>.
        /// For BGRA sources pass the remapped byte index so that byte-order
        /// correction happens here.
        /// </summary>
        private static byte[] DeinterleaveChannel(
            byte[] interleaved,
            int    channelIndex,
            int    totalChannels,
            int    bytesPerSample,
            int    pixelCount)
        {
            var output       = new byte[pixelCount * bytesPerSample];
            var stride       = totalChannels * bytesPerSample;
            var channelStart = channelIndex  * bytesPerSample;

            if (channelStart < 0 || channelStart + bytesPerSample > stride)
                return output;

            for (int px = 0; px < pixelCount; px++)
            {
                var srcOffset = px * stride + channelStart;
                var dstOffset = px * bytesPerSample;

                if (srcOffset < 0 || srcOffset + bytesPerSample > interleaved.Length)
                    break;

                for (int byteIdx = 0; byteIdx < bytesPerSample; byteIdx++)
                    output[dstOffset + byteIdx] = interleaved[srcOffset + byteIdx];
            }

            return output;
        }
    }
}

