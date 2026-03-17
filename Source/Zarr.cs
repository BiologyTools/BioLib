using AForge;
using BioLib;
using ZarrNET.Core;
using System;
using System.Collections.Generic;
using System.IO;
using ZarrNET;
using ZarrNET.Core;

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

            // Derive bits-per-sample and RGB channel count directly from the
            // buffer PixelFormat — the same pattern used throughout the codebase
            // (Fiji.cs, QuPath.cs).  This is more reliable than b.RGBChannelCount
            // or b.bitsPerPixel, which can disagree with the actual buffer layout.
            //
            // Format32bppArgb / Format32bppRgb:
            //   In-memory layout is BGRA (B at byte 0, A at byte 3).
            //   We write only the 3 colour channels remapped to R,G,B order
            //   and discard alpha.  srcChannelCount=4 so stride arithmetic is
            //   correct; outChannelCount=3 is what we write to the Zarr array.
            var pixelFormat = b.Buffers[0].PixelFormat;
            int bitsPerSample;
            int srcChannelCount;   // channels packed in the raw buffer
            int outChannelCount;   // channels written to Zarr
            bool isBGRA = false;

            switch (pixelFormat)
            {
                case PixelFormat.Format8bppIndexed:
                    bitsPerSample = 8;
                    srcChannelCount = 1;
                    outChannelCount = 1;
                    break;
                case PixelFormat.Format16bppGrayScale:
                    bitsPerSample = 16;
                    srcChannelCount = 1;
                    outChannelCount = 1;
                    break;
                case PixelFormat.Format24bppRgb:
                    bitsPerSample = 8;
                    srcChannelCount = 3;
                    outChannelCount = 3;
                    break;
                case PixelFormat.Format32bppArgb:
                case PixelFormat.Format32bppRgb:
                    // Raw buffer is BGRA (4 bytes/pixel).
                    // We extract 3 channels remapped to R,G,B order and drop A.
                    bitsPerSample = 8;
                    srcChannelCount = 4;
                    outChannelCount = 3;
                    isBGRA = true;
                    break;
                case PixelFormat.Format48bppRgb:
                    bitsPerSample = 16;
                    srcChannelCount = 3;
                    outChannelCount = 3;
                    break;
                default:
                    srcChannelCount = 1;
                    outChannelCount = 1;
                    bitsPerSample = Math.Max(b.bitsPerPixel, 8);
                    break;
            }

            var bytesPerSample = bitsPerSample / 8;
            var zarrDataType = MapDataType(bitsPerSample);
            var totalChannels = b.SizeC * outChannelCount;

            int tileW = 512;
            int tileH = 512;

            // ------------------------------------------------------------------
            // 2. Build per-level descriptors from b.Resolutions
            //    Level 0 is always the full-resolution image (b.SizeX / b.SizeY).
            //    Subsequent levels are taken directly from b.Resolutions[1..N].
            // ------------------------------------------------------------------

            var levelDescriptors = new List<ResolutionLevelDescriptor>();
            for (int lvl = 0; lvl < b.Resolutions.Count; lvl++)
            {
                var res = b.Resolutions[lvl];
                double dsample = b.GetLevelDownsample(lvl);   // 1.0 for level 0
                levelDescriptors.Add(new ResolutionLevelDescriptor(res.SizeX, res.SizeY, dsample));
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
                Name = Path.GetFileNameWithoutExtension(b.Filename),
                DataType = zarrDataType,
                PhysicalSizeX = b.PhysicalSizeX,
                PhysicalSizeY = b.PhysicalSizeY,
                PhysicalSizeZ = b.PhysicalSizeZ,
                ChunkY = tileH,
                ChunkX = tileW,
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
                for (int levelIndex = 0; levelIndex < levelDescriptors.Count; levelIndex++)
                {
                    var lvlDesc = levelDescriptors[levelIndex];

                    for (int t = 0; t < b.SizeT; t++)
                    {
                        for (int z = 0; z < b.SizeZ; z++)
                        {
                            for (int c = 0; c < b.SizeC; c++)
                            {
                                if (outChannelCount == 1)
                                {
                                    WritePlaneInTiles(
                                        writer, b, lvlDesc,
                                        t, c, z,
                                        bytesPerSample, tileW, tileH,
                                        levelIndex: levelIndex);
                                }
                                else
                                {
                                    for (int rgb = 0; rgb < outChannelCount; rgb++)
                                    {
                                        int globalC = c * outChannelCount + rgb;

                                        WritePlaneInTiles(
                                            writer, b, lvlDesc,
                                            t, globalC, z,
                                            bytesPerSample, tileW, tileH,
                                            levelIndex: levelIndex,
                                            rgbChannelIndex: rgb,
                                            srcChannelCount: srcChannelCount,
                                            isBGRA: isBGRA,
                                            logicalC: c);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            // Write Zarr v2 sidecar files so ImageJ / Fiji's N5 plugin can open
            // the dataset. The N5 plugin does not support Zarr v3 zarr.json and
            // requires .zgroup / .zattrs / .zarray files instead.
            WriteV2CompatibilityFiles(outputDir, b, levelDescriptors, zarrDataType, tileW, tileH, totalChannels);

            Recorder.Record(BioLib.Recorder.GetCurrentMethodInfo(), false, b);
        }

        // =====================================================================
        // Zarr v2 compatibility layer for ImageJ / Fiji N5 plugin
        // =====================================================================

        /// <summary>
        /// Writes Zarr v2 sidecar metadata files (.zgroup, .zattrs, .zarray)
        /// alongside the v3 zarr.json files so that ImageJ/Fiji's N5 plugin
        /// (which only understands Zarr v2 + OME-NGFF 0.4) can open the dataset.
        /// </summary>
        private static void WriteV2CompatibilityFiles(
            string outputDir,
            BioImage b,
            List<ResolutionLevelDescriptor> levelDescriptors,
            string zarrDataType,
            int tileW, int tileH,
            int totalChannels)
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            try
            {
                // --- root .zgroup ---
                File.WriteAllText(Path.Combine(outputDir, ".zgroup"),
                    "{\n  \"zarr_format\": 2\n}\n");

                // --- root .zattrs (OME-NGFF 0.4 multiscales) ---
                // Build datasets array entries with pre-computed strings to avoid
                // escaped-quote issues inside interpolation holes.
                var datasetEntries = new System.Text.StringBuilder();
                for (int i = 0; i < levelDescriptors.Count; i++)
                {
                    string sv = levelDescriptors[i].Downsample.ToString("G", ic);
                    if (i > 0) datasetEntries.Append(",\n        ");
                    datasetEntries.Append(
                        "{\"path\": \"" + i + "\", " +
                        "\"coordinateTransformations\": [{\"type\": \"scale\", " +
                        "\"scale\": [1, 1, 1, " + sv + ", " + sv + "]}]}");
                }

                string name = Path.GetFileNameWithoutExtension(b.Filename);
                string psx = (b.PhysicalSizeX > 0 ? b.PhysicalSizeX : 1.0).ToString("G", ic);
                string psy = (b.PhysicalSizeY > 0 ? b.PhysicalSizeY : 1.0).ToString("G", ic);
                string psz = (b.PhysicalSizeZ > 0 ? b.PhysicalSizeZ : 1.0).ToString("G", ic);

                string zattrs =
                    "{\n  \"multiscales\": [\n    {\n" +
                    "      \"version\": \"0.4\",\n" +
                    "      \"name\": \"" + name + "\",\n" +
                    "      \"axes\": [\n" +
                    "        {\"name\": \"t\", \"type\": \"time\"},\n" +
                    "        {\"name\": \"c\", \"type\": \"channel\"},\n" +
                    "        {\"name\": \"z\", \"type\": \"space\", \"unit\": \"micrometer\"},\n" +
                    "        {\"name\": \"y\", \"type\": \"space\", \"unit\": \"micrometer\"},\n" +
                    "        {\"name\": \"x\", \"type\": \"space\", \"unit\": \"micrometer\"}\n" +
                    "      ],\n" +
                    "      \"datasets\": [\n        " + datasetEntries + "\n      ],\n" +
                    "      \"coordinateTransformations\": [\n" +
                    "        {\"type\": \"scale\", \"scale\": [1, 1, " + psz + ", " + psy + ", " + psx + "]}\n" +
                    "      ]\n" +
                    "    }\n  ]\n}\n";

                File.WriteAllText(Path.Combine(outputDir, ".zattrs"), zattrs);

                // --- per-level .zarray ---
                // Map ZarrNET data type string to NumPy dtype string
                string numpyDtype;
                if (zarrDataType == "uint8") numpyDtype = "|u1";
                else if (zarrDataType == "float32") numpyDtype = "<f4";
                else numpyDtype = "<u2";

                for (int i = 0; i < levelDescriptors.Count; i++)
                {
                    var lvl = levelDescriptors[i];
                    string levelDir = Path.Combine(outputDir, i.ToString());
                    Directory.CreateDirectory(levelDir);

                    string zarray =
                        "{\n" +
                        "  \"zarr_format\": 2,\n" +
                        "  \"shape\": [" + b.SizeT + ", " + totalChannels + ", " + b.SizeZ + ", " + lvl.SizeY + ", " + lvl.SizeX + "],\n" +
                        "  \"chunks\": [1, 1, 1, " + tileH + ", " + tileW + "],\n" +
                        "  \"dtype\": \"" + numpyDtype + "\",\n" +
                        "  \"compressor\": {\n" +
                        "    \"id\": \"blosc\",\n" +
                        "    \"cname\": \"lz4\",\n" +
                        "    \"clevel\": 5,\n" +
                        "    \"shuffle\": 1\n" +
                        "  },\n" +
                        "  \"fill_value\": 0,\n" +
                        "  \"order\": \"C\",\n" +
                        "  \"filters\": null\n" +
                        "}\n";

                    File.WriteAllText(Path.Combine(levelDir, ".zarray"), zarray);
                }

                Console.WriteLine("Zarr v2 compatibility files written successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Warning: failed to write Zarr v2 compatibility files: " + ex.Message);
            }
        }

        // =====================================================================
        // Tiled plane writer
        // =====================================================================

        /// <summary>
        /// Walks the XY extent of a single (t, c, z) plane in tile-sized
        /// chunks at the given resolution level, fetching each tile via
        /// BioImage.GetTile and writing it as a sub-region into the Zarr array.
        /// </summary>
        private async static void WritePlaneInTiles(
            OmeZarrWriter writer,
            BioImage b,
            ResolutionLevelDescriptor lvlDesc,
            int t, int c, int z,
            int bytesPerSample,
            int tileW, int tileH,
            int levelIndex = 0,
            int rgbChannelIndex = -1,
            int srcChannelCount = 1,
            bool isBGRA = false,
            int logicalC = -1)
        {
            bool needsDeinterleave = rgbChannelIndex >= 0;
            int srcC = needsDeinterleave ? logicalC : c;

            for (int tileY = 0; tileY < lvlDesc.SizeY; tileY += tileH)
            {
                for (int tileX = 0; tileX < lvlDesc.SizeX; tileX += tileW)
                {
                    // Clamp to image bounds — edge tiles may be smaller.
                    int actualW = Math.Min(tileW, lvlDesc.SizeX - tileX);
                    int actualH = Math.Min(tileH, lvlDesc.SizeY - tileY);
                    int pixelCount = actualW * actualH;
                    int interleavedBytes = pixelCount * srcChannelCount * bytesPerSample;
                    int singleChannelBytes = pixelCount * bytesPerSample;

                    // Fetch tile from BioLib at the requested pyramid level.
                    var tileBitmap = await b.GetTile(
                        b.Coords[z, srcC, t], levelIndex, tileX, tileY, actualW, actualH);

                    byte[] pixelBytes = tileBitmap.Bytes;

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

                    writer.WriteRegionAsync(
                        t, c, z,
                        tileY, tileX,
                        actualH, actualW,
                        pixelBytes,
                        levelIndex: levelIndex).Wait();
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
                8 => "uint8",
                16 => "uint16",
                32 => "float32",
                _ => "uint16"
            };
        }

        /// <summary>
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
            int channelIndex,
            int totalChannels,
            int bytesPerSample,
            int pixelCount)
        {
            var output = new byte[pixelCount * bytesPerSample];
            var stride = totalChannels * bytesPerSample;
            var channelStart = channelIndex * bytesPerSample;

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