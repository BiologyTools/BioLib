using BioLib;
using ZarrNET.Core;
using ZarrNET;
using System;
using System.IO;
using OmeZarr.Core.OmeZarr;

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

            var rgbChannelCount = 1;
            var bitsPerSample = b.bitsPerPixel / Math.Max(rgbChannelCount, 1);
            var bytesPerSample = bitsPerSample / 8;
            var zarrDataType = MapDataType(bitsPerSample);

            // BioLib's SizeC is the number of "logical" channels.  When buffers
            // are RGB-interleaved (RGBChannelCount == 3) each buffer packs 3
            // colour samples per pixel, so the Zarr channel count is SizeC × RGB.
            var totalChannels = b.SizeC * rgbChannelCount;

            // Tile size — controls peak memory.  Aligned to the chunk grid so
            // each tile write targets exactly one chunk in Y and X, avoiding
            // read-modify-write overhead on the store.
            int tileW = 512;
            int tileH = 512;

            var coord = new ZCT(b.SizeZ, totalChannels, b.SizeT);

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
        /// chunks, fetching each tile via <see cref="BioImage.GetTile"/> and
        /// writing it as a sub-region into the Zarr array.
        ///
        /// Peak memory is bounded to one tile (~tileW × tileH × bytesPerSample)
        /// regardless of full image dimensions.
        ///
        /// For RGB sources, <paramref name="rgbChannelIndex"/> /
        /// <paramref name="rgbChannelCount"/> extract a single colour
        /// component from the interleaved tile before writing.
        /// </summary>
        private async static void WritePlaneInTiles(
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

            for (int tileY = 0; tileY < desc.ChunkY; tileY += tileH)
            {
                for (int tileX = 0; tileX < desc.ChunkX; tileX += tileW)
                {
                    // Clamp to image bounds — edge tiles may be smaller.
                    int actualW = Math.Min(tileW, desc.SizeX - tileX);
                    int actualH = Math.Min(tileH, desc.SizeY - tileY);

                    // Fetch only this tile from BioLib — no full plane in RAM.
                    var tileCoord = new ZCT(z, srcC, t);
                    var tileBitmap = await b.GetTile(b.Coords[z,c,t],b.Level, tileX, tileY, actualW, actualH);
                    tileBitmap.To8Bit(false);
                    writer.WriteRegionAsync(
                        t, c, z,
                        tileY, tileX,
                        actualH, actualW,
                        tileBitmap.Bytes).Wait();
                }
            }
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