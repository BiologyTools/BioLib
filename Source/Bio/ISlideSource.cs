using BruTile;
using BruTile.Cache;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using AForge;
using Image = SixLabors.ImageSharp.Image;
using OpenSlideGTK;
namespace BioLib
{
    public class LruCache<TKey, TValue>
    {
        private readonly int capacity;
        public Dictionary<Info, LinkedListNode<(Info key, TValue value)>> cacheMap = new Dictionary<Info, LinkedListNode<(Info key, TValue value)>>();
        private LinkedList<(Info key, TValue value)> lruList = new LinkedList<(Info key, TValue value)>();

        public LruCache(int capacity)
        {
            this.capacity = capacity;
        }

        public TValue Get(Info key)
        {
            foreach (LinkedListNode<(Info key, TValue value)> item in cacheMap.Values)
            {
                Info k = item.Value.key;
                if (k.Coordinate == key.Coordinate && k.Index == key.Index)
                {
                    lruList.Remove(item);
                    lruList.AddLast(item);
                    return item.Value.value;
                }
            }
            return default(TValue);
        }

        public void Add(Info key, TValue value)
        {
            if (cacheMap.Count >= capacity)
            {
                var oldest = lruList.First;
                if (oldest != null)
                {
                    lruList.RemoveFirst();
                    cacheMap.Remove(oldest.Value.key);
                }
            }

            if (cacheMap.ContainsKey(key))
            {
                lruList.Remove(cacheMap[key]);
            }

            var newNode = new LinkedListNode<(Info key, TValue value)>((key, value));
            lruList.AddLast(newNode);
            cacheMap[key] = newNode;
        }
        public void Dispose()
        {
            foreach (LinkedListNode<(Info key, TValue value)> item in cacheMap.Values)
            {
                lruList.Remove(item);
            }
        }
    }
    public class Info
    {
        public int Level { get; set; }
        public ZCT Coordinate { get; set; }
        public TileIndex Index { get; set; }
        public Extent Extent { get; set; }
        public Info(ZCT coordinate, TileIndex index, Extent extent, int level)
        {
            Coordinate = coordinate;
            Index = index;
            Extent = extent;
            Level = level;
        }
    }
    public class TileCache
    {
        public LruCache<Info, byte[]> cache;
        private int capacity;
        SlideSourceBase source = null;
        public TileCache(SlideSourceBase source, int capacity = 1000)
        {
            this.source = source;
            this.capacity = capacity;
            this.cache = new LruCache<Info, byte[]>(capacity);
        }

        public async Task<byte[]> GetTile(Info inf)
        {
            byte[] data = cache.Get(inf);
            if (data != null)
            {
                return data;
            }
            byte[] tile = await LoadTile(inf);
            if (tile != null)
                AddTile(inf, tile);
            return tile;
        }

        public byte[] GetTileSync(Info inf,double unitsPerPixel)
        {
            try
            {
                if (SlideSourceBase.useGPU)
                {
                    TileInfo tf = new TileInfo();
                    tf.Index = inf.Index;
                    tf.Extent = inf.Extent;
                    if (this.source.Image.BioImage.SlideBase.stitch.HasTile(tf))
                        return null;
                }
            }
            catch (Exception)
            {
                SlideSourceBase.useGPU = false;
            }
            
            byte[] data = cache.Get(inf);
            if (data != null)
            {
                return data;
            }
            byte[] tile = LoadTileSync(inf);
            if (tile != null && !SlideSourceBase.useGPU)
                AddTile(inf, tile);
            return tile;
        }

        private void AddTile(Info tileId, byte[] tile)
        {
            cache.Add(tileId, tile);
        }

        private async Task<byte[]> LoadTile(Info tileId)
        {
            try
            {
                return await source.GetTileAsync(tileId);
            }
            catch (Exception e)
            {
                return null;
            }
        }
        private byte[] LoadTileSync(Info tileId)
        {
            try
            {
                return source.GetTile(tileId);
            }
            catch (Exception e)
            {
                return null;
            }
        }
        public void Dispose()
        {
            cache.Dispose();
        }
    }

    public abstract class SlideSourceBase : ISlideSource, IDisposable
    {
        #region Static
        public static bool UseRealResolution { get; set; } = true;

        private static IDictionary<string, Func<string, bool, ISlideSource>> keyValuePairs = new Dictionary<string, Func<string, bool, ISlideSource>>();

