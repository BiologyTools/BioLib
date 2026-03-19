using BruTile;
using BruTile.Cache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using AForge;
using ZarrNET.Core.OmeZarr.Nodes;
using ZarrNET.Core.OmeZarr.Metadata;
using ZarrNET.Core.Nodes;

namespace BioLib
{
    public class SlideBase : SlideSourceBase
    {
        public readonly SlideImage SlideImage;
        public SlideBase(BioImage source, SlideImage im, bool enableCache = true)
        {
            Source = source.file;
            SlideImage = im;
            Image = im;
            double minUnitsPerPixel;
            if (source.PhysicalSizeX < source.PhysicalSizeY) minUnitsPerPixel = source.PhysicalSizeX; else minUnitsPerPixel = source.PhysicalSizeY;
            MinUnitsPerPixel = UseRealResolution ? minUnitsPerPixel : 1;
            if (MinUnitsPerPixel <= 0) MinUnitsPerPixel = 1;
            var height = SlideImage.Dimensions.Height;
            var width = SlideImage.Dimensions.Width;
            //ExternInfo = GetInfo();
            Schema = new TileSchema
            {
                YAxis = YAxis.OSM,
                Format = "jpg",
                Extent = new Extent(0, -height, width, 0),
                OriginX = 0,
                OriginY = 0,
            };
            InitResolutions(Schema.Resolutions, 256, 256);
        }

        public static string DetectVendor(string source)
        {
            return SlideImage.DetectVendor(source);
        }

        /// <summary>
        /// Replaces the BruTile schema using the field pyramid dimensions from
        /// <c>ZarrWellLevels</c>. Called after construction when the BioImage is
        /// a well-plate so the tile coordinate space matches the active field,
        /// not the whole-plate resolution list.
        /// </summary>

        private static void Log(string msg)
        {
            try { System.IO.File.AppendAllText(@"C:\\Users\\Public\\biolog.txt", msg + "\n"); }
            catch { }
        }
        public void RebuildSchemaForWell(BioImage b)
        {
            if (b.ZarrWellLevels == null || b.ZarrWellLevels.Count == 0)
            {
                Log($"[RebuildSchemaForWell] ZarrWellLevels empty — schema not rebuilt");
                return;
            }
            int fi = b.WellIndex;
            var fieldLevels = b.ZarrWellLevels[fi];
            if (fieldLevels == null || fieldLevels.Count == 0)
            {
                Log($"[RebuildSchemaForWell] fieldLevels null/empty for fi={fi}");
                return;
            }

            int w0 = 1, h0 = 1;
            GetFieldDims(fieldLevels[0], out w0, out h0);
            Log($"[RebuildSchemaForWell] field fi={fi}, level0 w={w0} h={h0}, levels={fieldLevels.Count}");

            var newSchema = new BruTile.TileSchema
            {
                YAxis    = BruTile.YAxis.OSM,
                Format   = "jpg",
                Extent   = new BruTile.Extent(0, -h0, w0, 0),
                OriginX  = 0,
                OriginY  = 0,
            };

            for (int lev = 0; lev < fieldLevels.Count; lev++)
            {
                GetFieldDims(fieldLevels[lev], out int wL, out int hL);
                double downsample = w0 > 0 ? (double)w0 / Math.Max(1, wL) : Math.Pow(2, lev);
                newSchema.Resolutions[lev] = new BruTile.Resolution(lev, downsample, 256, 256);
                Log($"[RebuildSchemaForWell]   level {lev}: w={wL} h={hL} unitsPerPixel={downsample}");
            }

            Schema = newSchema;
        }

        private static void GetFieldDims(ResolutionLevelNode node, out int w, out int h)
        {
            w = 1; h = 1;
            var axes  = node.EffectiveAxes;
            var shape = node.Shape;
            for (int i = 0; i < axes.Length; i++)
            {
                switch (axes[i].Name.ToLowerInvariant())
                {
                    case "x": w = (int)shape[i]; break;
                    case "y": h = (int)shape[i]; break;
                }
            }
        }

        
        public override IReadOnlyDictionary<string, byte[]> GetExternImages()
        {
            throw new NotImplementedException();
            /*
            Dictionary<string, byte[]> images = new Dictionary<string, byte[]>();
            var r = Math.Max(Schema.Extent.Height, Schema.Extent.Width) / 512;
            images.Add("preview", GetSlice(new SliceInfo { Extent = Schema.Extent, Resolution = r }));
            foreach (var item in SlideImage.GetAssociatedImages())
            {
                var dim = item.Value.Dimensions;
                images.Add(item.Key, ImageUtil.GetJpeg(item.Value.Data, 4, 4 * (int)dim.Width, (int)dim.Width, (int)dim.Height));
            }
            return images;
            */
        }
        private static Image<Rgb24> CreateImageFromRgbaData(byte[] rgbaData, int width, int height)
        {
            Image<Rgb24> image = new Image<Rgb24>(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 4;
                    byte r = rgbaData[index];
                    byte g = rgbaData[index + 1];
                    byte b = rgbaData[index + 2];
                    // byte a = rgbaData[index + 3]; // Alpha channel, not used in Rgb24

                    image[x, y] = new Rgb24(r, g, b);
                }
            }

