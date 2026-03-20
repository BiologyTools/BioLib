using AForge;
using BioLib;
using ZarrNET.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
            var pixelFormat    = b.Buffers[0].PixelFormat;
            int bitsPerSample;
            int srcChannelCount;   // channels packed in the raw buffer
            int outChannelCount;   // channels written to Zarr
            bool isBGRA = false;

            switch (pixelFormat)
            {
                case PixelFormat.Format8bppIndexed:
                    bitsPerSample   = 8;
                    srcChannelCount = 1;
                    outChannelCount = 1;
                    break;
                case PixelFormat.Format16bppGrayScale:
                    bitsPerSample   = 16;
                    srcChannelCount = 1;
                    outChannelCount = 1;
                    break;
                case PixelFormat.Format24bppRgb:
                    bitsPerSample   = 8;
                    srcChannelCount = 3;
                    outChannelCount = 3;
                    break;
                case PixelFormat.Format32bppArgb:
                case PixelFormat.Format32bppRgb:
                    // Raw buffer is BGRA (4 bytes/pixel).
                    // We extract 3 channels remapped to R,G,B order and drop A.
                    bitsPerSample   = 8;
                    srcChannelCount = 4;
                    outChannelCount = 3;
                    isBGRA          = true;
                    break;
                case PixelFormat.Format48bppRgb:
                    bitsPerSample   = 16;
                    srcChannelCount = 3;
                    outChannelCount = 3;
                    break;
                default:
                    srcChannelCount = 1;
                    outChannelCount = 1;
                    bitsPerSample   = Math.Max(b.bitsPerPixel, 8);
                    break;
            }

            var bytesPerSample = bitsPerSample / 8;
            var zarrDataType   = MapDataType(bitsPerSample);
            var totalChannels  = b.SizeC * outChannelCount;

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
                var res        = b.Resolutions[lvl];
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

            Recorder.Record(BioLib.Recorder.GetCurrentMethodInfo(), false, b);
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
                8  => "uint8",
                16 => "uint16",
                32 => "float32",
                _  => "uint16"
            };
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

            // Each sub-directory of labels/ is one label image.
            foreach (string labelPath in Directory.GetDirectories(labelsDir))
            {
                string labelName = Path.GetFileName(labelPath);
                try
                {
                    result.AddRange(ReadOneLabelStore(b, labelPath, labelName));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Zarr.LoadLabelsAsROIs] Skipping label '{labelName}': {ex.Message}");
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

            // Open the label store with the same ZarrNET reader used for the
            // parent image so we get the same axis/shape metadata.
            var reader = OmeZarrReader.OpenAsync(labelPath).Result;
            var ms     = reader.AsMultiscaleImageAsync().Result;
            var levels = ms.OpenAllResolutionLevelsAsync().Result.ToList();

            if (levels.Count == 0)
                return result;

            // Always use the finest (full-resolution) level.
            var level = levels[0];
            var axes  = level.EffectiveAxes;
            var shape = level.Shape;

            int sizeX = GetAxisSizeStatic(axes, shape, "x");
            int sizeY = GetAxisSizeStatic(axes, shape, "y");
            int sizeZ = GetAxisSizeStatic(axes, shape, "z", 1);
            int sizeC = GetAxisSizeStatic(axes, shape, "c", 1);
            int sizeT = GetAxisSizeStatic(axes, shape, "t", 1);

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
                        long[] start = BuildStart(axes, shape, t, c, z);
                        long[] end   = BuildEnd(axes, shape, t, c, z, sizeX, sizeY);

                        var region     = new PixelRegion(start, end);
                        var regionData = level.ReadPixelRegionAsync(region).Result;
                        byte[] raw     = regionData.Data;

                        // Determine bytes-per-sample from raw buffer length.
                        int totalPixels   = sizeX * sizeY;
                        int bytesPerPixel = raw.Length / totalPixels;

                        // Collect unique non-zero label IDs and their pixel masks.
                        var labelMasks = SplitLabelPlane(raw, sizeX, sizeY, bytesPerPixel);

                        var zct = new AForge.ZCT(z, c, t);

                        foreach (var kv in labelMasks)
                        {
                            int    labelId   = kv.Key;
                            float[] maskData = kv.Value;

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
        private static Dictionary<int, float[]> SplitLabelPlane(
            byte[] raw, int w, int h, int bytesPerPixel)
        {
            int total  = w * h;
            var result = new Dictionary<int, float[]>();

            for (int i = 0; i < total; i++)
            {
                int labelId = ReadLabelId(raw, i, bytesPerPixel);
                if (labelId == 0)
                    continue;

                if (!result.TryGetValue(labelId, out float[]? mask))
                {
                    mask = new float[total];
                    result[labelId] = mask;
                }
                mask[i] = 1.0f;
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
