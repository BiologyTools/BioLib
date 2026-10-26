using BruTile;
using NetVips;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BioLib
{
    public static class ImageUtil
    {
        public static Image JoinVips8(
            IEnumerable<Tuple<Extent, byte[]>> tiles,
            Extent sourceExtent,
            Extent destinationExtent)
        {
            return JoinVips(tiles, sourceExtent, destinationExtent,
                Enums.BandFormat.Uchar, bands: 1, bytesPerSample: 1);
        }

        public static Image JoinVipsRGB24(
            IEnumerable<Tuple<Extent, byte[]>> tiles,
            Extent sourceExtent,
            Extent destinationExtent)
        {
            return JoinVips(tiles, sourceExtent, destinationExtent,
                Enums.BandFormat.Uchar, bands: 3, bytesPerSample: 1);
        }

        public static Image JoinVips16(
            IEnumerable<Tuple<Extent, byte[]>> tiles,
            Extent sourceExtent,
            Extent destinationExtent)
        {
            return JoinVips(tiles, sourceExtent, destinationExtent,
                Enums.BandFormat.Ushort, bands: 1, bytesPerSample: 2);
        }

        private static Image JoinVips(
            IEnumerable<Tuple<Extent, byte[]>> sourceTiles,
            Extent sourceExtent,
            Extent destinationExtent,
            Enums.BandFormat format,
            int bands,
            int bytesPerSample)
        {
            var tiles = sourceTiles?.Where(tile => tile?.Item2?.Length > 0).ToList();
            if (tiles == null || tiles.Count == 0)
                return null;

            sourceExtent = sourceExtent.ToIntegerExtent();
            destinationExtent = destinationExtent.ToIntegerExtent();
            int canvasWidth = checked((int)sourceExtent.Width);
            int canvasHeight = checked((int)sourceExtent.Height);
            int destinationWidth = checked((int)destinationExtent.Width);
            int destinationHeight = checked((int)destinationExtent.Height);
            if (canvasWidth <= 0 || canvasHeight <= 0 || destinationWidth <= 0 || destinationHeight <= 0)
                return null;

            Image canvas = Image.Black(canvasWidth, canvasHeight, bands: bands).Cast(format);
            foreach (var tile in tiles)
            {
                var tileExtent = tile.Item1.ToIntegerExtent();
                var intersection = sourceExtent.Intersect(tileExtent);
                if (intersection.Width <= 0 || intersection.Height <= 0)
                    continue;

                (int sourceWidth, int sourceHeight) =
                    InferSourceDimensions(tile.Item2.Length, bands, bytesPerSample, tileExtent);
                using Image source = Image.NewFromMemory(
                    tile.Item2, sourceWidth, sourceHeight, bands, format);

                double horizontalScale = tileExtent.Width / sourceWidth;
                double verticalScale = tileExtent.Height / sourceHeight;
                using Image scaled = horizontalScale == 1 && verticalScale == 1
                    ? source.Copy()
                    : source.Resize(horizontalScale, vscale: verticalScale, kernel: Enums.Kernel.Nearest);

                int tileX = checked((int)Math.Round(intersection.MinX - tileExtent.MinX));
                int tileY = checked((int)Math.Round(intersection.MinY - tileExtent.MinY));
                int canvasX = checked((int)Math.Round(intersection.MinX - sourceExtent.MinX));
                int canvasY = checked((int)Math.Round(intersection.MinY - sourceExtent.MinY));
                int copyWidth = Math.Min(checked((int)Math.Round(intersection.Width)), scaled.Width - tileX);
                int copyHeight = Math.Min(checked((int)Math.Round(intersection.Height)), scaled.Height - tileY);
                copyWidth = Math.Min(copyWidth, canvasWidth - canvasX);
                copyHeight = Math.Min(copyHeight, canvasHeight - canvasY);
                if (copyWidth <= 0 || copyHeight <= 0)
                    continue;

                using Image cropped = scaled.Crop(tileX, tileY, copyWidth, copyHeight);
                Image updated = canvas.Insert(cropped, canvasX, canvasY);
                canvas.Dispose();
                canvas = updated;
            }

            if (destinationWidth != canvasWidth || destinationHeight != canvasHeight)
            {
                double horizontalScale = (double)destinationWidth / canvasWidth;
                double verticalScale = (double)destinationHeight / canvasHeight;
                Image resized = canvas.Resize(
                    horizontalScale, vscale: verticalScale, kernel: Enums.Kernel.Nearest);
                canvas.Dispose();
                canvas = resized;
            }
            return canvas;
        }

        private static (int Width, int Height) InferSourceDimensions(
            int byteLength,
            int bands,
            int bytesPerSample,
            Extent tileExtent)
        {
            int pixels = byteLength / checked(bands * bytesPerSample);
            int extentWidth = Math.Max(1, checked((int)Math.Round(tileExtent.Width)));
            int extentHeight = Math.Max(1, checked((int)Math.Round(tileExtent.Height)));
            if (checked(extentWidth * extentHeight) == pixels)
                return (extentWidth, extentHeight);

            int square = Math.Max(1, checked((int)Math.Sqrt(pixels)));
            if (checked(square * square) == pixels)
                return (square, square);
            if (pixels % extentWidth == 0)
                return (extentWidth, pixels / extentWidth);

            throw new ArgumentException("Tile byte length does not match its pixel dimensions.");
        }
    }
}