            return image;
        }
        public byte[] GetTile(TileInfo tileInfo, ZCT coord)
        {
            try
            {
                if (Schema == null)
                {
                    Console.WriteLine("Schema is null.");
                    return null;
                }
                var r          = Schema.Resolutions[tileInfo.Index.Level].UnitsPerPixel;
                var tileWidth  = Schema.Resolutions[tileInfo.Index.Level].TileWidth;
                var tileHeight = Schema.Resolutions[tileInfo.Index.Level].TileHeight;

                var curLevelOffsetXPixel = tileInfo.Extent.MinX / r;
                var curLevelOffsetYPixel = -tileInfo.Extent.MaxY / r;

                // Clamp request to actual image pixels at this level (edge tiles are smaller)
                var curTileWidth  = (int)(tileInfo.Extent.MaxX  > Schema.Extent.Width
                    ? tileWidth  - (tileInfo.Extent.MaxX  - Schema.Extent.Width)  / r
                    : tileWidth);
                var curTileHeight = (int)(-tileInfo.Extent.MinY > Schema.Extent.Height
                    ? tileHeight - (-tileInfo.Extent.MinY - Schema.Extent.Height) / r
                    : tileHeight);

                // Guard against degenerate extents
                curTileWidth  = Math.Max(1, curTileWidth);
                curTileHeight = Math.Max(1, curTileHeight);

                if (SlideImage == null)
                {
                    Console.WriteLine("SlideImage is null.");
                    return null;
                }

                var bgraData = SlideImage.ReadRegion(
                    tileInfo.Index.Level, coord,
                    (long)curLevelOffsetXPixel, (long)curLevelOffsetYPixel,
                    curTileWidth, curTileHeight);

                if (bgraData == null || bgraData.Length == 0)
                    return null;

                int expectedSize = tileWidth * tileHeight * 4;

                // Full tile — return as-is.
                // Return the raw BGRA data (4 bytes/pixel) so the GL upload path
                // receives a buffer that matches PixelFormat.Bgra / PixelType.UnsignedByte.
                if (bgraData.Length == expectedSize)
                    return bgraData;

                // Edge tile: ReadRegion returned fewer pixels than a full tile because
                // this tile hangs off the image boundary. Pad into a full tileWidth x
                // tileHeight RGBA buffer (rows outside the image stay transparent/zeroed)
                // so the GL upload always receives a consistently-sized, correctly-strided
                // buffer and never reads past the end.
                int actualW = Math.Min(curTileWidth,  tileWidth);
                int actualH = Math.Min(curTileHeight, tileHeight);
                int srcStride = actualW * 4;

                // If ReadRegion returned even less than expected, derive safe row count
                if (bgraData.Length < actualW * actualH * 4)
                {
                    int safePixels = bgraData.Length / 4;
                    actualH = safePixels / actualW;
                    if (actualH == 0) return null;
                }

                byte[] padded = new byte[expectedSize]; // zero-initialised = transparent
                for (int row = 0; row < actualH; row++)
                {
                    int srcOffset = row * srcStride;
                    int dstOffset = row * tileWidth * 4;
                    Buffer.BlockCopy(bgraData, srcOffset, padded, dstOffset, srcStride);
                }
                return padded;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + " " + e.StackTrace);
            }
            return null;
        }
        public static byte[] ConvertRgbaToRgb(byte[] rgbaArray)
        {
            // Initialize a new byte array for RGB24 format
            byte[] rgbArray = new byte[(rgbaArray.Length / 4) * 3];

            for (int i = 0, j = 0; i < rgbaArray.Length; i += 4, j += 3)
            {
                // Copy the R, G, B values, skip the A value
                rgbArray[j] = rgbaArray[i];     // B
                rgbArray[j + 1] = rgbaArray[i + 1]; // G
                rgbArray[j + 2] = rgbaArray[i + 2]; // R
            }

            return rgbArray;
        }

        protected void InitResolutions(IDictionary<int, BruTile.Resolution> resolutions, int tileWidth, int tileHeight)
        {
            for (int i = 0; i < SlideImage.LevelCount; i++)
            {
                /*
                bool useInternalWidth = int.TryParse(ExternInfo.TryGetValue($"openslide.level[{i}].tile-width", out var _w) ? (string)_w : null, out var w) && w >= tileWidth;
                bool useInternalHeight = int.TryParse(ExternInfo.TryGetValue($"openslide.level[{i}].tile-height", out var _h) ? (string)_h : null, out var h) && h >= tileHeight;

                bool useInternalSize = useInternalHeight && useInternalWidth;
                var tw = useInternalSize ? w : tileWidth;
                var th = useInternalSize ? h : tileHeight;
                resolutions.Add(i, new Resolution(i, MinUnitsPerPixel * SlideImage.GetLevelDownsample(i), tw, th));
                */
                resolutions.Add(i, new BruTile.Resolution(i, SlideImage.BioImage.GetUnitPerPixel(i), tileWidth, tileHeight));
            }
        }

        #region IDisposable
        private bool disposedValue;
        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    SlideImage.Dispose();
                }
                disposedValue = true;
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
