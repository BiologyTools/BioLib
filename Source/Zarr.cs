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
                File.AppendAllText(@"C:\Users\Public\biolog.txt", msg + Environment.NewLine);
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
        public static void SaveZarr(BioImage b, string outputDir)
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

            var bytesPerSample = bitsPerSample / 8;
            var zarrDataType   = MapDataType(bitsPerSample);
            var totalChannels  = b.SizeC;

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
                for (int lvl = 0; lvl < b.Resolutions.Count; lvl++)
                {
                    var res        = b.Resolutions[lvl];
                    double dsample = b.GetLevelDownsample(lvl);   // 1.0 for level 0
                    levelDescriptors.Add(new ResolutionLevelDescriptor(res.SizeX, res.SizeY, dsample));
                    Log($"[SaveZarr] level={lvl} size={res.SizeX}x{res.SizeY} downsample={dsample}");
                }

            // Fallback: if there is somehow no Resolutions list, use the image dims.
            if (levelDescriptors.Count == 0)
                levelDescriptors.Add(new ResolutionLevelDescriptor(b.SizeX, b.SizeY, 1.0));

            // ------------------------------------------------------------------
            // 3. Build the base descriptor (T/C/Z/dtype — shared by all levels)
            // ------------------------------------------------------------------

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
                            for (int c = 0; c < b.SizeC; c++)
                            {
                                BioImage.Status = $"Writing level {levelIndex + 1}/{levelDescriptors.Count} plane T{t + 1}/{b.SizeT} Z{z + 1}/{b.SizeZ} C{c + 1}/{b.SizeC}";
                                if (sourceIsInterleavedRgb)
                                {
                                    WritePlaneInTiles(
                                        writer, b, lvlDesc,
                                        t, c, z,
                                        bytesPerSample, tileW, tileH,
                                        levelIndex: levelIndex,
                                        rgbChannelIndex: c,
                                        srcChannelCount: sourceChannelCount,
                                        isBGRA: sourceIsBGRA,
                                        logicalC: 0).GetAwaiter().GetResult();
                                }
                                else
                                {
                                    WritePlaneInTiles(
                                        writer, b, lvlDesc,
                                        t, c, z,
                                        bytesPerSample, tileW, tileH,
                                        levelIndex: levelIndex).GetAwaiter().GetResult();
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

            Recorder.Record(BioLib.Recorder.GetCurrentMethodInfo(), false, b);
            BioImage.Progress = 100;
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
            int rgbChannelIndex = -1,
            int srcChannelCount = 1,
            bool isBGRA         = false,
            int logicalC        = -1)
        {
            bool needsDeinterleave = rgbChannelIndex >= 0;
            int  srcC              = needsDeinterleave ? logicalC : c;

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
                    byte[] pixelBytes = await b.GetTileBytesRaw(
                        b.GetFrameIndex(z, srcC, t), levelIndex, tileX, tileY, actualW, actualH,
                        new AForge.ZCT(z, srcC, t)).ConfigureAwait(false);

                    if (needsDeinterleave)
                    {
                        // Ensure the buffer is the full interleaved size before
                        // extracting a single channel from it.
                        if (pixelBytes.Length != interleavedBytes)
                        {
                            var trimmed = new byte[interleavedBytes];
                            Buffer.BlockCopy(pixelBytes, 0, trimmed, 0,
                                Math.Min(pixelBytes.Length, interleavedBytes));
                            pixelBytes = trimmed;
                        }

                        // For BGRA buffers the byte order is [B, G, R, A].
                        // Map output channel index:  0→R(byte2), 1→G(byte1), 2→B(byte0)
                        int srcByteIndex = isBGRA ? (2 - rgbChannelIndex) : rgbChannelIndex;

                        pixelBytes = DeinterleaveChannel(
                            pixelBytes, srcByteIndex, srcChannelCount,
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

            Recorder.Record(BioLib.Recorder.GetCurrentMethodInfo(), false, b, outputDir);
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

            Recorder.Record(BioLib.Recorder.GetCurrentMethodInfo(), false, zarrDir);
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
                File.AppendAllText(@"C:\Users\Public\biolog.txt",
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
                        File.AppendAllText(@"C:\Users\Public\biolog.txt",
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
                        File.AppendAllText(@"C:\Users\Public\biolog.txt",
                            "[BioLib.LoadLabelsAsROIs] EXCEPTION labelName=" + labelName +
                            " type=" + ex.GetType().Name +
                            " message=" + ex.Message + "\n");
                    }
                    catch { }
                }
            }

            Recorder.Record(BioLib.Recorder.GetCurrentMethodInfo(), false, b, zarrDir);
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
                File.AppendAllText(@"C:\Users\Public\biolog.txt",
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
                File.AppendAllText(@"C:\Users\Public\biolog.txt",
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
                File.AppendAllText(@"C:\Users\Public\biolog.txt",
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

            double physX = b.PhysicalSizeX > 0 ? b.PhysicalSizeX : 1.0;
            double physY = b.PhysicalSizeY > 0 ? b.PhysicalSizeY : 1.0;

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
                            File.AppendAllText(@"C:\Users\Public\biolog.txt",
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
                            File.AppendAllText(@"C:\Users\Public\biolog.txt",
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

            for (int px = 0; px < pixelCount; px++)
            {
                var srcOffset = px * stride + channelStart;
                var dstOffset = px * bytesPerSample;

                for (int byteIdx = 0; byteIdx < bytesPerSample; byteIdx++)
                    output[dstOffset + byteIdx] = interleaved[srcOffset + byteIdx];
            }

            return output;
        }
    }
}
