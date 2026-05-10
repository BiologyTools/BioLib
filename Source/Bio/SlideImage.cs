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
using System.Diagnostics;
using System.Threading;
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
                var tile = BioImage.GetTile(
                    BioImage.GetFrameIndex(zct.Z, zct.C, zct.T),
                    level,
                    (int)x,
                    (int)y,
                    (int)width,
                    (int)height).Result;
                if (tile == null)
                    return null;

                var bytes = tile.Bytes;
                tile.Dispose();
                return bytes;
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
            var tile = BioImage.GetTile(
                BioImage.GetFrameIndex(zct.Z, zct.C, zct.T),
                level,
                (int)x,
                (int)y,
                (int)width,
                (int)height).Result;
            if (tile == null)
            {
                data = null;
                return false;
            }

            data = tile.Bytes;
            tile.Dispose();
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

        public async Task<byte[]> TryReadRegionAsync(int level, long x, long y, long width, long height, ZCT zct, CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            // Final defence: never forward zero/negative dimensions to the zarr reader.
            // Callers should have caught this already, but if a negative curTileWidth or
            // curTileHeight slips through it causes the reader to clamp to chunk 0 and
            // return pixel data from a different plane, producing the boundary-noise strip.
            if (width <= 0 || height <= 0 || x < 0 || y < 0)
                return null;

            // Use ZarrWellLevels whenever available — this handles well-plate images
            // regardless of how Type is set at call time.
            if (BioImage.Type == BioImage.ImageType.well && BioImage.ZarrWellLevels?.Count > 0)
            {
                int fi          = Math.Clamp(BioImage.WellIndex, 0, BioImage.ZarrWellLevels.Count - 1);
                var fieldLevels = BioImage.ZarrWellLevels[fi];
                if (fieldLevels == null || fieldLevels.Count == 0)
                {
                    // WellIndex points to a field not yet loaded — fall back to the
                    // first non-null field so tiles still render.
                    fieldLevels = BioImage.ZarrWellLevels.FirstOrDefault(l => l != null && l.Count > 0);
                    if (fieldLevels == null)
                        return null;
                }

                int lev       = Math.Clamp(level, 0, fieldLevels.Count - 1);
                var levelNode = fieldLevels[lev];

                // Clamp requested region to the actual level dimensions so we never
                // ask ReadTileAsync for pixels that lie beyond the image boundary.
                // Zarr returns a full fixed-size chunk regardless, and pixels beyond
                // the image edge contain data from a different plane / chunk-zero,
                // producing the bright-noise strip at the right and bottom edges.
                int levelNodeW = 0, levelNodeH = 0;
                var axes  = levelNode.EffectiveAxes;
                var shape = levelNode.Shape;
                for (int i = 0; i < axes.Length; i++)
                {
                    switch (axes[i].Name.ToLowerInvariant())
                    {
                        case "x": levelNodeW = (int)shape[i]; break;
                        case "y": levelNodeH = (int)shape[i]; break;
                    }
                }
                // Clamp the read region so it stays within the image.
                long clampedW = (levelNodeW > 0) ? Math.Min(width,  levelNodeW - x) : width;
                long clampedH = (levelNodeH > 0) ? Math.Min(height, levelNodeH - y) : height;
                if (clampedW <= 0 || clampedH <= 0)
                    return null;

                var resultTask = levelNode.ReadTileAsync(
                    (int)x, (int)y, (int)clampedW, (int)clampedH,
                    z: zct.Z, c: zct.C, t: zct.T, ct: ct);
                var resultCompleted = await global::System.Threading.Tasks.Task.WhenAny(
                    resultTask,
                    global::System.Threading.Tasks.Task.Delay(global::System.Threading.Timeout.Infinite, ct)).ConfigureAwait(false);
                if (resultCompleted != resultTask)
                    return null;

                var result = await resultTask.ConfigureAwait(false);

                if (result == null)
                    return null;

                // Convert raw pixel data to BGRA (4 bytes/pixel) which is what
                // the tile renderer expects.
                // IMPORTANT: allocate and copy only clampedW × clampedH pixels.
                // resultW/resultH from the zarr reader may equal the full chunk size
                // (256×256) even for boundary tiles — those extra pixels lie outside
                // the image and contain data from another plane, causing the noise strip.
                int copyW = (int)Math.Min(clampedW, width);
                int copyH = (int)Math.Min(clampedH, height);
                int pixelCount  = copyW * copyH;
                byte[] bgra     = new byte[pixelCount * 4];
                bool is16       = result.DataType == "uint16";
                int srcStride   = copyW * (is16 ? 2 : 1);
                int dstStride   = copyW * 4;

                // For 16-bit: use a shared display range so all tiles normalize
                // consistently and produce no visible seams.
                // The range is computed once from the first tile and cached on BioImage.
                ushort normMin = 0, normMax = ushort.MaxValue;
                if (is16 && result.Data.Length >= 2)
                {
                    if (BioImage.ZarrDisplayMax == 0)
                    {
                        // First tile — scan to establish the shared range.
                        ushort scanMin = ushort.MaxValue, scanMax = 0;
                        for (int row = 0; row < copyH; row++)
                            for (int col = 0; col < copyW; col++)
                            {
                                int off = row * srcStride + col * 2;
                                if (off + 1 >= result.Data.Length) continue;
                                ushort v = (ushort)(result.Data[off] | (result.Data[off + 1] << 8));
                                if (v < scanMin) scanMin = v;
                                if (v > scanMax) scanMax = v;
                            }
                        if (scanMax > 0)
                        {
                            uint ceiling = 1;
                            while (ceiling - 1 < scanMax && ceiling < (1u << 16))
                                ceiling <<= 1;

                            BioImage.ZarrDisplayMin = 0;
                            BioImage.ZarrDisplayMax = (ushort)Math.Min(ushort.MaxValue, ceiling - 1);
                        }
                    }
                    if (BioImage.ZarrDisplayMax > BioImage.ZarrDisplayMin)
                    {
                        normMin = BioImage.ZarrDisplayMin;
                        normMax = (ushort)BioImage.ZarrDisplayMax;
                    }
                }

                for (int row = 0; row < copyH; row++)
                {
                    for (int col = 0; col < copyW; col++)
                    {
                        int srcOff = row * srcStride + col * (is16 ? 2 : 1);
                        int dstOff = row * dstStride + col * 4;
                        byte gray;
                        if (is16)
                        {
                            ushort v = (ushort)(result.Data[srcOff] | (result.Data[srcOff + 1] << 8));
                            gray = (byte)(Math.Clamp((float)(v - normMin) / (normMax - normMin), 0f, 1f) * 255f);
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
                return bgra;
            }

            bool isZarr16 = BioImage?.Filename != null &&
                            BioImage.Filename.IndexOf(".zarr", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            BioImage.Resolutions != null &&
                            BioImage.Resolutions.Count > level &&
                            BioImage.Resolutions[level].PixelFormat == PixelFormat.Format16bppGrayScale;
            if (isZarr16)
            {
                byte[] raw = await BioImage.GetTileBytesRaw(
                    BioImage.GetFrameIndex(zct.Z, zct.C, zct.T),
                    level,
                    (int)x,
                    (int)y,
                    (int)width,
                    (int)height,
                    zct).ConfigureAwait(false);

                if (raw == null || raw.Length == 0)
                    return null;

                int w = (int)width;
                int h = (int)height;
                byte[] bgra = new byte[w * h * 4];
                int min = ushort.MaxValue;
                int max = ushort.MinValue;
                if (BioImage != null && BioImage.ZarrDisplayMax > BioImage.ZarrDisplayMin)
                {
                    min = BioImage.ZarrDisplayMin;
                    max = BioImage.ZarrDisplayMax;
                }
                else
                {
                    int valueCount = Math.Min(w * h, raw.Length / 2);
                    for (int i = 0; i < valueCount; i++)
                    {
                        int s = i * 2;
                        ushort v = (ushort)(raw[s] | (raw[s + 1] << 8));
                        if (v < min) min = v;
                        if (v > max) max = v;
                    }
                }
                if (max <= min)
                    max = min + 1;
                float scale = 255f / (max - min);
                int rawPixels = Math.Min(w * h, raw.Length / 2);
                for (int i = 0; i < rawPixels; i++)
                {
                    int s = i * 2;
                    ushort v = (ushort)(raw[s] | (raw[s + 1] << 8));
                    byte g = (byte)System.Math.Clamp((v - min) * scale, 0f, 255f);
                    int d = i * 4;
                    bgra[d + 0] = g;
                    bgra[d + 1] = g;
                    bgra[d + 2] = g;
                    bgra[d + 3] = 255;
                }
                return bgra;
            }

            var tileTask = BioImage.GetTile(BioImage.GetFrameIndex(zct.Z, zct.C, zct.T), level, (int)x, (int)y, (int)width, (int)height);
            var tileCompleted = await global::System.Threading.Tasks.Task.WhenAny(
                tileTask,
                global::System.Threading.Tasks.Task.Delay(global::System.Threading.Timeout.Infinite, ct)).ConfigureAwait(false);
            if (tileCompleted != tileTask)
                return null;

            using Bitmap bts = await tileTask.ConfigureAwait(false);
            if (bts.PixelFormat == PixelFormat.Format24bppRgb ||
                bts.PixelFormat == PixelFormat.Format32bppArgb ||
                bts.PixelFormat == PixelFormat.Format32bppRgb ||
                bts.PixelFormat == PixelFormat.Format32bppPArgb)
            {
                byte[] bgra = bts.Bytes;
                return bgra;
            }

            if (bts.PixelFormat == PixelFormat.Format16bppGrayScale)
            {
                int w = bts.Width;
                int h = bts.Height;
                byte[] src = bts.Bytes;
                byte[] bgra = new byte[w * h * 4];
                bool littleEndian = bts.LittleEndian;
                int min = ushort.MaxValue;
                int max = ushort.MinValue;
                if (BioImage != null && BioImage.ZarrDisplayMax > BioImage.ZarrDisplayMin)
                {
                    min = BioImage.ZarrDisplayMin;
                    max = BioImage.ZarrDisplayMax;
                }
                else
                {
                    for (int i = 0; i < w * h; i++)
                    {
                        int s = i * 2;
                        ushort v = littleEndian
                            ? (ushort)(src[s] | (src[s + 1] << 8))
                            : (ushort)(src[s + 1] | (src[s] << 8));
                        if (v < min) min = v;
                        if (v > max) max = v;
                    }
                }
                if (max <= min)
                    max = min + 1;
                float scale = 255f / (max - min);
                for (int i = 0; i < w * h; i++)
                {
                    int s = i * 2;
                    ushort v = littleEndian
                        ? (ushort)(src[s] | (src[s + 1] << 8))
                        : (ushort)(src[s + 1] | (src[s] << 8));
                    byte g = (byte)System.Math.Clamp((v - min) * scale, 0f, 255f);
                    int d = i * 4;
                    bgra[d + 0] = g;
                    bgra[d + 1] = g;
                    bgra[d + 2] = g;
                    bgra[d + 3] = 255;
                }
                return bgra;
            }

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

        public async Task<byte[]> ReadRegionAsync(int level, long curLevelOffsetXPixel, long curLevelOffsetYPixel, int curTileWidth, int curTileHeight, ZCT coord, CancellationToken ct = default)
        {
            try
            {
                byte[] bts = await TryReadRegionAsync(level, curLevelOffsetXPixel, curLevelOffsetYPixel, curTileWidth, curTileHeight, coord, ct);
                return bts;
            }
            catch (Exception e)
            {
                return null;
            }
        }
        #endregion
    }
}

