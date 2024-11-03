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
using OpenSlideGTK;
using Image = SixLabors.ImageSharp.Image;

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
        public static Image<Rgb24> JoinRGB24(IEnumerable<Tuple<Extent, byte[]>> srcPixelTiles, Extent srcPixelExtent, Extent dstPixelExtent)
        {
            if (srcPixelTiles == null || !srcPixelTiles.Any())
                return null;

            // Convert extents to integer values to ensure precision
            srcPixelExtent = srcPixelExtent.ToIntegerExtent();
            dstPixelExtent = dstPixelExtent.ToIntegerExtent();

            int canvasWidth = (int)srcPixelExtent.Width;
            int canvasHeight = (int)srcPixelExtent.Height;
            int dstWidth = (int)dstPixelExtent.Width;
            int dstHeight = (int)dstPixelExtent.Height;

            // Initialize a blank canvas
            var canvas = new Image<Rgb24>(canvasWidth, canvasHeight);

            // Process each tile
            foreach (var tile in srcPixelTiles)
            {
                var tileExtent = tile.Item1.ToIntegerExtent();
                var intersect = srcPixelExtent.Intersect(tileExtent);

                // Skip tiles that don’t overlap with the source area or have no data
                if (intersect.Width == 0 || intersect.Height == 0 || tile.Item2 == null)
                    continue;

                // Load the tile data as a 256x256 Image<Rgb24>
                const int originalTileSize = 256;
                var tileImage = Image.LoadPixelData<Rgb24>(tile.Item2, originalTileSize, originalTileSize);

                // Resize the tile to match the actual tile extent dimensions
                tileImage.Mutate(x => x.Resize((int)tileExtent.Width, (int)tileExtent.Height));

                // Calculate offsets for placing the tile within the canvas
                int tileOffsetX = (int)(intersect.MinX - tileExtent.MinX);
                int tileOffsetY = (int)(intersect.MinY - tileExtent.MinY);
                int canvasOffsetX = (int)(intersect.MinX - srcPixelExtent.MinX);
                int canvasOffsetY = (int)(intersect.MinY - srcPixelExtent.MinY);

                // Ensure the offsets respect image boundaries and copy the data precisely
                for (int y = 0; y < intersect.Height; y++)
                {
                    for (int x = 0; x < intersect.Width; x++)
                    {
                        int canvasX = canvasOffsetX + x;
                        int canvasY = canvasOffsetY + y;
                        int tileX = tileOffsetX + x;
                        int tileY = tileOffsetY + y;

                        // Only copy if within bounds
                        if (canvasX < canvasWidth && canvasY < canvasHeight && tileX < tileImage.Width && tileY < tileImage.Height)
                        {
                            canvas[canvasX, canvasY] = tileImage[tileX, tileY];
                        }
                    }
                }

                tileImage.Dispose();
            }

            // Resize if necessary to match the destination extent
            if (dstWidth != canvasWidth || dstHeight != canvasHeight)
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
        /// <summary>
        /// Join by <paramref name="srcPixelTiles"/> and cut by <paramref name="srcPixelExtent"/> then scale to <paramref name="dstPixelExtent"/>(only height an width is useful).
        /// </summary>
        /// <param name="srcPixelTiles">tile with tile extent collection</param>
        /// <param name="srcPixelExtent">canvas extent</param>
        /// <param name="dstPixelExtent">jpeg output size</param>
        /// <returns></returns>
        public static Image<L16> Join16(IEnumerable<Tuple<Extent, byte[]>> srcPixelTiles, Extent srcPixelExtent, Extent dstPixelExtent)
        {
            if (srcPixelTiles == null || !srcPixelTiles.Any())
                return null;

            // Convert extents to integer values to ensure precision
            srcPixelExtent = srcPixelExtent.ToIntegerExtent();
            dstPixelExtent = dstPixelExtent.ToIntegerExtent();

            int canvasWidth = (int)srcPixelExtent.Width;
            int canvasHeight = (int)srcPixelExtent.Height;
            int dstWidth = (int)dstPixelExtent.Width;
            int dstHeight = (int)dstPixelExtent.Height;

            // Initialize a blank canvas
            var canvas = new Image<L16>(canvasWidth, canvasHeight);

            // Process each tile
            foreach (var tile in srcPixelTiles)
            {
                var tileExtent = tile.Item1.ToIntegerExtent();
                var intersect = srcPixelExtent.Intersect(tileExtent);

                // Skip tiles that don’t overlap with the source area or have no data
                if (intersect.Width == 0 || intersect.Height == 0 || tile.Item2 == null)
                    continue;

                // Load the tile data as a 256x256 Image<L16>
                const int originalTileSize = 256;
                var tileImage = Image.LoadPixelData<L16>(tile.Item2, originalTileSize, originalTileSize);

                // Resize the tile to match the actual tile extent dimensions
                tileImage.Mutate(x => x.Resize((int)tileExtent.Width, (int)tileExtent.Height));

                // Calculate offsets for placing the tile within the canvas
                int tileOffsetX = (int)(intersect.MinX - tileExtent.MinX);
                int tileOffsetY = (int)(intersect.MinY - tileExtent.MinY);
                int canvasOffsetX = (int)(intersect.MinX - srcPixelExtent.MinX);
                int canvasOffsetY = (int)(intersect.MinY - srcPixelExtent.MinY);

                // Ensure the offsets respect image boundaries and copy the data precisely
                for (int y = 0; y < intersect.Height; y++)
                {
                    for (int x = 0; x < intersect.Width; x++)
                    {
                        int canvasX = canvasOffsetX + x;
                        int canvasY = canvasOffsetY + y;
                        int tileX = tileOffsetX + x;
                        int tileY = tileOffsetY + y;

                        // Only copy if within bounds
                        if (canvasX < canvasWidth && canvasY < canvasHeight && tileX < tileImage.Width && tileY < tileImage.Height)
                        {
                            canvas[canvasX, canvasY] = tileImage[tileX, tileY];
                        }
                    }
                }

                tileImage.Dispose();
            }

            // Resize if necessary to match the destination extent
            if (dstWidth != canvasWidth || dstHeight != canvasHeight)
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

            // Convert extents to integer values for precision
            srcPixelExtent = srcPixelExtent.ToIntegerExtent();
            dstPixelExtent = dstPixelExtent.ToIntegerExtent();

            int canvasWidth = (int)srcPixelExtent.Width;
            int canvasHeight = (int)srcPixelExtent.Height;

            // Create a blank canvas as an RGB image in srgb color space
            NetVips.Image canvas = NetVips.Image.Black(canvasWidth, canvasHeight, bands: 3).Cast(Enums.BandFormat.Uchar).Colourspace(Enums.Interpretation.Srgb);

            foreach (var tile in srcPixelTiles)
            {
                if (tile.Item2 == null)
                    continue;

                fixed (byte* pTileData = tile.Item2)
                {
                    var tileExtent = tile.Item1.ToIntegerExtent();

                    // Load tile from memory, assuming it's a 256x256 RGB 24-bit image by default
                    const int originalTileSize = 256;
                    NetVips.Image tileImage = NetVips.Image.NewFromMemory((IntPtr)pTileData, (ulong)tile.Item2.Length, originalTileSize, originalTileSize, 3, Enums.BandFormat.Uchar);

                    // Resize the tile image to match its tileExtent dimensions
                    tileImage = tileImage.Resize((double)tileExtent.Width / originalTileSize, vscale: (double)tileExtent.Height / originalTileSize);

                    // Convert the tile to srgb color space if necessary
                    tileImage = tileImage.Colourspace(Enums.Interpretation.Srgb);

                    // Calculate the intersection area between the source extent and tile extent
                    var intersect = srcPixelExtent.Intersect(tileExtent);
                    if (intersect.Width == 0 || intersect.Height == 0)
                        continue;

                    // Calculate offsets within the tile and the canvas for cropping and compositing
                    int tileOffsetPixelX = (int)Math.Ceiling(intersect.MinX - tileExtent.MinX);
                    int tileOffsetPixelY = (int)Math.Ceiling(intersect.MinY - tileExtent.MinY);
                    int canvasOffsetPixelX = (int)Math.Ceiling(intersect.MinX - srcPixelExtent.MinX);
                    int canvasOffsetPixelY = (int)Math.Ceiling(intersect.MinY - srcPixelExtent.MinY);

                    // Crop the resized tile to match the intersecting region and composite it onto the canvas
                    using (var croppedTile = tileImage.Crop(tileOffsetPixelX, tileOffsetPixelY, (int)intersect.Width, (int)intersect.Height))
                    {
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

            // Convert extents to integer values for precision
            srcPixelExtent = srcPixelExtent.ToIntegerExtent();
            dstPixelExtent = dstPixelExtent.ToIntegerExtent();

            int canvasWidth = (int)srcPixelExtent.Width;
            int canvasHeight = (int)srcPixelExtent.Height;

            // Create a blank canvas as a 16-bit grayscale image
            NetVips.Image canvas = NetVips.Image.Black(canvasWidth, canvasHeight, bands: 1).Cast(Enums.BandFormat.Ushort);

            foreach (var tile in srcPixelTiles)
            {
                if (tile.Item2 == null)
                    continue;

                fixed (byte* pTileData = tile.Item2)
                {
                    var tileExtent = tile.Item1.ToIntegerExtent();

                    // Load tile from memory, assuming it's a 256x256 16-bit grayscale by default
                    const int originalTileSize = 256;
                    NetVips.Image tileImage = NetVips.Image.NewFromMemory((IntPtr)pTileData, (ulong)tile.Item2.Length, originalTileSize, originalTileSize, 1, Enums.BandFormat.Ushort);

                    // Resize the tile image to match its tileExtent dimensions
                    tileImage = tileImage.Resize((double)tileExtent.Width / originalTileSize, vscale: (double)tileExtent.Height / originalTileSize);

                    // Calculate the intersection area between the source extent and tile extent
                    var intersect = srcPixelExtent.Intersect(tileExtent);
                    if (intersect.Width == 0 || intersect.Height == 0)
                        continue;

                    // Calculate offsets within the tile and the canvas for cropping and compositing
                    int tileOffsetPixelX = (int)Math.Ceiling(intersect.MinX - tileExtent.MinX);
                    int tileOffsetPixelY = (int)Math.Ceiling(intersect.MinY - tileExtent.MinY);
                    int canvasOffsetPixelX = (int)Math.Ceiling(intersect.MinX - srcPixelExtent.MinX);
                    int canvasOffsetPixelY = (int)Math.Ceiling(intersect.MinY - srcPixelExtent.MinY);

                    // Crop the resized tile to match the intersecting region and composite it onto the canvas
                    using (var croppedTile = tileImage.Crop(tileOffsetPixelX, tileOffsetPixelY, (int)intersect.Width, (int)intersect.Height))
                    {
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