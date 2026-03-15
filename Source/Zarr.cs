using AForge;
using BioLib;
using ZarrNET.Core;
using System;
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
    /// the output array shape is always (T, C, Z, Y, X) with scalar pixels.a
    /// </summary>
    public static class Zarr
    {
        // =====================================================================
        // Public entry point
        // =====================================================================

        /// <summary>
        /// Saves the full 5D content of a <see cref="BioImage"/> as an
        /// OME-Zarr v3 dataset at <paramref name="outputDir"/>.
        /// </summary>
        public static void SaveZarr(BioImage b, string outputDir)
        {
            // ------------------------------------------------------------------
            // 1. Resolve dimensions and data type from the BioImage
            // ------------------------------------------------------------------

            // For stack images derive format from the loaded buffers.
            // For pyramidal images (buffers not loaded) derive from bitsPerPixel
            // and RGBChannelsCount which are always populated.
            int rgbChannelCount;
            int bytesPerSample;
            string zarrDataType;

            if (!b.isPyramidal && b.Buffers != null && b.Buffers.Count > 0)
            {
                var fmt = b.Resolutions[0].PixelFormat;
                switch (fmt)
                {
                    case AForge.PixelFormat.Format8bppIndexed:
                        bytesPerSample = 1; rgbChannelCount = 1; zarrDataType = "uint8";  break;
                    case AForge.PixelFormat.Format16bppGrayScale:
                        bytesPerSample = 2; rgbChannelCount = 1; zarrDataType = "uint16"; break;
                    case AForge.PixelFormat.Format24bppRgb:
                        bytesPerSample = 1; rgbChannelCount = 3; zarrDataType = "uint8";  break;
                    case AForge.PixelFormat.Format48bppRgb:
                        bytesPerSample = 2; rgbChannelCount = 3; zarrDataType = "uint16"; break;
                    default:
                        bytesPerSample = 1; rgbChannelCount = 1; zarrDataType = "uint8";  break;
                }
            }
            else
            {
                var fmt = b.Resolutions[0].PixelFormat;
                switch (fmt)
                {
                    case AForge.PixelFormat.Format8bppIndexed:
                        bytesPerSample = 1; rgbChannelCount = 1; zarrDataType = "uint8"; break;
                    case AForge.PixelFormat.Format16bppGrayScale:
                        bytesPerSample = 2; rgbChannelCount = 1; zarrDataType = "uint16"; break;
                    case AForge.PixelFormat.Format24bppRgb:
                        bytesPerSample = 1; rgbChannelCount = 3; zarrDataType = "uint8"; break;
                    case AForge.PixelFormat.Format48bppRgb:
                        bytesPerSample = 2; rgbChannelCount = 3; zarrDataType = "uint16"; break;
                    default:
                        bytesPerSample = 1; rgbChannelCount = 1; zarrDataType = "uint8"; break;
                }
                // Pyramidal: buffers are not loaded — derive from metadata.
                rgbChannelCount = (b.Channels != null && b.Channels.Count > 0) ? b.Channels[0].SamplesPerPixel : 1;
            }

            // Tile size — controls peak memory.  Aligned to the chunk grid so
            // each tile write targets exactly one chunk in Y and X, avoiding
            // read-modify-write overhead on the store.
            int tileW = 512;
            int tileH = 512;

            var coord = new ZarrNET.Core.ZCT(b.Coordinate.Z, b.Coordinate.C, b.Coordinate.T);

            var descriptor = new BioImageDescriptor(b.SizeX, b.SizeY, coord)
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
            // 2. Create the writer (bootstraps zarr.json metadata on disk)
            // ------------------------------------------------------------------

            var writer = OmeZarrWriter.CreateAsync(outputDir, descriptor).Result;

            try
            {
                // ------------------------------------------------------------------
                // 3. Stream tiles into the array
                // ------------------------------------------------------------------
                    for (int t = 0; t < b.SizeT; t++)
                    {
                        for (int z = 0; z < b.SizeZ; z++)
                        {
                            for (int c = 0; c < b.SizeC; c++)
                            {
                                if (rgbChannelCount == 1)
                                {
                                    WritePlaneInTiles(
                                        writer, b, descriptor,
                                        t, c, z,
                                        bytesPerSample, tileW, tileH);
                                }
                                else
                                {
                                    for (int rgb = 0; rgb < rgbChannelCount; rgb++)
                                    {
                                        int globalC = c * rgbChannelCount + rgb;

                                        WritePlaneInTiles(
                                            writer, b, descriptor,
                                            t, globalC, z,
                                            bytesPerSample, tileW, tileH,
                                            rgbChannelIndex: rgb,
                                            rgbChannelCount: rgbChannelCount,
                                            logicalC: c);
                                    }
                                }
                            }
                        }
                    }
            }
            finally
            {
                writer.DisposeAsync().AsTask().Wait();
            }
            Recorder.Record(BioLib.Recorder.GetCurrentMethodInfo(), false, b);
        }

        // =====================================================================
        // Tiled plane writer — kept with SaveZarr because they form one unit
        // =====================================================================

        /// <summary>
        /// Walks the XY extent of a single (t, c, z) plane in tile-sized
        /// chunks, fetching each tile via BioImage.GetTile and writing it
        /// as a sub-region into the Zarr array.
        /// </summary>
        private static void WritePlaneInTiles(
    OmeZarrWriter writer,
    BioImage b,
    BioImageDescriptor desc,
    int t, int c, int z,
    int bytesPerSample,
    int tileW, int tileH,
    int rgbChannelIndex = -1,
    int rgbChannelCount = 1,
    int logicalC = -1)
        {
            bool needsDeinterleave = rgbChannelIndex >= 0;
            int srcC = needsDeinterleave ? logicalC : c;

            if (!b.isPyramidal)
            {
                // ── Stack image path ──────────────────────────────────────────
                // Buffers are fully loaded in memory. Read raw bytes directly,
                // bypassing GetImageRGBA() which always returns 32bpp RGBA.
                int frameIndex = b.GetFrameIndex(z, srcC, t);
                AForge.Bitmap plane = b.Buffers[frameIndex];
                int stride = plane.Stride;

                for (int tileY = 0; tileY < desc.SizeY; tileY += tileH)
                {
                    for (int tileX = 0; tileX < desc.SizeX; tileX += tileW)
                    {
                        int actualW  = Math.Min(tileW, desc.SizeX - tileX);
                        int actualH  = Math.Min(tileH, desc.SizeY - tileY);
                        int rowBytes = actualW * bytesPerSample;

                        byte[] pixelBytes = new byte[rowBytes * actualH];
                        for (int row = 0; row < actualH; row++)
                        {
                            int srcOffset = (tileY + row) * stride + tileX * bytesPerSample;
                            Buffer.BlockCopy(plane.Bytes, srcOffset, pixelBytes, row * rowBytes, rowBytes);
                        }

                        if (needsDeinterleave)
                            pixelBytes = DeinterleaveChannel(pixelBytes, rgbChannelIndex, rgbChannelCount, bytesPerSample, actualW * actualH);

                        writer.WriteRegionAsync(t, c, z, tileY, tileX, actualH, actualW, pixelBytes).Wait();
                    }
                }
            }
            else
            {
                // ── Pyramidal image path ──────────────────────────────────────
                // Buffers are not loaded. Fetch each tile via GetTile which reads
                // from the slide source (SlideImage / Zarr / BioFormats).
                // GetTile returns a Bitmap whose .Bytes are in the native pixel
                // format at resolution level 0.
                for (int tileY = 0; tileY < desc.SizeY; tileY += tileH)
                {
                    for (int tileX = 0; tileX < desc.SizeX; tileX += tileW)
                    {
                        int actualW = Math.Min(tileW, desc.SizeX - tileX);
                        int actualH = Math.Min(tileH, desc.SizeY - tileY);

                        // Set the image coordinate so GetTile reads the right plane.
                        b.Coordinate = new AForge.ZCT(z, srcC, t);

                        var tileBitmap = b.GetTile(
                            b.GetFrameIndex(z, srcC, t), 0, tileX, tileY, actualW, actualH).Result;

                        if (tileBitmap == null)
                            continue;

                        // Extract raw bytes at the correct bit depth, stripping any
                        // stride padding and discarding alpha if the bitmap is RGBA.
                        byte[] pixelBytes = ExtractRawPixels(tileBitmap, actualW, actualH, bytesPerSample);

                        if (needsDeinterleave)
                            pixelBytes = DeinterleaveChannel(pixelBytes, rgbChannelIndex, rgbChannelCount, bytesPerSample, actualW * actualH);

                        writer.WriteRegionAsync(t, c, z, tileY, tileX, actualH, actualW, pixelBytes).Wait();
                    }
                }
            }
        }
        /// <summary>
        /// Copies exactly width × height × bytesPerSample from the bitmap's
        /// buffer, stripping any stride padding the bitmap format may include.
        /// </summary>
        /// <summary>
        /// Extracts raw pixels from a tile bitmap at the target bit depth.
        /// Handles RGBA bitmaps (from SlideImage paths) by discarding the alpha
        /// channel and packing only the colour samples.
        /// </summary>
        private static byte[] ExtractRawPixels(
            Bitmap tileBitmap, int width, int height, int bytesPerSample)
        {
            int pixelCount = width * height;
            int totalBytes = pixelCount * bytesPerSample;
            var src = tileBitmap.Bytes;

            // Fast path: buffer is already exactly the right size.
            if (src.Length == totalBytes)
                return src;

            // If the bitmap is 32bpp RGBA (4 bytes/px) and we want 1 byte/px (grayscale 8-bit),
            // take the red channel (byte 0 of each pixel).
            if (bytesPerSample == 1 && src.Length == pixelCount * 4)
            {
                var output = new byte[totalBytes];
                for (int i = 0; i < pixelCount; i++)
                    output[i] = src[i * 4];
                return output;
            }

            // If the bitmap is 32bpp RGBA and we want 2 bytes/px (grayscale 16-bit packed as RGBA),
            // take the red+green bytes of each pixel as the 16-bit sample.
            if (bytesPerSample == 2 && src.Length == pixelCount * 4)
            {
                var output = new byte[totalBytes];
                for (int i = 0; i < pixelCount; i++)
                {
                    output[i * 2]     = src[i * 4];
                    output[i * 2 + 1] = src[i * 4 + 1];
                }
                return output;
            }

            // General case: strip stride padding row by row.
            var result   = new byte[totalBytes];
            int rowBytes = width * bytesPerSample;
            int stride   = tileBitmap.Stride;
            for (int row = 0; row < height; row++)
                Buffer.BlockCopy(src, row * stride, result, row * rowBytes, rowBytes);

            return result;
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
        /// Extracts one colour channel from an interleaved RGB(A) buffer.
        ///
        /// Given pixels stored as [R0 G0 B0 R1 G1 B1 ...] with
        /// <paramref name="bytesPerSample"/> bytes per component, returns
        /// a contiguous single-channel plane.
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