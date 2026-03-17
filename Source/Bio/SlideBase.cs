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
