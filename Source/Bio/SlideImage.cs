using AForge;
using GLib;
using OpenSlideGTK;
using OpenSlideGTK.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ZarrNET.Core.OmeZarr.Nodes;
using ZarrNET.Core.OmeZarr.Metadata;

namespace BioLib
{
    /// <summary>
    /// openslide wrapper
    /// </summary>
    public partial class SlideImage : IDisposable
    {
        public BioImage BioImage { get; set; }


        /// <summary>
        /// Quickly determine whether a whole slide image is recognized.
        /// </summary>
        /// <remarks>
        /// If OpenSlide recognizes the file referenced by <paramref name="filename"/>, 
        /// return a string identifying the slide format vendor.This is equivalent to the
        /// value of the <see cref="NativeMethods.VENDOR"/> property. Calling
        /// <see cref="Open(string)"/> on this file will return a valid 
        /// OpenSlide object or an OpenSlide object in error state.
        ///
        /// Otherwise, return <see langword="null"/>.Calling <see cref="
        /// Open(string)"/> on this file will also
        /// return <see langword="null"/>.</remarks>
        /// <param name="filename">The filename to check. On Windows, this must be in UTF-8.</param>
        /// <returns>An identification of the format vendor for this file, or NULL.</returns>
        public static string DetectVendor(string filename)
        {
            return filename;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="isOwner">close handle when disposed</param>
        /// <exception cref="OpenSlideException"/>
        public SlideImage()
        {
            
        }

        /// <summary>
        /// Add .dll directory to PATH
        /// </summary>
        /// <param name="path"></param>
        /// <exception cref="OpenSlideException"/>
        public static void Initialize(string path = null)
        {
            
        }
        static SlideImage()
        {
            Initialize();
        }

        /// <summary>
        /// Open.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        /// <exception cref="OpenSlideException"/>
        public static SlideImage Open(BioImage b)
        {
            SlideImage im = new SlideImage();
            im.BioImage = b;
            return im;
        }

        /// <summary>
        /// Get the number of levels in the whole slide image.
        /// </summary>
        /// <return>The number of levels, or -1 if an error occurred.</return> 
        /// <exception cref="OpenSlideException"/>
        public int LevelCount
        {
            get
            {
                // For well-plate fields use the active field's pyramid level count.
                if (BioImage.Type == BioImage.ImageType.well &&
                    BioImage.ZarrWellLevels?.Count > 0)
                {
                    int fi = Math.Clamp(BioImage.WellIndex, 0, BioImage.ZarrWellLevels.Count - 1);
                    return BioImage.ZarrWellLevels[fi]?.Count ?? 1;
                }
                if (BioImage.MacroResolution.HasValue)
                    return BioImage.Resolutions.Count - 2;
                return BioImage.Resolutions.Count;
            }
        }

        private ImageDimension? _dimensionsRef;
        private readonly object _dimensionsSynclock = new object();

        public ImageDimension Dimensions
        {
            get
            {
                if (_dimensionsRef == null)
                {
                    lock (_dimensionsSynclock)
                    {
                        if (_dimensionsRef == null)
                            _dimensionsRef = GetLevelDimension(0);
                    }
                }
                return _dimensionsRef.Value;
            }
        }

        public ImageDimension GetLevelDimension(int level)
        {
            // For well-plate fields, derive dimensions from the field's pyramid shape.
            if (BioImage.Type == BioImage.ImageType.well &&
                BioImage.ZarrWellLevels?.Count > 0)
            {
                int fi = Math.Clamp(BioImage.WellIndex, 0, BioImage.ZarrWellLevels.Count - 1);
                var fieldLevels = BioImage.ZarrWellLevels[fi];
                if (fieldLevels != null && fieldLevels.Count > 0)
                {
                    int lev   = Math.Clamp(level, 0, fieldLevels.Count - 1);
                    var shape = fieldLevels[lev].Shape;
                    var axes  = fieldLevels[lev].EffectiveAxes;
                    int w = 1, h = 1;
                    for (int i = 0; i < axes.Length; i++)
                    {
                        switch (axes[i].Name.ToLowerInvariant())
                        {
                            case "x": w = (int)shape[i]; break;
                            case "y": h = (int)shape[i]; break;
                        }
                    }
                    return new ImageDimension(w, h);
                }
            }
            return new ImageDimension(BioImage.Resolutions[level].SizeX, BioImage.Resolutions[level].SizeY);
        }

        /// <summary>
        /// Get all level dimensions.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="OpenSlideException"/>
        public IEnumerable<ImageDimension> GetLevelDimensions()
        {
            var count = LevelCount;
            for (int i = 0; i < count; i++)
            {
                yield return GetLevelDimension(i);
            }
        }

        /// <summary>
        /// Clears the cached level-0 dimensions so <see cref="Dimensions"/> is
        /// re-derived from the active well field on the next access.
        /// Call this whenever <see cref="BioImage.WellIndex"/> changes for a well image.
        /// </summary>
        public void ResetDimensionsCache() => _dimensionsRef = null;
        /// <summary>
        /// Calculates the base downsampling factor between two levels of a slide.
        /// </summary>
        /// <param name="originalDimension">The dimension (width or height) of the original level.</param>
        /// <param name="nextLevelDimension">The dimension (width or height) of the next level.</param>
        /// <returns>The base downsampling factor.</returns>
        public static double CalculateBaseFactor(int originalResolution, int lastLevelResolution, int totalLevels)
        {
            if (totalLevels <= 1)
            {
                throw new ArgumentException("Total levels must be greater than 1 to calculate a base factor.");
            }
            if (lastLevelResolution <= 0 || originalResolution <= 0)
            {
                throw new ArgumentException("Resolutions must be greater than 0.");
            }

            // Calculate the base downsampling factor
            double baseFactor = Math.Pow((double)originalResolution / lastLevelResolution, 1.0 / (totalLevels - 1));
            return baseFactor;
        }

        /// <summary>
        /// Calculates the downsample factors for each level of a slide.
        /// </summary>
        /// <param name="baseDownsampleFactor">The downsample factor between each level.</param>
        /// <param name="totalLevels">Total number of levels in the slide.</param>
        /// <returns>A list of downsample factors for each level.</returns>
        public static List<double> GetLevelDownsamples(double baseDownsampleFactor, int totalLevels)
        {
            var levelDownsamples = new List<double>();

            for (int level = 0; level < totalLevels; level++)
            {
                // Calculate the downsample factor for the current level.
                // Math.Pow is used to raise the baseDownsampleFactor to the power of the level.
                double downsampleFactorAtLevel = Math.Pow(baseDownsampleFactor, level);
                levelDownsamples.Add(downsampleFactorAtLevel);
            }

            return levelDownsamples;
        }
        

        /*
        /// <summary>
        /// Get the best level to use for displaying the given downsample.
        /// </summary>
        /// <param name="downsample">The downsample factor.</param> 
        /// <return>The level identifier, or -1 if an error occurred.</return> 
        /// <exception cref="OpenSlideException"/>
        public int GetBestLevelForDownsample(double downsample)
        {
            if (NativeMethods.isWindows)
            {
                var result = NativeMethods.Windows.GetBestLevelForDownsample(Handle, downsample);
                return result != -1 ? result : CheckIfThrow(result);
            } else if (NativeMethods.isLinux)
            {
                var result = NativeMethods.Linux.GetBestLevelForDownsample(Handle, downsample);
                return result != -1 ? result : CheckIfThrow(result);
            }
            else
            {
                var result = NativeMethods.OSX.GetBestLevelForDownsample(Handle, downsample);
                return result != -1 ? result : CheckIfThrow(result);
            }
        }
        */
        /// <summary>
        /// Copy pre-multiplied BGRA data from a whole slide image.
        /// </summary>
        /// <param name="level">The desired level.</param>
        /// <param name="x">The top left x-coordinate, in the level 0 reference frame.</param>
        /// <param name="y">The top left y-coordinate, in the level 0 reference frame.</param>
        /// <param name="width">The width of the region. Must be non-negative.</param>
        /// <param name="height">The height of the region. Must be non-negative.</param>
        /// <returns>The pixel data of this region.</returns>
        /// <exception cref="ArgumentOutOfRangeException"/>
        /// <exception cref="OpenSlideException"/>
        public unsafe byte[] ReadRegion(int level, ZCT zct, long x, long y, long width, long height)
        {
            try
            {
                return BioImage.GetTile(BioImage.GetFrameIndex(zct.Z, zct.C, zct.T), level, (int)x, (int)y, (int)width, (int)height).Result.Bytes; ;
            }
            catch (Exception e)
            {
                if (BioImage == null)
                    Console.WriteLine("BioImage is null. ");
                Console.WriteLine(e.Message + " " + e.StackTrace);
            }
            return null;
        }

        /// <summary>
        /// Copy pre-multiplied BGRA data from a whole slide image.
        /// </summary>
        /// <param name="level">The desired level.</param>
        /// <param name="x">The top left x-coordinate, in the level 0 reference frame.</param>
        /// <param name="y">The top left y-coordinate, in the level 0 reference frame.</param>
        /// <param name="width">The width of the region. Must be non-negative.</param>
        /// <param name="height">The height of the region. Must be non-negative.</param>
        /// <param name="data">The BGRA pixel data of this region.</param>
        /// <returns></returns>
        public bool TryReadRegion(int level, long x, long y, long width, long height, out byte[] data, ZCT zct)
        {
            data = BioImage.GetTile(BioImage.GetFrameIndex(zct.Z,zct.C,zct.T), level, (int)x, (int)y, (int)width, (int)height).Result.Bytes;
            if (data == null)
                return false;
            else
                return true;
        }
        /// <summary>
        /// Copy pre-multiplied BGRA data from a whole slide image.
        /// </summary>
        /// <param name="level">The desired level.</param>
        /// <param name="x">The top left x-coordinate, in the level 0 reference frame.</param>
        /// <param name="y">The top left y-coordinate, in the level 0 reference frame.</param>
        /// <param name="width">The width of the region. Must be non-negative.</param>
        /// <param name="height">The height of the region. Must be non-negative.</param>
        /// <param name="data">The BGRA pixel data of this region.</param>
        /// <returns></returns>

        private static void Log(string msg)
        {
            try { System.IO.File.AppendAllText(@"C:\\Users\\Public\\biolog.txt", msg + "\n"); }
            catch { }
        }
        public async Task<byte[]> TryReadRegionAsync(int level, long x, long y, long width, long height, ZCT zct)
        {
            Log($"[TryReadRegionAsync] Type={BioImage.Type} WellLevels={BioImage.ZarrWellLevels?.Count}");
            // Use ZarrWellLevels whenever available — this handles well-plate images
            // regardless of how Type is set at call time.
            if (BioImage.ZarrWellLevels?.Count > 0)
            {
                Log($"[Well TryReadRegionAsync] level={level} x={x} y={y} w={width} h={height} zct={zct.Z},{zct.C},{zct.T} bioLevel={BioImage.WellIndex} wellLevelCount={BioImage.ZarrWellLevels.Count}");
                int fi          = Math.Clamp(BioImage.WellIndex, 0, BioImage.ZarrWellLevels.Count - 1);
                var fieldLevels = BioImage.ZarrWellLevels[fi];
                if (fieldLevels == null || fieldLevels.Count == 0)
                {
                    Log($"[Well TryReadRegionAsync] fieldLevels null/empty for fi={fi}");
                    return null;
                }

                int lev       = Math.Clamp(level, 0, fieldLevels.Count - 1);
                var levelNode = fieldLevels[lev];
                Log($"[Well TryReadRegionAsync] Using zarr lev={lev}, shape={string.Join(",", levelNode.Shape)}");

                var result = await levelNode.ReadTileAsync(
                    (int)x, (int)y, (int)width, (int)height,
                    zct.T, zct.C, zct.Z).ConfigureAwait(false);

                if (result == null)
                {
                    Log($"[Well TryReadRegionAsync] ReadTileAsync returned null");
                    return null;
                }
                Log($"[Well TryReadRegionAsync] Got {result.Data.Length} bytes, dtype={result.DataType}");

                // Determine actual returned size from result shape/axes.
                int resultW = (int)width, resultH = (int)height;
                for (int i = 0; i < result.Axes.Length; i++)
                {
                    switch (result.Axes[i].Name.ToLowerInvariant())
                    {
                        case "x": resultW = (int)result.Shape[i]; break;
                        case "y": resultH = (int)result.Shape[i]; break;
                    }
                }

                // Convert raw pixel data to BGRA (4 bytes/pixel) which is what
                // the tile renderer expects.
                int pixelCount  = (int)width * (int)height;
                byte[] bgra     = new byte[pixelCount * 4];
                bool is16       = result.DataType == "uint16";
                int srcStride   = resultW * (is16 ? 2 : 1);
                int dstStride   = (int)width * 4;

                // For 16-bit data use a per-tile min/max linear stretch so that
                // fluorescence images (which have signal in e.g. 0-4000 out of 65535)
                // render with full contrast rather than appearing nearly black when
                // the top-byte shift `v >> 8` is used.
                ushort tileMin = ushort.MaxValue, tileMax = 0;
                if (is16)
                {
                    int rows = Math.Min(resultH, (int)height);
                    int cols = Math.Min(resultW, (int)width);
                    for (int row = 0; row < rows; row++)
                        for (int col = 0; col < cols; col++)
                        {
                            int off = row * srcStride + col * 2;
                            ushort v = (ushort)(result.Data[off] | (result.Data[off + 1] << 8));
                            if (v < tileMin) tileMin = v;
                            if (v > tileMax) tileMax = v;
                        }
                    // Avoid divide-by-zero on flat tiles; fall back to top-byte shift.
                    if (tileMax <= tileMin) { tileMin = 0; tileMax = 65535; }
                }

                for (int row = 0; row < Math.Min(resultH, (int)height); row++)
                {
                    for (int col = 0; col < Math.Min(resultW, (int)width); col++)
                    {
                        int srcOff = row * srcStride + col * (is16 ? 2 : 1);
                        int dstOff = row * dstStride + col * 4;
                        byte gray;
                        if (is16)
                        {
                            ushort v = (ushort)(result.Data[srcOff] | (result.Data[srcOff + 1] << 8));
                            gray = (byte)(((v - tileMin) * 255) / (tileMax - tileMin));
                        }
                        else
                        {
                            gray = result.Data[srcOff];
                        }
                        bgra[dstOff]     = gray; // B
                        bgra[dstOff + 1] = gray; // G
                        bgra[dstOff + 2] = gray; // R
                        bgra[dstOff + 3] = 255;  // A
                    }
                }
                Log($"[Well TryReadRegionAsync] Returning {bgra.Length} BGRA bytes");
                return bgra;
            }

            Bitmap bts = await BioImage.GetTile(BioImage.GetFrameIndex(zct.Z, zct.C, zct.T), level, (int)x, (int)y, (int)width, (int)height);
            return bts.Bytes;
        }
        ///<summary>
        ///Close an OpenSlide object.
        ///</summary>
        ///<remarks>
        ///No other threads may be using the object.
        ///After this call returns, the object cannot be used anymore.
        ///</remarks>
        public void Close()
        {
           
        }

        #region IDisposable

        private bool disposedValue;

        /// <summary>
        /// Dispose
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                }

                Close();
                disposedValue = true;
            }
        }

        /// <summary>
        /// </summary>
        ~SlideImage()
        {
            Dispose(disposing: false);
        }

        /// <summary>
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public async Task<byte[]> ReadRegionAsync(int level, long curLevelOffsetXPixel, long curLevelOffsetYPixel, int curTileWidth, int curTileHeight, ZCT coord)
        {
            try
            {
                byte[] bts = await TryReadRegionAsync(level, curLevelOffsetXPixel, curLevelOffsetYPixel, curTileWidth, curTileHeight,coord);
                return bts;
            }
            catch (Exception e)
            {
                Log($"[ReadRegionAsync] EXCEPTION: {e.GetType().Name}: {e.Message}");
                return null;
            }
        }
        #endregion
    }
}