        /// <summary>
        /// resister decode for Specific format
        /// </summary>
        /// <param name="extensionUpper">dot and extension upper</param>
        /// <param name="factory">file path,enable cache,decoder</param>
        public static void Resister(string extensionUpper, Func<string, bool, ISlideSource> factory)
        {
            keyValuePairs.Add(extensionUpper, factory);
        }


        public static ISlideSource Create(BioImage source, SlideImage im, bool enableCache = true)
        {

            var ext = Path.GetExtension(source.file).ToUpper();
            try
            {
                if (keyValuePairs.TryGetValue(ext, out var factory) && factory != null)
                    return factory.Invoke(source.file, enableCache);

                if (!string.IsNullOrEmpty(SlideBase.DetectVendor(source.file)))
                {
                    SlideBase b = new SlideBase(source, im, enableCache);

                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return null;
        }
        #endregion
        public double MinUnitsPerPixel { get; protected set; }
        public static Extent destExtent;
        public static Extent sourceExtent;
        public static double curUnitsPerPixel = 1;
        public static bool UseVips = true;
        public TileCache cache = null;
        public Stitch stitch = new Stitch();
        public static bool useGPU = true;
        public async Task<byte[]> GetSlice(SliceInfo sliceInfo)
        {
            if (cache == null)
                cache = new TileCache(this);
            var curLevel = Image.BioImage.LevelFromResolution(sliceInfo.Resolution);
            var curUnitsPerPixel = sliceInfo.Resolution;
            var tileInfos = Schema.GetTileInfos(sliceInfo.Extent, curLevel);
            List<Tuple<Extent, byte[]>> tiles = new List<Tuple<Extent, byte[]>>();
           
            foreach (BruTile.TileInfo t in tileInfos)
            {
                Info tf = new Info(sliceInfo.Coordinate, t.Index, t.Extent, curLevel);
                byte[] c = cache.GetTileSync(tf, curUnitsPerPixel);
                if (c != null)
                {
                    if (Image.BioImage.Resolutions[curLevel].PixelFormat == PixelFormat.Format16bppGrayScale)
                    {
                        c = Convert16BitToRGB(c);
                    }
                    else
                    if (Image.BioImage.Resolutions[curLevel].PixelFormat == PixelFormat.Format48bppRgb)
                    {
                        c = Convert48BitToRGB(c);
                    }
                    if (useGPU)
                    {
                        TileInfo tileInfo = new TileInfo();
                        tileInfo.Extent = t.Extent.WorldToPixelInvertedY(curUnitsPerPixel);
                        tileInfo.Index = t.Index;
                        stitch.AddTile(Tuple.Create(tileInfo,c));
                    }
                    else
                        tiles.Add(Tuple.Create(t.Extent.WorldToPixelInvertedY(curUnitsPerPixel), c));
                }
            }
            var srcPixelExtent = sliceInfo.Extent.WorldToPixelInvertedY(curUnitsPerPixel);
            var dstPixelExtent = sliceInfo.Extent.WorldToPixelInvertedY(sliceInfo.Resolution);
            var dstPixelHeight = sliceInfo.Parame.DstPixelHeight > 0 ? sliceInfo.Parame.DstPixelHeight : dstPixelExtent.Height;
            var dstPixelWidth = sliceInfo.Parame.DstPixelWidth > 0 ? sliceInfo.Parame.DstPixelWidth : dstPixelExtent.Width;
            destExtent = new Extent(0, 0, dstPixelWidth, dstPixelHeight);
            sourceExtent = srcPixelExtent;
            if (useGPU)
            {
                try
                {
                    return stitch.StitchImages(tileInfos.ToList(),(int)Math.Round(dstPixelWidth), (int)Math.Round(dstPixelHeight), Math.Round(srcPixelExtent.MinX), Math.Round(srcPixelExtent.MinY),curUnitsPerPixel);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message.ToString());
                    UseVips = true;
                    useGPU = false;
                }
            }
            if (UseVips)
            {
                try
                {
                    NetVips.Image im = null;
                    if (this.Image.BioImage.Resolutions[curLevel].PixelFormat == PixelFormat.Format16bppGrayScale)
                        im = ImageUtil.JoinVips16(tiles, srcPixelExtent, new Extent(0, 0, dstPixelWidth, dstPixelHeight));
                    else if (this.Image.BioImage.Resolutions[curLevel].PixelFormat == PixelFormat.Format24bppRgb)
                        im = ImageUtil.JoinVipsRGB24(tiles, srcPixelExtent, new Extent(0, 0, dstPixelWidth, dstPixelHeight));
                    return im.WriteToMemory();
                }
                catch (Exception e)
                {
                    UseVips = false;
                    Console.WriteLine("Failed to use LibVips please install Libvips for your platform.");
                    Console.WriteLine(e.Message);
                }
            }
            try
            {
                Image im = null;
                if (this.Image.BioImage.Resolutions[curLevel].PixelFormat == PixelFormat.Format16bppGrayScale)
                {
                    im = ImageUtil.Join16(tiles, srcPixelExtent, new Extent(0, 0, dstPixelWidth, dstPixelHeight));
                    byte[] bts = Get16Bytes((Image<L16>)im);
                    im.Dispose();
                    return bts;
                }
                else if (this.Image.BioImage.Resolutions[curLevel].PixelFormat == PixelFormat.Format24bppRgb)
                {
                    im = ImageUtil.JoinRGB24(tiles, srcPixelExtent, new Extent(0, 0, dstPixelWidth, dstPixelHeight));
                    byte[] bts = GetRgb24Bytes((Image<Rgb24>)im);
                    im.Dispose();
                    return bts;
                }
            }
            catch (Exception er)
            {
                Console.WriteLine(er.Message);
                return null;
            }
            return null;
        }
        public byte[] Convert16BitToRGB(byte[] input16BitGrayscale)
        {
            if (input16BitGrayscale.Length % 2 != 0)
            {
                throw new ArgumentException("Input data length must be even, as each grayscale value is 2 bytes.");
            }

            int pixelCount = input16BitGrayscale.Length / 2;
            byte[] rgbData = new byte[pixelCount * 3];

            for (int i = 0, j = 0; i < input16BitGrayscale.Length; i += 2, j += 3)
            {
                // Combine the two bytes into a single 16-bit grayscale value
                ushort grayscale16 = (ushort)((input16BitGrayscale[i + 1] << 8) | input16BitGrayscale[i]);

                // Convert the 16-bit grayscale value to an 8-bit value
                byte grayscale8 = (byte)(grayscale16 >> 8); // Normalize to 8-bit by shifting

                // Set the RGB values (R = G = B = grayscale)
                rgbData[j] = grayscale8;     // Red channel
                rgbData[j + 1] = grayscale8; // Green channel
                rgbData[j + 2] = grayscale8; // Blue channel
            }

            return rgbData;
        }
        public byte[] Convert48BitToRGB(byte[] input48BitData)
        {
            if (input48BitData.Length % 6 != 0)
            {
                throw new ArgumentException("Input data length must be a multiple of 6, as each pixel is 6 bytes.");
            }

            int pixelCount = input48BitData.Length / 6;
            byte[] rgbData = new byte[pixelCount * 3];

            for (int i = 0, j = 0; i < input48BitData.Length; i += 6, j += 3)
            {
                // Read 16-bit values for Red, Green, and Blue channels
                ushort red16 = (ushort)((input48BitData[i + 1] << 8) | input48BitData[i]);
                ushort green16 = (ushort)((input48BitData[i + 3] << 8) | input48BitData[i + 2]);
                ushort blue16 = (ushort)((input48BitData[i + 5] << 8) | input48BitData[i + 4]);

                // Convert 16-bit values to 8-bit by shifting right (essentially downscaling from 16-bit to 8-bit)
                byte red8 = (byte)(red16 >> 8);
                byte green8 = (byte)(green16 >> 8);
                byte blue8 = (byte)(blue16 >> 8);

                // Store the 8-bit values in the RGB array
                rgbData[j] = red8;   // Red
                rgbData[j + 1] = green8; // Green
                rgbData[j + 2] = blue8;  // Blue
            }

            return rgbData;
        }

        public byte[] GetRgb24Bytes(Image<Rgb24> image)
        {
            int width = image.Width;
            int height = image.Height;
            byte[] rgbBytes = new byte[width * height * 3]; // 3 bytes per pixel (RGB)

            int byteIndex = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Rgb24 pixel = image[x, y];
                    rgbBytes[byteIndex++] = pixel.R;
                    rgbBytes[byteIndex++] = pixel.G;
                    rgbBytes[byteIndex++] = pixel.B;
                }
            }

