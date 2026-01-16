using BruTile;
using System;
using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;
using NetVips;
using System.Drawing.Imaging;
using AForge;

namespace BioLib
{
    public class ImageUtil
    {
        /// <summary>
        /// Join by <paramref name="srcPixelTiles"/> and cut by <paramref name="srcPixelExtent"/> then scale to <paramref name="dstPixelExtent"/>(only height an width is useful).
        /// </summary>
        /// <param name="srcPixelTiles">tile with tile extent collection</param>
        /// <param name="srcPixelExtent">canvas extent</param>
        /// <param name="dstPixelExtent">jpeg output size</param>
        /// <returns></returns>
        public static Image<L8> Join8Bit(IEnumerable<Tuple<Extent, byte[]>> srcPixelTiles, Extent srcPixelExtent, Extent dstPixelExtent)
        {
            if (srcPixelTiles == null || !srcPixelTiles.Any())
                return null;

            // Convert extents to integer extents
            srcPixelExtent = srcPixelExtent.ToIntegerExtent();
            dstPixelExtent = dstPixelExtent.ToIntegerExtent();

            int canvasWidth = (int)srcPixelExtent.Width;
            int canvasHeight = (int)srcPixelExtent.Height;
            int dstWidth = (int)dstPixelExtent.Width;
            int dstHeight = (int)dstPixelExtent.Height;

            // Create the canvas image
            var canvas = new Image<L8>(canvasWidth, canvasHeight);

            foreach (var tile in srcPixelTiles)
            {
                try
                {
                    if (tile?.Item2 == null) // Skip null tiles
                        continue;

                    var tileExtent = tile.Item1.ToIntegerExtent();
                    var intersect = srcPixelExtent.Intersect(tileExtent);

                    // Skip tiles that do not intersect with the source extent
                    if (intersect.Width == 0 || intersect.Height == 0)
                        continue;

                    // Create an image from the tile data
                    using Image<L8> tileRawData = (Image<L8>)CreateImageFromBytes(
                        tile.Item2,
                        (int)tileExtent.Width,
                        (int)tileExtent.Height,
                        AForge.PixelFormat.Format8bppIndexed
                    );

                    // Compute offsets
                    int tileOffsetX = (int)(intersect.MinX - tileExtent.MinX);
                    int tileOffsetY = (int)(intersect.MinY - tileExtent.MinY);
                    int canvasOffsetX = (int)(intersect.MinX - srcPixelExtent.MinX);
                    int canvasOffsetY = (int)(intersect.MinY - srcPixelExtent.MinY);

                    // Copy intersected region from tile to canvas
                    for (int y = 0; y < intersect.Height; y++)
                    {
                        for (int x = 0; x < intersect.Width; x++)
                        {
                            int canvasX = canvasOffsetX + x;
                            int canvasY = canvasOffsetY + y;
                            int tileX = tileOffsetX + x;
                            int tileY = tileOffsetY + y;

                            // Use the older approach to manipulate pixel data
                            canvas[canvasX, canvasY] = tileRawData[tileX, tileY];
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing tile: {ex.Message}\n{ex.StackTrace}");
                }
            }

            // Resize if necessary
            if (dstWidth != canvasWidth || dstHeight != canvasHeight)
            {
                try
                {
                    canvas.Mutate(x => x.Resize(dstWidth, dstHeight));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error resizing canvas: {ex.Message}\n{ex.StackTrace}");
                    return null;
                }
            }

            return canvas; // Return the canvas image
        }

        /// <summary>
        /// Join by <paramref name="srcPixelTiles"/> and cut by <paramref name="srcPixelExtent"/> then scale to <paramref name="dstPixelExtent"/>(only height and width is useful).
        /// </summary>
        /// <param name="srcPixelTiles">tile with tile extent collection</param>
        /// <param name="srcPixelExtent">canvas extent</param>
        /// <param name="dstPixelExtent">jpeg output size</param>
        /// <returns></returns>
        public static Image<Rgb24> JoinRGB24(IEnumerable<Tuple<Extent, byte[]>> srcPixelTiles, Extent srcPixelExtent, Extent dstPixelExtent)
        {
            if (srcPixelTiles == null || !srcPixelTiles.Any())
                return null;

            try
            {
                // Safely convert to integer extents
                srcPixelExtent = SafeToIntegerExtent(srcPixelExtent);
                dstPixelExtent = SafeToIntegerExtent(dstPixelExtent);

                int canvasWidth = (int)srcPixelExtent.Width;
                int canvasHeight = (int)srcPixelExtent.Height;
                int dstWidth = (int)dstPixelExtent.Width;
                int dstHeight = (int)dstPixelExtent.Height;

                // Validate dimensions
                if (canvasWidth <= 0 || canvasHeight <= 0)
                {
                    Console.WriteLine($"Invalid canvas dimensions: {canvasWidth}x{canvasHeight}");
                    return null;
                }

                Image<Rgb24> canvas = new Image<Rgb24>(canvasWidth, canvasHeight);

                foreach (var tile in srcPixelTiles)
                {
                    try
                    {
                        // Skip invalid tiles
                        if (tile?.Item2 == null || tile.Item2.Length == 0)
                            continue;

                        // Safely convert tile extent
                        var tileExtent = SafeToIntegerExtent(tile.Item1);

                        // Validate tile extent
                        if (tileExtent.Width <= 0 || tileExtent.Height <= 0)
                            continue;

                        // Calculate intersection manually to avoid errors
                        var intersect = SafeIntersect(srcPixelExtent, tileExtent);

                        // Skip if no intersection
                        if (intersect == null || intersect.Value.Width <= 0 || intersect.Value.Height <= 0)
                            continue;

                        // Create image from tile data
                        Image<Rgb24> tileRawData = null;
                        try
                        {
                            tileRawData = (Image<Rgb24>)CreateImageFromBytes(
                                tile.Item2,
                                (int)tileExtent.Width,
                                (int)tileExtent.Height,
                                AForge.PixelFormat.Format24bppRgb
                            );
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to create image from tile data: {ex.Message}");
                            continue;
                        }

                        if (tileRawData == null)
                            continue;

                        // Calculate offsets (using Floor instead of Ceiling for better precision)
                        int tileOffsetPixelX = (int)Math.Max(0, intersect.Value.MinX - tileExtent.MinX);
                        int tileOffsetPixelY = (int)Math.Max(0, intersect.Value.MinY - tileExtent.MinY);
                        int canvasOffsetPixelX = (int)Math.Max(0, intersect.Value.MinX - srcPixelExtent.MinX);
                        int canvasOffsetPixelY = (int)Math.Max(0, intersect.Value.MinY - srcPixelExtent.MinY);

                        // Calculate safe copy dimensions
                        int copyWidth = (int)Math.Min(
                            Math.Min(intersect.Value.Width, tileRawData.Width - tileOffsetPixelX),
                            canvas.Width - canvasOffsetPixelX
                        );

                        int copyHeight = (int)Math.Min(
                            Math.Min(intersect.Value.Height, tileRawData.Height - tileOffsetPixelY),
                            canvas.Height - canvasOffsetPixelY
                        );

                        // Validate copy dimensions
                        if (copyWidth <= 0 || copyHeight <= 0)
                        {
                            tileRawData.Dispose();
                            continue;
                        }

                        // Copy the tile region to the canvas with bounds checking
                        for (int y = 0; y < copyHeight; y++)
                        {
                            int canvasY = canvasOffsetPixelY + y;
                            int tileY = tileOffsetPixelY + y;

                            // Safety check
                            if (canvasY < 0 || canvasY >= canvas.Height ||
                                tileY < 0 || tileY >= tileRawData.Height)
                                break;

                            for (int x = 0; x < copyWidth; x++)
                            {
                                int canvasX = canvasOffsetPixelX + x;
                                int tileX = tileOffsetPixelX + x;

                                // Safety check
                                if (canvasX < 0 || canvasX >= canvas.Width ||
                                    tileX < 0 || tileX >= tileRawData.Width)
                                    break;

                                canvas[canvasX, canvasY] = tileRawData[tileX, tileY];
                            }
                        }

                        tileRawData.Dispose();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error processing tile: {e.Message}");
                    }
                }

                // Resize if needed
                if (dstWidth > 0 && dstHeight > 0 &&
                    (dstWidth != canvasWidth || dstHeight != canvasHeight))
                {
                    try
                    {
                        canvas.Mutate(x => x.Resize(dstWidth, dstHeight));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error resizing canvas: {e.Message}");
                    }
                }

                return canvas;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Fatal error in JoinRGB24: {e.Message}\n{e.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Safely convert extent to integer extent, ensuring min < max
        /// </summary>
        private static Extent SafeToIntegerExtent(Extent extent)
        {
            if (extent == null)
                return new Extent(0, 0, 1, 1);

            try
            {
                double minX = Math.Floor(extent.MinX);
                double maxX = Math.Ceiling(extent.MaxX);
                double minY = Math.Floor(extent.MinY);
                double maxY = Math.Ceiling(extent.MaxY);

                // Ensure min < max by at least 1 pixel
                if (minX >= maxX)
                {
                    maxX = minX + 1;
                }

                if (minY >= maxY)
                {
                    maxY = minY + 1;
                }

                return new Extent(minX, minY, maxX, maxY);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in SafeToIntegerExtent: {e.Message}");
                return new Extent(0, 0, 1, 1);
            }
        }

        /// <summary>
        /// Safely calculate intersection of two extents
        /// </summary>
        private static Extent? SafeIntersect(Extent a, Extent b)
        {
            if (a == null || b == null)
                return null;

            try
            {
                double minX = Math.Max(a.MinX, b.MinX);
                double minY = Math.Max(a.MinY, b.MinY);
                double maxX = Math.Min(a.MaxX, b.MaxX);
                double maxY = Math.Min(a.MaxY, b.MaxY);

                // Check if there's an actual intersection
                if (minX >= maxX || minY >= maxY)
                {
                    return null; // No intersection
                }

                return new Extent(minX, minY, maxX, maxY);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in SafeIntersect: {e.Message}");
                return null;
            }
        }
        /// <summary>
        /// Join by <paramref name="srcPixelTiles"/> and cut by <paramref name="srcPixelExtent"/> then scale to <paramref name="dstPixelExtent"/>(only height an width is useful).
        /// </summary>
        /// <param name="srcPixelTiles">tile with tile extent collection</param>
        /// <param name="srcPixelExtent">canvas extent</param>
        /// <param name="dstPixelExtent">jpeg output size</param>
        /// <returns></returns>
        public static Image<L16>Join16(IEnumerable<Tuple<Extent, byte[]>> srcPixelTiles, Extent srcPixelExtent, Extent dstPixelExtent)
        {
            if (srcPixelTiles == null || srcPixelTiles.Count() == 0)
                return null;
            srcPixelExtent = srcPixelExtent.ToIntegerExtent();
            dstPixelExtent = dstPixelExtent.ToIntegerExtent();
            int canvasWidth = (int)srcPixelExtent.Width;
            int canvasHeight = (int)srcPixelExtent.Height;
            var dstWidth = (int)dstPixelExtent.Width;
            var dstHeight = (int)dstPixelExtent.Height;
            Image<L16> canvas = new Image<L16>(canvasWidth, canvasHeight);
            foreach (var tile in srcPixelTiles)
            {
                try
                {
                    var tileExtent = tile.Item1.ToIntegerExtent();
                    var intersect = srcPixelExtent.Intersect(tileExtent);
                    if (intersect.Width == 0 || intersect.Height == 0)
                        continue;
                    if (tile.Item2 == null)
                        continue;
                    Image<L16> tileRawData = (Image<L16>)CreateImageFromBytes(tile.Item2, (int)tileExtent.Width, (int)tileExtent.Height, AForge.PixelFormat.Format16bppGrayScale);
                    var tileOffsetPixelX = (int)Math.Ceiling(intersect.MinX - tileExtent.MinX);
                    var tileOffsetPixelY = (int)Math.Ceiling(intersect.MinY - tileExtent.MinY);
                    var canvasOffsetPixelX = (int)Math.Ceiling(intersect.MinX - srcPixelExtent.MinX);
                    var canvasOffsetPixelY = (int)Math.Ceiling(intersect.MinY - srcPixelExtent.MinY);
                    //We copy the tile region to the canvas.
                    for (int y = 0; y < intersect.Height; y++)
                    {
                        for (int x = 0; x < intersect.Width; x++)
                        {
                            int indx = canvasOffsetPixelX + x;
                            int indy = canvasOffsetPixelY + y;
                            int tindx = tileOffsetPixelX + x;
                            int tindy = tileOffsetPixelY + y;
                            canvas[indx, indy] = tileRawData[tindx, tindy];
                        }
                    }
                    tileRawData.Dispose();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }

            }
            if (dstWidth != canvasWidth || dstHeight != canvasHeight)
            {
                try
                {
                    canvas.Mutate(x => x.Resize(dstWidth, dstHeight));
                    return canvas;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }

            }
            return canvas;
        }

        /// <summary>
        /// Join by <paramref name="srcPixelTiles"/> and cut by <paramref name="srcPixelExtent"/> then scale to <paramref name="dstPixelExtent"/>(only height an width is useful).
        /// </summary>
        /// <param name="srcPixelTiles">tile with tile extent collection</param>
        /// <param name="srcPixelExtent">canvas extent</param>
        /// <param name="dstPixelExtent">jpeg output size</param>
        /// <returns></returns>
        public static unsafe NetVips.Image JoinVipsRGB24(IEnumerable<Tuple<Extent, byte[]>> srcPixelTiles, Extent srcPixelExtent, Extent dstPixelExtent)
        {
            if (srcPixelTiles == null || !srcPixelTiles.Any())
                return null;

            srcPixelExtent = srcPixelExtent.ToIntegerExtent();
            dstPixelExtent = dstPixelExtent.ToIntegerExtent();
            int canvasWidth = (int)srcPixelExtent.Width;
            int canvasHeight = (int)srcPixelExtent.Height;

            // Create a base canvas. Adjust as necessary, for example, using a transparent image if needed.
            NetVips.Image canvas = NetVips.Image.Black(canvasWidth, canvasHeight, bands: 3);

            foreach (var tile in srcPixelTiles)
            {
                if (tile.Item2 == null)
                    continue;

                fixed (byte* pTileData = tile.Item2)
                {
                    var tileExtent = tile.Item1.ToIntegerExtent();
                    NetVips.Image tileImage = NetVips.Image.NewFromMemory((IntPtr)pTileData, (ulong)tile.Item2.Length, (int)tileExtent.Width, (int)tileExtent.Height, 3, Enums.BandFormat.Uchar);

                    // Calculate positions and sizes for cropping and inserting
                    var intersect = srcPixelExtent.Intersect(tileExtent);
                    if (intersect.Width == 0 || intersect.Height == 0)
                        continue;

                    int tileOffsetPixelX = (int)Math.Ceiling(intersect.MinX - tileExtent.MinX);
                    int tileOffsetPixelY = (int)Math.Ceiling(intersect.MinY - tileExtent.MinY);
                    int canvasOffsetPixelX = (int)Math.Ceiling(intersect.MinX - srcPixelExtent.MinX);
                    int canvasOffsetPixelY = (int)Math.Ceiling(intersect.MinY - srcPixelExtent.MinY);

                    using (var croppedTile = tileImage.Crop(tileOffsetPixelX, tileOffsetPixelY, (int)intersect.Width, (int)intersect.Height))
                    {
                        // Instead of inserting directly, we composite over the base canvas
                        canvas = canvas.Composite2(croppedTile, Enums.BlendMode.Over, canvasOffsetPixelX, canvasOffsetPixelY);
                    }
                }
            }

            // Resize if the destination extent differs from the source canvas size
            if ((int)dstPixelExtent.Width != canvasWidth || (int)dstPixelExtent.Height != canvasHeight)
            {
                double scaleX = (double)dstPixelExtent.Width / canvasWidth;
                double scaleY = (double)dstPixelExtent.Height / canvasHeight;
                canvas = canvas.Resize(scaleX, vscale: scaleY, kernel: Enums.Kernel.Nearest);
            }

            return canvas;
        }

        /// <summary>
        /// Join by <paramref name="srcPixelTiles"/> and cut by <paramref name="srcPixelExtent"/> then scale to <paramref name="dstPixelExtent"/>(only height an width is useful).
        /// </summary>
        /// <param name="srcPixelTiles">tile with tile extent collection</param>
        /// <param name="srcPixelExtent">canvas extent</param>
        /// <param name="dstPixelExtent">jpeg output size</param>
        /// <returns></returns>
        public static unsafe NetVips.Image JoinVips16(IEnumerable<Tuple<Extent, byte[]>> srcPixelTiles, Extent srcPixelExtent, Extent dstPixelExtent)
        {
            if (srcPixelTiles == null || !srcPixelTiles.Any())
                return null;

            srcPixelExtent = srcPixelExtent.ToIntegerExtent();
            dstPixelExtent = dstPixelExtent.ToIntegerExtent();
            int canvasWidth = (int)srcPixelExtent.Width;
            int canvasHeight = (int)srcPixelExtent.Height;

            // Create a base canvas. Adjust as necessary, for example, using a transparent image if needed.
            Bitmap bf = new Bitmap(canvasWidth, canvasHeight, AForge.PixelFormat.Format16bppGrayScale);
            NetVips.Image canvas = NetVips.Image.NewFromMemory(bf.Bytes, bf.SizeX, bf.SizeX, 1, Enums.BandFormat.Ushort);

            foreach (var tile in srcPixelTiles)
            {
                if (tile.Item2 == null)
                    continue;

                fixed (byte* pTileData = tile.Item2)
                {
                    var tileExtent = tile.Item1.ToIntegerExtent();
                    NetVips.Image tileImage = NetVips.Image.NewFromMemory((IntPtr)pTileData, (ulong)tile.Item2.Length, (int)tileExtent.Width, (int)tileExtent.Height, 1, Enums.BandFormat.Ushort);

                    // Calculate positions and sizes for cropping and inserting
                    var intersect = srcPixelExtent.Intersect(tileExtent);
                    if (intersect.Width == 0 || intersect.Height == 0)
                        continue;

                    int tileOffsetPixelX = (int)Math.Ceiling(intersect.MinX - tileExtent.MinX);
                    int tileOffsetPixelY = (int)Math.Ceiling(intersect.MinY - tileExtent.MinY);
                    int canvasOffsetPixelX = (int)Math.Ceiling(intersect.MinX - srcPixelExtent.MinX);
                    int canvasOffsetPixelY = (int)Math.Ceiling(intersect.MinY - srcPixelExtent.MinY);

                    using (var croppedTile = tileImage.Crop(tileOffsetPixelX, tileOffsetPixelY, (int)intersect.Width, (int)intersect.Height))
                    {
                        // Instead of inserting directly, we composite over the base canvas
                        canvas = canvas.Composite2(croppedTile, Enums.BlendMode.Over, canvasOffsetPixelX, canvasOffsetPixelY);
                    }
                }
            }

            // Resize if the destination extent differs from the source canvas size
            if ((int)dstPixelExtent.Width != canvasWidth || (int)dstPixelExtent.Height != canvasHeight)
            {
                double scaleX = (double)dstPixelExtent.Width / canvasWidth;
                double scaleY = (double)dstPixelExtent.Height / canvasHeight;
                canvas = canvas.Resize(scaleX, vscale: scaleY, kernel: Enums.Kernel.Nearest);
            }

            return canvas;
        }

        public static SixLabors.ImageSharp.Image CreateImageFromBytes(byte[] rgbBytes, int width, int height, AForge.PixelFormat px)
        {
            if (px == AForge.PixelFormat.Format24bppRgb)
            {
                if (rgbBytes.Length != width * height * 3)
                {
                    throw new ArgumentException("Byte array size does not match the dimensions of the image");
                }

                // Create a new image of the specified size
                Image<Rgb24> image = new Image<Rgb24>(width, height);

                // Index for the byte array
                int byteIndex = 0;

                // Iterate over the image pixels
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        // Create a color from the next three bytes
                        Rgb24 color = new Rgb24(rgbBytes[byteIndex], rgbBytes[byteIndex + 1], rgbBytes[byteIndex + 2]);
                        byteIndex += 3;
                        // Set the pixel
                        image[x, y] = color;
                    }
                }

                return image;
            }
            else
            if (px == AForge.PixelFormat.Format16bppGrayScale)
            {
                if (rgbBytes.Length != width * height * 2)
                {
                    throw new ArgumentException("Byte array size does not match the dimensions of the image");
                }

                // Create a new image of the specified size
                Image<L16> image = new Image<L16>(width, height);

                // Index for the byte array
                int byteIndex = 0;

                // Iterate over the image pixels
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        // Create a color from the next three bytes
                        L16 color = new L16(BitConverter.ToUInt16(rgbBytes, byteIndex));
                        byteIndex += 2;
                        // Set the pixel
                        image[x, y] = color;
                    }
                }

                return image;
            }
            else
            if (px == AForge.PixelFormat.Format8bppIndexed)
            {
                if (rgbBytes.Length != width * height)
                {
                    throw new ArgumentException("Byte array size does not match the dimensions of the image");
                }

                // Create a new image of the specified size
                Image<L8> image = new Image<L8>(width, height);

                // Index for the byte array
                int byteIndex = 0;

                // Iterate over the image pixels
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        // Create a color from the next three bytes
                        L8 color = new L8(rgbBytes[byteIndex]);
                        byteIndex++;
                        // Set the pixel
                        image[x, y] = color;
                    }
                }

                return image;
            }
            else
            if (px == AForge.PixelFormat.Format32bppArgb)
            {
                if (rgbBytes.Length != width * height * 4)
                {
                    throw new ArgumentException("Byte array size does not match the dimensions of the image");
                }

                // Create a new image of the specified size
                Image<Bgra32> image = new Image<Bgra32>(width, height);

                // Index for the byte array
                int byteIndex = 0;

                // Iterate over the image pixels
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        // Create a color from the next three bytes
                        Bgra32 color = new Bgra32(rgbBytes[byteIndex], rgbBytes[byteIndex + 1], rgbBytes[byteIndex + 2], rgbBytes[byteIndex + 3]);
                        byteIndex += 4;
                        // Set the pixel
                        image[x, y] = color;
                    }
                }

                return image;
            }
            return null;
        }

    }

}
