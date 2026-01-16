using AForge;
using BruTile;
using BruTile.Cache;
using OpenSlideGTK;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Image = SixLabors.ImageSharp.Image;
namespace BioLib
{
    public class LruCache<TileInformation, TValue>
    {
        public class Info
        {
            public ZCT Coordinate { get; set; }
            public TileIndex Index { get; set; }
        }
        private readonly int capacity = 200;
        private Dictionary<Info, LinkedListNode<(Info key, TValue value)>> cacheMap = new Dictionary<Info, LinkedListNode<(Info key, TValue value)>>();
        private LinkedList<(Info key, TValue value)> lruList = new LinkedList<(Info key, TValue value)>();

        public LruCache(int capacity)
        {
            this.capacity = capacity;
        }
        public int Count
        {
            get
            {
                return cacheMap.Count;
            }
        }
        public TValue Get(Info key)
        {
            foreach (LinkedListNode<(Info key, TValue value)> item in cacheMap.Values)
            {
                Info k = item.Value.key;
                if(k.Coordinate == key.Coordinate && k.Index == key.Index)
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

        public void RemoveTile(Info key)
        {
            foreach (LinkedListNode<(Info key, TValue value)> item in cacheMap.Values)
            {
                Info k = item.Value.key;
                if (k.Coordinate == key.Coordinate && k.Index == key.Index)
                {
                    lruList.Remove(item);
                }
            }
        }
        public void Dispose()
        {
            foreach (LinkedListNode<(Info key, TValue value)> item in cacheMap.Values)
            {
                lruList.Remove(item);
            }
        }
    }
    public class TileCache
    {
        private LruCache<TileInformation, byte[]> cache;
        private int capacity;
        SlideSourceBase source = null;
        public TileCache(SlideSourceBase source, int capacity = 200)
        {
            this.source = source;
            this.capacity = capacity;
            this.cache = new LruCache<TileInformation, byte[]>(capacity);
        }

        public void DisposeTile(TileInformation info)
        {
            for (int i = 0; i < cache.Count; i++)
            {
                var inf = new LruCache<TileInformation, byte[]>.Info();
                cache.RemoveTile(inf);
            }
        }
        public void DisposeTile(TileInfo info)
        {
            for (int i = 0; i < cache.Count; i++)
            {
                var inf = new LruCache<TileInformation, byte[]>.Info();
                cache.RemoveTile(inf);
            }
        }
        public async Task<byte[]> GetTile(TileInformation info)
        {
            LruCache<TileInformation, byte[]>.Info inf = new LruCache<TileInformation, byte[]>.Info();
            inf.Coordinate = info.Coordinate;
            inf.Index = info.Index;
            byte[] data = cache.Get(inf);
            if (data != null)
            {
                return data;
            }
            byte[] tile = await LoadTile(info);
            if(tile!=null)
            AddTile(info, tile);
            return tile;
        }

        public void AddTile(TileInformation tileId, byte[] tile)
        {
            LruCache<TileInformation, byte[]>.Info inf = new LruCache<TileInformation, byte[]>.Info();
            inf.Coordinate = tileId.Coordinate;
            inf.Index = tileId.Index;
            cache.Add(inf, tile);
        }

        private async Task<byte[]> LoadTile(TileInformation tileId)
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
        public void Dispose()
        {
            cache.Dispose();
        }
    }

    public class TileInformation
    {
        public TileInformation(TileIndex index, Extent ext, ZCT coord)
        {
            Index = index;
            Extent = ext;
            Coordinate = coord;
        }
        public TileIndex Index { get; set; }
        public Extent Extent { get; set; }
        public ZCT Coordinate { get; set; }
    }

    public abstract class SlideSourceBase : ISlideSource, IDisposable
    {
        #region Static
        public static bool UseRealResolution { get; set; } = true;

        private static IDictionary<string, Func<string, bool, ISlideSource>> keyValuePairs = new Dictionary<string, Func<string, bool, ISlideSource>>();
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
        public static bool UseGPU = true;
        public TileCache cache = null;
        public Stitch stitch = new Stitch();
        public bool HasTile(TileInfo tile, ZCT coord)
        {
            if (cache.GetTile(new TileInformation(tile.Index, tile.Extent, coord)) != null)
                return true;
            else
                return false;
        }
        private int GetOptimalBatchSize(PointD PyramidalOrigin, AForge.Size PyramidalSize)
        {

            // Small viewport (< 800x600): fetch all tiles at once
            if (PyramidalSize.Width < 800 && PyramidalSize.Height < 600)
                return 150;

            // Medium viewport (< 1920x1080): moderate batching
            if (PyramidalSize.Width < 1920 && PyramidalSize.Height < 1080)
                return 75;

            // Large viewport (fullscreen): aggressive batching
            // Process visible center tiles first, then outer tiles
            return 50;
        }

        private async Task<byte[]> FetchSingleTileAsync(TileInfo tile, int level)
        {
            // Your existing tile fetch logic here
            // This should use the cache and only fetch if not cached
            try
            {
                byte[] tileData = await GetTileAsync(tile);
                // Process tile data...
                return tileData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching tile: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Fetches tiles in priority order: center viewport first, then edges.
        /// Ensures visible content appears before prefetch content.
        /// </summary>
        public async Task FetchTilesAsync(List<BruTile.TileInfo> tiles, int level, ZCT coordinate, PointD PyramidalOrigin, AForge.Size PyramidalSize)
        {
            if (tiles == null || tiles.Count == 0)
                return;

            // --------------------------------------------------------------------
            // 1. Calculate viewport center (base-resolution coordinates)
            // --------------------------------------------------------------------
            double centerX = PyramidalOrigin.X + (PyramidalSize.Width * 0.5);
            double centerY = PyramidalOrigin.Y + (PyramidalSize.Height * 0.5);

            // --------------------------------------------------------------------
            // 2. Sort tiles by squared distance (avoid sqrt)
            // --------------------------------------------------------------------
            var prioritizedTiles = tiles
                .OrderBy(tile =>
                {
                    double dx = tile.Extent.CenterX - centerX;
                    double dy = tile.Extent.CenterY - centerY;
                    return (dx * dx) + (dy * dy);
                })
                .ToList();

            // --------------------------------------------------------------------
            // 3. Fetch tiles in batches
            // --------------------------------------------------------------------
            int batchSize = GetOptimalBatchSize(PyramidalOrigin, PyramidalSize);

            for (int i = 0; i < prioritizedTiles.Count; i += batchSize)
            {
                var batch = prioritizedTiles.Skip(i).Take(batchSize).ToList();

                // Parallel fetch within batch
                byte[][] results = await Task.WhenAll(
                    batch.Select(tile => FetchSingleTileAsync(tile, level))
                );

                // ----------------------------------------------------------------
                // 4. Insert into appropriate cache
                // ----------------------------------------------------------------
                for (int j = 0; j < batch.Count; j++)
                {
                    var tile = batch[j];
                    var data = results[j];

                    if (data == null)
                        continue;
                    var info = new Info(
                        coordinate,
                        tile.Index,
                        this.Schema.Extent,
                        level
                    );
                    TileInformation tf = new TileInformation(tile.Index, tile.Extent, coord);
                    cache.AddTile(tf, data);
                }
            }
        }

        public async Task<byte[]> GetSlice(SliceInfo sliceInfo, PointD PyramidalOrign, AForge.Size PyramidalSize)
        {
            if (cache == null)
                cache = new TileCache(this);
            var curLevel = Image.BioImage.LevelFromResolution(sliceInfo.Resolution);
            var curUnitsPerPixel = Schema.Resolutions[curLevel].UnitsPerPixel;

            var tileInfos = Schema.GetTileInfos(sliceInfo.Extent, curLevel);
            if(tileInfos.Count() == 0)
            {
                tileInfos = Schema.GetTileInfos(sliceInfo.Extent.PixelToWorldInvertedY(curUnitsPerPixel), curLevel);
            }
            await FetchTilesAsync(tileInfos.ToList(), curLevel, sliceInfo.Coordinate, PyramidalOrign, PyramidalSize);

            var srcPixelExtent = sliceInfo.Extent.WorldToPixelInvertedY(curUnitsPerPixel);
            var dstPixelExtent = sliceInfo.Extent.WorldToPixelInvertedY(sliceInfo.Resolution);
            var dstPixelHeight = sliceInfo.Parame.DstPixelHeight > 0 ? sliceInfo.Parame.DstPixelHeight : dstPixelExtent.Height;
            var dstPixelWidth = sliceInfo.Parame.DstPixelWidth > 0 ? sliceInfo.Parame.DstPixelWidth : dstPixelExtent.Width;
            destExtent = new Extent(0, 0, dstPixelWidth, dstPixelHeight);
            sourceExtent = srcPixelExtent;
            if (UseGPU)
            {
                try
                {
                    if (tileInfos.Count() > 0)
                        return stitch.StitchImages(tileInfos.ToList(), (int)Math.Round(dstPixelWidth), (int)Math.Round(dstPixelHeight), Math.Round(srcPixelExtent.MinX), Math.Round(srcPixelExtent.MinY), curUnitsPerPixel);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message.ToString());
                    UseVips = true;
                    UseGPU = false;
                }
            }
            else
            if (UseVips)
            {
                try
                {
                    List<Tuple<Extent, byte[]>> tiles = new List<Tuple<Extent, byte[]>>();
                    foreach (BruTile.TileInfo t in tileInfos)
                    {
                        TileInformation tf = new TileInformation(t.Index,t.Extent,sliceInfo.Coordinate);
                        byte[] c = await cache.GetTile(tf);
                        if(c!=null)
                            tiles.Add(Tuple.Create(t.Extent.WorldToPixelInvertedY(curUnitsPerPixel), c));
                    }
                    NetVips.Image im = null;
                    if (Image.BioImage.Resolutions[curLevel].PixelFormat == PixelFormat.Format16bppGrayScale)
                        im = ImageUtil.JoinVips16(tiles, srcPixelExtent, new Extent(0, 0, dstPixelWidth, dstPixelHeight));
                    else if (Image.BioImage.Resolutions[curLevel].PixelFormat == PixelFormat.Format24bppRgb)
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
                List<Tuple<Extent, byte[]>> tiles = new List<Tuple<Extent, byte[]>>();
                foreach (BruTile.TileInfo t in tileInfos)
                {
                    TileInformation tf = new TileInformation(t.Index, t.Extent, sliceInfo.Coordinate);
                    byte[] c = await cache.GetTile(tf);
                    if (c != null)
                        tiles.Add(Tuple.Create(t.Extent.WorldToPixelInvertedY(curUnitsPerPixel), c));
                }
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
                else if (this.Image.BioImage.Resolutions[curLevel].PixelFormat == PixelFormat.Format8bppIndexed)
                {
                    im = ImageUtil.Join8Bit(tiles, srcPixelExtent, new Extent(0, 0, dstPixelWidth, dstPixelHeight));
                    byte[] bts = Get8BitBytes((Image<L8>)im);
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

        public byte[] Get8BitBytes(Image<L8> image)
        {
            int width = image.Width;
            int height = image.Height;
            byte[] rgbBytes = new byte[width * height]; // 3 bytes per pixel (RGB)

            int byteIndex = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    L8 pixel = image[x, y];
                    rgbBytes[byteIndex++] = pixel.PackedValue;
                }
            }

            return rgbBytes;
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
                    rgbBytes[byteIndex++] = pixel.B;
                    rgbBytes[byteIndex++] = pixel.G;
                    rgbBytes[byteIndex++] = pixel.R;
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

        public async Task<byte[]> GetTileAsync(TileInformation tileInfo)
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
            var bgraData = await Image.ReadRegionAsync(tileInfo.Index.Level, (long)curLevelOffsetXPixel, (long)curLevelOffsetYPixel, curTileWidth, curTileHeight,tileInfo.Coordinate);
            return bgraData;
        }
        static ZCT coord = new ZCT();
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
        Task<byte[]> GetSlice(SliceInfo sliceInfo, PointD PyramidalOrigin, AForge.Size PyramidalSize);
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
            Extent = new Extent(xPixel, yPixel, xPixel + widthPixel,yPixel + heightPixel);
            Resolution = unitsPerPixel;
            Coordinate = coord;
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

    public static class ExtentEx
    {
        /// <summary>
        /// Convert OSM world to pixel
        /// </summary>
        /// <param name="extent">world extent</param>
        /// <param name="unitsPerPixel">resolution,um/pixel</param>
        /// <returns></returns>
        public static Extent WorldToPixelInvertedY(this Extent extent, double unitsPerPixel)
        {
            return new Extent(extent.MinX / unitsPerPixel, -extent.MaxY / unitsPerPixel, extent.MaxX / unitsPerPixel, -extent.MinY / unitsPerPixel);
        }


        /// <summary>
        /// Convert pixel to OSM world.
        /// </summary>
        /// <param name="extent">pixel extent</param>
        /// <param name="unitsPerPixel">resolution,um/pixel</param>
        /// <returns></returns>
        public static Extent PixelToWorldInvertedY(this Extent extent, double unitsPerPixel)
        {
            return new Extent(extent.MinX * unitsPerPixel, -extent.MaxY * unitsPerPixel, extent.MaxX * unitsPerPixel, -extent.MinY * unitsPerPixel);
        }

        /// <summary>
        /// Convert double to int.
        /// </summary>
        /// <param name="extent"></param>
        /// <returns></returns>
        public static Extent ToIntegerExtent(this Extent extent)
        {
            return new Extent((int)Math.Round(extent.MinX), (int)Math.Round(extent.MinY), (int)Math.Round(extent.MaxX), (int)Math.Round(extent.MaxY));
        }
    }

    public static class ObjectEx
    {
        /// <summary>
        /// Get fields and properties
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static Dictionary<string, object> GetFieldsProperties(this object obj)
        {
            Dictionary<string, object> keys = new Dictionary<string, object>();
            foreach (var item in obj.GetType().GetFields())
            {
                keys.Add(item.Name, item.GetValue(obj));
            }
            foreach (var item in obj.GetType().GetProperties())
            {
                try
                {
                    if (item.GetIndexParameters().Any()) continue;
                    keys.Add(item.Name, item.GetValue(obj));
                }
                catch (Exception) { }
            }
            return keys;
        }
    }
}