            return rgbBytes;
        }
        public byte[] Get16Bytes(Image<L16> image)
        {
            int width = image.Width;
            int height = image.Height;
            byte[] bytes = new byte[width * height * 2];

            int byteIndex = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    L16 pixel = image[x, y];
                    byte[] bts = BitConverter.GetBytes(pixel.PackedValue);
                    bytes[byteIndex++] = bts[0];
                    bytes[byteIndex++] = bts[1];
                }
            }

            return bytes;
        }

        public SlideImage Image { get; set; }

        public ITileSchema Schema { get; protected set; }

        public string Name { get; protected set; }

        public Attribution Attribution { get; protected set; }

        public IReadOnlyDictionary<string, object> ExternInfo { get; protected set; }

        public string Source { get; protected set; }

        public abstract IReadOnlyDictionary<string, byte[]> GetExternImages();

        #region IDisposable
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //_bgraCache.Dispose();
                }
                disposedValue = true;
            }
        }

        ~SlideSourceBase()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        public byte[] GetTile(Info tileInfo)
        {
            if (tileInfo == null)
                return null;
            var r = Schema.Resolutions[tileInfo.Index.Level].UnitsPerPixel;
            var tileWidth = Schema.Resolutions[tileInfo.Index.Level].TileWidth;
            var tileHeight = Schema.Resolutions[tileInfo.Index.Level].TileHeight;
            var curLevelOffsetXPixel = tileInfo.Extent.MinX / Schema.Resolutions[tileInfo.Index.Level].UnitsPerPixel;
            var curLevelOffsetYPixel = -tileInfo.Extent.MaxY / Schema.Resolutions[tileInfo.Index.Level].UnitsPerPixel;
            var curTileWidth = (int)(tileInfo.Extent.MaxX > Schema.Extent.Width ? tileWidth - (tileInfo.Extent.MaxX - Schema.Extent.Width) / r : tileWidth);
            var curTileHeight = (int)(-tileInfo.Extent.MinY > Schema.Extent.Height ? tileHeight - (-tileInfo.Extent.MinY - Schema.Extent.Height) / r : tileHeight);
            var bgraData = Image.ReadRegion(tileInfo.Index.Level, (long)curLevelOffsetXPixel, (long)curLevelOffsetYPixel, curTileWidth, curTileHeight);
            return bgraData;
        }
        public async Task<byte[]> GetTileAsync(Info tileInfo)
        {
            if (tileInfo == null)
                return null;
            var r = Schema.Resolutions[tileInfo.Index.Level].UnitsPerPixel;
            var tileWidth = Schema.Resolutions[tileInfo.Index.Level].TileWidth;
            var tileHeight = Schema.Resolutions[tileInfo.Index.Level].TileHeight;
            var curLevelOffsetXPixel = tileInfo.Extent.MinX / Schema.Resolutions[tileInfo.Index.Level].UnitsPerPixel;
            var curLevelOffsetYPixel = -tileInfo.Extent.MaxY / Schema.Resolutions[tileInfo.Index.Level].UnitsPerPixel;
            var curTileWidth = (int)(tileInfo.Extent.MaxX > Schema.Extent.Width ? tileWidth - (tileInfo.Extent.MaxX - Schema.Extent.Width) / r : tileWidth);
            var curTileHeight = (int)(-tileInfo.Extent.MinY > Schema.Extent.Height ? tileHeight - (-tileInfo.Extent.MinY - Schema.Extent.Height) / r : tileHeight);
            var bgraData = await Image.ReadRegionAsync(tileInfo.Index.Level, (long)curLevelOffsetXPixel, (long)curLevelOffsetYPixel, curTileWidth, curTileHeight, tileInfo.Coordinate);
            return bgraData;
        }
        public async Task<byte[]> GetTileAsync(BruTile.TileInfo tileInfo)
        {
            if (tileInfo == null)
                return null;
            var r = Schema.Resolutions[tileInfo.Index.Level].UnitsPerPixel;
            var tileWidth = Schema.Resolutions[tileInfo.Index.Level].TileWidth;
            var tileHeight = Schema.Resolutions[tileInfo.Index.Level].TileHeight;
            var curLevelOffsetXPixel = tileInfo.Extent.MinX / Schema.Resolutions[tileInfo.Index.Level].UnitsPerPixel;
            var curLevelOffsetYPixel = -tileInfo.Extent.MaxY / Schema.Resolutions[tileInfo.Index.Level].UnitsPerPixel;
            var curTileWidth = (int)(tileInfo.Extent.MaxX > Schema.Extent.Width ? tileWidth - (tileInfo.Extent.MaxX - Schema.Extent.Width) / r : tileWidth);
            var curTileHeight = (int)(-tileInfo.Extent.MinY > Schema.Extent.Height ? tileHeight - (-tileInfo.Extent.MinY - Schema.Extent.Height) / r : tileHeight);
            var bgraData = await Image.ReadRegionAsync(tileInfo.Index.Level, (long)curLevelOffsetXPixel, (long)curLevelOffsetYPixel, curTileWidth, curTileHeight, new ZCT());
            return bgraData;
        }
        public static byte[] ConvertRgbaToRgb(byte[] rgbaArray)
        {
            // Initialize a new byte array for RGB24 format
            byte[] rgbArray = new byte[(rgbaArray.Length / 4) * 3];

            for (int i = 0, j = 0; i < rgbaArray.Length; i += 4, j += 3)
            {
                // Copy the R, G, B values, skip the A value
                rgbArray[j] = rgbaArray[i + 2];     // B
                rgbArray[j + 1] = rgbaArray[i + 1]; // G
                rgbArray[j + 2] = rgbaArray[i]; // R
            }

            return rgbArray;
        }
        #endregion
    }

    /// <summary>
    /// 
    /// </summary>
    public interface ISlideSource : ITileSource, ISliceProvider, ISlideExternInfo
    {

    }

    /// <summary>
    /// </summary>
    public interface ISlideExternInfo
    {
        /// <summary>
        /// File path.
        /// </summary>
        string Source { get; }

        /// <summary>
        /// Extern info.
        /// </summary>
        IReadOnlyDictionary<string, object> ExternInfo { get; }

        /// <summary>
        /// Extern image.
        /// </summary>
        /// <returns></returns>
        IReadOnlyDictionary<string, byte[]> GetExternImages();
    }

    /// <summary>
    /// </summary>
    public interface ISliceProvider
    {
        /// <summary>
        /// um/pixel
        /// </summary>
        double MinUnitsPerPixel { get; }

        /// <summary>
        /// Get slice.
        /// </summary>
        /// <param name="sliceInfo">Slice info</param>
        /// <returns></returns>
        Task<byte[]> GetSlice(SliceInfo sliceInfo);
    }

    /// <summary>
    /// Slice info.
    /// </summary>
    public class SliceInfo
    {
        public SliceInfo() { }

        /// <summary>
        /// Create a world extent by pixel and resolution.
        /// </summary>
        /// <param name="xPixel">pixel x</param>
        /// <param name="yPixel">pixel y</param>
        /// <param name="widthPixel">pixel width</param>
        /// <param name="heightPixel">pixel height</param>
        /// <param name="unitsPerPixel">um/pixel</param>
        public SliceInfo(double xPixel, double yPixel, double widthPixel, double heightPixel, double unitsPerPixel, ZCT coord)
        {
            Extent = new Extent(xPixel, yPixel, xPixel + widthPixel, yPixel + heightPixel).PixelToWorldInvertedY(unitsPerPixel);
            Resolution = unitsPerPixel;
        }

        /// <summary>
        /// um/pixel
        /// </summary>
        public double Resolution
        {
            get;
            set;
        } = 1;
        /// <summary>
        /// ZCT Coordinate
        /// </summary>
        public ZCT Coordinate { get; set; }

        /// <summary>
        /// World extent.
        /// </summary>
        public Extent Extent
        {
            get;
            set;
        }
        public SliceParame Parame
        {
            get;
            set;
        } = new SliceParame();
    }

    public class SliceParame
    {
        /// <summary>
        /// Scale to width,default 0(no scale)
        /// /// </summary>
        public int DstPixelWidth { get; set; } = 0;

        /// <summary>
        /// Scale to height,default 0(no scale)
        /// </summary>
        public int DstPixelHeight { get; set; } = 0;

        /// <summary>
        /// Sample mode.
        /// </summary>
        public SampleMode SampleMode { get; set; } = SampleMode.Nearest;

        /// <summary>
        /// Image quality.
        /// </summary>
        public int? Quality { get; set; }
    }


    public enum SampleMode
    {
        /// <summary>
        /// Nearest.
        /// </summary>
        Nearest = 0,
        /// <summary>
        /// Nearest up.
        /// </summary>
        NearestUp,
        /// <summary>
        /// Nearest down.
        /// </summary>
        NearestDown,
        /// <summary>
        /// Top.
        /// </summary>
        Top,
        /// <summary>
        /// Bottom.
        /// </summary>
        /// <remarks>
        /// maybe very slow, just for clearer images.
        /// </remarks>
        Bottom,
    }

    /// <summary>
    /// Image type.
    /// </summary>
    public enum ImageType : int
    {
        /// <summary>
        /// </summary>
        Label,

        /// <summary>
        /// </summary>
        Title,

        /// <summary>
        /// </summary>
        Preview,
    }

}