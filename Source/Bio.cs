
using AForge;
using AForge.Imaging;
using AForge.Imaging.Filters;
using BitMiracle.LibTiff.Classic;
using loci.common.services;
using loci.formats;
using loci.formats.services;
using ome.xml.model.primitives;
using loci.formats.meta;
using ome.units.quantity;
using ome;
using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Runtime.Serialization;
using System.Text;
using Bitmap = AForge.Bitmap;
using Color = AForge.Color;
using loci.formats.@in;
using Gtk;
using System.Linq;
using NetVips;
using System.Threading.Tasks;
using OpenSlideGTK;
using System.Reflection;
using Newtonsoft.Json.Serialization;
namespace BioLib
{
    /* A class declaration. */
    public static class Images
    {
        public static List<BioImage> images = new List<BioImage>();

        /// 
        /// @param ids The id of the image you want to get.
        public static BioImage GetImage(string ids)
        {
            ids = ids.Replace("\\", "/");
            for (int i = 0; i < images.Count; i++)
            {
                if (images[i].ID == ids || images[i].Filename == Path.GetFileName(ids))
                    return images[i];
            }
            return null;
        }
        /// It adds an image to the list of images
        /// 
        /// @param BioImage A class that contains the image data and other information.
        public static void AddImage(BioImage im)
        {
            if (images.Contains(im))
                return;
            im.Filename = GetImageName(im.Filename);
            images.Add(im);
        }
        /// It takes a string as an argument, and returns the number of times that string appears in the
        /// list of images
        /// 
        /// @param s The name of the image
        /// 
        /// @return The number of images that contain the name of the image.
        public static int GetImageCountByName(string s)
        {
            int i = 0;
            s = RemoveExtensions(s);
            if (s.Contains("-"))
            {
                string ss = RemoveExtensions(s.Substring(s.LastIndexOf("-"), s.Length - s.LastIndexOf("-")));
                s = ss;
            }
            for (int im = 0; im < images.Count; im++)
            {
                if (images[im].ID.Contains(s))
                    i++;
            }
            return i;
        }
        /// <summary>
        /// Removes extensions from filename including multiple extensions like .ome.tif.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static string RemoveExtensions(string file)
        {
            return Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file));
        }
        /// <summary>
        /// Return extensions of a file including multiple extensions like .ome.tif.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static string GetExtensions(string file)
        {
            string[] ex = file.Split(".");
            if (ex.Length == 1)
                return "." + ex[0];
            else
            if (ex.Length > 2)
                return "." + ex[ex.Length - 2] + "." + ex[ex.Length - 1];
            else
                return "." + ex.Last();
        }

        /// <summary>
        /// Get a unique image name from a string.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string GetImageName(string s)
        {
            //Here we create a unique ID for an image.
            int i = Images.GetImageCountByName(s);
            if (i == 0)
                return Path.GetFileName(s);
            string name = RemoveExtensions(s);
            string ext = GetExtensions(s);
            if (name.EndsWith("-" + i))
                name = name.Substring(name.LastIndexOf('-'), name.Length - name.LastIndexOf('-'));
            return name + "-" + (i + 1) + ext;
        }
        /// This function removes an image from the table
        /// 
        /// @param BioImage This is the image that you want to remove.
        public static void RemoveImage(BioImage im)
        {
            RemoveImage(im.Filename);
        }
        /// It removes an image from the table
        /// 
        /// @param id The id of the image to remove.
        /// 
        /// @return The image is being returned.
        public static void RemoveImage(string id)
        {
            BioImage im = GetImage(id);
            if (im == null)
                return;
            images.Remove(im);
            im.Dispose();
        }
        /// It updates an image from the table
        /// 
        /// @param id The id of the image to update.
        /// @param im The BioImage to update with.
        /// @return The image is being returned.
        public static void UpdateImage(BioImage im)
        {
            for (int i = 0; i < images.Count; i++)
            {
                if (images[i].ID == im.ID)
                {
                    images[i] = im;
                    return;
                }
            }
        }
    }
    /* A struct that is used to store the resolution of an image. */
    /* A struct that is used to store the resolution of an image. */
    public struct Resolution
    {
        int x;
        int y;
        PixelFormat format;
        double px, py, pz, sx, sy, sz;
        public int SizeX
        {
            get { return x; }
            set { x = value; }
        }
        public int SizeY
        {
            get { return y; }
            set { y = value; }
        }
        public double PhysicalSizeX
        {
            get { return px; }
            set { px = value; }
        }
        public double PhysicalSizeY
        {
            get { return py; }
            set { py = value; }
        }
        public double PhysicalSizeZ
        {
            get { return pz; }
            set { pz = value; }
        }
        public double StageSizeX
        {
            get { return sx; }
            set { sx = value; }
        }
        public double StageSizeY
        {
            get { return sy; }
            set { sy = value; }
        }
        public double StageSizeZ
        {
            get { return sz; }
            set { sz = value; }
        }
        public double VolumeWidth
        {
            get
            {
                return PhysicalSizeX * SizeX;
            }
        }
        public double VolumeHeight
        {
            get
            {
                return PhysicalSizeY * SizeY;
            }
        }
        public PixelFormat PixelFormat
        {
            get { return format; }
            set { format = value; }
        }
        public int RGBChannelsCount
        {
            get
            {
                if (PixelFormat == PixelFormat.Format8bppIndexed || PixelFormat == PixelFormat.Format16bppGrayScale)
                    return 1;
                else if (PixelFormat == PixelFormat.Format24bppRgb || PixelFormat == PixelFormat.Format48bppRgb)
                    return 3;
                else
                    return 4;
            }
        }
        public long SizeInBytes
        {
            get
            {
                if (format == PixelFormat.Format8bppIndexed)
                    return (long)y * (long)x;
                else if (format == PixelFormat.Format16bppGrayScale)
                    return (long)y * (long)x * 2;
                else if (format == PixelFormat.Format24bppRgb)
                    return (long)y * (long)x * 3;
                else if (format == PixelFormat.Format32bppRgb || format == PixelFormat.Format32bppArgb)
                    return (long)y * (long)x * 4;
                else if (format == PixelFormat.Format48bppRgb || format == PixelFormat.Format48bppRgb)
                    return (long)y * (long)x * 6;
                throw new NotSupportedException(format + " is not supported.");
            }
        }
        public Resolution(int w, int h, int omePx, int bitsPerPixel, double physX, double physY, double physZ, double stageX, double stageY, double stageZ)
        {
            x = w;
            y = h;
            sx = stageX;
            sy = stageY;
            sz = stageZ;
            format = BioImage.GetPixelFormat(omePx, bitsPerPixel);
            px = physX;
            py = physY;
            pz = physZ;
        }
        public Resolution(int w, int h, PixelFormat f, double physX, double physY, double physZ, double stageX, double stageY, double stageZ)
        {
            x = w;
            y = h;
            format = f;
            sx = stageX;
            sy = stageY;
            sz = stageZ;
            px = physX;
            py = physY;
            pz = physZ;
        }
        public Resolution Copy()
        {
            return new Resolution(x, y, format, px, py, pz, sx, sy, sz);
        }
        public override string ToString()
        {
            return "new Resolution(" + SizeX + "," + SizeY + "," + format + "," + PhysicalSizeX + "," + PhysicalSizeY + "," + PhysicalSizeZ + "," + StageSizeX + "," + StageSizeY + "," + StageSizeZ + ");";
        }
    }

    /* The ROI class is a class that contains a list of points, a bounding box, and a type */
    public class ROI : IDisposable
    {
        /* Defining an enum. */
        public enum Type
        {
            Rectangle,
            Point,
            Line,
            Polygon,
            Polyline,
            Freeform,
            Ellipse,
            Label,
            Mask
        }
        /* A property of a class. */
        public PointD Point
        {
            get
            {
                if (type == Type.Mask)
                    return new PointD(roiMask.X * roiMask.PhysicalSizeX, roiMask.Y * roiMask.PhysicalSizeY);
                return Points[0];
            }
            set
            {
                UpdatePoint(value, 0);
                UpdateBoundingBox();
            }
        }
        public RectangleD Rect
        {
            get
            {
                if (Points.Count == 0)
                    return new RectangleD(0, 0, 0, 0);
                if (type == Type.Line || type == Type.Polyline || type == Type.Polygon || type == Type.Freeform || type == Type.Label)
                    return BoundingBox;
                if (type == Type.Rectangle || type == Type.Ellipse)
                    return new RectangleD(Points[0].X, Points[0].Y, Points[1].X - Points[0].X, Points[2].Y - Points[0].Y);
                if (type == Type.Mask)
                {
                    return new RectangleD(roiMask.X * roiMask.PhysicalSizeX, roiMask.Y * roiMask.PhysicalSizeY, roiMask.Width * roiMask.PhysicalSizeX, roiMask.Height * roiMask.PhysicalSizeY);
                }
                else
                    return new RectangleD(Points[0].X, Points[0].Y, W, H);
            }
            set
            {
                if (type == Type.Line || type == Type.Polyline || type == Type.Polygon || type == Type.Freeform)
                {
                    BoundingBox = value;
                }
                else
                if (Points.Count < 4 && (type == Type.Rectangle || type == Type.Ellipse))
                {
                    AddPoint(new PointD(value.X, value.Y));
                    AddPoint(new PointD(value.X + value.W, value.Y));
                    AddPoint(new PointD(value.X, value.Y + value.H));
                    AddPoint(new PointD(value.X + value.W, value.Y + value.H));
                }
                else
                if (type == Type.Rectangle || type == Type.Ellipse)
                {
                    Points[0] = new PointD(value.X, value.Y);
                    Points[1] = new PointD(value.X + value.W, value.Y);
                    Points[2] = new PointD(value.X, value.Y + value.H);
                    Points[3] = new PointD(value.X + value.W, value.Y + value.H);
                }
                UpdateBoundingBox();
            }
        }
        public double X
        {
            get
            {
                if (type == Type.Mask)
                {
                    return roiMask.X * roiMask.PhysicalSizeX;
                }
                return Point.X;
            }
            set
            {
                Rect = new RectangleD(value, Y, W, H);
            }
        }
        public double Y
        {
            get
            {
                if (type == Type.Mask)
                {
                    return roiMask.Y * roiMask.PhysicalSizeY;
                }
                return Point.Y;
            }
            set
            {
                Rect = new RectangleD(X, value, W, H);
            }
        }
        public double W
        {
            get
            {
                if (type == Type.Mask)
                {
                    return roiMask.Width * roiMask.PhysicalSizeX;
                }
                if (type == Type.Point)
                    return strokeWidth;
                else
                    return BoundingBox.W;
            }
            set
            {
                Rect = new RectangleD(X, Y, value, H);
            }
        }
        public double H
        {
            get
            {
                if (type == Type.Mask)
                {
                    return roiMask.Height * roiMask.PhysicalSizeY;
                }
                if (type == Type.Point)
                    return strokeWidth;
                else
                    return BoundingBox.H;
            }
            set
            {
                Rect = new RectangleD(X, Y, W, value);
            }
        }
        public int Resolution
        {
            get { return resolution; }
            set { resolution = value; }
        }
        public enum CoordinateSystem
        {
            pixel,
            micron
        }
        public Type type;
        public static float selectBoxSize = 8f;
        private List<PointD> Points = new List<PointD>();
        public List<PointD> PointsD
        {
            get
            {
                return Points;
            }
        }
        private List<RectangleD> selectBoxs = new List<RectangleD>();
        public List<int> selectedPoints = new List<int>();
        public RectangleD BoundingBox;
        public float fontSize = 12;
        public Cairo.FontSlant slant;
        public Cairo.FontWeight weight;
        public string family = "Times New Roman";
        public ZCT coord;
        public Color strokeColor;
        public Color fillColor;
        public bool isFilled = false;
        public string id = "";
        public string roiID = "";
        public string roiName = "";
        public string properties = "";
        public int serie = 0;
        private string text = "";
        private int resolution = 0;
        public double strokeWidth = 1;
        public int shapeIndex = 0;
        public bool closed = false;
        bool selected = false;
        public bool Selected
        {
            get { return selected; }
            set
            {
                if (roiMask != null)
                    roiMask.Selected = value;
                selected = value;
                if (!selected)
                    selectedPoints.Clear();
            }
        }
        public bool subPixel = false;
        public Mask roiMask { get; set; }
        /// <summary>
        /// Represents a Mask layer.
        /// </summary>
        public class Mask : IDisposable
        {
            public float min = 0;
            float[] mask;
            int width;
            int height;
            public double X { get; set; }
            public double Y { get; set; }
            public int Width { get { return width; } set { width = value; } }
            public int Height { get { return height; } set { height = value; } }
            public double PhysicalSizeX { get; set; }
            public double PhysicalSizeY { get; set; }
            bool updatePixbuf = true;
            bool updateColored = true;
            Gdk.Pixbuf pixbuf;
            Gdk.Pixbuf colored;
            bool selected = false;
            internal bool Selected
            {
                get { return selected; }
                set
                {
                    selected = value;
                }
            }
            public bool IsSelected(int x, int y)
            {
                int ind = y * width + x;
                if (ind >= mask.Length)
                    return false;
                if (mask[ind] > min)
                {
                    return true;
                }
                return false;
            }
            public float GetValue(int x, int y)
            {
                int ind = y * width + x;
                if (ind > mask.Length)
                    throw new ArgumentException("Point " + x + "," + y + " is outside the mask.");
                return mask[ind];
            }
            public void SetValue(int x, int y, float val)
            {
                int ind = y * width + x;
                if (ind > mask.Length)
                    throw new ArgumentException("Point " + x + "," + y + " is outside the mask.");
                mask[ind] = val;
                updatePixbuf = true;
                updateColored = true;
            }
            public Mask(float[] fts, int width, int height, double physX, double physY, double x, double y)
            {
                this.width = width;
                this.height = height;
                X = x; Y = y;
                PhysicalSizeX = physX;
                PhysicalSizeY = physY;
                mask = fts;
                byte[] bt = GetBytesCropped();
                mask = new float[bt.Length];
                for (int i = 0; i < bt.Length; i++)
                {
                    mask[i] = (float)bt[i];
                }
            }
            public Mask(byte[] fts, int width, int height, double physX, double physY, double x, double y)
            {
                this.width = width;
                this.height = height;
                this.X = x / physX;
                this.Y = y / physY;
                PhysicalSizeX = physX;
                PhysicalSizeY = physY;
                mask = new float[fts.Length];
                for (int i = 0; i < fts.Length; i++)
                {
                    mask[i] = (float)fts[i];
                }
            }
            public Gdk.Pixbuf Pixbuf
            {
                get
                {
                    if (updatePixbuf)
                    {
                        if (pixbuf != null)
                            pixbuf.Dispose();
                        byte[] pixelData = new byte[width * height * 4];
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                int ind = y * width + x;
                                if (mask[ind] > 0)
                                {
                                    pixelData[4 * ind] = (byte)(mask[ind] / 255);// Blue
                                    pixelData[4 * ind + 1] = (byte)(mask[ind] / 255);// Green
                                    pixelData[4 * ind + 2] = (byte)(mask[ind] / 255);// Red
                                    pixelData[4 * ind + 3] = 125;// Alpha
                                }
                                else
                                    pixelData[4 * ind + 3] = 0;
                            }
                        }
                        pixbuf = new Gdk.Pixbuf(pixelData, true, 8, width, height, width * 4);
                        updatePixbuf = false;
                        return pixbuf;
                    }
                    else
                        return pixbuf;
                }
            }
            void UpdateColored(AForge.Color col, byte alpha)
            {
                if (mask == null || mask.Length < width * height)
                    throw new InvalidOperationException("Invalid mask size.");
                // Get the minimum and maximum values of the mask for normalization
                var (min, max) = GetMinAndMax(mask);
                min = 0;
                byte[] pixelData = new byte[width * height * 4];

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int ind = y * width + x;

                        if (ind < mask.Length)
                        {
                            float value = mask[ind];
                            if (value > 0)
                            {
                                pixelData[4 * ind] = col.R;       // Red
                                pixelData[4 * ind + 1] = col.G;  // Green
                                pixelData[4 * ind + 2] = col.B;  // Blue
                                pixelData[4 * ind + 3] = alpha;  // Alpha
                            }
                        }
                    }
                }

                // Create Gdk.Pixbuf with the updated pixel data
                colored = new Gdk.Pixbuf(pixelData, true, 8, width, height, width * 4);

                // Mark as updated
                updateColored = false;
            }

            public Gdk.Pixbuf GetColored(AForge.Color col, byte alpha, bool forceUpdate = false)
            {
                if (updateColored || forceUpdate)
                {
                    UpdateColored(col, alpha);
                    return colored;
                }
                else
                    return colored;
            }

            public byte[] GetColoredBytes(AForge.Color col)
            {
                byte[] pixelData = new byte[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int ind = y * width + x;

                        if (ind < mask.Length)
                        {
                            float value = mask[ind];
                            if (value > 0)
                            {
                                pixelData[ind] = (byte)value;
                            }
                        }
                    }
                }
                return pixelData;
            }

            /// <summary>
            /// Crops a mask based on non-zero values and converts it to an 8-bit grayscale image.
            /// </summary>
            /// <param name="fullMask">The input float array representing the full image mask.</param>
            /// <param name="imageWidth">The width of the full image.</param>
            /// <param name="imageHeight">The height of the full image.</param>
            /// <param name="threshold">The threshold to consider a pixel as part of the crop.</param>
            /// <returns>
            /// A tuple containing:
            /// - 8-bit grayscale byte array.
            /// - Width and height of the cropped region.
            /// - Starting X and Y coordinates of the crop in the original image.
            /// </returns>
            public static (byte[] grayImage, int cropWidth, int cropHeight, int startX, int startY) OutputAs8BitImage(
                float[] fullMask, int imageWidth, int imageHeight, float threshold = 0.0f)
            {
                if (fullMask == null || fullMask.Length != imageWidth * imageHeight)
                    throw new ArgumentException("Invalid mask dimensions or null mask.");

                // Crop the mask using the threshold
                var cropResult = CropFullImageMask(fullMask, imageWidth, imageHeight, threshold);
                float[] croppedMask = cropResult.croppedMask;

                // Handle empty crop
                if (croppedMask == null || croppedMask.Length == 0)
                    return (Array.Empty<byte>(), 0, 0, 0, 0);

                int cropWidth = cropResult.cropWidth;
                int cropHeight = cropResult.cropHeight;

                // Normalize and convert to 8-bit grayscale
                byte[] grayImage = new byte[cropWidth * cropHeight];
                for (int i = 0; i < croppedMask.Length; i++)
                {
                    grayImage[i] = (byte)(Math.Clamp(croppedMask[i], 0.0f, 1.0f) * 255);
                }

                return (grayImage, cropWidth, cropHeight, cropResult.startX, cropResult.startY);
            }
            /// <summary>
            /// Gets the minimum and maximum values from a float array.
            /// </summary>
            /// <param name="floatArray">The input float array.</param>
            /// <returns>A tuple containing the minimum and maximum values.</returns>
            public static (float min, float max) GetMinAndMax(float[] floatArray)
            {
                if (floatArray == null || floatArray.Length == 0)
                    throw new ArgumentException("Input array must not be null or empty.");

                float min = floatArray[0];
                float max = floatArray[0];

                foreach (float value in floatArray)
                {
                    if (value < min)
                    {
                        min = value;
                    }
                    if (value > max)
                    {
                        max = value;
                    }
                }

                return (min, max);
            }
            private byte[] GetBytesCropped()
            {
                // Crop the mask using a threshold of 0
                var cropResult = CropFullImageMask(mask, this.Width, this.Height, 0.0f);
                float[] croppedMask = cropResult.croppedMask;

                // Handle empty mask case
                if (croppedMask == null || croppedMask.Length == 0)
                    return Array.Empty<byte>();

                // Get the minimum and maximum values of the mask for normalization
                var (min, max) = GetMinAndMax(croppedMask);
                min = 0;
                // Handle case where all values are equal (avoid division by zero)
                if (Math.Abs(max - min) < float.Epsilon)
                    return Array.Empty<byte>();

                // Prepare the byte array for the normalized output
                int cropWidth = cropResult.cropWidth;
                int cropHeight = cropResult.cropHeight;
                byte[] bytes = new byte[cropWidth * cropHeight];

                // Normalize float values and convert to byte
                for (int y = 0; y < cropHeight; y++)
                {
                    for (int x = 0; x < cropWidth; x++)
                    {
                        int index = y * cropWidth + x; // 1D index for the mask
                        if (croppedMask[index] > 0)
                        {
                            // Normalize the value: (value - min) / (max - min) * 255
                            float normalized = (croppedMask[index] - min) / (max - min);
                            bytes[index] = (byte)(normalized * 255);
                        }
                    }
                }
                X = cropResult.startX;
                Y = cropResult.startY;
                width = cropWidth;
                height = cropHeight;
                return bytes;
            }
            public byte[] GetBytes()
            {
                byte[] rgbaBytes = new byte[width * height];
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        int index = y * Width + x; // 1D index for the mask
                        // Set RGBA channels
                        rgbaBytes[index] = (byte)mask[index];
                    }
                }
                return rgbaBytes;
            }

            /// <summary>
            /// Crops the smallest rectangular region containing all "white" pixels (values above a threshold) from a full image mask.
            /// </summary>
            /// <param name="fullMask">The input float array representing the entire image mask.</param>
            /// <param name="imageWidth">The width of the full image.</param>
            /// <param name="imageHeight">The height of the full image.</param>
            /// <param name="threshold">The threshold to consider a pixel as "white".</param>
            /// <returns>
            /// A tuple containing:
            /// - Cropped float array mask.
            /// - Crop width.
            /// - Crop height.
            /// - Starting X and Y coordinates of the crop in the original image.
            /// </returns>
            public static (float[] croppedMask, int cropWidth, int cropHeight, int startX, int startY) CropFullImageMask(
                float[] fullMask, int imageWidth, int imageHeight, float threshold = 0.0f)
            {
                if (fullMask == null || fullMask.Length != imageWidth * imageHeight)
                    throw new ArgumentException("Invalid mask dimensions or null mask.");

                // Initialize bounding box values
                int minX = imageWidth, minY = imageHeight, maxX = -1, maxY = -1;

                // Find the bounding box of "white" pixels
                for (int y = 0; y < imageHeight; y++)
                {
                    for (int x = 0; x < imageWidth; x++)
                    {
                        int index = y * imageWidth + x;
                        if (fullMask[index] > threshold) // Pixel exceeds threshold, considered "white"
                        {
                            minX = Math.Min(minX, x);
                            minY = Math.Min(minY, y);
                            maxX = Math.Max(maxX, x);
                            maxY = Math.Max(maxY, y);
                        }
                    }
                }

                // Handle cases where no white pixels are found
                if (minX > maxX || minY > maxY)
                    return (Array.Empty<float>(), 0, 0, 0, 0); // Return an empty mask

                // Calculate the cropped region dimensions
                int cropWidth = maxX - minX + 1;
                int cropHeight = maxY - minY + 1;

                // Extract the cropped region
                float[] croppedMask = new float[cropWidth * cropHeight];
                for (int y = 0; y < cropHeight; y++)
                {
                    for (int x = 0; x < cropWidth; x++)
                    {
                        int sourceIndex = (minY + y) * imageWidth + (minX + x);
                        int targetIndex = y * cropWidth + x;

                        croppedMask[targetIndex] = fullMask[sourceIndex];
                    }
                }

                return (croppedMask, cropWidth, cropHeight, minX, minY);
            }

            public void Dispose()
            {
                if (pixbuf != null)
                    pixbuf.Dispose();
                if (colored != null)
                    colored.Dispose();
                mask = null;
            }
        }
        /*
        public Size TextSize
        {
            get
            {
                return TextRenderer.MeasureText(text, font);
            }
        }
        */
        public ROI Copy()
        {
            ROI copy = new ROI();
            copy.id = id;
            copy.roiID = roiID;
            copy.roiName = roiName;
            copy.text = text;
            copy.strokeWidth = strokeWidth;
            copy.strokeColor = strokeColor;
            copy.fillColor = fillColor;
            copy.Points = Points;
            copy.Selected = Selected;
            copy.shapeIndex = shapeIndex;
            copy.closed = closed;
            copy.family = family;
            copy.fontSize = fontSize;
            copy.slant = slant;
            copy.selectBoxs = selectBoxs;
            copy.BoundingBox = BoundingBox;
            copy.isFilled = isFilled;
            copy.coord = coord;
            copy.selectedPoints = selectedPoints;

            return copy;
        }
        public ROI Copy(ZCT cord)
        {
            ROI copy = new ROI();
            copy.type = type;
            copy.id = id;
            copy.roiID = roiID;
            copy.roiName = roiName;
            copy.text = text;
            copy.strokeWidth = strokeWidth;
            copy.strokeColor = strokeColor;
            copy.fillColor = fillColor;
            copy.Points.AddRange(Points);
            copy.Selected = Selected;
            copy.shapeIndex = shapeIndex;
            copy.closed = closed;
            copy.family = family;
            copy.fontSize = fontSize;
            copy.slant = slant;
            copy.selectBoxs.AddRange(selectBoxs);
            copy.BoundingBox = BoundingBox;
            copy.isFilled = isFilled;
            copy.coord = cord;
            copy.selectedPoints = selectedPoints;
            return copy;
        }
        public string Text
        {
            get
            {
                return text;
            }
            set
            {
                text = value;
                if (type == Type.Label)
                {
                    UpdateBoundingBox();
                }
            }
        }
        /// > This function returns a rectangle that is the bounding box of the object, but with a
        /// border of half the scale
        /// 
        /// @param scale the scale of the image
        /// 
        /// @return A rectangle with the following properties:
        public RectangleD GetSelectBound(double scaleX, double scaleY)
        {
            if (type == Type.Mask)
                return BoundingBox;
            double fx = scaleX / 2;
            double fy = scaleY / 2;
            return new RectangleD(BoundingBox.X - fx, BoundingBox.Y - fy, BoundingBox.W + scaleX, BoundingBox.H + scaleY);
        }
        /* Creating a new ROI object. */
        public ROI()
        {
            coord = new ZCT(0, 0, 0);
            strokeColor = Color.Yellow;
            BoundingBox = new RectangleD(0, 0, 1, 1);
        }
        /// <summary>
        /// The function "ImagePoints" takes a Resolution object as input and returns an array of PointD
        /// objects that have been converted to image space using the provided resolution values.
        /// </summary>
        /// <param name="Resolution">The "Resolution" parameter is an object that contains information
        /// about the resolution of an image. It typically includes properties such as the stage size
        /// (width and height), physical size (width and height), and possibly other properties related
        /// to the image resolution.</param>
        /// <returns>
        /// The method is returning an array of PointD objects.
        /// </returns>
        public PointD[] ImagePoints(Resolution res)
        {
            if (type == Type.Rectangle || type == Type.Mask)
            {
                PointD[] pts = new PointD[4];
                pts[0] = PointsD[0];
                pts[1] = new PointD(PointsD[0].X + BoundingBox.W, PointsD[0].Y);
                pts[2] = new PointD(PointsD[0].X + BoundingBox.W, PointsD[0].Y + BoundingBox.H);
                pts[3] = new PointD(PointsD[0].X, PointsD[0].Y + BoundingBox.H);
                return BioImage.ToImageSpace(pts.ToList(), res.StageSizeX, res.StageSizeY, res.PhysicalSizeX, res.PhysicalSizeY);
            }
            else
                return BioImage.ToImageSpace(PointsD, res.StageSizeX, res.StageSizeY, res.PhysicalSizeX, res.PhysicalSizeY);
        }
        /// It returns an array of RectangleF objects that are used to draw the selection boxes around
        /// the points of the polygon
        /// 
        /// @param s the size of the select box
        /// 
        /// @return A list of RectangleF objects.
        public RectangleD[] GetSelectBoxes(double s)
        {
            double f = s / 2;
            selectBoxs.Clear();
            for (int i = 0; i < Points.Count; i++)
            {
                selectBoxs.Add(new RectangleD((float)(Points[i].X - f), (float)(Points[i].Y - f), (float)s, (float)s));
            }
            return selectBoxs.ToArray();
        }
        /// It returns an array of RectangleF objects that are used to draw the selection boxes around
        /// the points of the polygon
        /// 
        /// @param s the size of the select box
        /// 
        /// @return A list of RectangleF objects.
        public RectangleD[] GetSelectBoxes()
        {
            double f = ROI.selectBoxSize / 2;
            selectBoxs.Clear();
            for (int i = 0; i < Points.Count; i++)
            {
                selectBoxs.Add(new RectangleD((float)(Points[i].X - f), (float)(Points[i].Y - f), (float)ROI.selectBoxSize, (float)ROI.selectBoxSize));
            }
            return selectBoxs.ToArray();
        }
        /// Create a new ROI object, add a point to it, and return it
        /// 
        /// @param ZCT a class that contains the Z, C, and T coordinates of the image.
        /// @param x x coordinate of the point
        /// @param y The y coordinate of the point
        /// 
        /// @return A new ROI object
        public static ROI CreatePoint(ZCT coord, double x, double y)
        {
            ROI an = new ROI();
            an.coord = coord;
            an.AddPoint(new PointD(x, y));
            an.type = Type.Point;
            return an;
        }
        /// Create a new ROI object, set its type to Line, add two points to it, and return it
        /// 
        /// @param ZCT Z is the Z-axis, C is the color channel, and T is the time frame.
        /// @param PointD X,Y
        /// @param PointD X,Y
        /// 
        /// @return A new ROI object.

        public static ROI CreateLine(ZCT coord, PointD x1, PointD x2)
        {
            ROI an = new ROI();
            an.coord = coord;
            an.type = Type.Line;
            an.AddPoint(x1);
            an.AddPoint(x2);
            return an;
        }
        /// Create a new ROI object with a rectangle shape, and add a line to the recorder
        /// 
        /// @param ZCT The ZCT coordinates of the image you want to create the ROI on.
        /// @param x x coordinate of the top left corner of the rectangle
        /// @param y y-coordinate of the top-left corner of the rectangle
        /// @param w width
        /// @param h height
        /// 
        /// @return A new ROI object.
        public static ROI CreateRectangle(ZCT coord, double x, double y, double w, double h)
        {
            ROI an = new ROI();
            an.coord = coord;
            an.type = Type.Rectangle;
            an.Rect = new RectangleD(x, y, w, h);
            return an;
        }
        /// Create an ellipse ROI at the specified ZCT coordinate with the specified width and height
        /// 
        /// @param ZCT The ZCT coordinates of the image you want to create the ROI on.
        /// @param x x-coordinate of the top-left corner of the rectangle
        /// @param y The y-coordinate of the upper-left corner of the rectangle to create.
        /// @param w width
        /// @param h height
        /// 
        /// @return A new ROI object.
        public static ROI CreateEllipse(ZCT coord, double x, double y, double w, double h)
        {
            ROI an = new ROI();
            an.coord = coord;
            an.type = Type.Ellipse;
            an.Rect = new RectangleD(x, y, w, h);
            return an;
        }
        /// > Create a new ROI object of type Polygon, with the given coordinate system and points
        /// 
        /// @param ZCT The ZCT coordinate of the ROI.
        /// @param pts an array of PointD objects, which are just a pair of doubles (x,y)
        /// 
        /// @return A ROI object
        public static ROI CreatePolygon(ZCT coord, PointD[] pts)
        {
            ROI an = new ROI();
            an.coord = coord;
            an.type = Type.Polygon;
            an.AddPoints(pts);
            an.closed = true;
            return an;
        }
        /// > Create a new ROI object of type Freeform, with the specified ZCT coordinate and points
        /// 
        /// @param ZCT A class that contains the Z, C, and T coordinates of the ROI.
        /// @param pts an array of PointD objects, which are just a pair of doubles (x,y)
        /// 
        /// @return A new ROI object.
        public static ROI CreateFreeform(ZCT coord, PointD[] pts)
        {
            ROI an = new ROI();
            an.coord = coord;
            an.type = Type.Freeform;
            an.AddPoints(pts);
            an.closed = true;
            return an;
        }
        public static ROI CreateMask(ZCT coord, float[] mask, int width, int height, PointD loc, double physicalX, double physicalY)
        {
            ROI an = new ROI();
            an.coord = coord;
            an.type = Type.Mask;
            an.roiMask = new Mask(mask, width, height, physicalX, physicalY, loc.X, loc.Y);
            an.AddPoint(loc);
            an.X = loc.X + (an.roiMask.X * an.roiMask.PhysicalSizeX);
            an.Y = loc.X + (an.roiMask.Y * an.roiMask.PhysicalSizeY);
            return an;
        }
        public static ROI CreateMask(ZCT coord, Byte[] mask, int width, int height, PointD loc, double physicalX, double physicalY)
        {
            ROI an = new ROI();
            an.coord = coord;
            an.type = Type.Mask;
            an.roiMask = new Mask(mask, width, height, physicalX, physicalY, loc.X, loc.Y);
            an.AddPoint(loc);
            an.X = loc.X + an.roiMask.X;
            an.Y = loc.X + an.roiMask.Y;
            return an;
        }
        // Calculate the center point of the ROI
        public PointD GetCenter()
        {
            return new PointD(Rect.X + (Rect.W / 2.0), Rect.Y + (Rect.H / 2.0));
        }

        // Calculate the distance to another point
        public double DistanceTo(PointD point)
        {
            var center = GetCenter();
            return (float)Math.Sqrt(Math.Pow(center.X - point.X, 2) + Math.Pow(center.Y - point.Y, 2));
        }
        /// This function updates the point at the specified index
        /// 
        /// @param PointD A class that contains an X and Y coordinate.
        /// @param i The index of the point to update
        public void UpdatePoint(PointD p, int i)
        {
            if (i < Points.Count)
            {
                Points[i] = p;
            }
            UpdateBoundingBox();
        }
        /// This function returns the point at the specified index
        /// 
        /// @param i The index of the point to get.
        /// 
        /// @return The point at index i in the Points array.
        public PointD GetPoint(int i)
        {
            return Points[i];
        }
        /// It returns an array of PointD objects
        /// 
        /// @return An array of PointD objects.
        public PointD[] GetPoints()
        {
            return Points.ToArray();
        }
        /// It converts a list of points to an array of points
        /// 
        /// @return A PointF array.
        public PointF[] GetPointsF()
        {
            PointF[] pfs = new PointF[Points.Count];
            for (int i = 0; i < Points.Count; i++)
            {
                pfs[i].X = (float)Points[i].X;
                pfs[i].Y = (float)Points[i].Y;
            }
            return pfs;
        }
        /// > Adds a point to the list of points and updates the bounding box
        /// 
        /// @param PointD 
        public void AddPoint(PointD p)
        {
            Points.Add(p);
            UpdateBoundingBox();
        }
        /// > Adds a range of points to the Points collection and updates the bounding box
        /// 
        /// @param p The points to add to the polygon
        public void AddPoints(PointD[] p)
        {
            Points.AddRange(p);
            UpdateBoundingBox();
        }
        /// > Adds a range of integer points to the Points collection and updates the bounding box
        /// 
        /// @param p The points to add to the polygon
        public void AddPoints(int[] xp, int[] yp)
        {
            for (int i = 0; i < xp.Length; i++)
            {
                Points.Add(new PointD(xp[i], yp[i]));
            }
            UpdateBoundingBox();
        }
        /// > Adds a range of float points to the Points collection and updates the bounding box
        /// 
        /// @param p The points to add to the polygon
        public void AddPoints(float[] xp, float[] yp)
        {
            for (int i = 0; i < xp.Length; i++)
            {
                Points.Add(new PointD(xp[i], yp[i]));
            }
            UpdateBoundingBox();
        }
        /// It removes points from a list of points based on an array of indexes
        /// 
        /// @param indexs an array of integers that represent the indexes of the points to be removed
        public void RemovePoints(int[] indexs)
        {
            List<PointD> inds = new List<PointD>();
            for (int i = 0; i < Points.Count; i++)
            {
                bool found = false;
                for (int ind = 0; ind < indexs.Length; ind++)
                {
                    if (indexs[ind] == i)
                        found = true;
                }
                if (!found)
                    inds.Add(Points[i]);
            }
            Points = inds;
            UpdateBoundingBox();
        }
        public void ClearPoints()
        {
            Points.Clear();
        }
        /// This function returns the number of points in the polygon
        /// 
        /// @return The number of points in the list.
        public int GetPointCount()
        {
            return Points.Count;
        }
        /// It takes a string of points and returns an array of points
        /// 
        /// @param s The string to convert to points.
        /// 
        /// @return A list of points.
        public PointD[] stringToPoints(string s)
        {
            List<PointD> pts = new List<PointD>();
            string[] ints = s.Split(' ');
            for (int i = 0; i < ints.Length; i++)
            {
                string[] sints;
                if (s.Contains("\t"))
                    sints = ints[i].Split('\t');
                else
                    sints = ints[i].Split(',');
                double x = double.Parse(sints[0], CultureInfo.InvariantCulture);
                double y = double.Parse(sints[1], CultureInfo.InvariantCulture);
                pts.Add(new PointD(x, y));
            }
            return pts.ToArray();
        }
        /// This function takes a BioImage object and returns a string of the points in the image space
        /// 
        /// @param BioImage The image that the ROI is on
        /// 
        /// @return The points of the polygon in the image space.
        public string PointsToString(BioImage b)
        {
            string pts = "";
            PointD[] ps = b.ToImageSpace(Points);
            for (int j = 0; j < ps.Length; j++)
            {
                if (j == ps.Length - 1)
                    pts += ps[j].X.ToString(CultureInfo.InvariantCulture) + "," + ps[j].Y.ToString(CultureInfo.InvariantCulture);
                else
                    pts += ps[j].X.ToString(CultureInfo.InvariantCulture) + "," + ps[j].Y.ToString(CultureInfo.InvariantCulture) + " ";
            }
            return pts;
        }
        /// It takes a list of points and returns a rectangle that contains all of the points
        /// 
        public void UpdateBoundingBox()
        {
            if (type == Type.Mask)
            {
                double minx = double.MaxValue;
                double miny = double.MaxValue;
                double maxx = double.MinValue;
                double maxy = double.MinValue;
                for (int y = 0; y < roiMask.Height; y++)
                {
                    for (int x = 0; x < roiMask.Width; x++)
                    {
                        if (roiMask.IsSelected(x, y))
                        {
                            if (minx > x)
                                minx = x;
                            if (miny > y)
                                miny = y;
                            if (maxx < x)
                                maxx = x;
                            if (maxy < y)
                                maxy = y;
                        }
                    }
                }
                BoundingBox = new RectangleD(PointsD[0].X + (minx * roiMask.PhysicalSizeX), PointsD[0].Y + (miny * roiMask.PhysicalSizeY),
                    (maxx - minx) * roiMask.PhysicalSizeX, (maxy - miny) * roiMask.PhysicalSizeY);
                return;
            }
            PointD min = new PointD(double.MaxValue, double.MaxValue);
            PointD max = new PointD(double.MinValue, double.MinValue);
            foreach (PointD p in Points)
            {
                if (min.X > p.X)
                    min.X = p.X;
                if (min.Y > p.Y)
                    min.Y = p.Y;

                if (max.X < p.X)
                    max.X = p.X;
                if (max.Y < p.Y)
                    max.Y = p.Y;
            }
            RectangleD r = new RectangleD();
            r.X = min.X;
            r.Y = min.Y;
            r.W = max.X - min.X;
            r.H = max.Y - min.Y;
            if (r.W == 0)
                r.W = 1;
            if (r.H == 0)
                r.H = 1;
            BoundingBox = r;
        }
        public void Dispose()
        {
            if (roiMask != null)
            {
                roiMask.Dispose();
            }
        }
    }

    public class Cell
    {
        public List<ROI> ROIs = new List<ROI>();

        public int Depth
        {
            get { return ROIs.Count; }
        }
        public VolumeD Volume
        {
            get; set;
        }
        public Cell(ROI first)
        {
            ROIs.Add(first);
        }
        public Cell FromROIs(ROI first, ROI[] rois)
        {
            if (first == null)
                throw new ArgumentNullException(nameof(first), "First ROI cannot be null.");

            if (rois == null || rois.Length == 0)
                throw new ArgumentNullException(nameof(rois), "ROIs array cannot be null or empty.");

            // Create the cell using the first ROI
            var cell = new Cell(first);

            // Maintain a set of unused ROIs
            var unusedROIs = new List<ROI>(rois);

            for (int i = 0; i < rois.Length; i++)
            {
                ROI r = rois[i];
                if (r.Rect.IntersectsWith(first.GetCenter()))
                {

                }
            }

            return cell;
        }
    }

    /* It's a class that holds a string, an IFilter, and a Type */
    public class Filt
    {
        /* Defining an enum. */
        public enum Type
        {
            Base = 0,
            Base2 = 1,
            InPlace = 2,
            InPlace2 = 3,
            InPlacePartial = 4,
            Resize = 5,
            Rotate = 6,
            Transformation = 7,
            Copy = 8
        }
        public string name;
        public IFilter filt;
        public Type type;
        public Filt(string s, IFilter f, Type t)
        {
            name = s;
            filt = f;
            type = t;
        }
    }
    public static class Filters
    {
        public static int[,] indexs;
        /// It takes a string as an argument and returns a filter object
        /// 
        /// @param name The name of the filter.
        /// 
        /// @return The value of the key "name" in the dictionary "filters"
        public static Filt GetFilter(string name)
        {
            foreach (Filt item in filters)
            {
                if (item.name == name)
                    return item;
            }
            return null;
        }
        /// It returns a filter from the filters dictionary, using the indexs array to find the index of
        /// the filter in the dictionary
        /// 
        /// @param type The type of filter you want to get.
        /// @param index The index of the filter in the list of filters.
        /// 
        /// @return The value of the filter at the index of the type and index.
        public static Filt GetFilter(int type, int index)
        {
            return filters[indexs[type, index]];
        }
        public static List<Filt> filters = new List<Filt>();
        /// It takes an image, applies a filter to it, and returns the filtered image
        /// 
        /// @param id The id of the image to apply the filter to.
        /// @param name The name of the filter.
        /// @param inPlace If true, the image will be modified in place. If false, a new image will be
        /// created.
        /// 
        /// @return The image that was filtered.
        public static BioImage Base(string id, string name, bool inPlace)
        {
            BioImage img = Images.GetImage(id);
            if (!inPlace)
                img = BioImage.Copy(img);
            try
            {
                Filt f = GetFilter(name);
                BaseFilter fi = (BaseFilter)f.filt;
                for (int i = 0; i < img.Buffers.Count; i++)
                {
                    Bitmap bm = fi.Apply(img.Buffers[i]);
                    img.Buffers[i] = bm;
                }
                if (!inPlace)
                {
                    Images.AddImage(img);
                }
            }
            catch (Exception e)
            {
                MessageDialog dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, e.Message);
                dialog.Show();
            }
            return img;
        }
        /// It takes two images, applies a filter to the first image, and then applies the second image
        /// to the first image
        /// 
        /// @param id the image id
        /// @param id2 The image to be filtered
        /// @param name The name of the filter.
        /// @param inPlace If true, the image will be modified in place. If false, a new image will be
        /// created.
        /// 
        /// @return The image that was filtered.
        public static BioImage Base2(string id, string id2, string name, bool inPlace)
        {
            BioImage c2 = Images.GetImage(id);
            BioImage img = Images.GetImage(id2);
            if (!inPlace)
                img = BioImage.Copy(img);
            try
            {
                Filt f = GetFilter(name);
                BaseFilter2 fi = (BaseFilter2)f.filt;
                for (int i = 0; i < img.Buffers.Count; i++)
                {
                    fi.OverlayImage = c2.Buffers[i];
                    img.Buffers[i] = fi.Apply(img.Buffers[i]);
                }
                if (!inPlace)
                {
                    Images.AddImage(img);
                }
                return img;
            }
            catch (Exception e)
            {
                MessageDialog dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, e.Message);
                dialog.Show();
            }
            return img;
        }
        /// It takes an image, applies a filter to it, and returns the image
        /// 
        /// @param id The id of the image to apply the filter to.
        /// @param name The name of the filter
        /// @param inPlace If true, the image will be modified in place. If false, a copy of the image
        /// will be created and modified.
        /// 
        /// @return The image that was passed in.
        public static BioImage InPlace(string id, string name, bool inPlace)
        {
            BioImage img = Images.GetImage(id);
            if (!inPlace)
                img = BioImage.Copy(img);
            try
            {
                Filt f = GetFilter(name);
                BaseInPlaceFilter fi = (BaseInPlaceFilter)f.filt;
                for (int i = 0; i < img.Buffers.Count; i++)
                {
                    img.Buffers[i] = fi.Apply((Bitmap)img.Buffers[i]);
                }
                if (!inPlace)
                {
                    Images.AddImage(img);
                }
                return img;
            }
            catch (Exception e)
            {
                MessageDialog dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, e.Message);
                dialog.Show();
            }
            return img;
        }
        /// This function takes an image, applies a filter to it, and returns the filtered image
        /// 
        /// @param id the image id
        /// @param id2 the image to be filtered
        /// @param name The name of the filter
        /// @param inPlace true or false
        /// 
        /// @return The image that was passed in.
        public static BioImage InPlace2(string id, string id2, string name, bool inPlace)
        {
            BioImage c2 = Images.GetImage(id);
            BioImage img = Images.GetImage(id2);
            if (!inPlace)
                img = BioImage.Copy(img);
            try
            {
                Filt f = GetFilter(name);
                BaseInPlaceFilter2 fi = (BaseInPlaceFilter2)f.filt;
                for (int i = 0; i < img.Buffers.Count; i++)
                {
                    fi.OverlayImage = c2.Buffers[i];
                    img.Buffers[i] = fi.Apply(img.Buffers[i]);
                }
                if (!inPlace)
                {
                    Images.AddImage(img);
                }
                return img;
            }
            catch (Exception e)
            {
                MessageDialog dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, e.Message);
                dialog.Show();
            }
            return img;
        }
        /// It takes an image, applies a filter to it, and returns the filtered image
        /// 
        /// @param id the id of the image to be filtered
        /// @param name The name of the filter
        /// @param inPlace If true, the image is modified in place. If false, a copy of the image is
        /// created and modified.
        /// 
        /// @return The image that was passed in.
        public static BioImage InPlacePartial(string id, string name, bool inPlace)
        {
            BioImage img = Images.GetImage(id);
            if (!inPlace)
                img = BioImage.Copy(img);
            try
            {
                Filt f = GetFilter(name);
                BaseInPlacePartialFilter fi = (BaseInPlacePartialFilter)f.filt;
                for (int i = 0; i < img.Buffers.Count; i++)
                {
                    img.Buffers[i] = fi.Apply(img.Buffers[i]);
                }
                if (!inPlace)
                {
                    Images.AddImage(img);
                }
                return img;
            }
            catch (Exception e)
            {
                MessageDialog dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, e.Message);
                dialog.Show();
            }
            return img;
        }
        /// This function takes an image, resizes it, and returns the resized image
        /// 
        /// @param id the id of the image to be resized
        /// @param name The name of the filter to use.
        /// @param inPlace If true, the image will be modified in place. If false, a new image will be
        /// created.
        /// @param w width
        /// @param h height
        /// 
        /// @return The image that was resized.
        public static BioImage Resize(string id, string name, bool inPlace, int w, int h)
        {
            BioImage img = Images.GetImage(id);
            if (!inPlace)
                img = BioImage.Copy(img);
            try
            {
                Filt f = GetFilter(name);
                BaseResizeFilter fi = (BaseResizeFilter)f.filt;
                fi.NewHeight = h;
                fi.NewWidth = w;
                for (int i = 0; i < img.Buffers.Count; i++)
                {
                    img.Buffers[i] = fi.Apply(img.Buffers[i]);
                }
                if (!inPlace)
                {
                    Images.AddImage(img);
                }
            }
            catch (Exception e)
            {
                MessageDialog dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, e.Message);
                dialog.Show();
            }
            return img;
        }
        /// It takes an image, rotates it, and returns the rotated image
        /// 
        /// @param id the id of the image to be rotated
        /// @param name The name of the filter
        /// @param inPlace whether to apply the filter to the original image or to a copy of it
        /// @param angle the angle to rotate the image
        /// @param a alpha
        /// @param r red
        /// @param g the image to be rotated
        /// @param b blue
        /// 
        /// @return The image that was rotated.
        public static BioImage Rotate(string id, string name, bool inPlace, float angle, int a, int r, int g, int b)
        {
            BioImage img = Images.GetImage(id);
            if (!inPlace)
                img = BioImage.Copy(Images.GetImage(id));
            try
            {
                Filt f = GetFilter(name);
                BaseRotateFilter fi = (BaseRotateFilter)f.filt;
                fi.Angle = angle;
                fi.FillColor = Color.FromArgb(a, r, g, b);
                for (int i = 0; i < img.Buffers.Count; i++)
                {
                    img.Buffers[i] = fi.Apply(img.Buffers[i]);
                }
                if (!inPlace)
                {
                    Images.AddImage(img);
                }
            }
            catch (Exception e)
            {
                MessageDialog dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, e.Message);
                dialog.Show();
            }
            return img;

        }
        /// This function takes an image, applies a transformation filter to it, and returns the
        /// transformed image
        /// 
        /// @param id the id of the image to be transformed
        /// @param name The name of the filter.
        /// @param inPlace If true, the image will be modified in place. If false, a new image will be
        /// created.
        /// @param angle the angle of rotation
        /// 
        /// @return The image that was transformed.
        public static BioImage Transformation(string id, string name, bool inPlace, float angle)
        {
            BioImage img = Images.GetImage(id);
            if (!inPlace)
                img = BioImage.Copy(img);
            try
            {
                Filt f = GetFilter(name);
                BaseTransformationFilter fi = (BaseTransformationFilter)f.filt;
                for (int i = 0; i < img.Buffers.Count; i++)
                {
                    img.Buffers[i] = fi.Apply(img.Buffers[i]);
                }
                if (!inPlace)
                {
                    Images.AddImage(img);
                }
            }
            catch (Exception e)
            {
                MessageDialog dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, e.Message);
                dialog.Show();
            }
            return img;
        }
        /// It takes an image, applies a filter to it, and returns the filtered image
        /// 
        /// @param id the id of the image to be filtered
        /// @param name The name of the filter to apply
        /// @param inPlace If true, the image will be modified in place. If false, a copy of the image
        /// will be created and the copy will be modified.
        /// 
        /// @return The image that was copied.
        public static BioImage Copy(string id, string name, bool inPlace)
        {
            BioImage img = Images.GetImage(id);
            if (!inPlace)
                img = BioImage.Copy(img);
            try
            {
                Filt f = GetFilter(name);
                BaseUsingCopyPartialFilter fi = (BaseUsingCopyPartialFilter)f.filt;
                for (int i = 0; i < img.Buffers.Count; i++)
                {
                    img.Buffers[i] = fi.Apply((Bitmap)img.Buffers[i]);
                }
                if (!inPlace)
                {
                    Images.AddImage(img);
                }
            }
            catch (Exception e)
            {
                MessageDialog dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, e.Message);
                dialog.Show();
            }
            return img;
        }
        /// It takes an image, crops it, and returns the cropped image
        /// 
        /// @param id the id of the image to crop
        /// @param x x coordinate of the top left corner of the rectangle
        /// @param y y-coordinate of the top-left corner of the rectangle
        /// @param w width
        /// @param h height
        /// 
        /// @return The cropped image.
        public static BioImage Crop(string id, double x, double y, double w, double h)
        {
            BioImage c = Images.GetImage(id);
            RectangleF r = c.ToImageSpace(new RectangleD(x, y, w, h));
            Rectangle rec = new Rectangle((int)r.X, (int)r.Y, (int)Math.Abs(r.Width), (int)Math.Abs(r.Height));
            BioImage img = BioImage.Copy(c, false);
            for (int i = 0; i < img.Buffers.Count; i++)
            {
                img.Buffers[i].Crop(rec);
            }
            BioImage.AutoThreshold(img, true);
            if (img.bitsPerPixel > 8)
                img.StackThreshold(true);
            else
                img.StackThreshold(false);
            return img;
        }
        /// This function takes a string and a rectangle and returns a BioImage
        /// 
        /// @param id the id of the image to crop
        /// @param RectangleD 
        /// 
        /// @return A BioImage object.
        public static BioImage Crop(string id, RectangleD r)
        {
            return Crop(id, r.X, r.Y, r.W, r.H);
        }

        /// It creates a dictionary of filters and their names
        public static void Init()
        {
            //Base Filters
            indexs = new int[9, 32];
            indexs[0, 0] = filters.Count;
            Filt f = new Filt("AdaptiveSmoothing", new AdaptiveSmoothing(), Filt.Type.Base);
            filters.Add(f);
            indexs[0, 1] = filters.Count;
            f = new Filt("BayerFilter", new BayerFilter(), Filt.Type.Base);
            filters.Add(f);
            indexs[0, 2] = filters.Count;
            f = new Filt("BayerFilterOptimized", new BayerFilterOptimized(), Filt.Type.Base);
            filters.Add(f);
            indexs[0, 3] = filters.Count;
            f = new Filt("BayerDithering", new BayerDithering(), Filt.Type.Base);
            filters.Add(f);
            indexs[0, 4] = filters.Count;
            f = new Filt("ConnectedComponentsLabeling", new ConnectedComponentsLabeling(), Filt.Type.Base);
            filters.Add(f);
            indexs[0, 5] = filters.Count;
            f = new Filt("ExtractChannel", new ExtractChannel(), Filt.Type.Base);
            filters.Add(f);
            indexs[0, 6] = filters.Count;
            f = new Filt("ExtractNormalizedRGBChannel", new ExtractNormalizedRGBChannel(), Filt.Type.Base);
            filters.Add(f);
            indexs[0, 7] = filters.Count;
            f = new Filt("Grayscale", new Grayscale(0.2125, 0.7154, 0.0721), Filt.Type.Base);
            filters.Add(f);
            //f = new Filt("TexturedFilter", new TexturedFilter());
            //filters.Add(f);
            indexs[0, 8] = filters.Count;
            f = new Filt("WaterWave", new WaterWave(), Filt.Type.Base);
            filters.Add(f);
            indexs[0, 9] = filters.Count;
            f = new Filt("YCbCrExtractChannel", new YCbCrExtractChannel(), Filt.Type.Base);
            filters.Add(f);
            indexs[0, 10] = filters.Count;
            //BaseFilter2

            f = new Filt("ThresholdedDifference", new ThresholdedDifference(), Filt.Type.Base2);
            filters.Add(f);
            indexs[1, 0] = filters.Count;
            f = new Filt("ThresholdedEuclideanDifference", new ThresholdedEuclideanDifference(), Filt.Type.Base2);
            filters.Add(f);
            indexs[1, 1] = filters.Count;

            //BaseInPlaceFilter
            indexs[2, 0] = filters.Count;
            f = new Filt("BackwardQuadrilateralTransformation", new BackwardQuadrilateralTransformation(), Filt.Type.InPlace);
            filters.Add(f);
            indexs[2, 1] = filters.Count;
            f = new Filt("BlobsFiltering", new BlobsFiltering(), Filt.Type.InPlace);
            filters.Add(f);
            indexs[2, 2] = filters.Count;
            f = new Filt("BottomHat", new BottomHat(), Filt.Type.InPlace);
            filters.Add(f);
            indexs[2, 3] = filters.Count;
            f = new Filt("BradleyLocalThresholding", new BradleyLocalThresholding(), Filt.Type.InPlace);
            filters.Add(f);
            indexs[2, 4] = filters.Count;
            f = new Filt("CanvasCrop", new CanvasCrop(new Rectangle(0, 0, 0, 0)), Filt.Type.InPlace);
            filters.Add(f);
            indexs[2, 5] = filters.Count;
            f = new Filt("CanvasFill", new CanvasFill(new Rectangle(0, 0, 0, 0)), Filt.Type.InPlace);
            filters.Add(f);
            indexs[2, 6] = filters.Count;
            f = new Filt("CanvasMove", new CanvasMove(new IntPoint()), Filt.Type.InPlace);
            filters.Add(f);
            indexs[2, 7] = filters.Count;
            f = new Filt("FillHoles", new FillHoles(), Filt.Type.InPlace);
            filters.Add(f);
            indexs[2, 8] = filters.Count;
            f = new Filt("FlatFieldCorrection", new FlatFieldCorrection(), Filt.Type.InPlace);
            filters.Add(f);
            indexs[2, 9] = filters.Count;
            f = new Filt("TopHat", new TopHat(), Filt.Type.InPlace);
            filters.Add(f);

            //BaseInPlaceFilter2
            indexs[3, 0] = filters.Count;
            f = new Filt("Add", new Add(), Filt.Type.InPlace2);
            filters.Add(f);
            indexs[3, 1] = filters.Count;
            f = new Filt("Difference", new Difference(), Filt.Type.InPlace2);
            filters.Add(f);
            indexs[3, 2] = filters.Count;
            f = new Filt("Intersect", new Intersect(), Filt.Type.InPlace2);
            filters.Add(f);
            indexs[3, 3] = filters.Count;
            f = new Filt("Merge", new Merge(), Filt.Type.InPlace2);
            filters.Add(f);
            indexs[3, 4] = filters.Count;
            f = new Filt("Morph", new Morph(), Filt.Type.InPlace2);
            filters.Add(f);
            indexs[3, 5] = filters.Count;
            f = new Filt("MoveTowards", new MoveTowards(), Filt.Type.InPlace2);
            filters.Add(f);
            indexs[3, 6] = filters.Count;
            f = new Filt("StereoAnaglyph", new StereoAnaglyph(), Filt.Type.InPlace2);
            filters.Add(f);
            indexs[3, 7] = filters.Count;
            f = new Filt("Subtract", new Subtract(), Filt.Type.InPlace2);
            filters.Add(f);
            //f = new Filt("Add", new TexturedMerge(), Filt.Type.InPlace2);
            //filters.Add(f);

            //BaseInPlacePartialFilter
            indexs[4, 0] = filters.Count;
            f = new Filt("AdditiveNoise", new AdditiveNoise(), Filt.Type.InPlacePartial);
            filters.Add(f);
            //f = new Filt("ApplyMask", new ApplyMask(), Filt.Type.InPlacePartial2);
            //filters.Add(f);
            indexs[4, 1] = filters.Count;
            f = new Filt("BrightnessCorrection", new BrightnessCorrection(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 2] = filters.Count;
            f = new Filt("ChannelFiltering", new ChannelFiltering(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 3] = filters.Count;
            f = new Filt("ColorFiltering", new ColorFiltering(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 4] = filters.Count;
            f = new Filt("ColorRemapping", new ColorRemapping(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 5] = filters.Count;
            f = new Filt("ContrastCorrection", new ContrastCorrection(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 6] = filters.Count;
            f = new Filt("ContrastStretch", new ContrastStretch(), Filt.Type.InPlacePartial);
            filters.Add(f);

            //f = new Filt("ErrorDiffusionDithering", new ErrorDiffusionDithering(), Filt.Type.InPlacePartial);
            //filters.Add(f);
            indexs[4, 7] = filters.Count;
            f = new Filt("EuclideanColorFiltering", new EuclideanColorFiltering(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 8] = filters.Count;
            f = new Filt("GammaCorrection", new GammaCorrection(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 9] = filters.Count;
            f = new Filt("HistogramEqualization", new HistogramEqualization(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 10] = filters.Count;
            f = new Filt("HorizontalRunLengthSmoothing", new HorizontalRunLengthSmoothing(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 11] = filters.Count;
            f = new Filt("HSLFiltering", new HSLFiltering(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 12] = filters.Count;
            f = new Filt("HueModifier", new HueModifier(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 13] = filters.Count;
            f = new Filt("Invert", new Invert(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 14] = filters.Count;
            f = new Filt("LevelsLinear", new LevelsLinear(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 15] = filters.Count;
            f = new Filt("LevelsLinear16bpp", new LevelsLinear16bpp(), Filt.Type.InPlacePartial);
            filters.Add(f);
            //indexs[4, 16] = 46;
            //f = new Filt("MaskedFilter", new MaskedFilter(), Filt.Type.InPlacePartial);
            //filters.Add(f);
            //f = new Filt("Mirror", new Mirror(), Filt.Type.InPlacePartial);
            //filters.Add(f);
            indexs[4, 16] = filters.Count;
            f = new Filt("OrderedDithering", new OrderedDithering(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 17] = filters.Count;
            f = new Filt("OtsuThreshold", new OtsuThreshold(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 19] = filters.Count;
            f = new Filt("Pixellate", new Pixellate(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 20] = filters.Count;
            f = new Filt("PointedColorFloodFill", new PointedColorFloodFill(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 21] = filters.Count;
            f = new Filt("PointedMeanFloodFill", new PointedMeanFloodFill(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 22] = filters.Count;
            f = new Filt("ReplaceChannel", new Invert(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 23] = filters.Count;
            f = new Filt("RotateChannels", new LevelsLinear(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 24] = filters.Count;
            f = new Filt("SaltAndPepperNoise", new LevelsLinear16bpp(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 25] = filters.Count;
            f = new Filt("SaturationCorrection", new SaturationCorrection(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 26] = filters.Count;
            f = new Filt("Sepia", new Sepia(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 26] = filters.Count;
            f = new Filt("SimplePosterization", new SimplePosterization(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 27] = filters.Count;
            f = new Filt("SISThreshold", new SISThreshold(), Filt.Type.InPlacePartial);
            filters.Add(f);
            //f = new Filt("Texturer", new Texturer(), Filt.Type.InPlacePartial);
            //filters.Add(f);
            //f = new Filt("Threshold", new Threshold(), Filt.Type.InPlacePartial);
            //filters.Add(f);
            indexs[4, 28] = filters.Count;
            f = new Filt("ThresholdWithCarry", new ThresholdWithCarry(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 29] = filters.Count;
            f = new Filt("VerticalRunLengthSmoothing", new VerticalRunLengthSmoothing(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 30] = filters.Count;
            f = new Filt("YCbCrFiltering", new YCbCrFiltering(), Filt.Type.InPlacePartial);
            filters.Add(f);
            indexs[4, 31] = filters.Count;
            f = new Filt("YCbCrLinear", new YCbCrLinear(), Filt.Type.InPlacePartial);
            filters.Add(f);
            //f = new Filt("YCbCrReplaceChannel", new YCbCrReplaceChannel(), Filt.Type.InPlacePartial);
            //filters.Add(f);

            //BaseResizeFilter
            indexs[5, 0] = filters.Count;
            f = new Filt("ResizeBicubic", new ResizeBicubic(0, 0), Filt.Type.Resize);
            filters.Add(f);
            indexs[5, 1] = filters.Count;
            f = new Filt("ResizeBilinear", new ResizeBilinear(0, 0), Filt.Type.Resize);
            filters.Add(f);
            indexs[5, 2] = filters.Count;
            f = new Filt("ResizeNearestNeighbor", new ResizeNearestNeighbor(0, 0), Filt.Type.Resize);
            filters.Add(f);

            //BaseRotateFilter
            indexs[6, 0] = filters.Count;
            f = new Filt("RotateBicubic", new RotateBicubic(0), Filt.Type.Rotate);
            filters.Add(f);
            indexs[6, 1] = filters.Count;
            f = new Filt("RotateBilinear", new RotateBilinear(0), Filt.Type.Rotate);
            filters.Add(f);
            indexs[6, 2] = filters.Count;
            f = new Filt("RotateNearestNeighbor", new RotateNearestNeighbor(0), Filt.Type.Rotate);
            filters.Add(f);

            //Transformation
            indexs[7, 0] = filters.Count;
            f = new Filt("Crop", new Crop(new Rectangle(0, 0, 0, 0)), Filt.Type.Transformation);
            filters.Add(f);
            indexs[7, 1] = filters.Count;
            f = new Filt("QuadrilateralTransformation", new QuadrilateralTransformation(), Filt.Type.Transformation);
            filters.Add(f);
            //f = new Filt("QuadrilateralTransformationBilinear", new QuadrilateralTransformationBilinear(), Filt.Type.Transformation);
            //filters.Add(f);
            //f = new Filt("QuadrilateralTransformationNearestNeighbor", new QuadrilateralTransformationNearestNeighbor(), Filt.Type.Transformation);
            //filters.Add(f);
            indexs[7, 2] = filters.Count;
            f = new Filt("Shrink", new Shrink(), Filt.Type.Transformation);
            filters.Add(f);
            indexs[7, 3] = filters.Count;
            f = new Filt("SimpleQuadrilateralTransformation", new SimpleQuadrilateralTransformation(), Filt.Type.Transformation);
            filters.Add(f);
            indexs[7, 4] = filters.Count;
            f = new Filt("TransformFromPolar", new TransformFromPolar(), Filt.Type.Transformation);
            filters.Add(f);
            indexs[7, 5] = filters.Count;
            f = new Filt("TransformToPolar", new TransformToPolar(), Filt.Type.Transformation);
            filters.Add(f);

            //BaseUsingCopyPartialFilter
            indexs[8, 0] = filters.Count;
            f = new Filt("BinaryDilatation3x3", new BinaryDilatation3x3(), Filt.Type.Copy);
            filters.Add(f);
            indexs[8, 1] = filters.Count;
            f = new Filt("BilateralSmoothing ", new BilateralSmoothing(), Filt.Type.Copy);
            filters.Add(f);
            indexs[8, 2] = filters.Count;
            f = new Filt("BinaryErosion3x3 ", new BinaryErosion3x3(), Filt.Type.Copy);
            filters.Add(f);

        }
    }
    public class ImageInfo
    {
        bool HasPhysicalXY = false;
        bool HasPhysicalXYZ = false;
        private double physicalSizeX = -1;
        private double physicalSizeY = -1;
        private double physicalSizeZ = -1;
        public double PhysicalSizeX
        {
            get { return physicalSizeX; }
            set
            {
                physicalSizeX = value;
                HasPhysicalXY = true;
            }
        }
        public double PhysicalSizeY
        {
            get { return physicalSizeY; }
            set
            {
                physicalSizeY = value;
                HasPhysicalXY = true;
            }
        }
        public double PhysicalSizeZ
        {
            get { return physicalSizeZ; }
            set
            {
                physicalSizeZ = value;
                HasPhysicalXYZ = true;
            }
        }

        bool HasStageXY = false;
        bool HasStageXYZ = false;
        public double stageSizeX = -1;
        public double stageSizeY = -1;
        public double stageSizeZ = -1;
        public double StageSizeX
        {
            get { return stageSizeX; }
            set
            {
                stageSizeX = value;
                HasStageXY = true;
            }
        }
        public double StageSizeY
        {
            get { return stageSizeY; }
            set
            {
                stageSizeY = value;
                HasStageXY = true;
            }
        }
        public double StageSizeZ
        {
            get { return stageSizeZ; }
            set
            {
                stageSizeZ = value;
                HasStageXYZ = true;
            }
        }

        private int series = 0;
        public int Series
        {
            get { return series; }
            set { series = value; }
        }

        /// Copy() is a function that copies the values of the ImageInfo class to a new ImageInfo class
        /// 
        /// @return A copy of the ImageInfo object.
        public ImageInfo Copy()
        {
            ImageInfo inf = new ImageInfo();
            inf.PhysicalSizeX = PhysicalSizeX;
            inf.PhysicalSizeY = PhysicalSizeY;
            inf.PhysicalSizeZ = PhysicalSizeZ;
            inf.StageSizeX = StageSizeX;
            inf.StageSizeY = StageSizeY;
            inf.StageSizeZ = StageSizeZ;
            inf.HasPhysicalXY = HasPhysicalXY;
            inf.HasPhysicalXYZ = HasPhysicalXYZ;
            inf.StageSizeX = StageSizeX;
            inf.StageSizeY = StageSizeY;
            inf.StageSizeZ = StageSizeZ;
            inf.HasStageXY = HasStageXY;
            inf.HasStageXYZ = HasStageXYZ;
            return inf;
        }

    }

    public class NumPy
    {
        public enum NpyDataType
        {
            // Integer types
            UInt8 = 1,      // |u1
            Int8 = 2,       // |i1
            UInt16 = 3,     // |u2
            Int16 = 4,      // |i2
            UInt32 = 5,     // |u4
            Int32 = 6,      // |i4
            UInt64 = 7,     // |u8
            Int64 = 8,      // |i8

            // Floating point types
            Float16 = 9,    // |f2
            Float32 = 10,   // |f4
            Float64 = 11,   // |f8

            // Complex number types (optional depending on your use case)
            Complex64 = 12, // |c8
            Complex128 = 13 // |c16
        }
        public static string GetNpyTypeString(NpyDataType type)
        {
            switch (type)
            {
                case NpyDataType.UInt8:
                    return "|u1";
                case NpyDataType.Int8:
                    return "|i1";
                case NpyDataType.UInt16:
                    return "|u2";
                case NpyDataType.Int16:
                    return "|i2";
                case NpyDataType.UInt32:
                    return "|u4";
                case NpyDataType.Int32:
                    return "|i4";
                case NpyDataType.UInt64:
                    return "|u8";
                case NpyDataType.Int64:
                    return "|i8";
                case NpyDataType.Float16:
                    return "|f2";
                case NpyDataType.Float32:
                    return "|f4";
                case NpyDataType.Float64:
                    return "|f8";
                case NpyDataType.Complex64:
                    return "|c8";
                case NpyDataType.Complex128:
                    return "|c16";
                default:
                    throw new ArgumentException("Unsupported NpyDataType", nameof(type));
            }
        }
        public static NpyDataType GetNpyTypeEnum(string type)
        {
            switch (type)
            {
                case "|u1":
                    return NpyDataType.UInt8;
                case "|i1":
                    return NpyDataType.Int8;
                case "|u2":
                    return NpyDataType.UInt16;
                case "|i2":
                    return NpyDataType.Int16;
                case "|u4":
                    return NpyDataType.UInt32;
                case "|i4":
                    return NpyDataType.Int32;
                case "|u8":
                    return NpyDataType.UInt64;
                case "|i8":
                    return NpyDataType.Int64;
                case "|f2":
                    return NpyDataType.Float16;
                case "|f4":
                    return NpyDataType.Float32;
                case "|f8":
                    return NpyDataType.Float64;
                case "|c8":
                    return NpyDataType.Complex64;
                case "|c16":
                    return NpyDataType.Complex128;
                case "<f4":
                    return NpyDataType.Float32;
                case "<i4":
                    return NpyDataType.Int32;
                default:
                    throw new ArgumentException("Unsupported numpy type string", nameof(type));
            }
        }
        public class NpyHeader
        {
            // Data type descriptor (|u1 in numpy means unsigned 1-byte integer)
            public string descr { get; set; }

            // Fortran order flag
            public bool fortran_order { get; set; }

            // Shape of the array (a tuple or array of integers)
            public int[] shape { get; set; }
        }
        public static void SaveNumPy(BioImage b, string file)
        {
            float[] fs = new float[b.SizeT * b.SizeZ * b.SizeC * b.SizeX * b.SizeY];
            // Fill the flattened array
            int index = 0;
            for (int t = 0; t < b.SizeT; t++)
            {
                for (int z = 0; z < b.SizeZ; z++)
                {
                    for (int c = 0; c < b.SizeC; c++)
                    {
                        for (int y = 0; y < b.SizeY; y++)
                        {
                            for (int x = 0; x < b.SizeX; x++)
                            {
                                fs[index++] = b.Buffers[b.Coords[z, c, t]].GetValue(x, y);
                            }
                        }
                    }
                }
            }
            SaveFloatArrayAsNpy(file, fs, new int[] { b.SizeT, b.SizeC, b.SizeZ, b.SizeX, b.SizeY });
        }
        private static void SaveFloatArrayAsNpy(string filePath, float[] array, int[] shape)
        {
            using (var writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
            {
                // Write the NPY file header
                WriteNpyHeader(writer, shape);

                // Write the data
                foreach (var value in array)
                {
                    writer.Write(value);
                }
            }
        }
        private static void WriteNpyHeader(BinaryWriter writer, int[] shape)
        {
            // Magic string for NPY format
            byte[] magicString = { 0x93, (byte)'N', (byte)'U', (byte)'M', (byte)'P', (byte)'Y' };
            writer.Write(magicString);

            // NPY version
            writer.Write((byte)1); // Major version
            writer.Write((byte)0); // Minor version

            // Header dictionary
            string shapeString = string.Join(", ", shape) + (shape.Length == 1 ? "," : ""); // Trailing comma for single-dim arrays
            string header = string.Format(
                "{{'descr': '<f4', 'fortran_order': False, 'shape': ({0}), }}",
                shapeString
            ).PadRight(64); // Ensure header is 64-byte aligned for simplicity

            byte[] headerBytes = System.Text.Encoding.ASCII.GetBytes(header);

            // Length of the header
            ushort headerLength = (ushort)((headerBytes.Length + 63) & ~63); // Align to 64 bytes
            writer.Write(headerLength);

            // Write the header
            writer.Write(headerBytes);

            // Pad with spaces to align the header to 64 bytes
            for (int i = headerBytes.Length; i < headerLength; i++)
            {
                writer.Write((byte)' ');
            }
        }

        /// <summary>
        /// Converts a flat array into a multi-dimensional array based on the given shape.
        /// </summary>
        /// <typeparam name="T">Type of the elements in the array.</typeparam>
        /// <typeparam name="TArray">Type of the target multi-dimensional array.</typeparam>
        /// <param name="flatArray">The flat array containing the data.</param>
        /// <param name="shape">The shape of the target multi-dimensional array.</param>
        /// <returns>The multi-dimensional array.</returns>
        public static TArray ConvertToMultidimensional<T, TArray>(Array flatArray, int[] shape)
        {
            if (flatArray.Length != shape.Aggregate(1, (a, b) => a * b))
                throw new ArgumentException("The shape does not match the length of the flat array.");

            var result = (Array)Array.CreateInstance(typeof(T), shape);

            int[] indices = new int[shape.Length];
            for (int i = 0; i < flatArray.Length; i++)
            {
                // Compute the multi-dimensional indices
                int offset = i;
                for (int j = shape.Length - 1; j >= 0; j--)
                {
                    indices[j] = offset % shape[j];
                    offset /= shape[j];
                }

                // Set the value in the multi-dimensional array
                result.SetValue(flatArray.GetValue(i), indices);
            }

            return (TArray)(object)result;
        }
        static T ReadValue<T>(BinaryReader reader) where T : struct
        {
            if (typeof(T) == typeof(float))
                return (T)(object)reader.ReadSingle();
            if (typeof(T) == typeof(double))
                return (T)(object)reader.ReadDouble();
            if (typeof(T) == typeof(int))
                return (T)(object)reader.ReadInt32();
            if (typeof(T) == typeof(short))
                return (T)(object)reader.ReadInt16();
            if (typeof(T) == typeof(byte))
                return (T)(object)reader.ReadByte();

            throw new NotSupportedException($"Type {typeof(T)} is not supported.");
        }
        public static (int[], NpyDataType, Array) ReadNpyFile(string filePath)
        {
            using (var reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
            {
                // Verify .npy magic string
                byte[] magic = reader.ReadBytes(6);
                string magicString = Encoding.ASCII.GetString(magic);
                if (magicString != "?NUMPY")
                {
                    throw new InvalidOperationException("Invalid .npy file.");
                }

                // Skip the version information (2 bytes)
                reader.BaseStream.Seek(2, SeekOrigin.Current);

                // Read the header length (2 bytes)
                byte[] headerLengthBytes = reader.ReadBytes(2);
                int headerLength = BitConverter.ToInt16(headerLengthBytes, 0);

                // Read the header bytes
                byte[] headerBytes = reader.ReadBytes(headerLength);
                string headerString = Encoding.ASCII.GetString(headerBytes).Replace("True", "true").Replace("False", "false").Replace("(", "[").Replace(")", "]");

                // Configure Json.NET to handle camel case
                var settings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };

                // Deserialize the JSON string into an object of type NpyHeader
                NpyHeader header = JsonConvert.DeserializeObject<NpyHeader>(headerString, settings);
                var shape = header.shape;
                var dtype = header.descr;
                NpyDataType type = GetNpyTypeEnum(dtype);

                // Calculate the total number of elements in the array
                int totalElements = shape.Aggregate(1, (acc, dim) => acc * dim); // Multiply all dimensions

                // Calculate the data size based on the number of elements and element size
                int elementSize = GetElementSize(type);
                int dataSize = totalElements * elementSize;

                // Read the data bytes
                byte[] dataBytes = reader.ReadBytes(dataSize);

                // Convert the byte array to the proper data type array
                Array data = null;
                switch (type)
                {
                    case NpyDataType.UInt8:
                        data = new byte[dataBytes.Length];
                        Buffer.BlockCopy(dataBytes, 0, data, 0, dataBytes.Length);
                        break;

                    case NpyDataType.Int8:
                        data = new sbyte[dataBytes.Length];
                        Buffer.BlockCopy(dataBytes, 0, data, 0, dataBytes.Length);
                        break;

                    case NpyDataType.UInt16:
                        data = new ushort[totalElements]; // Using totalElements for multidimensional support
                        for (int i = 0; i < data.Length; i++)
                        {
                            data.SetValue(BitConverter.ToUInt16(dataBytes, i * 2), i);
                        }
                        break;

                    case NpyDataType.Int16:
                        data = new short[totalElements];
                        for (int i = 0; i < data.Length; i++)
                        {
                            data.SetValue(BitConverter.ToInt16(dataBytes, i * 2), i);
                        }
                        break;

                    case NpyDataType.UInt32:
                        data = new uint[totalElements];
                        for (int i = 0; i < data.Length; i++)
                        {
                            data.SetValue(BitConverter.ToUInt32(dataBytes, i * 4), i);
                        }
                        break;

                    case NpyDataType.Int32:
                        data = new int[totalElements];
                        for (int i = 0; i < data.Length; i++)
                        {
                            data.SetValue(BitConverter.ToInt32(dataBytes, i * 4), i);
                        }
                        break;

                    case NpyDataType.UInt64:
                        data = new ulong[totalElements];
                        for (int i = 0; i < data.Length; i++)
                        {
                            data.SetValue(BitConverter.ToUInt64(dataBytes, i * 8), i);
                        }
                        break;

                    case NpyDataType.Int64:
                        data = new long[totalElements];
                        for (int i = 0; i < data.Length; i++)
                        {
                            data.SetValue(BitConverter.ToInt64(dataBytes, i * 8), i);
                        }
                        break;

                    case NpyDataType.Float16:
                        // Convert each 2-byte segment to Float16 (not directly supported by BitConverter)
                        // You'll need a custom method to handle Float16 conversion
                        break;

                    case NpyDataType.Float32:
                        data = new float[totalElements];
                        for (int i = 0; i < data.Length; i++)
                        {
                            data.SetValue(BitConverter.ToSingle(dataBytes, i * 4), i);
                        }
                        break;

                    case NpyDataType.Float64:
                        data = new double[totalElements];
                        for (int i = 0; i < data.Length; i++)
                        {
                            data.SetValue(BitConverter.ToDouble(dataBytes, i * 8), i);
                        }
                        break;

                    case NpyDataType.Complex64:
                        data = new (float, float)[totalElements];
                        for (int i = 0; i < data.Length; i++)
                        {
                            var real = BitConverter.ToSingle(dataBytes, i * 8);
                            var imaginary = BitConverter.ToSingle(dataBytes, i * 8 + 4);
                            data.SetValue((real, imaginary), i);
                        }
                        break;

                    case NpyDataType.Complex128:
                        data = new (double, double)[totalElements];
                        for (int i = 0; i < data.Length; i++)
                        {
                            var real = BitConverter.ToDouble(dataBytes, i * 16);
                            var imaginary = BitConverter.ToDouble(dataBytes, i * 16 + 8);
                            data.SetValue((real, imaginary), i);
                        }
                        break;

                    default:
                        throw new ArgumentException("Unsupported NpyDataType", nameof(type));
                }

                return (shape, type, data);
            }
        }

        /// <summary>
        /// Helper function to get the size of the elements based on dtype
        /// </summary>
        /// <param name="dtype"></param>
        /// <returns></returns>
        public static int GetElementSize(NpyDataType type)
        {
            switch (type)
            {
                case NpyDataType.UInt8:
                case NpyDataType.Int8:
                    return 1; // 1 byte for UInt8 and Int8

                case NpyDataType.UInt16:
                case NpyDataType.Int16:
                    return 2; // 2 bytes for UInt16 and Int16

                case NpyDataType.UInt32:
                case NpyDataType.Int32:
                    return 4; // 4 bytes for UInt32 and Int32

                case NpyDataType.UInt64:
                case NpyDataType.Int64:
                    return 8; // 8 bytes for UInt64 and Int64

                case NpyDataType.Float16:
                    return 2; // 2 bytes for Float16

                case NpyDataType.Float32:
                    return 4; // 4 bytes for Float32

                case NpyDataType.Float64:
                    return 8; // 8 bytes for Float64

                case NpyDataType.Complex64:
                    return 8; // 8 bytes for Complex64 (2 * Float32)

                case NpyDataType.Complex128:
                    return 16; // 16 bytes for Complex128 (2 * Float64)

                default:
                    throw new ArgumentException("Unsupported NpyDataType", nameof(type));
            }
        }

        public static T ConvertJaggedArray<T>(Array jaggedArray)
        {
            // Ensure the provided array is of the correct jagged array type
            if (jaggedArray is int[][] jagged)
            {
                int rows = jagged.Length;
                int cols = jagged[0].Length;

                // Create an instance of the desired 2D array using reflection
                T result = (T)Activator.CreateInstance(typeof(T), new object[] { rows, cols });

                // Copy values from jagged array to the newly created 2D array
                for (int i = 0; i < rows; i++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        // Use reflection to set the value in the result array
                        var element = result.GetType().GetElementType();
                        result.GetType().GetMethod("SetValue").Invoke(result, new object[] { jagged[i][j], i, j });
                    }
                }

                return result;
            }

            throw new InvalidCastException("The input array is not of the expected type.");
        }

        public static T CreateJaggedArray<T>(int[] shape, T[] data, int dimension) where T : struct
        {
            // Base case: if we reached the last dimension, return the data array for that dimension
            if (dimension == shape.Length - 1)
            {
                // Create a 2D array for the last dimension
                T[,] array = (T[,])Activator.CreateInstance(typeof(T[,]), shape[dimension], data.Length / shape[dimension]);
                Array.Copy(data, array, data.Length); // Copy data into the array
                return (T)(object)array; // Cast to T (we will handle this later)
            }

            // Recursive case: create jagged array for higher dimensions
            int subDimensionSize = shape[dimension + 1];
            int firstDimLength = shape[dimension];

            // Create a jagged array for the current dimension
            Array jaggedArray = Array.CreateInstance(typeof(Array), shape[dimension]);

            int index = 0;
            for (int i = 0; i < firstDimLength; i++)
            {
                // Recursively create the sub-arrays
                var subArray = CreateJaggedArray(shape, data.Skip(index).Take(subDimensionSize).ToArray(), dimension + 1);
                jaggedArray.SetValue(subArray, i); // Set the sub-array at index
                index += subDimensionSize;
            }

            // Return the jagged array
            return (T)Convert.ChangeType(jaggedArray, typeof(T)); // Use Convert.ChangeType to cast System.Array to T
        }

        static string GetHeader(Array array)
        {
            // Get the shape of the array
            string shape = "(" + string.Join(",", array.GetDimensions().Select(dim => dim.ToString())) + ")";

            // Get the data type (assuming float32)
            string dtype = "|<f4"; // For little-endian float32 (numpy standard)

            // Construct the header
            string header = "{'descr': '" + dtype + "', 'fortran_order': False, 'shape': " + shape + "}";

            // Pad header length to be even
            int headerLength = header.Length + 1;
            if (headerLength % 2 != 0)
            {
                header += " ";
            }

            return header;
        }

        static void WriteArrayData(BinaryWriter writer, Array array)
        {
            // Flatten the array to 1D
            var flattenedArray = array.Cast<float>().ToArray();

            // Write the data
            foreach (var value in flattenedArray)
            {
                writer.Write(value);
            }
        }

        // Helper method to convert byte[] to the corresponding typed array (e.g., float[], int[], etc.)
        public static Array ConvertBytesToTypedArray(byte[] data, NpyDataType dataType)
        {
            switch (dataType)
            {
                case NpyDataType.UInt8:
                    return ConvertByteArrayToUInt8(data);
                case NpyDataType.Int8:
                    return ConvertByteArrayToInt8(data);
                case NpyDataType.UInt16:
                    return ConvertByteArrayToUInt16(data);
                case NpyDataType.Int16:
                    return ConvertByteArrayToInt16(data);
                case NpyDataType.UInt32:
                    return ConvertByteArrayToUInt32(data);
                case NpyDataType.Int32:
                    return ConvertByteArrayToInt32(data);
                case NpyDataType.UInt64:
                    return ConvertByteArrayToUInt64(data);
                case NpyDataType.Int64:
                    return ConvertByteArrayToInt64(data);
                case NpyDataType.Float16:
                    return ConvertByteArrayToFloat16(data);
                case NpyDataType.Float32:
                    return ConvertByteArrayToFloat32(data);
                case NpyDataType.Float64:
                    return ConvertByteArrayToFloat64(data);
                case NpyDataType.Complex64:
                    return ConvertByteArrayToComplex64(data);
                case NpyDataType.Complex128:
                    return ConvertByteArrayToComplex128(data);
                default:
                    throw new ArgumentException("Unsupported data type", nameof(dataType));
            }
        }

        // Conversion methods for each type

        private static byte[] ConvertByteArrayToUInt8(byte[] data)
        {
            return data;
        }

        private static sbyte[] ConvertByteArrayToInt8(byte[] data)
        {
            int length = data.Length;
            sbyte[] result = new sbyte[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = (sbyte)data[i];
            }
            return result;
        }

        private static ushort[] ConvertByteArrayToUInt16(byte[] data)
        {
            int length = data.Length / sizeof(ushort);
            ushort[] result = new ushort[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = BitConverter.ToUInt16(data, i * sizeof(ushort));
            }
            return result;
        }

        private static short[] ConvertByteArrayToInt16(byte[] data)
        {
            int length = data.Length / sizeof(short);
            short[] result = new short[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = BitConverter.ToInt16(data, i * sizeof(short));
            }
            return result;
        }

        private static uint[] ConvertByteArrayToUInt32(byte[] data)
        {
            int length = data.Length / sizeof(uint);
            uint[] result = new uint[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = BitConverter.ToUInt32(data, i * sizeof(uint));
            }
            return result;
        }

        private static int[] ConvertByteArrayToInt32(byte[] data)
        {
            int length = data.Length / sizeof(int);
            int[] result = new int[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = BitConverter.ToInt32(data, i * sizeof(int));
            }
            return result;
        }

        private static ulong[] ConvertByteArrayToUInt64(byte[] data)
        {
            int length = data.Length / sizeof(ulong);
            ulong[] result = new ulong[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = BitConverter.ToUInt64(data, i * sizeof(ulong));
            }
            return result;
        }

        private static long[] ConvertByteArrayToInt64(byte[] data)
        {
            int length = data.Length / sizeof(long);
            long[] result = new long[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = BitConverter.ToInt64(data, i * sizeof(long));
            }
            return result;
        }

        private static float[] ConvertByteArrayToFloat16(byte[] data)
        {
            // For Float16, .NET does not natively support conversion, so you might need a custom implementation
            // or use an external library to handle Float16 to Float32 conversion.
            // Here's a placeholder for that implementation:
            throw new NotImplementedException("Float16 is not natively supported in .NET. You need to convert it to Float32.");
        }

        private static float[] ConvertByteArrayToFloat32(byte[] data)
        {
            int length = data.Length / sizeof(float);
            float[] result = new float[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = BitConverter.ToSingle(data, i * sizeof(float));
            }
            return result;
        }

        private static double[] ConvertByteArrayToFloat64(byte[] data)
        {
            int length = data.Length / sizeof(double);
            double[] result = new double[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = BitConverter.ToDouble(data, i * sizeof(double));
            }
            return result;
        }

        private static (float, float)[] ConvertByteArrayToComplex64(byte[] data)
        {
            int length = data.Length / sizeof(float) / 2;
            (float, float)[] result = new (float, float)[length];
            for (int i = 0; i < length; i++)
            {
                float real = BitConverter.ToSingle(data, i * sizeof(float) * 2);
                float imaginary = BitConverter.ToSingle(data, i * sizeof(float) * 2 + sizeof(float));
                result[i] = (real, imaginary);
            }
            return result;
        }

        private static (double, double)[] ConvertByteArrayToComplex128(byte[] data)
        {
            int length = data.Length / sizeof(double) / 2;
            (double, double)[] result = new (double, double)[length];
            for (int i = 0; i < length; i++)
            {
                double real = BitConverter.ToDouble(data, i * sizeof(double) * 2);
                double imaginary = BitConverter.ToDouble(data, i * sizeof(double) * 2 + sizeof(double));
                result[i] = (real, imaginary);
            }
            return result;
        }
    }
    public static class ArrayExtensions
    {
        // Extension method to get the dimensions of an array
        public static int[] GetDimensions(this Array array)
        {
            int rank = array.Rank;
            int[] dimensions = new int[rank];
            for (int i = 0; i < rank; i++)
            {
                dimensions[i] = array.GetLength(i);
            }
            return dimensions;
        }
    }

    public class BioImage : IDisposable
    {
        public class WellPlate
        {
            public class Well
            {
                public string ID { get; set; }
                public int Index { get; set; }
                public int Column { get; set; }
                public int Row { get; set; }
                public System.Drawing.Color Color { get; set; }
                public List<Sample> Samples = new List<Sample>();
                public class Sample
                {
                    public string ID { get; set; }
                    public int Index { get; set; }
                    public PointD Position { get; set; }
                    public Timestamp Time { get; set; }
                }
            }
            public List<Well> Wells = new List<Well>();

            public PointD Origin;
            public string ID;
            public string Name;
            public WellPlate(BioImage b)
            {
                ID = b.meta.getPlateID(b.series);
                Name = b.meta.getPlateName(b.series);
                double x, y;
                if (b.meta.getPlateWellOriginX(b.series) != null)
                    x = b.meta.getPlateWellOriginX(b.series).value().doubleValue();
                else
                    x = 0;
                if (b.meta.getPlateWellOriginY(b.series) != null)
                    y = b.meta.getPlateWellOriginY(b.series).value().doubleValue();
                else
                    y = 0;
                Origin = new PointD(x, y);
                int ws = b.meta.getWellCount(b.series);
                for (int i = 0; i < ws; i++)
                {
                    Well w = new Well();
                    w.ID = b.meta.getWellID(b.series, i);
                    w.Column = b.meta.getWellColumn(b.series, i).getNumberValue().intValue();
                    w.Row = b.meta.getWellRow(b.series, i).getNumberValue().intValue();
                    int wsc = b.meta.getWellSampleCount(b.series, i);
                    for (int s = 0; s < wsc; s++)
                    {
                        Well.Sample sa = new Well.Sample();
                        sa.Time = b.meta.getWellSampleTimepoint(b.series, i, s);
                        sa.Index = b.meta.getWellSampleIndex(b.series, i, s).getNumberValue().intValue();
                        double sx, sy;
                        if (b.meta.getWellSamplePositionX(b.series, i, s) != null)
                            sx = b.meta.getWellSamplePositionX(b.series, i, s).value().doubleValue();
                        else sx = 0;
                        if (b.meta.getWellSamplePositionY(b.series, i, s) != null)
                            sy = b.meta.getWellSamplePositionY(b.series, i, s).value().doubleValue();
                        else sy = 0;
                        sa.Position = new PointD(sx, sy);
                        sa.ID = b.meta.getWellSampleID(b.series, i, s);
                        w.Samples.Add(sa);
                    }
                    ome.xml.model.primitives.Color c = b.meta.getWellColor(b.series, i);
                    if (c != null)
                        w.Color = System.Drawing.Color.FromArgb(c.getAlpha(), c.getRed(), c.getGreen(), c.getBlue());
                    Wells.Add(w);
                }

            }
        }
        public WellPlate Plate = null;
        public int[,,] Coords;
        private ZCT coordinate;
        public ZCT Coordinate
        {
            get
            {
                return coordinate;
            }
            set
            {
                coordinate = value;
            }
        }
        public enum ImageType
        {
            stack,
            pyramidal,
            well,
        }
        private string id;
        public ImageType Type { get; set; }
        public List<Channel> Channels = new List<Channel>();
        public List<Resolution> Resolutions = new List<Resolution>();
        public List<AForge.Bitmap> Buffers = new List<AForge.Bitmap>();
        public List<NetVips.Image> vipPages = new List<NetVips.Image>();
        int level = 0;
        public int Level
        {
            get
            {
                if (Type == ImageType.well)
                    return level;
                int l = 0;
                if (openslideBase != null)
                    l = OpenSlideGTK.TileUtil.GetLevel(openslideBase.Schema.Resolutions, Resolution);
                else
                    if (slideBase != null)
                    l = LevelFromResolution(Resolution);
                if (l < 0)
                    return 0;
                return l;
            }
            set
            {
                if (value < 0)
                    return;
                level = value;
            }
        }
        public VolumeD Volume;
        public List<ROI> Annotations = new List<ROI>();
        public string filename = "";
        public string script = "";
        public string Filename
        {
            get
            {
                return filename;
            }
            set
            {
                filename = value;
            }
        }
        public int[] rgbChannels = new int[3] { 0, 1, 2 };
        public int RGBChannelCount
        {
            get
            {
                return Buffers[0].RGBChannelsCount;
            }
        }
        public int bitsPerPixel;
        public int imagesPerSeries = 0;
        public int seriesCount = 1;
        public double frameInterval = 0;
        public bool littleEndian = false;
        public bool isGroup = false;
        public long loadTimeMS = 0;
        public long loadTimeTicks = 0;
        public bool selected = false;
        public Statistics Statistics
        {
            get
            {
                return statistics;
            }
            set
            {
                statistics = value;
            }
        }
        private int sizeZ, sizeC, sizeT;
        private Statistics statistics;
        private double resolution = 1;
        public double Resolution
        {
            get { return resolution; }
            set
            {
                if (value < 0)
                    return;
                resolution = value;
            }
        }
        public static float Progress = 0;
        ImageInfo imageInfo = new ImageInfo();
        /// It copies the BioImage b and returns a new BioImage object.
        /// 
        /// @param BioImage The BioImage object to copy
        /// @param rois If true, the ROIs will be copied. If false, the ROIs will be ignored.
        public static BioImage Copy(BioImage b, bool rois)
        {
            BioImage bi = new BioImage(b.ID);
            if (rois)
                foreach (ROI an in b.Annotations)
                {
                    bi.Annotations.Add(an);
                }
            foreach (Bitmap bf in b.Buffers)
            {
                bi.Buffers.Add(bf.Copy());
            }
            foreach (Channel c in b.Channels)
            {
                bi.Channels.Add(c);
            }
            bi.Volume = b.Volume;
            bi.Coords = b.Coords;
            bi.sizeZ = b.sizeZ;
            bi.sizeC = b.sizeC;
            bi.sizeT = b.sizeT;
            bi.series = b.series;
            bi.seriesCount = b.seriesCount;
            bi.frameInterval = b.frameInterval;
            bi.littleEndian = b.littleEndian;
            bi.isGroup = b.isGroup;
            bi.imageInfo = b.imageInfo;
            bi.bitsPerPixel = b.bitsPerPixel;
            bi.file = b.file;
            bi.filename = b.filename;
            foreach (var item in b.Resolutions)
            {
                bi.Resolutions.Add(item);
            }
            bi.statistics = b.statistics;
            bi.MacroResolution = b.MacroResolution;
            bi.LabelResolution = b.LabelResolution;
            bi.Resolution = b.Resolution;
            bi.imagesPerSeries = b.imagesPerSeries;
            bi.imRead = b.imRead;
            bi.Type = b.Type;
            bi.tifRead = b.tifRead;
            if (b.OpenSlideBase != null)
            {
                bi.openslideBase = b.openslideBase;
                bi.openSlideImage = b.openSlideImage;
            }
            else
            {
                bi.slideBase = b.slideBase;
            }
            bi.PyramidalOrigin = b.PyramidalOrigin;
            bi.PyramidalSize = b.PyramidalSize;
            bi.Plate = b.Plate;
            Recorder.Record(BioLib.Recorder.GetCurrentMethodInfo(), false, b, rois);
            return bi;
        }
        /// Copy a BioImage object.
        /// 
        /// @param BioImage The image to copy
        /// 
        /// @return A copy of the BioImage object.
        public static BioImage Copy(BioImage b)
        {
            return Copy(b, true);
        }
        /// Copy the image and optionally the ROIs
        /// 
        /// @param rois Boolean value indicating whether to copy the ROIs or not.
        /// 
        /// @return A copy of the BioImage object.
        public BioImage Copy(bool rois)
        {
            return BioImage.Copy(this, rois);
        }
        /// > This function copies the current BioImage object and returns a new BioImage object
        /// 
        /// @return A copy of the BioImage object.
        public BioImage Copy()
        {
            return BioImage.Copy(this, true);
        }
        /// CopyInfo() copies the information from one BioImage to another
        /// 
        /// @param BioImage the image to copy
        /// @param copyAnnotations true
        /// @param copyChannels true
        /// 
        /// @return A new BioImage object.
        public static BioImage CopyInfo(BioImage b, bool copyAnnotations, bool copyChannels)
        {
            BioImage bi = new BioImage(b.ID);
            if (copyAnnotations)
                foreach (ROI an in b.Annotations)
                {
                    bi.Annotations.Add(an);
                }
            if (copyChannels)
                foreach (Channel c in b.Channels)
                {
                    bi.Channels.Add(c.Copy());
                }
            foreach (var item in b.Resolutions)
            {
                bi.Resolutions.Add(item);
            }
            bi.Coords = b.Coords;
            bi.Volume = b.Volume;
            bi.sizeZ = b.sizeZ;
            bi.sizeC = b.sizeC;
            bi.sizeT = b.sizeT;
            bi.series = b.series;
            bi.seriesCount = b.seriesCount;
            bi.frameInterval = b.frameInterval;
            bi.littleEndian = b.littleEndian;
            bi.isGroup = b.isGroup;
            bi.imageInfo = b.imageInfo;
            bi.bitsPerPixel = b.bitsPerPixel;
            bi.Coordinate = b.Coordinate;
            bi.file = b.file;
            bi.Filename = b.Filename;
            bi.ID = Images.GetImageName(b.file);
            bi.statistics = b.statistics;
            bi.MacroResolution = b.MacroResolution;
            bi.LabelResolution = b.LabelResolution;
            bi.Resolution = b.Resolution;
            bi.imagesPerSeries = b.imagesPerSeries;
            bi.imRead = b.imRead;
            bi.tifRead = b.tifRead;
            bi.Type = b.Type;
            bi.PyramidalOrigin = b.PyramidalOrigin;
            bi.PyramidalSize = b.PyramidalSize;
            if (b.OpenSlideBase != null)
            {
                bi.openslideBase = b.openslideBase;
                bi.openSlideImage = b.openSlideImage;
            }
            else
            {
                bi.slideBase = b.slideBase;
            }
            bi.Plate = b.Plate;
            return bi;
        }
        /// <summary>
        /// Get the downsampling factor of a given level.
        /// </summary>
        /// <param name="level">The desired level.</param>
        /// <return>
        /// The downsampling factor for this level.
        /// </return> 
        /// <exception cref="OpenSlideException"/>
        public double GetLevelDownsample(int level)
        {
            int originalWidth = Resolutions[0].SizeX; // Width of the original level
            int nextLevelWidth = Resolutions[level].SizeX; // Width of the next level (downsampled)
            return (double)originalWidth / (double)nextLevelWidth;
        }
        public double[] GetLevelDownsamples()
        {
            double[] ds = new double[Resolutions.Count];
            for (int i = 0; i < Resolutions.Count; i++)
            {
                ds[i] = Resolutions[0].PhysicalSizeX * GetLevelDownsample(i);
            }
            return ds;
        }
        /// <summary>
        /// Returns the level of a given resolution.
        /// </summary>
        /// <param name="Resolution"></param>
        /// <returns></returns>
        public int LevelFromResolution(double Resolution)
        {
            int l = 0;
            double[] ds = GetLevelDownsamples();
            if (MacroResolution.HasValue)
            {
                for (int i = 0; i < MacroResolution.Value; i++)
                {
                    if (ds[i] < Resolution)
                        l = i;
                }
            }
            else
            {
                for (int i = 0; i < Resolutions.Count; i++)
                {
                    if (ds[i] < Resolution)
                        l = i;
                }
            }
            return l;
        }
        /// <summary>
        /// Get Unit Per Pixel for pyramidal images.
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        public double GetUnitPerPixel(int level)
        {
            return Resolutions[0].PhysicalSizeX * GetLevelDownsample(level);
        }
        public string ID
        {
            get { return id; }
            set { id = value; }
        }
        public int ImageCount
        {
            get
            {
                return Buffers.Count;
            }
        }
        public double PhysicalSizeX
        {
            get
            {
                if (isPyramidal)
                    return Resolutions[Level].PhysicalSizeX;
                else
                if (Plate != null)
                    return Resolutions[serie].PhysicalSizeX;
                else if (Resolutions.Count > 0)
                    return Resolutions[0].PhysicalSizeX;
                else
                    return 0;
            }
        }
        public double PhysicalSizeY
        {
            get
            {
                if (isPyramidal)
                    return Resolutions[Level].PhysicalSizeY;
                else
                 if (Plate != null)
                    return Resolutions[serie].PhysicalSizeY;
                else if (Resolutions.Count > 0)
                    return Resolutions[0].PhysicalSizeY;
                else
                    return 0;
            }
        }
        public double PhysicalSizeZ
        {
            get
            {
                if (isPyramidal)
                    return Resolutions[Level].PhysicalSizeZ;
                else
                if (Plate != null)
                    return Resolutions[serie].PhysicalSizeZ;
                else if (Resolutions.Count > 0)
                    return Resolutions[0].PhysicalSizeZ;
                else
                    return 0;
            }
        }
        public double StageSizeX
        {
            get
            {
                if (isPyramidal)
                    return Resolutions[Level].StageSizeX;
                else
                if (Plate != null)
                    return Resolutions[serie].StageSizeX;
                else if (Resolutions.Count > 0)
                    return Resolutions[0].StageSizeX;
                else
                    return imageInfo.stageSizeX;
            }
            set
            {
                imageInfo.StageSizeX = value;
            }
        }
        public double StageSizeY
        {
            get
            {
                if (isPyramidal)
                    return Resolutions[Level].StageSizeY;
                else
                if (Plate != null)
                    return Resolutions[serie].StageSizeY;
                else if (Resolutions.Count > 0)
                    return Resolutions[0].StageSizeY;
                else
                    return imageInfo.stageSizeY;
            }
            set { imageInfo.StageSizeY = value; }
        }
        public double StageSizeZ
        {
            get
            {
                if (isPyramidal)
                    return Resolutions[Level].StageSizeZ;
                else
                if (Plate != null)
                    return Resolutions[serie].StageSizeZ;
                else if (Resolutions.Count > 1)
                    return Resolutions[0].StageSizeZ;
                else
                    return imageInfo.stageSizeZ;
            }
            set { imageInfo.StageSizeZ = value; }
        }

        AForge.Size s = new AForge.Size(1920, 1080);
        public AForge.Size PyramidalSize { get { return s; } set { s = value; } }
        PointD pyramidalOrigin = new PointD(0, 0);
        public PointD PyramidalOrigin
        {
            get { return pyramidalOrigin; }
            set
            {
                pyramidalOrigin = value;
            }
        }
        public int series
        {
            get
            {
                return imageInfo.Series;
            }
            set
            {
                imageInfo.Series = value;
            }
        }

        static bool initialized = false;
        OpenSlideBase openslideBase;
        public OpenSlideBase OpenSlideBase { get { return openslideBase; } }
        SlideBase slideBase;
        public SlideBase SlideBase { get { return slideBase; } set { slideBase = value; } }
        public Channel RChannel
        {
            get
            {
                if(Channels.Count >= 3)
                    return Channels[rgbChannels[0]];
                else
                    return Channels[0];
            }
        }
        public Channel GChannel
        {
            get
            {
                if (Channels.Count >= 3)
                    return Channels[rgbChannels[1]];
                else
                    return Channels[0];
            }
        }
        public Channel BChannel
        {
            get
            {
                if (Channels.Count >= 3)
                    return Channels[rgbChannels[2]];
                else
                    return Channels[0];
            }
        }
        private List<ROI> annotationsR = new List<ROI>();
        public List<ROI> AnnotationsR
        {
            get
            {
                return GetAnnotations(Coordinate.Z, RChannel.Index, Coordinate.T);
            }
        }
        private List<ROI> annotationsG = new List<ROI>();
        public List<ROI> AnnotationsG
        {
            get
            {
                return GetAnnotations(Coordinate.Z, GChannel.Index, Coordinate.T);
            }
        }
        private List<ROI> annotationsB = new List<ROI>();
        public List<ROI> AnnotationsB
        {
            get
            {
                return GetAnnotations(Coordinate.Z, BChannel.Index, Coordinate.T);
            }
        }
        public class ImageJDesc
        {
            public string ImageJ;
            public int images = 0;
            public int channels = 0;
            public int slices = 0;
            public int frames = 0;
            public bool hyperstack;
            public string mode;
            public string unit;
            public double finterval = 0;
            public double spacing = 0;
            public bool loop;
            public double min = 0;
            public double max = 0;
            public int count;
            public bool bit8color = false;

            /// The function "FromImage" takes a BioImage object and sets the properties of an
            /// ImageJDesc object based on the properties of the BioImage object.
            /// 
            /// @param BioImage The BioImage parameter represents an object that contains information
            /// about a biological image, such as the number of images, channels, slices, and frames, as
            /// well as other properties like frame interval, physical size, and channel ranges.
            /// 
            /// @return The method is returning an instance of the ImageJDesc class.
            public ImageJDesc FromImage(BioImage b)
            {
                ImageJ = "";
                images = b.ImageCount;
                channels = b.SizeC;
                slices = b.SizeZ;
                frames = b.SizeT;
                hyperstack = true;
                mode = "grayscale";
                unit = "micron";
                finterval = b.frameInterval;
                spacing = b.PhysicalSizeZ;
                loop = false;
                /*
                double dmax = double.MinValue;
                double dmin = double.MaxValue;
                foreach (Channel c in b.Channels)
                {
                    if(dmax < c.Max)
                        dmax = c.Max;
                    if(dmin > c.Min)
                        dmin = c.Min;
                }
                min = dmin;
                max = dmax;
                */
                min = b.Channels[0].RangeR.Min;
                max = b.Channels[0].RangeR.Max;
                return this;
            }
            /// It returns a string that contains the values of all the variables in the imagej image
            /// 
            /// @return A string representation of the imagej image data.
            public string GetString()
            {
                string s = "";
                s += "ImageJ=" + ImageJ + "\n";
                s += "images=" + images + "\n";
                s += "channels=" + channels.ToString() + "\n";
                s += "slices=" + slices.ToString() + "\n";
                s += "frames=" + frames.ToString() + "\n";
                s += "hyperstack=" + hyperstack.ToString() + "\n";
                s += "mode=" + mode.ToString() + "\n";
                s += "unit=" + unit.ToString() + "\n";
                s += "finterval=" + finterval.ToString() + "\n";
                s += "spacing=" + spacing.ToString() + "\n";
                s += "loop=" + loop.ToString() + "\n";
                s += "min=" + min.ToString() + "\n";
                s += "max=" + max.ToString() + "\n";
                return s;
            }
            /// It sets the string to the value of the parameter.
            /// 
            /// @param desc The description of the string.
            public void SetString(string desc)
            {
                string[] lines = desc.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                int maxlen = 20;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (i < maxlen)
                    {
                        string[] sp = lines[i].Split('=');
                        if (sp[0] == "ImageJ")
                            ImageJ = sp[1];
                        if (sp[0] == "images")
                            images = int.Parse(sp[1], CultureInfo.InvariantCulture);
                        if (sp[0] == "channels")
                            channels = int.Parse(sp[1], CultureInfo.InvariantCulture);
                        if (sp[0] == "slices")
                            slices = int.Parse(sp[1], CultureInfo.InvariantCulture);
                        if (sp[0] == "frames")
                            frames = int.Parse(sp[1], CultureInfo.InvariantCulture);
                        if (sp[0] == "hyperstack")
                            hyperstack = bool.Parse(sp[1]);
                        if (sp[0] == "mode")
                            mode = sp[1];
                        if (sp[0] == "unit")
                            unit = sp[1];
                        if (sp[0] == "finterval")
                            finterval = double.Parse(sp[1], CultureInfo.InvariantCulture);
                        if (sp[0] == "spacing")
                            spacing = double.Parse(sp[1], CultureInfo.InvariantCulture);
                        if (sp[0] == "loop")
                            loop = bool.Parse(sp[1]);
                        if (sp[0] == "min")
                            min = double.Parse(sp[1], CultureInfo.InvariantCulture);
                        if (sp[0] == "max")
                            max = double.Parse(sp[1], CultureInfo.InvariantCulture);
                        if (sp[0] == "8bitcolor")
                            bit8color = bool.Parse(sp[1]);
                    }
                    else
                        return;
                }

            }
        }
        public int SizeX
        {
            get
            {
                if (Buffers.Count > 0)
                    return Buffers[0].SizeX;
                else return 0;
            }
        }
        public int SizeY
        {
            get
            {
                if (Buffers.Count > 0)
                    return Buffers[0].SizeY;
                else return 0;
            }
        }
        public int SizeZ
        {
            get { return sizeZ; }
        }
        public int SizeC
        {
            get { return sizeC; }
        }
        public int SizeT
        {
            get { return sizeT; }
        }
        public static bool Planes = false;
        public enum Order
        {
            ZCT,
            CZT,
            TCZ
        }
        public double Magnification
        {
            get; set;
        }
        public Order StackOrder
        {
            get; set;
        }
        public object Tag { get; set; }
        public IntRange RRange
        {
            get
            {
                return RChannel.RangeR;
            }
        }
        public IntRange GRange
        {
            get
            {
                return GChannel.RangeG;
            }
        }
        public IntRange BRange
        {
            get
            {
                return BChannel.RangeB;
            }
        }
        public Bitmap SelectedBuffer
        {
            get
            {
                return Buffers[GetFrameIndex(Coordinate.Z, Coordinate.C, Coordinate.T)];
            }
        }
        public Stopwatch watch = new Stopwatch();
        public bool isRGB
        {
            get
            {
                if (RGBChannelCount == 3 || RGBChannelCount == 4)
                    return true;
                else
                    return false;
            }
        }
        public bool isTime
        {
            get
            {
                if (SizeT > 1)
                    return true;
                else
                    return false;
            }
        }
        public bool isSeries
        {
            get
            {
                if (seriesCount > 1)
                    return true;
                else
                    return false;
            }
        }
        public bool isPyramidal
        {
            get
            {
                if (Type == ImageType.pyramidal)
                    return true;
                else
                    return false;
            }
        }
        public string file;
        static string stat;
        public static string Status
        {
            get
            {
                return stat;
            }
            set
            {
                stat = value;
                Console.WriteLine(stat);
            }
        }
        public static string progFile;
        public static bool Initialized
        {
            get
            {
                return initialized;
            }
        }
        /// Converts a 16-bit image to an 8-bit image
        public void To8Bit()
        {
            if (Buffers[0].RGBChannelsCount == 4)
                To24Bit();
            PixelFormat px = Buffers[0].PixelFormat;
            if (px == PixelFormat.Format8bppIndexed)
                return;
            if (px == PixelFormat.Format48bppRgb)
            {
                To24Bit();
                List<AForge.Bitmap> bfs = new List<AForge.Bitmap>();
                int index = 0;
                for (int i = 0; i < Buffers.Count; i++)
                {
                    Bitmap[] bs = Bitmap.RGB24To8(Buffers[i]);
                    Bitmap br = new Bitmap(ID, bs[2], new ZCT(Buffers[i].Coordinate.Z, 0, Buffers[i].Coordinate.T), index, Buffers[i].Plane);
                    Bitmap bg = new Bitmap(ID, bs[1], new ZCT(Buffers[i].Coordinate.Z, 1, Buffers[i].Coordinate.T), index + 1, Buffers[i].Plane);
                    Bitmap bb = new Bitmap(ID, bs[0], new ZCT(Buffers[i].Coordinate.Z, 2, Buffers[i].Coordinate.T), index + 2, Buffers[i].Plane);
                    for (int b = 0; b < 3; b++)
                    {
                        bs[b].Dispose();
                    }
                    bs = null;
                    br.Stats = Statistics.FromBytes(br);
                    bg.Stats = Statistics.FromBytes(bg);
                    bb.Stats = Statistics.FromBytes(bb);
                    bfs.Add(br);
                    bfs.Add(bg);
                    bfs.Add(bb);
                    index += 3;
                }
                Buffers = bfs;
                UpdateCoords(SizeZ, 3, SizeT);
            }
            else
            if (px == PixelFormat.Format24bppRgb)
            {
                List<Bitmap> bfs = new List<Bitmap>();
                int index = 0;
                for (int i = 0; i < Buffers.Count; i++)
                {
                    Bitmap[] bs = Bitmap.RGB24To8(Buffers[i]);
                    Bitmap br = new Bitmap(ID, bs[2], new ZCT(Buffers[i].Coordinate.Z, 0, Buffers[i].Coordinate.T), index, Buffers[i].Plane);
                    Bitmap bg = new Bitmap(ID, bs[1], new ZCT(Buffers[i].Coordinate.Z, 1, Buffers[i].Coordinate.T), index + 1, Buffers[i].Plane);
                    Bitmap bb = new Bitmap(ID, bs[0], new ZCT(Buffers[i].Coordinate.Z, 2, Buffers[i].Coordinate.T), index + 2, Buffers[i].Plane);
                    for (int b = 0; b < 3; b++)
                    {
                        bs[b].Dispose();
                        bs[b] = null;
                    }
                    bs = null;
                    br.Stats = Statistics.FromBytes(br);
                    bg.Stats = Statistics.FromBytes(bg);
                    bb.Stats = Statistics.FromBytes(bb);
                    bfs.Add(br);
                    bfs.Add(bg);
                    bfs.Add(bb);
                    index += 3;
                }
                Buffers = bfs;
                UpdateCoords(SizeZ, 3, SizeT);
                Channels.Clear();
                Channels.Add(new Channel(0, 8, 1));
                Channels.Add(new Channel(0, 8, 1));
                Channels.Add(new Channel(0, 8, 1));
            }
            else
            if (px == PixelFormat.Format16bppGrayScale)
            {
                foreach (var item in Buffers)
                {
                    item.To8Bit();
                }
            }
            else
            if (px == PixelFormat.Float)
            {
                foreach (var item in Buffers)
                {
                    if (Statistics.StackMax <= 1)
                        item.To8Bit(true);
                    else
                        item.To8Bit(false);
                }
            }
            AutoThreshold(this, true);
            StackThreshold(false);
            bitsPerPixel = 8;
        }
        /// Converts the image to 16 bit.
        public void To16Bit()
        {
            if (Buffers[0].RGBChannelsCount == 4)
                To24Bit();
            if (Buffers[0].PixelFormat == PixelFormat.Format16bppGrayScale)
                return;
            bitsPerPixel = 16;
            if (Buffers[0].PixelFormat == PixelFormat.Format48bppRgb)
            {
                List<Bitmap> bfs = new List<Bitmap>();
                int index = 0;
                for (int i = 0; i < Buffers.Count; i++)
                {
                    Array.Reverse(Buffers[i].Bytes);
                    Bitmap[] bs = Bitmap.RGB48To16(ID, SizeX, SizeY, Buffers[i].Stride, Buffers[i].Bytes, Buffers[i].Coordinate, index, Buffers[i].Plane);
                    bfs.AddRange(bs);
                    index += 3;
                }
                Buffers = bfs;
                UpdateCoords(SizeZ, SizeC * 3, SizeT);
                if (Channels[0].SamplesPerPixel == 3)
                {
                    Channel c = Channels[0].Copy();
                    c.SamplesPerPixel = 1;
                    c.range = new IntRange[1];
                    Channels.Clear();
                    Channels.Add(c);
                    Channels.Add(c.Copy());
                    Channels.Add(c.Copy());
                    Channels[1].Index = 1;
                    Channels[2].Index = 2;
                }
            }
            else if (Buffers[0].PixelFormat == PixelFormat.Format8bppIndexed)
            {
                for (int i = 0; i < Buffers.Count; i++)
                {
                    Buffers[i] = AForge.Imaging.Image.Convert8bppTo16bpp(Buffers[i]);
                }
                for (int c = 0; c < Channels.Count; c++)
                {
                    for (int i = 0; i < Channels[c].range.Length; i++)
                    {
                        Channels[c].range[i].Min = (int)(((float)Channels[c].range[i].Min / (float)byte.MaxValue) * ushort.MaxValue);
                        Channels[c].range[i].Max = (int)(((float)Channels[c].range[i].Max / (float)byte.MaxValue) * ushort.MaxValue);
                    }
                    Channels[c].BitsPerPixel = 16;
                }
                bitsPerPixel = 16;
            }
            else if (Buffers[0].PixelFormat == PixelFormat.Format24bppRgb)
            {
                To48Bit();
                To16Bit();
            }
            else if (Buffers[0].PixelFormat == PixelFormat.Float)
            {
                for (int i = 0; i < Buffers.Count; i++)
                {
                    if (Statistics.StackMax <= 1)
                        Buffers[i].To16Bit(true);
                    else
                        Buffers[i].To16Bit(false);
                }
            }

            foreach (var item in Buffers)
            {
                item.Stats = Statistics.FromBytes(item);
            }
            AutoThreshold(this, true);
            StackThreshold(true);
        }
        /// Converts the image to 24 bit.
        public void To24Bit()
        {
            if (Buffers[0].PixelFormat == PixelFormat.Format24bppRgb)
                return;
            bitsPerPixel = 8;
            if (Buffers[0].PixelFormat == PixelFormat.Format32bppArgb || Buffers[0].PixelFormat == PixelFormat.Format32bppRgb)
            {
                for (int i = 0; i < Buffers.Count; i++)
                {
                    Buffers[i] = Bitmap.To24Bit(Buffers[i]);
                }
                if (Channels.Count == 4)
                {
                    Channels.RemoveAt(0);
                }
                else
                {
                    Channels[0].SamplesPerPixel = 3;
                }
            }
            else
            if (Buffers[0].PixelFormat == PixelFormat.Format48bppRgb)
            {
                //We run 8bit so we get 24 bit rgb.
                for (int i = 0; i < Buffers.Count; i++)
                {
                    Buffers[i] = AForge.Imaging.Image.Convert16bppTo8bpp(Buffers[i]);
                    Buffers[i].SwitchRedBlue();
                }
            }
            else
            if (Buffers[0].PixelFormat == PixelFormat.Format16bppGrayScale)
            {
                //We run 8bit so we get 24 bit rgb.
                for (int i = 0; i < Buffers.Count; i++)
                {
                    Buffers[i] = AForge.Imaging.Image.Convert16bppTo8bpp(Buffers[i]);
                }
                To24Bit();
            }
            else if(Buffers[0].PixelFormat == PixelFormat.Format8bppIndexed)
            {
                List<Bitmap> bms = new List<Bitmap>();
                //We run 8bit so we get 24 bit rgb.
                for (int i = 0; i < Buffers.Count; i+=3)
                {
                    bms.Add(Bitmap.RGB8To24(new Bitmap[] { Buffers[i], Buffers[i+1], Buffers[i+2] }));
                }
                Buffers.Clear();
                Buffers.AddRange(bms);
                UpdateCoords(SizeZ, 1, SizeT);
            }
            foreach (var item in Buffers)
            {
                item.Stats = Statistics.FromBytes(item);
            }
            AutoThreshold(this, true);
            StackThreshold(false);
        }
        /// Converts the image to 32 bit.
        public void To32Bit()
        {
            if (Buffers[0].PixelFormat == PixelFormat.Format32bppArgb)
                return;
            if (Buffers[0].PixelFormat != PixelFormat.Format24bppRgb)
            {
                To24Bit();
            }
            for (int i = 0; i < Buffers.Count; i++)
            {
                UnmanagedImage b = Bitmap.To32Bit(Buffers[i]);
                Buffers[i].Image = b;
            }
            AutoThreshold(this, true);
        }
        /// It converts a 16 bit image to a 48 bit image
        /// 
        /// @return A list of Bitmaps.
        public void To48Bit()
        {
            if (Buffers[0].RGBChannelsCount == 4)
                To24Bit();
            if (Buffers[0].PixelFormat == PixelFormat.Format48bppRgb)
                return;
            if (Buffers[0].PixelFormat == PixelFormat.Format8bppIndexed || Buffers[0].PixelFormat == PixelFormat.Format16bppGrayScale)
            {
                if (Buffers[0].PixelFormat == PixelFormat.Format8bppIndexed)
                {
                    for (int i = 0; i < Buffers.Count; i++)
                    {
                        Buffers[i].Image = AForge.Imaging.Image.Convert8bppTo16bpp(Buffers[i]);
                    }
                }
                List<Bitmap> bfs = new List<Bitmap>();
                if (Buffers.Count % 3 != 0 && Buffers.Count % 2 != 0)
                    for (int i = 0; i < Buffers.Count; i++)
                    {
                        Bitmap bs = new Bitmap(ID, SizeX, SizeY, Buffers[i].PixelFormat, Buffers[i].Bytes, new ZCT(Buffers[i].Coordinate.Z, 0, Buffers[i].Coordinate.T), i, Buffers[i].Plane);
                        Bitmap bbs = Bitmap.RGB16To48(bs);
                        bs.Dispose();
                        bs = null;
                        bfs.Add(bbs);
                    }
                else
                    for (int i = 0; i < Buffers.Count; i += Channels.Count)
                    {
                        Bitmap[] bs = new Bitmap[3];
                        bs[0] = new Bitmap(ID, SizeX, SizeY, Buffers[i].PixelFormat, Buffers[i].Bytes, new ZCT(Buffers[i].Coordinate.Z, 0, Buffers[i].Coordinate.T), i, Buffers[i].Plane);
                        bs[1] = new Bitmap(ID, SizeX, SizeY, Buffers[i + 1].PixelFormat, Buffers[i + 1].Bytes, new ZCT(Buffers[i + 1].Coordinate.Z, 0, Buffers[i + 1].Coordinate.T), i + 1, Buffers[i + 1].Plane);
                        if (Channels.Count > 2)
                            bs[2] = new Bitmap(ID, SizeX, SizeY, Buffers[i + 2].PixelFormat, Buffers[i + 2].Bytes, new ZCT(Buffers[i].Coordinate.Z, 0, Buffers[i + 2].Coordinate.T), i + 2, Buffers[i].Plane);
                        Bitmap bbs = Bitmap.RGB16To48(bs);
                        for (int b = 0; b < 3; b++)
                        {
                            if (bs[b] != null)
                                bs[b].Dispose();
                            bs[b] = null;
                        }
                        bfs.Add(bbs);
                    }
                Buffers = bfs;
                UpdateCoords(SizeZ, 1, SizeT);
                Channel c = Channels[0].Copy();
                c.SamplesPerPixel = 3;
                rgbChannels[0] = 0;
                rgbChannels[1] = 0;
                rgbChannels[2] = 0;
                Channels.Clear();
                Channels.Add(c);
            }
            else
            if (Buffers[0].PixelFormat == PixelFormat.Format24bppRgb || Buffers[0].PixelFormat == PixelFormat.Format32bppArgb)
            {
                for (int i = 0; i < Buffers.Count; i++)
                {
                    Buffers[i] = AForge.Imaging.Image.Convert8bppTo16bpp(Buffers[i]);
                    Buffers[i].SwitchRedBlue();
                }
            }
            else
            {
                int index = 0;
                List<Bitmap> buffers = new List<Bitmap>();
                for (int i = 0; i < Buffers.Count; i += 3)
                {
                    Bitmap[] bf = new Bitmap[3];
                    bf[0] = Buffers[i];
                    bf[1] = Buffers[i + 1];
                    bf[2] = Buffers[i + 2];
                    Bitmap inf = Bitmap.RGB16To48(bf);
                    buffers.Add(inf);
                    for (int b = 0; b < 3; b++)
                    {
                        bf[b].Dispose();
                    }
                    index++;
                }
                Buffers = buffers;
                UpdateCoords(SizeZ, 1, SizeT);
            }
            bitsPerPixel = 16;
            AutoThreshold(this, true);
            StackThreshold(true);
        }
        public void ToShort()
        {
            foreach (var b in Buffers)
            {
                b.ToShort();
            }
            AutoThreshold(this, true);
            StackThreshold(false);
        }
        public void ToFloat()
        {
            foreach (var b in Buffers)
            {
                b.ToFloat();
                b.Stats = Statistics.FromBytes(b);
            }
            AutoThreshold(this, true);
            StackThreshold(false);
        }
        public int? MacroResolution { get; set; }
        public int? LabelResolution { get; set; }
        /*
        void SetLabelMacroResolutions(Resolution[] ress, List<Tuple<int, int>> prs, int serie)
        {
            AForge.Size s = new AForge.Size(int.MaxValue, int.MaxValue);
            bool noMacro = true;
            PixelFormat pf = ress[0].PixelFormat;
            for (int r = 0; r < prs[serie].Item2 - prs[serie].Item1; r++)
            {
                var res = ress[r];
                if (res.SizeX < s.Width && res.SizeY < s.Height)
                {
                    //If the pixel format changes it means this is the macro resolution.
                    if (pf != res.PixelFormat)
                    {
                        noMacro = false;
                        break;
                    }
                    pf = res.PixelFormat;
                    s = new AForge.Size(res.SizeX, res.SizeY);
                }
                else
                {
                    //If this level is bigger than the previous this is the macro resolution.
                    noMacro = false;
                    int mac = ress.Count() - 1;
                    int lab = ress.Count() - 2;
                    Resolutions.Add(ress[ress.Count() - 1]);
                    Resolutions.Add(ress[ress.Count() - 2]);
                    LabelResolution = mac;
                    MacroResolution = lab;
                    return;
                }
            }
            /*
            if(!LabelResolution.HasValue)
            {
                Resolutions.Add(ress[ress.Count() - 1]);
                Resolutions.Add(ress[ress.Count() - 2]);
                LabelResolution = Resolutions.Count - 1;
                MacroResolution = Resolutions.Count - 2;
            }
            */
        public void RotateFlip(AForge.RotateFlipType rot)
        {
            for (int i = 0; i < Buffers.Count; i++)
            {
                Buffers[i].RotateFlip(rot);
            }
            Volume = new VolumeD(new Point3D(StageSizeX, StageSizeY, StageSizeZ), new Point3D(PhysicalSizeX * SizeX, PhysicalSizeY * SizeY, PhysicalSizeZ * SizeZ));
        }
        /// Bake(int rmin, int rmax, int gmin, int gmax, int bmin, int bmax)
        /// 
        /// @param rmin The minimum value of the red channel.
        /// @param rmax The maximum value of the red channel.
        /// @param gmin The minimum value of the green channel.
        /// @param gmax The maximum value of the green channel.
        /// @param bmin The minimum value of the blue channel.
        /// @param bmax The maximum value of the blue channel.
        public void Bake(int rmin, int rmax, int gmin, int gmax, int bmin, int bmax)
        {
            Bake(new IntRange(rmin, rmax), new IntRange(gmin, gmax), new IntRange(bmin, bmax));
        }
        /// It takes a range of values for each channel, and creates a new image with the filtered
        /// values
        /// 
        /// @param IntRange 
        /// @param IntRange 
        /// @param IntRange 
        public void Bake(IntRange rf, IntRange gf, IntRange bf)
        {
            BioImage bm = new BioImage(Images.GetImageName(ID));
            bm = CopyInfo(this, true, true);
            for (int i = 0; i < Buffers.Count; i++)
            {
                ZCT co = Buffers[i].Coordinate;
                UnmanagedImage b = GetFiltered(i, rf, gf, bf);
                Bitmap inf = new Bitmap(bm.ID, b, co, i);
                bm.SetFrameIndex(co.Z, co.C, co.T, i);
                bm.Buffers.Add(inf);
            }
            foreach (Channel item in bm.Channels)
            {
                for (int i = 0; i < item.range.Length; i++)
                {
                    item.range[i].Min = 0;
                    if (bm.bitsPerPixel > 8)
                        item.range[i].Max = ushort.MaxValue;
                    else
                        item.range[i].Max = 255;
                }
            }
            AutoThreshold(bm, true);
            Images.AddImage(bm);
        }
        /// It takes a list of images and assigns them to a 3D array of coordinates
        public void UpdateCoords()
        {
            int z = 0;
            int c = 0;
            int t = 0;
            Coords = new int[SizeZ, SizeC, SizeT];
            for (int im = 0; im < Buffers.Count; im++)
            {
                ZCT co = new ZCT(z, c, t);
                SetFrameIndex(co.Z, co.C, co.T, im);
                Buffers[im].Coordinate = co;
                if (c < SizeC - 1)
                    c++;
                else
                {
                    c = 0;
                    if (z < SizeZ - 1)
                        z++;
                    else
                    {
                        z = 0;
                        if (t < SizeT - 1)
                            t++;
                        else
                            t = 0;
                    }
                }
            }
        }
        /// It takes the number of Z, C, and T planes in the image and then assigns each image buffer a
        /// coordinate in the ZCT space
        /// 
        /// @param sz size of the Z dimension
        /// @param sc number of channels
        /// @param st number of time points
        public void UpdateCoords(int sz, int sc, int st)
        {
            int z = 0;
            int c = 0;
            int t = 0;
            sizeZ = sz;
            sizeC = sc;
            sizeT = st;
            Coords = new int[sz, sc, st];
            for (int im = 0; im < Buffers.Count; im++)
            {
                ZCT co = new ZCT(z, c, t);
                SetFrameIndex(co.Z, co.C, co.T, im);
                Buffers[im].Coordinate = co;
                if (c < SizeC - 1)
                    c++;
                else
                {
                    c = 0;
                    if (z < SizeZ - 1)
                        z++;
                    else
                    {
                        z = 0;
                        if (t < SizeT - 1)
                            t++;
                        else
                            t = 0;
                    }
                }
            }
        }
        /// It takes a list of images and assigns them to a 3D array of coordinates
        /// 
        /// @param sz size of the Z dimension
        /// @param sc number of channels
        /// @param st number of time points
        /// @param order XYCZT or XYZCT
        public void UpdateCoords(int sz, int sc, int st, Order order)
        {
            int z = 0;
            int c = 0;
            int t = 0;
            sizeZ = sz;
            sizeC = sc;
            sizeT = st;
            Coords = new int[sz, sc, st];
            int fr = sz * sc * st;
            if (order == Order.CZT)
            {
                for (int im = 0; im < fr; im++)
                {
                    ZCT co = new ZCT(z, c, t);
                    SetFrameIndex(co.Z, co.C, co.T, im);
                    if (c < SizeC - 1)
                        c++;
                    else
                    {
                        c = 0;
                        if (z < SizeZ - 1)
                            z++;
                        else
                        {
                            z = 0;
                            if (t < SizeT - 1)
                                t++;
                            else
                                t = 0;
                        }
                    }
                }
            }
            else if (order == Order.ZCT)
            {
                for (int im = 0; im < fr; im++)
                {
                    ZCT co = new ZCT(z, c, t);
                    SetFrameIndex(co.Z, co.C, co.T, im);
                    if (z < SizeZ - 1)
                        z++;
                    else
                    {
                        z = 0;
                        if (c < SizeC - 1)
                            c++;
                        else
                        {
                            c = 0;
                            if (t < SizeT - 1)
                                t++;
                            else
                                t = 0;
                        }
                    }
                }
            }
            else
            {
                //Order.TZC 
                for (int im = 0; im < fr; im++)
                {
                    ZCT co = new ZCT(z, c, t);
                    SetFrameIndex(co.Z, co.C, co.T, im);
                    if (t < SizeT - 1)
                        t++;
                    else
                    {
                        t = 0;
                        if (z < SizeZ - 1)
                            z++;
                        else
                        {
                            z = 0;
                            if (c < SizeC - 1)
                                c++;
                            else
                                c = 0;
                        }
                    }
                }
            }
        }
        /// Convert a physical size to an image size
        /// 
        /// @param d the distance in microns
        /// 
        /// @return The value of d divided by the physicalSizeX.
        public double ToImageSizeX(double d)
        {
            return d / PhysicalSizeX;
        }
        /// Convert a physical size in Y direction to an image size in Y direction
        /// 
        /// @param d the distance in microns
        /// 
        /// @return The return value is the value of the parameter d divided by the value of the
        /// physicalSizeY field.
        public double ToImageSizeY(double d)
        {
            return d / PhysicalSizeY;
        }
        /// > Convert a stage coordinate to an image coordinate
        /// 
        /// @param x the x coordinate of the point in the image
        /// 
        /// @return The return value is a double.
        public double ToImageSpaceX(double x)
        {
            if (isPyramidal)
                return x;
            return (float)((x - StageSizeX) / PhysicalSizeX);
        }
        /// > Convert a Y coordinate from stage space to image space
        /// 
        /// @param y the y coordinate of the point in the image
        /// 
        /// @return The return value is the y-coordinate of the image.
        public double ToImageSpaceY(double y)
        {
            if (isPyramidal)
                return y;
            return (float)((y - StageSizeY) / PhysicalSizeY);
        }
        /// Convert a point in the stage coordinate system to a point in the image coordinate system
        /// 
        /// @param PointD 
        /// 
        /// @return A PointF object.
        public PointD ToImageSpace(PointD p)
        {
            PointD pp = new PointD();
            pp.X = (float)((p.X - StageSizeX) / PhysicalSizeX);
            pp.Y = (float)((p.Y - StageSizeY) / PhysicalSizeY);
            return pp;
        }
        /// Convert a list of points from stage space to image space
        /// 
        /// @param p List of points in stage space
        /// 
        /// @return A PointD array.
        public PointD[] ToImageSpace(List<PointD> p)
        {
            PointD[] ps = new PointD[p.Count];
            for (int i = 0; i < p.Count; i++)
            {
                PointD pp = new PointD();
                pp.X = ((p[i].X - StageSizeX) / PhysicalSizeX);
                pp.Y = ((p[i].Y - StageSizeY) / PhysicalSizeY);
                ps[i] = pp;
            }
            return ps;
        }
        /// > The function takes a list of points in the stage coordinate system and returns a list of
        /// points in the image coordinate system
        /// 
        /// @param p the points to be converted
        /// 
        /// @return A PointF[]
        public PointF[] ToImageSpace(PointF[] p)
        {
            PointF[] ps = new PointF[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                PointF pp = new PointF();
                pp.X = (float)((p[i].X - StageSizeX) / PhysicalSizeX);
                pp.Y = (float)((p[i].Y - StageSizeY) / PhysicalSizeY);
                ps[i] = pp;
            }
            return ps;
        }
        /// > Convert a rectangle in physical space to a rectangle in image space
        /// 
        /// @param RectangleD 
        /// 
        /// @return A RectangleF object.
        public RectangleF ToImageSpace(RectangleD p)
        {
            RectangleF r = new RectangleF();
            Point pp = new Point();
            r.X = (int)((p.X - StageSizeX) / PhysicalSizeX);
            r.Y = (int)((p.Y - StageSizeY) / PhysicalSizeY);
            r.Width = (int)(p.W / PhysicalSizeX);
            r.Height = (int)(p.H / PhysicalSizeY);
            return r;
        }
        /// > This function converts a point in the image space to a point in the stage space
        /// 
        /// @param PointD A class that contains an X and Y coordinate.
        /// 
        /// @return A PointD object.
        public PointD ToStageSpace(PointD p)
        {
            PointD pp = new PointD();
            if (isPyramidal)
            {
                pp.X = ((p.X * Resolutions[Level].PhysicalSizeX) + Volume.Location.X);
                pp.Y = ((p.Y * Resolutions[Level].PhysicalSizeY) + Volume.Location.Y);
                return pp;
            }
            else
            {
                pp.X = ((p.X * PhysicalSizeX) + Volume.Location.X);
                pp.Y = ((p.Y * PhysicalSizeY) + Volume.Location.Y);
                return pp;
            }
        }
        /// Convert a point in the image space to a point in the stage space
        /// 
        /// @param PointD A point in the image space
        /// @param resolution the resolution of the image (0, 1, 2, 3, 4)
        /// 
        /// @return A PointD object.
        public PointD ToStageSpace(PointD p, int resolution)
        {
            PointD pp = new PointD();
            pp.X = ((p.X * Resolutions[resolution].PhysicalSizeX) + Volume.Location.X);
            pp.Y = ((p.Y * Resolutions[resolution].PhysicalSizeY) + Volume.Location.Y);
            return pp;
        }
        /// > The function takes a point in the volume space and converts it to a point in the stage
        /// space
        /// 
        /// @param PointD A custom class that holds an X and Y coordinate.
        /// @param physicalSizeX The width of the stage in mm
        /// @param physicalSizeY The height of the stage in mm
        /// @param volumeX The X coordinate of the top left corner of the volume in stage space.
        /// @param volumeY The Y position of the top left corner of the volume in stage space.
        /// 
        /// @return A PointD object.
        public static PointD ToStageSpace(PointD p, double physicalSizeX, double physicalSizeY, double volumeX, double volumeY)
        {
            PointD pp = new PointD();
            pp.X = ((p.X * physicalSizeX) + volumeX);
            pp.Y = ((p.Y * physicalSizeY) + volumeY);
            return pp;
        }
        /// > Convert a rectangle from the coordinate space of the image to the coordinate space of the
        /// stage
        /// 
        /// @param RectangleD A rectangle with double precision coordinates.
        /// 
        /// @return A RectangleD object.
        public RectangleD ToStageSpace(RectangleD p)
        {
            RectangleD r = new RectangleD();
            r.X = ((p.X * PhysicalSizeX) + Volume.Location.X);
            r.Y = ((p.Y * PhysicalSizeY) + Volume.Location.Y);
            r.W = (p.W * PhysicalSizeX);
            r.H = (p.H * PhysicalSizeY);
            return r;
        }
        /// > This function takes a rectangle in the coordinate space of the image and converts it to
        /// the coordinate space of the stage
        /// 
        /// @param RectangleD A rectangle with double precision.
        /// @param physicalSizeX The width of the physical screen in pixels
        /// @param physicalSizeY The height of the stage in pixels
        /// @param volumeX The X position of the volume in stage space.
        /// @param volumeY The Y position of the top of the volume in stage space.
        /// 
        /// @return A RectangleD object.
        public static RectangleD ToStageSpace(RectangleD p, double physicalSizeX, double physicalSizeY, double volumeX, double volumeY)
        {
            RectangleD r = new RectangleD();
            r.X = ((p.X * physicalSizeX) + volumeX);
            r.Y = ((p.Y * physicalSizeY) + volumeY);
            r.W = (p.W * physicalSizeX);
            r.H = (p.H * physicalSizeY);
            return r;
        }
        /// > This function takes a list of points in the coordinate system of the image and returns a
        /// list of points in the coordinate system of the stage
        /// 
        /// @param p The array of points to convert
        /// 
        /// @return A PointD[] array.
        public PointD[] ToStageSpace(PointD[] p)
        {
            PointD[] ps = new PointD[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                PointD pp = new PointD();
                pp.X = ((p[i].X * PhysicalSizeX) + Volume.Location.X);
                pp.Y = ((p[i].Y * PhysicalSizeY) + Volume.Location.Y);
                ps[i] = pp;
            }
            return ps;
        }
        /// It takes a list of points, and converts them from a coordinate system where the origin is in
        /// the center of the image, to a coordinate system where the origin is in the top left corner
        /// of the image
        /// 
        /// @param p the array of points to convert
        /// @param physicalSizeX The width of the image in microns
        /// @param physicalSizeY The height of the image in microns
        /// @param volumeX The X position of the volume in stage space.
        /// @param volumeY The Y position of the top left corner of the volume in stage space.
        /// 
        /// @return A PointD array.
        public static PointD[] ToStageSpace(PointD[] p, double physicalSizeX, double physicalSizeY, double volumeX, double volumeY)
        {
            PointD[] ps = new PointD[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                PointD pp = new PointD();
                pp.X = ((p[i].X * physicalSizeX) + volumeX);
                pp.Y = ((p[i].Y * physicalSizeY) + volumeY);
                ps[i] = pp;
            }
            return ps;
        }
        /* Creating a new BioImage object. */
        public BioImage(string file)
        {
            id = file;
            this.file = file;
            filename = Images.GetImageName(id);
            Coordinate = new ZCT();
            rgbChannels[0] = 0;
            rgbChannels[1] = 0;
            rgbChannels[2] = 0;
        }
        /// It takes a BioImage object, and returns a new BioImage object that is a subset of the
        /// original
        /// 
        /// @param BioImage the image to be processed
        /// @param ser series number
        /// @param zs starting z-plane
        /// @param ze end of z-stack
        /// @param cs channel start
        /// @param ce channel end
        /// @param ts time start
        /// @param te time end
        /// 
        /// @return A new BioImage object.
        public static BioImage Substack(BioImage orig, int ser, int zs, int ze, int cs, int ce, int ts, int te)
        {
            BioImage b = CopyInfo(orig, false, false);
            //b.ID = Images.GetImageName(orig.ID);
            int i = 0;
            b.Coords = new int[ze - zs, ce - cs, te - ts];
            b.sizeZ = ze - zs;
            b.sizeC = ce - cs;
            b.sizeT = te - ts;
            for (int ti = 0; ti < b.SizeT; ti++)
            {
                for (int zi = 0; zi < b.SizeZ; zi++)
                {
                    for (int ci = 0; ci < b.SizeC; ci++)
                    {
                        int ind = orig.GetFrameIndex(zs + zi, cs + ci, ts + ti);
                        Bitmap bf = new Bitmap(Images.GetImageName(orig.id), orig.SizeX, orig.SizeY, orig.Buffers[0].PixelFormat, orig.Buffers[ind].Bytes, new ZCT(zi, ci, ti), i);
                        bf.Stats = Statistics.FromBytes(bf);
                        b.Buffers.Add(bf);
                        b.SetFrameIndex(zi, ci, ti, i);
                        i++;
                    }
                }
            }
            for (int ci = cs; ci < ce; ci++)
            {
                b.Channels.Add(orig.Channels[ci]);
            }
            b.Resolutions.Add(new Resolution(b.Buffers[0].SizeX, b.Buffers[0].SizeY, b.Buffers[0].PixelFormat, b.PhysicalSizeX, b.PhysicalSizeY, b.PhysicalSizeZ, b.StageSizeX, b.StageSizeY, b.StageSizeZ));
            AutoThreshold(b, false);
            if (b.bitsPerPixel > 8)
                b.StackThreshold(true);
            else
                b.StackThreshold(false);
            Images.AddImage(b);
            return b;
        }
        /// This function takes two images and merges them together
        /// 
        /// @param BioImage The image to be merged
        /// @param BioImage The image to be merged
        /// 
        /// @return A new BioImage object.
        public static BioImage MergeChannels(BioImage b2, BioImage b)
        {
            BioImage res = new BioImage(b2.ID);
            res.ID = Images.GetImageName(b2.ID);
            res.series = b2.series;
            res.sizeZ = b2.SizeZ;
            int cOrig = b2.SizeC;
            res.sizeC = b2.SizeC + b.SizeC;
            res.sizeT = b2.SizeT;
            res.bitsPerPixel = b2.bitsPerPixel;
            res.imageInfo = b2.imageInfo;
            res.littleEndian = b2.littleEndian;
            res.seriesCount = b2.seriesCount;
            res.imagesPerSeries = res.ImageCount / res.seriesCount;
            res.Coords = new int[res.SizeZ, res.SizeC, res.SizeT];
            res.Resolutions.Add(b2.Resolutions[0]);
            res.Volume = new VolumeD(new Point3D(res.StageSizeX, res.StageSizeY, res.StageSizeZ), new Point3D(b2.SizeX, b2.SizeY, b2.SizeZ));
            int i = 0;
            for (int ci = 0; ci < res.SizeC; ci++)
            {
                for (int ti = 0; ti < res.SizeT; ti++)
                {
                    for (int zi = 0; zi < res.SizeZ; zi++)
                    {
                        if (i < b.ImageCount)
                        {
                            ZCT co = new ZCT(zi, ci, ti);
                            int ind = b.GetFrameIndex(zi, 0, ti);
                            //This plane is not part of b1 so we add the planes from b2 channels.
                            Bitmap copy = new Bitmap(b.id, b.SizeX, b.SizeY, b.Buffers[0].PixelFormat, b.Buffers[ind].Bytes, co, ind);
                            res.SetFrameIndex(zi, ci, ti, ind);
                            res.Buffers.Add(copy);
                            //Lets copy the ROI's from the original image.
                            List<ROI> anns = b.GetAnnotations(zi, ci, ti);
                            if (anns.Count > 0)
                                res.Annotations.AddRange(anns);
                        }
                        else
                        {
                            ZCT co = new ZCT(zi, ci, ti);
                            int ind = b2.GetFrameIndex(zi, 0, ti);
                            //This plane is not part of b1 so we add the planes from b2 channels.
                            Bitmap copy = new Bitmap(b2.id, b2.SizeX, b2.SizeY, b2.Buffers[0].PixelFormat, b2.Buffers[ind].Bytes, co, ind);
                            res.SetFrameIndex(zi, ci, ti, b.ImageCount + ind);
                            res.Buffers.Add(copy);
                            //Lets copy the ROI's from the original image.
                            List<ROI> anns = b2.GetAnnotations(zi, ci, ti);
                            if (anns.Count > 0)
                                res.Annotations.AddRange(anns);
                        }
                        i++;
                    }
                }
            }
            for (int ci = 0; ci < b.SizeC; ci++)
            {
                res.Channels.Add(b.Channels[ci].Copy());
            }
            for (int ci = 0; ci < b2.SizeC; ci++)
            {
                res.Channels.Add(b2.Channels[ci].Copy());
            }
            res.rgbChannels[0] = 0;
            if (res.Channels.Count > 1)
                res.rgbChannels[1] = 1;
            if (res.Channels.Count > 2)
                res.rgbChannels[2] = 2;
            Images.AddImage(res);
            res.imagesPerSeries = res.Buffers.Count;
            //We wait for threshold image statistics calculation
            do
            {
                Thread.Sleep(100);
            } while (res.Buffers[res.Buffers.Count - 1].Stats == null);
            AutoThreshold(res, false);
            if (res.bitsPerPixel > 8)
                res.StackThreshold(true);
            else
                res.StackThreshold(false);
            return res;
        }
        /// MergeChannels(b, b2) takes two images, b and b2, and merges the channels of b2 into b
        /// 
        /// @param bname The name of the first image
        /// @param b2name The name of the image to merge with the first image.
        /// 
        /// @return A BioImage object.
        public static BioImage MergeChannels(string bname, string b2name)
        {
            BioImage b = Images.GetImage(bname);
            BioImage b2 = Images.GetImage(b2name);
            return MergeChannels(b, b2);
        }
        /// It takes a 3D image and merges the Z-stack into a single 2D image
        /// 
        /// @param BioImage The image to be merged
        /// 
        /// @return A new BioImage object.
        public static BioImage MergeZ(BioImage b)
        {
            BioImage bi = BioImage.CopyInfo(b, true, true);
            int ind = 0;
            for (int c = 0; c < b.SizeC; c++)
            {
                for (int t = 0; t < b.sizeT; t++)
                {
                    Merge m = new Merge(b.Buffers[b.GetFrameIndex(0, c, t)]);
                    Bitmap bm = new Bitmap(b.SizeX, b.SizeY, b.Buffers[0].PixelFormat);
                    for (int i = 1; i < b.sizeZ; i++)
                    {
                        m.OverlayImage = bm;
                        bm = m.Apply(b.Buffers[b.GetFrameIndex(i, c, t)]);
                    }
                    Bitmap bf = new Bitmap(b.file, bm, new ZCT(0, c, t), ind);
                    bi.Buffers.Add(bf);
                    bf.Stats = Statistics.FromBytes(bf);
                    ind++;
                }
            }
            Images.AddImage(bi);
            bi.UpdateCoords(1, b.SizeC, b.SizeT);
            bi.Coordinate = new ZCT(0, 0, 0);
            bi.Resolutions.Add(new Resolution(b.Buffers[0].SizeX, b.Buffers[0].SizeY, b.Buffers[0].PixelFormat, b.PhysicalSizeX, b.PhysicalSizeY, b.PhysicalSizeZ, b.StageSizeX, b.StageSizeY, b.StageSizeZ));

            AutoThreshold(bi, false);
            if (bi.bitsPerPixel > 8)
                bi.StackThreshold(true);
            else
                bi.StackThreshold(false);
            return bi;
        }
        /// It takes a 3D image and merges the time dimension into a single image
        /// 
        /// @param BioImage The image to be processed
        /// 
        /// @return A new BioImage object.
        public static BioImage MergeT(BioImage b)
        {
            BioImage bi = BioImage.CopyInfo(b, true, true);
            int ind = 0;
            for (int c = 0; c < b.SizeC; c++)
            {
                for (int z = 0; z < b.sizeZ; z++)
                {
                    Merge m = new Merge(b.Buffers[b.GetFrameIndex(z, c, 0)]);
                    Bitmap bm = new Bitmap(b.SizeX, b.SizeY, b.Buffers[0].PixelFormat);
                    for (int i = 1; i < b.sizeT; i++)
                    {
                        m.OverlayImage = bm;
                        bm = m.Apply(b.Buffers[b.GetFrameIndex(z, c, i)]);
                    }
                    Bitmap bf = new Bitmap(b.file, bm, new ZCT(z, c, 0), ind);
                    bi.Buffers.Add(bf);
                    bf.Stats = Statistics.FromBytes(bf);
                    ind++;
                }
            }
            Images.AddImage(bi);
            bi.UpdateCoords(1, b.SizeC, b.SizeT);
            bi.Coordinate = new ZCT(0, 0, 0);
            bi.Resolutions.Add(new Resolution(b.Buffers[0].SizeX, b.Buffers[0].SizeY, b.Buffers[0].PixelFormat, b.PhysicalSizeX, b.PhysicalSizeY, b.PhysicalSizeZ, b.StageSizeX, b.StageSizeY, b.StageSizeZ));
            AutoThreshold(bi, false);
            if (bi.bitsPerPixel > 8)
                bi.StackThreshold(true);
            else
                bi.StackThreshold(false);
            return bi;
        }
        /// It takes a single image and splits it into three images, one for each channel
        /// 
        /// @return A list of BioImages
        public BioImage[] SplitChannels()
        {
            BioImage[] bms;
            if (isRGB)
            {
                bms = new BioImage[3];
                BioImage ri = new BioImage(Path.GetFileNameWithoutExtension(ID) + "-1" + Path.GetExtension(ID));
                BioImage gi = new BioImage(Path.GetFileNameWithoutExtension(ID) + "-2" + Path.GetExtension(ID));
                BioImage bi = new BioImage(Path.GetFileNameWithoutExtension(ID) + "-3" + Path.GetExtension(ID));
                ri.sizeC = 1;
                gi.sizeC = 1;
                bi.sizeC = 1;
                ri.sizeZ = SizeZ;
                gi.sizeZ = SizeZ;
                bi.sizeZ = SizeZ;
                ri.sizeT = SizeT;
                gi.sizeT = SizeT;
                bi.sizeT = SizeT;

                ri.Coords = new int[SizeZ, 1, SizeT];
                gi.Coords = new int[SizeZ, 1, SizeT];
                bi.Coords = new int[SizeZ, 1, SizeT];
                int ind = 0;
                for (int i = 0; i < ImageCount; i++)
                {
                    if (Buffers[i].PixelFormat == PixelFormat.Format48bppRgb)
                    {
                        //For 48bit images we need to use our own function as AForge won't give us a proper image.
                        Bitmap[] bfs = Bitmap.RGB48To16(ID, SizeX, SizeY, Buffers[i].Stride, Buffers[i].Bytes, Buffers[i].Coordinate, ind, Buffers[i].Plane);
                        ind += 3;
                        ri.Buffers.Add(bfs[0]);
                        gi.Buffers.Add(bfs[1]);
                        bi.Buffers.Add(bfs[2]);
                        bfs[0].Stats = Statistics.FromBytes(bfs[0]);
                        bfs[1].Stats = Statistics.FromBytes(bfs[1]);
                        bfs[2].Stats = Statistics.FromBytes(bfs[2]);
                        ri.SetFrameIndex(Buffers[i].Coordinate.Z, Buffers[i].Coordinate.C, Buffers[i].Coordinate.T, i);
                        gi.SetFrameIndex(Buffers[i].Coordinate.Z, Buffers[i].Coordinate.C, Buffers[i].Coordinate.T, i);
                        bi.SetFrameIndex(Buffers[i].Coordinate.Z, Buffers[i].Coordinate.C, Buffers[i].Coordinate.T, i);
                    }
                    else
                    {

                        Bitmap rImage = extractR.Apply(Buffers[i]);
                        Bitmap rbf = new Bitmap(ri.ID, rImage, Buffers[i].Coordinate, ind++);
                        rbf.Stats = Statistics.FromBytes(rbf);
                        ri.Buffers.Add(rbf);
                        ri.SetFrameIndex(Buffers[i].Coordinate.Z, Buffers[i].Coordinate.C, Buffers[i].Coordinate.T, i);

                        Bitmap gImage = extractG.Apply(Buffers[i]);
                        Bitmap gbf = new Bitmap(gi.ID, gImage, Buffers[i].Coordinate, ind++);
                        gbf.Stats = Statistics.FromBytes(gbf);
                        gi.Buffers.Add(gbf);
                        gi.SetFrameIndex(Buffers[i].Coordinate.Z, Buffers[i].Coordinate.C, Buffers[i].Coordinate.T, i);

                        Bitmap bImage = extractB.Apply(Buffers[i]);
                        //Clipboard.SetImage(bImage);
                        Bitmap bbf = new Bitmap(bi.ID, bImage, Buffers[i].Coordinate, ind++);
                        bbf.Stats = Statistics.FromBytes(bbf);
                        bi.Buffers.Add(bbf);
                        bi.SetFrameIndex(Buffers[i].Coordinate.Z, Buffers[i].Coordinate.C, Buffers[i].Coordinate.T, i);

                    }
                }
                //We wait for threshold image statistics calculation
                do
                {
                    Thread.Sleep(100);
                } while (bi.Buffers[bi.Buffers.Count - 1].Stats == null);
                ri.Resolutions.Add(new Resolution(Buffers[0].SizeX, Buffers[0].SizeY, Buffers[0].PixelFormat, PhysicalSizeX, PhysicalSizeY, PhysicalSizeZ, StageSizeX, StageSizeY, StageSizeZ));
                gi.Resolutions.Add(new Resolution(Buffers[0].SizeX, Buffers[0].SizeY, Buffers[0].PixelFormat, PhysicalSizeX, PhysicalSizeY, PhysicalSizeZ, StageSizeX, StageSizeY, StageSizeZ));
                bi.Resolutions.Add(new Resolution(Buffers[0].SizeX, Buffers[0].SizeY, Buffers[0].PixelFormat, PhysicalSizeX, PhysicalSizeY, PhysicalSizeZ, StageSizeX, StageSizeY, StageSizeZ));
                ri.Channels.Add(Channels[0].Copy());
                gi.Channels.Add(Channels[0].Copy());
                bi.Channels.Add(Channels[0].Copy());
                AutoThreshold(ri, false);
                AutoThreshold(gi, false);
                AutoThreshold(bi, false);
                Images.AddImage(ri);
                Images.AddImage(gi);
                Images.AddImage(bi);
                bms[0] = ri;
                bms[1] = gi;
                bms[2] = bi;
            }
            else
            {
                bms = new BioImage[SizeC];
                for (int c = 0; c < SizeC; c++)
                {
                    BioImage b = BioImage.Substack(this, 0, 0, SizeZ, c, c + 1, 0, SizeT);
                    bms[c] = b;
                }
            }
            return bms;
        }
        /// > SplitChannels splits a BioImage into its constituent channels
        /// 
        /// @param BioImage The image to split
        /// 
        /// @return An array of BioImages
        public static BioImage[] SplitChannels(BioImage bb)
        {
            return bb.SplitChannels();
        }
        /// This function takes an image and splits it into its individual channels
        /// 
        /// @param name The name of the image to split.
        /// 
        /// @return An array of BioImage objects.
        public static BioImage[] SplitChannels(string name)
        {
            return SplitChannels(Images.GetImage(name));
        }

        /* Creating a new instance of the LevelsLinear class. */
        public static LevelsLinear filter8 = new LevelsLinear();
        public static LevelsLinear16bpp filter16 = new LevelsLinear16bpp();
        private static ExtractChannel extractR = new ExtractChannel(AForge.Imaging.RGB.R);
        private static ExtractChannel extractG = new ExtractChannel(AForge.Imaging.RGB.G);
        private static ExtractChannel extractB = new ExtractChannel(AForge.Imaging.RGB.B);

        /// > Get the image at the specified coordinates
        /// 
        /// @param z the z-stack index
        /// @param c channel
        /// @param t time
        /// 
        /// @return A Bitmap object.
        public Bitmap GetImageByCoord(int z, int c, int t)
        {
            return Buffers[GetFrameIndex(z, c, t)];
        }
        /// "Given a z, c, t coordinate, return the bitmap at that coordinate."
        /// 
        /// The function is called by the following code:
        /// 
        /// @param z the z-stack index
        /// @param c channel
        /// @param t time
        /// 
        /// @return A bitmap.
        public Bitmap GetBitmap(int z, int c, int t)
        {
            return Buffers[GetFrameIndex(z, c, t)];
        }
        /// > GetIndex(x,y) = (y * stridex + x) * 2
        /// 
        /// The stridex is the width of the image in bytes. 
        /// 
        /// The stridey is the height of the image in bytes. 
        /// 
        /// @param ix x coordinate of the pixel
        /// @param iy The y coordinate of the pixel
        /// 
        /// @return The index of the pixel in the array.
        public int GetIndex(int ix, int iy)
        {
            if (ix > SizeX || iy > SizeY || ix < 0 || iy < 0)
                return 0;
            int stridex = SizeX;
            int x = ix;
            int y = iy;
            if (bitsPerPixel > 8)
            {
                return (y * stridex + x) * 2;
            }
            else
            {
                return (y * stridex + x);
            }
        }
        /// > The function returns the index of the pixel in the buffer
        /// 
        /// @param ix x coordinate of the pixel
        /// @param iy The y coordinate of the pixel
        /// @param index 0 = Red, 1 = Green, 2 = Blue
        /// 
        /// @return The index of the pixel in the buffer.
        public int GetIndexRGB(int ix, int iy, int index)
        {
            int stridex = SizeX;
            //For 16bit (2*8bit) images we multiply buffer index by 2
            int x = ix;
            int y = iy;
            if (bitsPerPixel > 8)
            {
                return (y * stridex + x) * 2 * index;
            }
            else
            {
                return (y * stridex + x) * index;
            }
        }
        /// If the coordinate is within the bounds of the image, then return the value of the pixel at
        /// that coordinate
        /// 
        /// @param ZCTXY a struct that contains the X, Y, Z, C, and T coordinates of the pixel.
        /// 
        /// @return The value of the pixel at the given coordinate.
        public ushort GetValue(ZCTXY coord)
        {
            return GetValueRGB(coord, 0);
        }
        /// It takes a coordinate and an index and returns the value of the pixel at that coordinate
        /// 
        /// @param ZCTXY a struct that contains the Z, C, T, X, and Y coordinates of the pixel.
        /// @param index 0, 1, 2
        /// 
        /// @return A ushort value.
        public ushort GetValueRGB(ZCTXY coord, int index)
        {
            int ind = 0;
            if (coord.C >= SizeC)
            {
                coord.C = 0;
            }
            ind = GetFrameIndex(coord.Z, coord.C, coord.T);
            return (ushort)Buffers[ind].GetValue(coord.X, coord.Y, index);
        }
        /// > Get the value of the pixel at the given coordinates
        /// 
        /// @param ZCT Z is the Z-plane, C is the channel, T is the timepoint
        /// @param x x coordinate of the pixel
        /// @param y The y coordinate of the pixel
        /// 
        /// @return The value of the pixel at the given coordinates.
        public ushort GetValue(ZCT coord, int x, int y)
        {
            return GetValueRGB(new ZCTXY(coord.Z, coord.C, coord.T, x, y), 0);
        }
        /// > This function returns the value of the pixel at the specified ZCTXY coordinates
        /// 
        /// @param z The Z-plane of the image.
        /// @param c channel
        /// @param t time
        /// @param x x coordinate of the pixel
        /// @param y the y coordinate of the pixel
        /// 
        /// @return The value of the pixel at the given coordinates.
        public ushort GetValue(int z, int c, int t, int x, int y)
        {
            return GetValueRGB(new ZCTXY(z, c, t, x, y), 0);
        }
        /// > Get the value of a pixel at a given coordinate, x, y, and RGB index
        /// 
        /// @param ZCT The ZCT coordinate of the image.
        /// @param x x coordinate of the pixel
        /// @param y the y coordinate of the pixel
        /// @param RGBindex 0 = Red, 1 = Green, 2 = Blue
        /// 
        /// @return The value of the pixel at the given coordinates.
        public float GetValueRGB(ZCT coord, int x, int y, int RGBindex)
        {
            int i = GetFrameIndex(coord.Z, coord.C, coord.T);
            return Buffers[i].GetValue(x, y, RGBindex);
        }
        /// This function returns the value of the pixel at the specified coordinates in the specified
        /// channel, frame, and RGB index
        /// 
        /// @param z The Z-plane index
        /// @param c channel
        /// @param t time index
        /// @param x x coordinate of the pixel
        /// @param y The y coordinate of the pixel
        /// @param RGBindex 0 = Red, 1 = Green, 2 = Blue
        /// 
        /// @return The value of the pixel at the given coordinates.
        public float GetValueRGB(int z, int c, int t, int x, int y, int RGBindex)
        {
            return GetValueRGB(new ZCT(z, c, t), x, y, RGBindex);
        }
        /// It takes a coordinate and a value, and sets the value at that coordinate
        /// 
        /// @param ZCTXY a struct that contains the Z, C, T, X, and Y coordinates of the pixel
        /// @param value the value to be set
        public void SetValue(ZCTXY coord, ushort value)
        {
            int i = GetFrameIndex(coord.Z, coord.C, coord.T);
            Buffers[i].SetValue(coord.X, coord.Y, value);
        }
        /// It sets the value of a pixel in a buffer
        /// 
        /// @param x The x coordinate of the pixel to set.
        /// @param y The y coordinate of the pixel to set.
        /// @param ind The index of the buffer to set the value in.
        /// @param value The value to set the pixel to.
        public void SetValue(int x, int y, int ind, ushort value)
        {
            Buffers[ind].SetValue(x, y, value);
        }
        /// This function sets the value of a pixel at a given x,y coordinate in a given image plane
        /// 
        /// @param x x coordinate of the pixel
        /// @param y The y coordinate of the pixel to set.
        /// @param ZCT a struct that contains the Z, C, and T coordinates of the pixel
        /// @param value the value to set
        public void SetValue(int x, int y, ZCT coord, ushort value)
        {
            SetValue(x, y, GetFrameIndex(coord.Z, coord.C, coord.T), value);
        }
        /// It takes a coordinate, an RGB index, and a value, and sets the value of the pixel at that
        /// coordinate to the value
        /// 
        /// @param ZCTXY a struct that contains the Z, C, T, X, and Y coordinates of the pixel
        /// @param RGBindex 0 = Red, 1 = Green, 2 = Blue
        /// @param value the value to be set
        public void SetValueRGB(ZCTXY coord, int RGBindex, ushort value)
        {
            int ind = GetFrameIndex(coord.Z, coord.C, coord.T);
            Buffers[ind].SetValue(coord.X, coord.Y, RGBindex, value);
        }
        /// > This function returns a Bitmap object from the image data stored in the OME-TIFF file
        /// 
        /// @param ZCT Z = Z-stack, C = channel, T = timepoint
        /// 
        /// @return A Bitmap object.
        public Bitmap GetBitmap(ZCT coord)
        {
            return (Bitmap)GetImageByCoord(coord.Z, coord.C, coord.T);
        }
        /// > Get the image at the specified ZCT coordinate, and return a filtered version of it
        /// 
        /// @param ZCT a 3-tuple of integers (z, c, t)
        /// @param IntRange 
        /// @param IntRange 
        /// @param IntRange 
        /// 
        /// @return An UnmanagedImage object.
        public Bitmap GetFiltered(ZCT coord, IntRange r, IntRange g, IntRange b)
        {
            int index = GetFrameIndex(coord.Z, coord.C, coord.T);
            return GetFiltered(index, r, g, b);
        }
        public static IntRange MapIntRangeToByteRange(IntRange ushortRange)
        {
            // Ensure the range is valid
            if (ushortRange.Min < 0 || ushortRange.Max > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(ushortRange), "Range must be within 0 to ushort.MaxValue.");

            // Map each value in the range
            int mappedMin = (int)((ushortRange.Min / (float)ushort.MaxValue) * byte.MaxValue);
            int mappedMax = (int)((ushortRange.Max / (float)ushort.MaxValue) * byte.MaxValue);

            return new IntRange(mappedMin, mappedMax);
        }
        /// It takes an image, and returns a filtered version of that image
        /// 
        /// @param ind the index of the buffer to be filtered
        /// @param IntRange 
        /// @param IntRange 
        /// @param IntRange 
        /// 
        /// @return A filtered image.
        public Bitmap GetFiltered(int ind, IntRange r, IntRange g, IntRange b)
        {
            if (Buffers[ind].PixelFormat == PixelFormat.Float)
            {
                if (Statistics.StackMax <= 1)
                {
                    Bitmap bm = Buffers[ind].GetImageRGBA(true);
                    return bm;
                }
                else
                {
                    BioImage.filter8.InRed = MapIntRangeToByteRange(r);
                    BioImage.filter8.InGreen = MapIntRangeToByteRange(g);
                    BioImage.filter8.InBlue = MapIntRangeToByteRange(b);
                    Bitmap bm = BioImage.filter8.Apply(Buffers[ind].GetImageRGBA(false));
                    return bm;
                }
            }
            else
            if (Buffers[ind].BitsPerPixel > 8)
            {
                BioImage.filter16.InRed = r;
                BioImage.filter16.InGreen = g;
                BioImage.filter16.InBlue = b;
                Bitmap bm = BioImage.filter16.Apply(Buffers[ind]);
                return bm;
            }
            else
            {
                // set ranges
                BioImage.filter8.InRed = r;
                BioImage.filter8.InGreen = g;
                BioImage.filter8.InBlue = b;
                return BioImage.filter8.Apply(Buffers[ind]);
            }
        }
        /// It takes an image, and returns a channel of that image
        /// 
        /// @param ind the index of the buffer
        /// @param s 0, 1, 2
        public UnmanagedImage GetChannelImage(int ind, short s)
        {
            Bitmap bf = Buffers[ind];
            if (bf.isRGB)
            {
                if (s == 0)
                    return extractR.Apply(Buffers[ind].Image);
                else
                if (s == 1)
                    return extractG.Apply(Buffers[ind].Image);
                else
                    return extractB.Apply(Buffers[ind].Image);
            }
            else
                throw new InvalidOperationException();
        }
        /// > GetEmission() returns an UnmanagedImage object that is a composite of the emission
        /// channels
        /// 
        /// @param ZCT Z, C, T coordinates
        /// @param IntRange 
        /// @param IntRange 
        /// @param IntRange 
        /// 
        /// @return A Bitmap or an UnmanagedImage.
        public Bitmap GetEmission(ZCT coord, IntRange rf, IntRange gf, IntRange bf)
        {
            if (RGBChannelCount == 1)
            {
                Bitmap[] bs = new Bitmap[Channels.Count];
                List<Channel> chs = new List<Channel>();
                for (int c = 0; c < Channels.Count; c++)
                {
                    int index = GetFrameIndex(coord.Z, c, coord.T);
                    bs[c] = Buffers[index];
                    chs.Add(Channels[c]);
                }
                Bitmap bm = (Bitmap)Bitmap.GetEmissionBitmap(bs, chs.ToArray());
                return bm;
            }
            else
            {
                int index = GetFrameIndex(coord.Z, coord.C, coord.T);
                return Buffers[index];
            }
        }
        /// > Get the RGB bitmap for the specified ZCT coordinate
        /// 
        /// @param ZCT Z, C, T coordinates
        /// @param IntRange 
        /// @param IntRange 
        /// @param IntRange 
        /// 
        /// @return A Bitmap object.
        public Bitmap GetRGBBitmap(ZCT coord, IntRange rf, IntRange gf, IntRange bf)
        {
            int index = GetFrameIndex(coord.Z, 0, coord.T);
            if (Buffers[0].RGBChannelsCount == 1)
            {
                if (Channels.Count >= 3)
                {
                    Bitmap[] bs = new Bitmap[3];
                    bs[2] = Buffers[index + RChannel.Index];
                    bs[1] = Buffers[index + GChannel.Index];
                    bs[0] = Buffers[index + BChannel.Index];
                    return Bitmap.GetRGBBitmap(bs, rf, gf, bf);
                }
                else
                {
                    Bitmap[] bs = new Bitmap[3];
                    bs[2] = Buffers[index + RChannel.Index];
                    bs[1] = Buffers[index + RChannel.Index + 1];
                    bs[0] = Buffers[index + RChannel.Index + 2];
                    return Bitmap.GetRGBBitmap(bs, rf, gf, bf);
                }
            }
            else
            {
                if (Buffers[0].PixelFormat == PixelFormat.Float || Buffers[0].PixelFormat == PixelFormat.Short)
                {
                    return Buffers[index].GetImageRGBA();
                }
                else
                    return Buffers[index];
            }
        }
        /// It takes a byte array of RGB or RGBA data and converts it to a Bitmap
        /// 
        /// @param w width of the image
        /// @param h height of the image
        /// @param PixelFormat The pixel format of the image.
        /// @param bts the byte array of the image
        /// 
        /// @return A Bitmap object.
        public static unsafe Bitmap GetBitmapRGB(int w, int h, PixelFormat px, byte[] bts)
        {
            if (px == PixelFormat.Format32bppArgb)
            {
                //opening a 8 bit per pixel jpg image
                Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                //creating the bitmapdata and lock bits
                AForge.Rectangle rec = new AForge.Rectangle(0, 0, w, h);
                BitmapData bmd = bmp.LockBits(rec, ImageLockMode.ReadWrite, bmp.PixelFormat);
                //iterating through all the pixels in y direction
                for (int y = 0; y < h; y++)
                {
                    //getting the pixels of current row
                    byte* row = (byte*)bmd.Scan0 + (y * bmd.Stride);
                    int rowRGB = y * w * 4;
                    //iterating through all the pixels in x direction
                    for (int x = 0; x < w; x++)
                    {
                        int indexRGB = x * 4;
                        int indexRGBA = x * 4;
                        row[indexRGBA + 3] = bts[rowRGB + indexRGB + 3];//byte A
                        row[indexRGBA + 2] = bts[rowRGB + indexRGB + 2];//byte R
                        row[indexRGBA + 1] = bts[rowRGB + indexRGB + 1];//byte G
                        row[indexRGBA] = bts[rowRGB + indexRGB];//byte B
                    }
                }
                //unlocking bits and disposing image
                bmp.UnlockBits(bmd);
                return bmp;
            }
            else if (px == PixelFormat.Format24bppRgb)
            {
                //opening a 8 bit per pixel jpg image
                Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                //creating the bitmapdata and lock bits
                AForge.Rectangle rec = new AForge.Rectangle(0, 0, w, h);
                BitmapData bmd = bmp.LockBits(rec, ImageLockMode.ReadWrite, bmp.PixelFormat);
                //iterating through all the pixels in y direction
                for (int y = 0; y < h; y++)
                {
                    //getting the pixels of current row
                    byte* row = (byte*)bmd.Scan0 + (y * bmd.Stride);
                    int rowRGB = y * w * 3;
                    //iterating through all the pixels in x direction
                    for (int x = 0; x < w; x++)
                    {
                        int indexRGB = x * 3;
                        int indexRGBA = x * 4;
                        row[indexRGBA + 3] = byte.MaxValue;//byte A
                        row[indexRGBA + 2] = bts[rowRGB + indexRGB + 2];//byte R
                        row[indexRGBA + 1] = bts[rowRGB + indexRGB + 1];//byte G
                        row[indexRGBA] = bts[rowRGB + indexRGB];//byte B
                    }
                }
                //unlocking bits and disposing image
                bmp.UnlockBits(bmd);
                return bmp;
            }
            else
            if (px == PixelFormat.Format48bppRgb)
            {
                //opening a 8 bit per pixel jpg image
                Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                //creating the bitmapdata and lock bits
                AForge.Rectangle rec = new AForge.Rectangle(0, 0, w, h);
                BitmapData bmd = bmp.LockBits(rec, ImageLockMode.ReadWrite, bmp.PixelFormat);
                unsafe
                {
                    //iterating through all the pixels in y direction
                    for (int y = 0; y < h; y++)
                    {
                        //getting the pixels of current row
                        byte* row = (byte*)bmd.Scan0 + (y * bmd.Stride);
                        int rowRGB = y * w * 6;
                        //iterating through all the pixels in x direction
                        for (int x = 0; x < w; x++)
                        {
                            int indexRGB = x * 6;
                            int indexRGBA = x * 4;
                            int b = (int)((float)BitConverter.ToUInt16(bts, rowRGB + indexRGB) / 255);
                            int g = (int)((float)BitConverter.ToUInt16(bts, rowRGB + indexRGB + 2) / 255);
                            int r = (int)((float)BitConverter.ToUInt16(bts, rowRGB + indexRGB + 4) / 255);
                            row[indexRGBA + 3] = 255;//byte A
                            row[indexRGBA + 2] = (byte)(b);//byte R
                            row[indexRGBA + 1] = (byte)(g);//byte G
                            row[indexRGBA] = (byte)(r);//byte B
                        }
                    }
                }
                bmp.UnlockBits(bmd);
                return bmp;
            }
            else
            if (px == PixelFormat.Format8bppIndexed)
            {
                //opening a 8 bit per pixel jpg image
                Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                //creating the bitmapdata and lock bits
                AForge.Rectangle rec = new AForge.Rectangle(0, 0, w, h);
                BitmapData bmd = bmp.LockBits(rec, ImageLockMode.ReadWrite, bmp.PixelFormat);
                unsafe
                {
                    //iterating through all the pixels in y direction
                    for (int y = 0; y < h; y++)
                    {
                        //getting the pixels of current row
                        byte* row = (byte*)bmd.Scan0 + (y * bmd.Stride);
                        int rowRGB = y * w;
                        //iterating through all the pixels in x direction
                        for (int x = 0; x < w; x++)
                        {
                            int indexRGB = x;
                            int indexRGBA = x * 4;
                            byte b = bts[rowRGB + indexRGB];
                            row[indexRGBA + 3] = 255;//byte A
                            row[indexRGBA + 2] = (byte)(b);//byte R
                            row[indexRGBA + 1] = (byte)(b);//byte G
                            row[indexRGBA] = (byte)(b);//byte B
                        }
                    }
                }
                bmp.UnlockBits(bmd);
                return bmp;
            }
            else
            if (px == PixelFormat.Format16bppGrayScale)
            {
                //opening a 8 bit per pixel jpg image
                Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                //creating the bitmapdata and lock bits
                AForge.Rectangle rec = new AForge.Rectangle(0, 0, w, h);
                BitmapData bmd = bmp.LockBits(rec, ImageLockMode.ReadWrite, bmp.PixelFormat);
                unsafe
                {
                    //iterating through all the pixels in y direction
                    for (int y = 0; y < h; y++)
                    {
                        //getting the pixels of current row
                        byte* row = (byte*)bmd.Scan0 + (y * bmd.Stride);
                        int rowRGB = y * w * 2;
                        //iterating through all the pixels in x direction
                        for (int x = 0; x < w; x++)
                        {
                            int indexRGB = x * 2;
                            int indexRGBA = x * 4;
                            ushort b = (ushort)((float)BitConverter.ToUInt16(bts, rowRGB + indexRGB) / 255);
                            row[indexRGBA + 3] = 255;//byte A
                            row[indexRGBA + 2] = (byte)(b);//byte R
                            row[indexRGBA + 1] = (byte)(b);//byte G
                            row[indexRGBA] = (byte)(b);//byte B
                        }
                    }
                }
                bmp.UnlockBits(bmd);
                return bmp;
            }

            throw new NotSupportedException("Pixelformat " + px + " is not supported.");
        }
        public static Stopwatch swatch = new Stopwatch();
        /// > GetAnnotations() returns a list of ROI objects that are associated with the ZCT coordinate
        /// passed in as a parameter
        /// 
        /// @param ZCT a 3D coordinate (Z, C, T)
        /// 
        /// @return A list of ROI objects.
        public List<ROI> GetAnnotations(ZCT coord)
        {
            List<ROI> annotations = new List<ROI>();
            foreach (ROI an in Annotations)
            {
                if (an == null)
                    continue;
                if (an.coord == coord)
                    annotations.Add(an);
            }
            return annotations;
        }
        /// This function returns a list of ROI objects that are associated with the specified Z, C, and
        /// T coordinates
        /// 
        /// @param Z The Z-stack index
        /// @param C Channel
        /// @param T Time
        /// 
        /// @return A list of ROI objects.
        public List<ROI> GetAnnotations(int Z, int C, int T)
        {
            List<ROI> annotations = new List<ROI>();
            foreach (ROI an in Annotations)
            {
                if (an.coord.Z == Z && an.coord.Z == Z && an.coord.C == C && an.coord.T == T)
                    annotations.Add(an);
            }
            return annotations;
        }
        public bool Loading = false;
        /// <summary>
        /// Initializes ImageJ/Fiji with the given path.
        /// </summary>
        /// <param name="imageJPath"></param>
        public static void Initialize(string imageJPath)
        {
            //We initialize OME on a seperate thread so the user doesn't have to wait for initialization to
            //view images. 
            InitFactory();
            InitReader();
            InitWriter();
            if (imageJPath.Contains("Fiji"))
            {
                Fiji.Initialize(imageJPath);
            }
            else
                ImageJ.Initialize(imageJPath);
        }
        public static void Initialize()
        {
            //We initialize OME on a seperate thread so the user doesn't have to wait for initialization to
            //view images. 
            InitFactory();
            InitReader();
            InitWriter();
        }
        static bool inf = false, inr = false, inw = false;
        /// > Initialize the OME-XML library
        private static void InitFactory()
        {
            factory = new ServiceFactory();
            service = (OMEXMLService)factory.getInstance(typeof(OMEXMLService));
            inf = true;
            if (inf && inr && inw)
                initialized = true;
        }
        /// > Initialize the OME-XML library
        private static void InitReader()
        {
            reader = new ImageReader();
            inr = true;
            if (inf && inr && inw)
                initialized = true;
        }
        /// > Initialize the OME-XML library
        private static void InitWriter()
        {
            writer = new ImageWriter();
            inw = true;
            if (inf && inr && inw)
                initialized = true;
        }

        private static List<Resolution> GetResolutions(BioImage b)
        {
            ImageReader re = b.imRead;
            var meta = b.meta;
            List<Resolution> rss = new List<Resolution>();
            int sc = re.getSeriesCount();
            try
            {
                for (int s = 0; s < sc; s++)
                {
                    re.setSeries(s);
                    int ress = re.getResolutionCount();
                    for (int r = 0; r < ress; r++)
                    {
                        Status = "Reading resolution " + (r + 1).ToString() + "/" + ress;
                        Resolution res = new Resolution();
                        int rgbc = re.getRGBChannelCount();
                        int bps = re.getBitsPerPixel();
                        PixelFormat px;
                        try
                        {
                            px = GetPixelFormat(rgbc, meta.getPixelsType(r));
                        }
                        catch (Exception ex)
                        {
                            px = GetPixelFormat(rgbc, bps);
                            Console.WriteLine(ex.Message);
                        }
                        res.PixelFormat = px;
                        res.SizeX = re.getSizeX();
                        res.SizeY = re.getSizeY();
                        res.PhysicalSizeX = (96 / 2.54) / 1000;
                        res.PhysicalSizeY = (96 / 2.54) / 1000;
                        res.PhysicalSizeZ = b.frameInterval;
                        //For some reason trying to get undefined stage coordinates or physical size causing a bus error on MacOS so we won't look for them on MacOS.
                        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                        {
                            try
                            {
                                if (meta.getPixelsPhysicalSizeX(r) != null)
                                {
                                    res.PhysicalSizeX = meta.getPixelsPhysicalSizeX(r).value().doubleValue();
                                }
                                if (meta.getPixelsPhysicalSizeY(r) != null)
                                {
                                    res.PhysicalSizeY = meta.getPixelsPhysicalSizeY(r).value().doubleValue();
                                }
                                if (meta.getPixelsPhysicalSizeZ(r) != null)
                                {
                                    res.PhysicalSizeZ = meta.getPixelsPhysicalSizeZ(r).value().doubleValue();
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.Message);
                            }
                            if (!b.isPyramidal)
                            {
                                try
                                {
                                    if (meta.getStageLabelX(r) != null)
                                        res.StageSizeX = meta.getStageLabelX(r).value().doubleValue();
                                    if (meta.getStageLabelY(r) != null)
                                        res.StageSizeY = meta.getStageLabelY(r).value().doubleValue();
                                    if (meta.getStageLabelZ(r) != null)
                                        res.StageSizeZ = meta.getStageLabelZ(r).value().doubleValue();
                                    else
                                        res.StageSizeZ = 0;
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.Message);
                                    res.StageSizeX = 0;
                                    res.StageSizeY = 0;
                                }

                            }
                            if (b.frameInterval == 0 && meta.getPixelsPhysicalSizeZ(r) != null)
                            {
                                res.PhysicalSizeZ = meta.getPixelsPhysicalSizeZ(r).value().doubleValue();
                            }
                            else
                            {
                                res.PhysicalSizeZ = b.frameInterval;
                            }
                        }
                        else //IF OSX
                        //We need to determine if this image is pyramidal or not.
                        //For some reason trying to get undefined stage coordinates cause a bus error on MacOS so we won't look for them on MacOS.
                        {
                            if (!b.isPyramidal)
                            {
                                try
                                {
                                    if (meta.getPixelsPhysicalSizeX(r) != null)
                                    {
                                        res.PhysicalSizeX = meta.getPixelsPhysicalSizeX(r).value().doubleValue();
                                    }
                                    if (meta.getPixelsPhysicalSizeY(r) != null)
                                    {
                                        res.PhysicalSizeY = meta.getPixelsPhysicalSizeY(r).value().doubleValue();
                                    }
                                    if (meta.getPixelsPhysicalSizeZ(r) != null)
                                    {
                                        res.PhysicalSizeZ = meta.getPixelsPhysicalSizeZ(r).value().doubleValue();
                                    }
                                    else
                                        res.PhysicalSizeZ = 1.0;
                                    /* 
                                    if (meta.getStageLabelX(r) != null)
                                    res.StageSizeX = meta.getStageLabelX(r).value().doubleValue();
                                    if (meta.getStageLabelY(r) != null)
                                    res.StageSizeY = meta.getStageLabelY(r).value().doubleValue();
                                    if (meta.getStageLabelZ(r) != null)
                                    res.StageSizeZ = meta.getStageLabelZ(r).value().doubleValue();
                                    else
                                    res.StageSizeZ = 1;
                                    */

                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex);
                                }

                            }

                        }
                        rss.Add(res);
                    }
                    Console.WriteLine("Series: " + s + " Resolutions:" + ress);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return rss;
        }
        private static int GetPyramidCount(Resolution[] rss)
        {
            //We need to determine if this image is pyramidal or not.
            //We do this by seeing if the resolutions are downsampled or not.
            //We also need to determine number of pyramids in this image and which belong to the series we are opening.
            List<Tuple<int, int>> prs = new List<Tuple<int, int>>();
            int? sr = null;
            for (int r = 0; r < rss.Length - 1; r++)
            {
                if (rss[r].SizeX > rss[r + 1].SizeX && rss[r].PixelFormat == rss[r + 1].PixelFormat)
                {
                    if (sr == null)
                    {
                        sr = r;
                        prs.Add(new Tuple<int, int>(r, 0));
                    }
                }
                else
                {
                    if (rss[prs[prs.Count - 1].Item1].PixelFormat == rss[r].PixelFormat)
                        prs[prs.Count - 1] = new Tuple<int, int>(prs[prs.Count - 1].Item1, r);
                    sr = null;
                }
            }
            return prs.Count;
        }
        /// This function takes a string array of file names and a string ID and saves the files to the
        /// database
        /// 
        /// @param file The file path to the file you want to save.
        /// @param ID The ID of the series you want to save.
        public static void SaveFile(string file, string ID)
        {
            string[] sts = new string[1];
            sts[0] = ID;
            SaveSeries(sts, file);
        }
        /// It takes a list of image IDs, and saves them as a single multi-page TIFF file.
        /// 
        /// @param An array of IDs of the images to save
        /// @param The path to the file to save to.
        public static void SaveSeries(string[] IDs, string file)
        {
            string desc = "";
            int stride = 0;
            ImageJDesc j = new ImageJDesc();
            BioImage bi = Images.GetImage(IDs[0]);
            j.FromImage(bi);
            desc = j.GetString();
            for (int fi = 0; fi < IDs.Length; fi++)
            {
                string id = IDs[fi];
                BioImage b = Images.GetImage(id);
                string fn = Path.GetFileNameWithoutExtension(id);
                string dir = Path.GetDirectoryName(file);
                stride = b.Buffers[0].Stride;

                //Save ROIs to CSV file.
                if (b.Annotations.Count > 0)
                {
                    string f = dir + "//" + fn + ".csv";
                    ExportROIsCSV(f, b.Annotations);
                }

                //Embed ROI's to image description.
                for (int i = 0; i < b.Annotations.Count; i++)
                {
                    desc += "-ROI:" + b.series + ":" + ROIToString(b.Annotations[i]) + NewLine;
                }
                foreach (Channel c in b.Channels)
                {
                    string cj = JsonConvert.SerializeObject(c.info, Formatting.None);
                    desc += "-Channel:" + fi + ":" + cj + NewLine;
                }
                string json = JsonConvert.SerializeObject(b.imageInfo, Formatting.None);
                desc += "-ImageInfo:" + fi + ":" + json + NewLine;
            }

            Tiff image = Tiff.Open(file, "w");
            for (int fi = 0; fi < IDs.Length; fi++)
            {
                int im = 0;
                string id = IDs[fi];
                BioImage b = Images.GetImage(id);
                int sizec = 1;
                if (!b.isRGB)
                {
                    sizec = b.SizeC;
                }
                byte[] buffer;
                for (int c = 0; c < sizec; c++)
                {
                    for (int z = 0; z < b.SizeZ; z++)
                    {
                        for (int t = 0; t < b.SizeT; t++)
                        {
                            image.SetDirectory((short)(im + (b.Buffers.Count * fi)));
                            image.SetField(TiffTag.IMAGEWIDTH, b.SizeX);
                            image.SetField(TiffTag.IMAGEDESCRIPTION, desc);
                            image.SetField(TiffTag.IMAGELENGTH, b.SizeY);
                            image.SetField(TiffTag.BITSPERSAMPLE, b.bitsPerPixel);
                            image.SetField(TiffTag.SAMPLESPERPIXEL, b.RGBChannelCount);
                            image.SetField(TiffTag.ROWSPERSTRIP, b.SizeY);
                            image.SetField(TiffTag.ORIENTATION, BitMiracle.LibTiff.Classic.Orientation.TOPLEFT);
                            image.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
                            image.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
                            image.SetField(TiffTag.ROWSPERSTRIP, image.DefaultStripSize(0));
                            if (b.PhysicalSizeX != -1 && b.PhysicalSizeY != -1)
                            {
                                image.SetField(TiffTag.XRESOLUTION, (b.PhysicalSizeX * b.SizeX) / ((b.PhysicalSizeX * b.SizeX) * b.PhysicalSizeX));
                                image.SetField(TiffTag.YRESOLUTION, (b.PhysicalSizeY * b.SizeY) / ((b.PhysicalSizeY * b.SizeY) * b.PhysicalSizeY));
                                image.SetField(TiffTag.RESOLUTIONUNIT, ResUnit.NONE);
                            }
                            else
                            {
                                image.SetField(TiffTag.XRESOLUTION, 100.0);
                                image.SetField(TiffTag.YRESOLUTION, 100.0);
                                image.SetField(TiffTag.RESOLUTIONUNIT, ResUnit.INCH);
                            }
                            // specify that it's a page within the multipage file
                            image.SetField(TiffTag.SUBFILETYPE, FileType.PAGE);
                            // specify the page number
                            buffer = b.Buffers[im].GetSaveBytes(true);
                            image.SetField(TiffTag.PAGENUMBER, im + (b.Buffers.Count * fi), b.Buffers.Count * IDs.Length);
                            for (int i = 0, offset = 0; i < b.SizeY; i++)
                            {
                                image.WriteScanline(buffer, offset, i, 0);
                                offset += stride;
                            }
                            image.WriteDirectory();
                            im++;
                        }
                    }
                }
            }
            image.Dispose();
        }
        /// It opens a tiff file, reads the number of pages, reads the number of channels, and then
        /// reads each page into a BioImage object.
        /// 
        /// @param file the path to the file
        /// @param tab open image in new tab.
        /// @return An array of BioImage objects.
        public static BioImage[] OpenSeries(string file, bool tab)
        {
            Tiff image = Tiff.Open(file, "r");
            int pages = image.NumberOfDirectories();
            FieldValue[] f = image.GetField(TiffTag.IMAGEDESCRIPTION);
            int sp = image.GetField(TiffTag.SAMPLESPERPIXEL)[0].ToInt();
            ImageJDesc imDesc = new ImageJDesc();
            int count = 1;
            if (f != null)
            {
                string desc = f[0].ToString();
                if (desc.StartsWith("ImageJ"))
                {
                    imDesc.SetString(desc);
                    if (imDesc.channels != 0)
                        count = imDesc.channels;
                }
            }
            int scount = (pages * sp) / count;
            BioImage[] bs = new BioImage[pages];
            image.Close();
            for (int i = 0; i < pages; i++)
            {
                bs[i] = OpenFile(file, i, tab, true);
            }
            return bs;
        }
        /// This function opens a file and returns a BioImage object
        /// 
        /// @param file The path to the file to open.
        /// 
        /// @return A BioImage object.
        public static BioImage OpenFile(string file)
        {
            return OpenFile(file, 0, true, true);
        }
        public static BioImage OpenFile(string file, bool tab)
        {
            return OpenFile(file, 0, tab, true);
        }
        /// It opens a TIFF file and returns a BioImage object
        /// 
        /// @param file the file path
        /// @param series the series number of the image to open
        /// 
        /// @return A BioImage object.
        public static BioImage OpenFile(string file, int series, bool tab, bool addToImages)
        {
            return OpenFile(file, series, tab, addToImages, false, 0, 0, 0, 0);
        }
        static bool IsTiffTiled(string imagePath)
        {
            if (imagePath.EndsWith(".tif") || imagePath.EndsWith(".tiff"))
            {
                using (Tiff tiff = Tiff.Open(imagePath, "r"))
                {
                    if (tiff == null)
                    {
                        throw new Exception("Failed to open TIFF image.");
                    }
                    bool t = tiff.IsTiled();
                    tiff.Close();
                    return t;
                }
            }
            else return false;
        }
        /// The function `InitDirectoryResolution` initializes the resolution properties of a `BioImage`
        /// object based on the resolution information stored in a TIFF image.
        /// 
        /// @param BioImage BioImage is a class representing a bioimage, which contains information
        /// about the image resolution, size, and pixel format.
        /// @param Tiff The "Tiff" parameter is an object of the Tiff class. It represents a TIFF image
        /// file and provides methods and properties to access and manipulate the image data.
        /// @param ImageJDesc ImageJDesc is a class that contains information about the image dimensions
        /// and resolution in ImageJ format. It has the following properties:
        static void InitDirectoryResolution(BioImage b, Tiff image, ImageJDesc jdesc = null)
        {
            Resolution res = new Resolution();
            FieldValue[] rs = image.GetField(TiffTag.RESOLUTIONUNIT);
            string unit = "NONE";
            if (rs != null)
                unit = rs[0].ToString();
            if (jdesc != null)
                res.PhysicalSizeZ = jdesc.finterval;
            else
                res.PhysicalSizeZ = 2.54 / 96;
            if (unit == "CENTIMETER")
            {
                if (image.GetField(TiffTag.XRESOLUTION) != null)
                {
                    double x = image.GetField(TiffTag.XRESOLUTION)[0].ToDouble();
                    res.PhysicalSizeX = (1000 / x);
                }
                if (image.GetField(TiffTag.YRESOLUTION) != null)
                {
                    double y = image.GetField(TiffTag.YRESOLUTION)[0].ToDouble();
                    res.PhysicalSizeY = (1000 / y);
                }
            }
            else
            if (unit == "INCH")
            {
                if (image.GetField(TiffTag.XRESOLUTION) != null)
                {
                    double x = image.GetField(TiffTag.XRESOLUTION)[0].ToDouble();
                    res.PhysicalSizeX = (2.54 / x) / 2.54;
                }
                if (image.GetField(TiffTag.YRESOLUTION) != null)
                {
                    double y = image.GetField(TiffTag.YRESOLUTION)[0].ToDouble();
                    res.PhysicalSizeY = (2.54 / y) / 2.54;
                }
            }
            else
            if (unit == "NONE")
            {
                if (jdesc != null && jdesc.ImageJ != null)
                {
                    if (jdesc.unit == "micron")
                    {
                        if (image.GetField(TiffTag.XRESOLUTION) != null)
                        {
                            double x = image.GetField(TiffTag.XRESOLUTION)[0].ToDouble();
                            res.PhysicalSizeX = (2.54 / x) / 2.54;
                        }
                        if (image.GetField(TiffTag.YRESOLUTION) != null)
                        {
                            double y = image.GetField(TiffTag.YRESOLUTION)[0].ToDouble();
                            res.PhysicalSizeY = (2.54 / y) / 2.54;
                        }
                    }
                }
                else
                {
                    res.PhysicalSizeX = 2.54 / 96;
                    res.PhysicalSizeY = 2.54 / 96;
                    res.PhysicalSizeZ = 2.54 / 96;
                }
            }
            res.SizeX = image.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
            res.SizeY = image.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
            int bitsPerPixel = image.GetField(TiffTag.BITSPERSAMPLE)[0].ToInt();
            int RGBChannelCount = image.GetField(TiffTag.SAMPLESPERPIXEL)[0].ToInt();
            res.PixelFormat = GetPixelFormat(RGBChannelCount, bitsPerPixel);
            b.Resolutions.Add(res);
        }

        /// The OpenFile function opens a BioImage file, reads its metadata, and loads the image
        /// data into a BioImage object.
        /// 
        /// @param file The file path of the bioimage to be opened.
        /// @param series The series parameter is an integer that specifies the series number of the
        /// image to open.
        /// @param tab The "tab" parameter is a boolean value that determines whether the BioImage
        /// should be opened in a new tab or not. If set to true, the BioImage will be opened in a new
        /// tab. If set to false, the BioImage will be opened in the current tab.
        /// @param addToImages A boolean value indicating whether the BioImage should be added to the
        /// Images collection.
        /// @param tile The "tile" parameter is a boolean value that determines whether the image should
        /// be opened as a tiled image or not. If set to true, the image will be opened as a tiled
        /// image. If set to false, the image will be opened as a regular image.
        /// @param tileX The `tileX` parameter is an integer that represents the starting X coordinate
        /// of the tile. It is used when opening a tiled image to specify the position of the tile
        /// within the image.
        /// @param tileY The `tileY` parameter is used to specify the starting Y coordinate of the tile
        /// when opening a tiled image. It determines the position of the tile within the image.
        /// @param tileSizeX The tileSizeX parameter is the width of each tile in pixels. It is used
        /// when opening a tiled TIFF image to specify the size of the tiles.
        /// @param tileSizeY The tileSizeY parameter is the height of each tile in pixels when the image
        /// is tiled.
        /// 
        /// @return The method is returning a BioImage object.
        public static BioImage OpenFile(string file, int series, bool tab, bool addToImages, bool tile, int tileX, int tileY, int tileSizeX, int tileSizeY)
        {
            string fs = file.Replace("\\", "/");
            vips = VipsSupport(file);
            Console.WriteLine("Opening BioImage: " + file);
            if (file.EndsWith(".npy"))
                return BioImage.FromNumpy(file);
            bool ome = isOME(file);
            if (ome) return OpenOME(file, series, tab, addToImages, tile, tileX, tileY, tileSizeX, tileSizeY);
            bool tiled = IsTiffTiled(file);
            Console.WriteLine("IsTiled=" + tiled.ToString());
            tile = tiled;

            Stopwatch st = new Stopwatch();
            st.Start();
            Status = "Opening Image";
            progFile = file;
            BioImage b = new BioImage(file);
            if (tiled && file.EndsWith(".tif") && !file.EndsWith(".ome.tif"))
            {
                //To open this we need libvips
                vips = VipsSupport(b.file);
            }
            b.series = series;
            b.file = file;
            if (tiled)
                b.Type = ImageType.pyramidal;
            string fn = Path.GetFileNameWithoutExtension(file);
            string dir = Path.GetDirectoryName(file);
            if (File.Exists(fn + ".csv"))
            {
                string f = dir + "//" + fn + ".csv";
                b.Annotations = BioImage.ImportROIsCSV(f);
            }
            if (file.EndsWith("tif") || file.EndsWith("tiff") || file.EndsWith("TIF") || file.EndsWith("TIFF"))
            {
                Tiff image = Tiff.Open(file, "r");
                b.tifRead = image;
                int SizeX = image.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
                int SizeY = image.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
                b.bitsPerPixel = image.GetField(TiffTag.BITSPERSAMPLE)[0].ToInt();
                b.littleEndian = !image.IsBigEndian();
                int RGBChannelCount = image.GetField(TiffTag.SAMPLESPERPIXEL)[0].ToInt();
                string desc = "";

                FieldValue[] f = image.GetField(TiffTag.IMAGEDESCRIPTION);
                desc = f[0].ToString();
                ImageJDesc imDesc = null;
                b.sizeC = 1;
                b.sizeT = 1;
                b.sizeZ = 1;
                bool imagej = false;
                if (f != null && !tile)
                {
                    imDesc = new ImageJDesc();
                    desc = f[0].ToString();
                    if (desc.StartsWith("ImageJ"))
                    {
                        imDesc.SetString(desc);
                        if (imDesc.channels != 0)
                            b.sizeC = imDesc.channels;
                        else
                            b.sizeC = 1;
                        if (imDesc.slices != 0)
                            b.sizeZ = imDesc.slices;
                        else
                            b.sizeZ = 1;
                        if (imDesc.frames != 0)
                            b.sizeT = imDesc.frames;
                        else
                            b.sizeT = 1;
                        if (imDesc.finterval != 0)
                            b.frameInterval = imDesc.finterval;
                        else
                            b.frameInterval = 1;
                        if (imDesc.spacing != 0)
                            b.imageInfo.PhysicalSizeZ = imDesc.spacing;
                        else
                            b.imageInfo.PhysicalSizeZ = 1;
                        imagej = true;
                    }
                }
                int stride = 0;
                PixelFormat PixelFormat;
                if (RGBChannelCount == 1)
                {
                    if (b.bitsPerPixel > 8)
                    {
                        PixelFormat = PixelFormat.Format16bppGrayScale;
                        stride = SizeX * 2;
                    }
                    else
                    {
                        PixelFormat = PixelFormat.Format8bppIndexed;
                        stride = SizeX;
                    }
                }
                else
                if (RGBChannelCount == 3)
                {
                    b.sizeC = 1;
                    if (b.bitsPerPixel > 8)
                    {
                        PixelFormat = PixelFormat.Format48bppRgb;
                        stride = SizeX * 2 * 3;
                    }
                    else
                    {
                        PixelFormat = PixelFormat.Format24bppRgb;
                        stride = SizeX * 3;
                    }
                }
                else
                {
                    PixelFormat = PixelFormat.Format32bppArgb;
                    stride = SizeX * 4;
                }

                string[] sts = desc.Split('\n');
                int index = 0;
                for (int i = 0; i < sts.Length; i++)
                {
                    if (sts[i].StartsWith("-Channel"))
                    {
                        string val = sts[i].Substring(9);
                        val = val.Substring(0, val.IndexOf(':'));
                        int serie = int.Parse(val);
                        if (serie == series && sts[i].Length > 7)
                        {
                            string cht = sts[i].Substring(sts[i].IndexOf('{'), sts[i].Length - sts[i].IndexOf('{'));
                            Channel.ChannelInfo info = JsonConvert.DeserializeObject<Channel.ChannelInfo>(cht);
                            Channel ch = new Channel(index, b.bitsPerPixel, info.SamplesPerPixel);
                            ch.info = info;
                            b.Channels.Add(ch);
                            if (index == 0)
                            {
                                b.rgbChannels[0] = 0;
                            }
                            else
                            if (index == 1)
                            {
                                b.rgbChannels[1] = 1;
                            }
                            else
                            if (index == 2)
                            {
                                b.rgbChannels[2] = 2;
                            }
                            index++;
                        }
                    }
                    else
                    if (sts[i].StartsWith("-ROI"))
                    {
                        string val = sts[i].Substring(5);
                        val = val.Substring(0, val.IndexOf(':'));
                        int serie = int.Parse(val);
                        if (serie == series && sts[i].Length > 7)
                        {
                            string s = sts[i].Substring(sts[i].IndexOf("ROI:") + 4, sts[i].Length - (sts[i].IndexOf("ROI:") + 4));
                            string ro = s.Substring(s.IndexOf(":") + 1, s.Length - (s.IndexOf(':') + 1));
                            ROI roi = StringToROI(ro);
                            b.Annotations.Add(roi);
                        }
                    }
                    else
                    if (sts[i].StartsWith("-ImageInfo"))
                    {
                        string val = sts[i].Substring(11);
                        val = val.Substring(0, val.IndexOf(':'));
                        int serie = int.Parse(val);
                        if (serie == series && sts[i].Length > 10)
                        {
                            string cht = sts[i].Substring(sts[i].IndexOf('{'), sts[i].Length - sts[i].IndexOf('{'));
                            b.imageInfo = JsonConvert.DeserializeObject<ImageInfo>(cht);
                        }
                    }
                }
                b.Coords = new int[b.SizeZ, b.SizeC, b.SizeT];
                if (tiled && tileSizeX == 0 && tileSizeY == 0)
                {
                    tileSizeX = 1920;
                    tileSizeY = 1080;
                }

                //If this is a tiff file not made by Bio we init channels based on RGBChannels.
                if (b.Channels.Count == 0)
                    b.Channels.Add(new Channel(0, b.bitsPerPixel, RGBChannelCount));

                //Lets check to see the channels are correctly defined in this file
                for (int ch = 0; ch < b.Channels.Count; ch++)
                {
                    if (b.Channels[ch].SamplesPerPixel != RGBChannelCount)
                    {
                        b.Channels[ch].SamplesPerPixel = RGBChannelCount;
                    }
                }

                b.Buffers = new List<Bitmap>();
                int pages = image.NumberOfDirectories() / b.seriesCount;
                if (!imagej)
                    b.sizeZ = pages;
                int str = image.ScanlineSize();
                bool inter = true;
                if (stride != str)
                    inter = false;
                InitDirectoryResolution(b, image, imDesc);
                if (tiled)
                {
                    Console.WriteLine("Opening tiles.");
                    if (vips)
                        OpenVips(b);
                    for (int t = 0; t < b.SizeT; t++)
                    {
                        for (int c = 0; c < b.SizeC; c++)
                        {
                            for (int z = 0; z < b.SizeZ; z++)
                            {
                                Bitmap bmp = GetTile(b, b.GetFrameIndex(z, c, t), b.level, tileX, tileY, tileSizeX, tileSizeY);
                                b.Buffers.Add(bmp);
                                bmp.Stats = Statistics.FromBytes(bmp);
                            }
                        }
                    }
                    Console.WriteLine("Calculating statisitics.");
                }
                else
                {
                    for (int p = series * pages; p < (series + 1) * pages; p++)
                    {
                        image.SetDirectory((short)p);

                        byte[] bytes = new byte[stride * SizeY];
                        for (int im = 0, offset = 0; im < SizeY; im++)
                        {
                            image.ReadScanline(bytes, offset, im, 0);
                            offset += stride;
                        }
                        Bitmap inf = new Bitmap(file, SizeX, SizeY, b.Resolutions[series].PixelFormat, bytes, new ZCT(0, 0, 0), p, null, b.littleEndian, inter);
                        b.Buffers.Add(inf);
                        inf.Stats = Statistics.FromBytes(inf);
                    }
                }
                image.Close();
                b.UpdateCoords();
            }
            else
            {
                Gdk.Pixbuf pf = new Gdk.Pixbuf(file);
                b.littleEndian = BitConverter.IsLittleEndian;
                PixelFormat px = GetPixelFormat(pf.NChannels, pf.BitsPerSample);
                b.Resolutions.Add(new Resolution(pf.Width, pf.Height, px, 96 * (1 / 2.54) / 1000, 96 * (1 / 2.54) / 1000, 96 * (1 / 2.54) / 1000, 0, 0, 0));
                b.bitsPerPixel = pf.BitsPerSample;
                b.Buffers.Add(new Bitmap(pf.Width, pf.Height, pf.Width * pf.NChannels, px, pf.Pixels));
                b.Buffers.Last().ID = Bitmap.CreateID(file, 0);
                b.Buffers.Last().Stats = Statistics.FromBytes(b.Buffers.Last());
                b.Channels.Add(new Channel(0, b.bitsPerPixel, b.RGBChannelCount));
                b.Coords = new int[1, 1, 1];
                b.sizeC = 1;
                b.sizeT = 1;
                b.sizeZ = 1;
            }
            if (b.StageSizeX == -1)
            {
                b.imageInfo.Series = 0;
                b.StageSizeX = 0;
                b.StageSizeY = 0;
                b.StageSizeZ = 0;
            }
            b.Volume = new VolumeD(new Point3D(b.StageSizeX, b.StageSizeY, b.StageSizeZ), new Point3D(b.PhysicalSizeX * b.SizeX, b.PhysicalSizeY * b.SizeY, b.PhysicalSizeZ * b.SizeZ));

            //If file is ome and we have OME support then check for annotation in metadata.
            if (ome)
            {
                b.Annotations.AddRange(OpenOMEROIs(file, series));
            }
            AutoThreshold(b, false);
            if (b.bitsPerPixel > 8)
                b.StackThreshold(true);
            else
                b.StackThreshold(false);
            if (addToImages)
                Images.AddImage(b);
            //pr.Close();
            //pr.Dispose();
            st.Stop();
            b.loadTimeMS = st.ElapsedMilliseconds;
            Console.WriteLine("BioImage loaded " + b.ToString());
            BioLib.Recorder.Record(BioLib.Recorder.GetCurrentMethodInfo(), false, file, series, tab, addToImages, tile, tileX, tileY, tileSizeX, tileSizeY);
            return b;
        }
        /// > The function checks if the image is a Tiff image and if it is, it checks if the image is a
        /// series of images
        /// 
        /// @param file the path to the file
        /// 
        /// @return a boolean value.
        public static bool isTiffSeries(string file)
        {
            Tiff image = Tiff.Open(file, "r");
            string desc = "";
            FieldValue[] f = image.GetField(TiffTag.IMAGEDESCRIPTION);
            image.Close();
            string[] sts = desc.Split('\n');
            int index = 0;
            for (int i = 0; i < sts.Length; i++)
            {
                if (sts[i].StartsWith("-ImageInfo"))
                {
                    string val = sts[i].Substring(11);
                    val = val.Substring(0, val.IndexOf(':'));
                    int serie = int.Parse(val);
                    if (sts[i].Length > 10)
                    {
                        string cht = sts[i].Substring(sts[i].IndexOf('{'), sts[i].Length - sts[i].IndexOf('{'));
                        ImageInfo info = JsonConvert.DeserializeObject<ImageInfo>(cht);
                        if (info.Series > 1)
                            return true;
                        else
                            return false;
                    }
                }
            }
            return false;
        }
        /// If the file is a TIFF, check the ImageDescription tag for the string "OME-XML". If it's
        /// there, return true. If it's not a TIFF, return true. If it's a PNG, JPG, JPEG, or BMP,
        /// return false
        /// 
        /// @param file the path to the image file
        /// 
        /// @return A boolean value.
        public static bool isOME(string file)
        {
            if (file.EndsWith("ome.tif") || file.EndsWith("ome.tiff"))
            {
                return true;
            }
            if (file.EndsWith(".tif") || file.EndsWith(".TIF") || file.EndsWith("tiff") || file.EndsWith("TIFF"))
            {
                Tiff image = Tiff.Open(file, "r");
                string desc = "";
                FieldValue[] f = image.GetField(TiffTag.IMAGEDESCRIPTION);
                desc = f[0].ToString();
                image.Close();
                if (desc.Contains("OME-XML"))
                    return true;
                else
                    return false;
            }
            if ((file.EndsWith("png") || file.EndsWith("PNG") || file.EndsWith("jpg") || file.EndsWith("JPG") ||
                file.EndsWith("jpeg") || file.EndsWith("JPEG") || file.EndsWith("bmp") || file.EndsWith("BMP")))
            {
                return false;
            }
            else return true;
        }
        /// > If the file is an OME file and has more than one series, return true
        /// 
        /// @param file the file to be checked
        /// 
        /// @return A boolean value.
        public static bool isOMESeries(string file)
        {
            if (!isOME(file))
                return false;
            ImageReader reader = new ImageReader();
            var meta = (IMetadata)((OMEXMLService)new ServiceFactory().getInstance(typeof(OMEXMLService))).createOMEXMLMetadata();
            reader.setMetadataStore((MetadataStore)meta);
            file = file.Replace("\\", "/");
            reader.setId(file);
            bool ser = false;
            if (reader.getSeriesCount() > 1)
                ser = true;
            reader.close();
            reader = null;
            return ser;
        }
        public static void SaveOME(string file, string ID)
        {
            BioImage[] sts = new BioImage[1];
            sts[0] = Images.GetImage(ID);
            SaveOMESeries(sts, file, BioImage.Planes);
        }

        public static void SaveOME(BioImage image, string file)
        {
            BioImage[] sts = new BioImage[1];
            sts[0] = image;
            SaveOMESeries(sts, file, BioImage.Planes);
        }
        /// This function takes a list of image files and saves them as a single OME-TIFF file
        /// 
        /// @param files an array of file paths to the images to be saved
        /// @param f the file name to save to
        /// @param planes if true, the planes will be saved as well.
        public static void SaveOMESeries(BioImage[] files, string f, bool planes)
        {
            if (File.Exists(f))
                File.Delete(f);
            loci.formats.meta.IMetadata omexml = service.createOMEXMLMetadata();
            Status = "Saving OME Image Metadata.";
            for (int fi = 0; fi < files.Length; fi++)
            {
                int serie = fi;
                BioImage b = files[fi];
                // create OME-XML metadata store

                omexml.setImageID("Image:" + serie, serie);
                omexml.setPixelsID("Pixels:" + serie, serie);
                omexml.setPixelsInterleaved(java.lang.Boolean.TRUE, serie);
                omexml.setPixelsDimensionOrder(ome.xml.model.enums.DimensionOrder.XYCZT, serie);
                if (b.bitsPerPixel > 8)
                    omexml.setPixelsType(ome.xml.model.enums.PixelType.UINT16, serie);
                else
                    omexml.setPixelsType(ome.xml.model.enums.PixelType.UINT8, serie);
                omexml.setPixelsSizeX(new PositiveInteger(java.lang.Integer.valueOf(b.SizeX)), serie);
                omexml.setPixelsSizeY(new PositiveInteger(java.lang.Integer.valueOf(b.SizeY)), serie);
                omexml.setPixelsSizeZ(new PositiveInteger(java.lang.Integer.valueOf(b.SizeZ)), serie);
                omexml.setPixelsSizeC(new PositiveInteger(java.lang.Integer.valueOf(b.SizeC)), serie);
                omexml.setPixelsSizeT(new PositiveInteger(java.lang.Integer.valueOf(b.SizeT)), serie);
                if (BitConverter.IsLittleEndian)
                    omexml.setPixelsBigEndian(java.lang.Boolean.FALSE, serie);
                else
                    omexml.setPixelsBigEndian(java.lang.Boolean.TRUE, serie);
                ome.units.quantity.Length p1 = new ome.units.quantity.Length(java.lang.Double.valueOf(b.PhysicalSizeX), ome.units.UNITS.MICROMETER);
                omexml.setPixelsPhysicalSizeX(p1, serie);
                ome.units.quantity.Length p2 = new ome.units.quantity.Length(java.lang.Double.valueOf(b.PhysicalSizeY), ome.units.UNITS.MICROMETER);
                omexml.setPixelsPhysicalSizeY(p2, serie);
                ome.units.quantity.Length p3 = new ome.units.quantity.Length(java.lang.Double.valueOf(b.PhysicalSizeZ), ome.units.UNITS.MICROMETER);
                if (b.PhysicalSizeZ == 0)
                    omexml.setPixelsPhysicalSizeZ(new ome.units.quantity.Length(java.lang.Double.valueOf(1), ome.units.UNITS.MICROMETER), serie);
                else
                    omexml.setPixelsPhysicalSizeZ(new ome.units.quantity.Length(java.lang.Double.valueOf(b.PhysicalSizeZ), ome.units.UNITS.MICROMETER), serie);
                ome.units.quantity.Length s1 = new ome.units.quantity.Length(java.lang.Double.valueOf(b.Volume.Location.X), ome.units.UNITS.MICROMETER);
                omexml.setStageLabelX(s1, serie);
                ome.units.quantity.Length s2 = new ome.units.quantity.Length(java.lang.Double.valueOf(b.Volume.Location.Y), ome.units.UNITS.MICROMETER);
                omexml.setStageLabelY(s2, serie);
                ome.units.quantity.Length s3 = new ome.units.quantity.Length(java.lang.Double.valueOf(b.Volume.Location.Z), ome.units.UNITS.MICROMETER);
                omexml.setStageLabelZ(s3, serie);
                omexml.setStageLabelName("StageLabel:" + serie, serie);
                List<Channel> chs = new List<Channel>();
                if (b.Channels.Count == 1)
                {
                    if (b.Channels[0].range.Length == 3)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            Channel ch = b.Channels[0].Copy();
                            ch.SamplesPerPixel = 1;
                            ch.range = new IntRange[] { b.Channels[0].range[c] };
                            chs.Add(ch);
                        }
                    }
                    else
                        chs.Add(b.Channels[0]);
                }
                else
                    chs.AddRange(b.Channels);

                for (int channel = 0; channel < chs.Count; channel++)
                {
                    Channel c = chs[channel];
                    for (int r = 0; r < c.range.Length; r++)
                    {
                        omexml.setChannelID("Channel:" + channel + ":" + serie, r, channel);
                        omexml.setChannelSamplesPerPixel(new PositiveInteger(java.lang.Integer.valueOf(c.range.Length)), r, channel);
                        if (c.LightSourceWavelength != 0)
                        {
                            omexml.setChannelLightSourceSettingsID("LightSourceSettings:" + channel, r, channel);
                            ome.units.quantity.Length lw = new ome.units.quantity.Length(java.lang.Double.valueOf(c.LightSourceWavelength), ome.units.UNITS.NANOMETER);
                            omexml.setChannelLightSourceSettingsWavelength(lw, r, channel);
                            omexml.setChannelLightSourceSettingsAttenuation(PercentFraction.valueOf(c.LightSourceAttenuation), r, channel);
                        }
                        omexml.setChannelName(c.Name, r, channel);
                        if (c.Color != null)
                        {
                            ome.xml.model.primitives.Color col = new ome.xml.model.primitives.Color(c.Color.Value.R, c.Color.Value.G, c.Color.Value.B, c.Color.Value.A);
                            omexml.setChannelColor(col, r, channel);
                        }
                        if (c.Emission != 0)
                        {
                            ome.units.quantity.Length em = new ome.units.quantity.Length(java.lang.Double.valueOf(c.Emission), ome.units.UNITS.NANOMETER);
                            omexml.setChannelEmissionWavelength(em, r, channel);
                            ome.units.quantity.Length ex = new ome.units.quantity.Length(java.lang.Double.valueOf(c.Excitation), ome.units.UNITS.NANOMETER);
                            omexml.setChannelExcitationWavelength(ex, r, channel);
                        }
                        /*
                        if (c.ContrastMethod != null)
                        {
                            ome.xml.model.enums.ContrastMethod cm = (ome.xml.model.enums.ContrastMethod)Enum.Parse(typeof(ome.xml.model.enums.ContrastMethod), c.ContrastMethod);
                            omexml.setChannelContrastMethod(cm, serie, channel + r);
                        }
                        if (c.IlluminationType != null)
                        {
                            ome.xml.model.enums.IlluminationType il = (ome.xml.model.enums.IlluminationType)Enum.Parse(typeof(ome.xml.model.enums.IlluminationType), c.IlluminationType.ToUpper());
                            omexml.setChannelIlluminationType(il, serie, channel + r);
                        }
                        if (c.AcquisitionMode != null)
                        {
                            ome.xml.model.enums.AcquisitionMode am = (ome.xml.model.enums.AcquisitionMode)Enum.Parse(typeof(ome.xml.model.enums.AcquisitionMode), c.AcquisitionMode.ToUpper());
                            omexml.setChannelAcquisitionMode(am, serie, channel + r);
                        }
                        */
                        omexml.setChannelFluor(c.Fluor, r, channel);
                        if (c.LightSourceIntensity != 0)
                        {
                            ome.units.quantity.Power pw = new ome.units.quantity.Power(java.lang.Double.valueOf(c.LightSourceIntensity), ome.units.UNITS.VOLT);
                            omexml.setLightEmittingDiodePower(pw, r, channel);
                            omexml.setLightEmittingDiodeID(c.DiodeName, r, channel);
                        }
                    }
                }

                int i = 0;
                foreach (ROI an in b.Annotations)
                {
                    if (an.roiID == "")
                        omexml.setROIID("ROI:" + i.ToString() + ":" + serie, i);
                    else
                        omexml.setROIID(an.roiID, i);
                    if (an.roiName != "")
                        omexml.setROIName(an.roiName, i);
                    else
                        omexml.setROIName("ROI:" + i.ToString() + ":" + serie, i);
                    if (an.type == ROI.Type.Point)
                    {
                        if (an.id != "")
                            omexml.setPointID(an.id, i, serie);
                        else
                            omexml.setPointID("Shape:" + i + ":" + serie, i, serie);
                        omexml.setPointX(java.lang.Double.valueOf(b.ToImageSpaceX(an.X)), i, serie);
                        omexml.setPointY(java.lang.Double.valueOf(b.ToImageSpaceY(an.Y)), i, serie);
                        omexml.setPointTheZ(new NonNegativeInteger(java.lang.Integer.valueOf(an.coord.Z)), i, serie);
                        omexml.setPointTheC(new NonNegativeInteger(java.lang.Integer.valueOf(an.coord.C)), i, serie);
                        omexml.setPointTheT(new NonNegativeInteger(java.lang.Integer.valueOf(an.coord.T)), i, serie);
                        if (an.Text != "")
                            omexml.setPointText(an.Text, i, serie);
                        else
                            omexml.setPointText(i.ToString(), i, serie);
                        ome.units.quantity.Length fl = new ome.units.quantity.Length(java.lang.Double.valueOf(an.fontSize), ome.units.UNITS.PIXEL);
                        omexml.setPointFontSize(fl, i, serie);
                        ome.xml.model.primitives.Color col = new ome.xml.model.primitives.Color(an.strokeColor.R, an.strokeColor.G, an.strokeColor.B, an.strokeColor.A);
                        omexml.setPointStrokeColor(col, i, serie);
                        ome.units.quantity.Length sw = new ome.units.quantity.Length(java.lang.Double.valueOf(an.strokeWidth), ome.units.UNITS.PIXEL);
                        omexml.setPointStrokeWidth(sw, i, serie);
                        ome.xml.model.primitives.Color colf = new ome.xml.model.primitives.Color(an.fillColor.R, an.fillColor.G, an.fillColor.B, an.fillColor.A);
                        omexml.setPointFillColor(colf, i, serie);
                    }
                    else
                    if (an.type == ROI.Type.Polygon || an.type == ROI.Type.Freeform)
                    {
                        if (an.id != "")
                            omexml.setPolygonID(an.id, i, serie);
                        else
                            omexml.setPolygonID("Shape:" + i + ":" + serie, i, serie);
                        omexml.setPolygonPoints(an.PointsToString(b), i, serie);
                        omexml.setPolygonTheZ(new NonNegativeInteger(java.lang.Integer.valueOf(an.coord.Z)), i, serie);
                        omexml.setPolygonTheC(new NonNegativeInteger(java.lang.Integer.valueOf(an.coord.C)), i, serie);
                        omexml.setPolygonTheT(new NonNegativeInteger(java.lang.Integer.valueOf(an.coord.T)), i, serie);
                        if (an.Text != "")
                            omexml.setPolygonText(an.Text, i, serie);
                        else
                            omexml.setPolygonText(i.ToString(), i, serie);
                        ome.units.quantity.Length fl = new ome.units.quantity.Length(java.lang.Double.valueOf(an.fontSize), ome.units.UNITS.PIXEL);
                        omexml.setPolygonFontSize(fl, i, serie);
                        ome.xml.model.primitives.Color col = new ome.xml.model.primitives.Color(an.strokeColor.R, an.strokeColor.G, an.strokeColor.B, an.strokeColor.A);
                        omexml.setPolygonStrokeColor(col, i, serie);
                        ome.units.quantity.Length sw = new ome.units.quantity.Length(java.lang.Double.valueOf(an.strokeWidth), ome.units.UNITS.PIXEL);
                        omexml.setPolygonStrokeWidth(sw, i, serie);
                        ome.xml.model.primitives.Color colf = new ome.xml.model.primitives.Color(an.fillColor.R, an.fillColor.G, an.fillColor.B, an.fillColor.A);
                        omexml.setPolygonFillColor(colf, i, serie);
                    }
                    else
                    if (an.type == ROI.Type.Rectangle)
                    {
                        if (an.id != "")
                            omexml.setRectangleID(an.id, i, serie);
                        else
                            omexml.setRectangleID("Shape:" + i + ":" + serie, i, serie);
                        omexml.setRectangleWidth(java.lang.Double.valueOf(b.ToImageSizeX(an.W)), i, serie);
                        omexml.setRectangleHeight(java.lang.Double.valueOf(b.ToImageSizeY(an.H)), i, serie);
                        omexml.setRectangleX(java.lang.Double.valueOf(b.ToImageSpaceX(an.Rect.X)), i, serie);
                        omexml.setRectangleY(java.lang.Double.valueOf(b.ToImageSpaceY(an.Rect.Y)), i, serie);
                        omexml.setRectangleTheZ(new NonNegativeInteger(java.lang.Integer.valueOf(an.coord.Z)), i, serie);
                        omexml.setRectangleTheC(new NonNegativeInteger(java.lang.Integer.valueOf(an.coord.C)), i, serie);
                        omexml.setRectangleTheT(new NonNegativeInteger(java.lang.Integer.valueOf(an.coord.T)), i, serie);
                        omexml.setRectangleText(i.ToString(), i, serie);
                        if (an.Text != "")
                            omexml.setRectangleText(an.Text, i, serie);
                        else
                            omexml.setRectangleText(i.ToString(), i, serie);
                        ome.units.quantity.Length fl = new ome.units.quantity.Length(java.lang.Double.valueOf(an.fontSize), ome.units.UNITS.PIXEL);
                        omexml.setRectangleFontSize(fl, i, serie);
                        ome.xml.model.primitives.Color col = new ome.xml.model.primitives.Color(an.strokeColor.R, an.strokeColor.G, an.strokeColor.B, an.strokeColor.A);
                        omexml.setRectangleStrokeColor(col, i, serie);
                        ome.units.quantity.Length sw = new ome.units.quantity.Length(java.lang.Double.valueOf(an.strokeWidth), ome.units.UNITS.PIXEL);
                        omexml.setRectangleStrokeWidth(sw, i, serie);
                        ome.xml.model.primitives.Color colf = new ome.xml.model.primitives.Color(an.fillColor.R, an.fillColor.G, an.fillColor.B, an.fillColor.A);
                        omexml.setRectangleFillColor(colf, i, serie);
                    }
                    else
                    if (an.type == ROI.Type.Line)
                    {
                        if (an.id != "")
                            omexml.setLineID(an.id, i, serie);
                        else
                            omexml.setLineID("Shape:" + i + ":" + serie, i, serie);
                        omexml.setLineX1(java.lang.Double.valueOf(b.ToImageSpaceX(an.GetPoint(0).X)), i, serie);
                        omexml.setLineY1(java.lang.Double.valueOf(b.ToImageSpaceY(an.GetPoint(0).Y)), i, serie);
                        omexml.setLineX2(java.lang.Double.valueOf(b.ToImageSpaceX(an.GetPoint(1).X)), i, serie);
                        omexml.setLineY2(java.lang.Double.valueOf(b.ToImageSpaceY(an.GetPoint(1).Y)), i, serie);
                        omexml.setLineTheZ(new NonNegativeInteger(java.lang.Integer.valueOf(an.coord.Z)), i, serie);
                        omexml.setLineTheC(new NonNegativeInteger(java.lang.Integer.valueOf(an.coord.C)), i, serie);
                        omexml.setLineTheT(new NonNegativeInteger(java.lang.Integer.valueOf(an.coord.T)), i, serie);
                        if (an.Text != "")
                            omexml.setLineText(an.Text, i, serie);
                        else
                            omexml.setLineText(i.ToString(), i, serie);
                        ome.units.quantity.Length fl = new ome.units.quantity.Length(java.lang.Double.valueOf(an.fontSize), ome.units.UNITS.PIXEL);
                        omexml.setLineFontSize(fl, i, serie);
                        ome.xml.model.primitives.Color col = new ome.xml.model.primitives.Color(an.strokeColor.R, an.strokeColor.G, an.strokeColor.B, an.strokeColor.A);
                        omexml.setLineStrokeColor(col, i, serie);
                        ome.units.quantity.Length sw = new ome.units.quantity.Length(java.lang.Double.valueOf(an.strokeWidth), ome.units.UNITS.PIXEL);
                        omexml.setLineStrokeWidth(sw, i, serie);
                        ome.xml.model.primitives.Color colf = new ome.xml.model.primitives.Color(an.fillColor.R, an.fillColor.G, an.fillColor.B, an.fillColor.A);
                        omexml.setLineFillColor(colf, i, serie);
                    }
                    else
                    if (an.type == ROI.Type.Ellipse)
                    {

                        if (an.id != "")
                            omexml.setEllipseID(an.id, i, serie);
                        else
                            omexml.setEllipseID("Shape:" + i + ":" + serie, i, serie);
                        //We need to change System.Drawing.Rectangle to ellipse radius;
                        double w = (double)an.W / 2;
                        double h = (double)an.H / 2;
                        omexml.setEllipseRadiusX(java.lang.Double.valueOf(b.ToImageSizeX(w)), i, serie);
                        omexml.setEllipseRadiusY(java.lang.Double.valueOf(b.ToImageSizeY(h)), i, serie);

                        double x = an.Point.X + w;
                        double y = an.Point.Y + h;
                        omexml.setEllipseX(java.lang.Double.valueOf(b.ToImageSpaceX(x)), i, serie);
                        omexml.setEllipseY(java.lang.Double.valueOf(b.ToImageSpaceX(y)), i, serie);
                        omexml.setEllipseTheZ(new NonNegativeInteger(java.lang.Integer.valueOf(an.coord.Z)), i, serie);
                        omexml.setEllipseTheC(new NonNegativeInteger(java.lang.Integer.valueOf(an.coord.C)), i, serie);
                        omexml.setEllipseTheT(new NonNegativeInteger(java.lang.Integer.valueOf(an.coord.T)), i, serie);
                        if (an.Text != "")
                            omexml.setEllipseText(an.Text, i, serie);
                        else
                            omexml.setEllipseText(i.ToString(), i, serie);
                        ome.units.quantity.Length fl = new ome.units.quantity.Length(java.lang.Double.valueOf(an.fontSize), ome.units.UNITS.PIXEL);
                        omexml.setEllipseFontSize(fl, i, serie);
                        ome.xml.model.primitives.Color col = new ome.xml.model.primitives.Color(an.strokeColor.R, an.strokeColor.G, an.strokeColor.B, an.strokeColor.A);
                        omexml.setEllipseStrokeColor(col, i, serie);
                        ome.units.quantity.Length sw = new ome.units.quantity.Length(java.lang.Double.valueOf(an.strokeWidth), ome.units.UNITS.PIXEL);
                        omexml.setEllipseStrokeWidth(sw, i, serie);
                        ome.xml.model.primitives.Color colf = new ome.xml.model.primitives.Color(an.fillColor.R, an.fillColor.G, an.fillColor.B, an.fillColor.A);
                        omexml.setEllipseFillColor(colf, i, serie);
                    }
                    else
                    if (an.type == ROI.Type.Label)
                    {
                        if (an.id != "")
                            omexml.setLabelID(an.id, i, serie);
                        else
                            omexml.setLabelID("Shape:" + i + ":" + serie, i, serie);
                        omexml.setLabelX(java.lang.Double.valueOf(b.ToImageSpaceX(an.Rect.X)), i, serie);
                        omexml.setLabelY(java.lang.Double.valueOf(b.ToImageSpaceY(an.Rect.Y)), i, serie);
                        omexml.setLabelTheZ(new NonNegativeInteger(java.lang.Integer.valueOf(an.coord.Z)), i, serie);
                        omexml.setLabelTheC(new NonNegativeInteger(java.lang.Integer.valueOf(an.coord.C)), i, serie);
                        omexml.setLabelTheT(new NonNegativeInteger(java.lang.Integer.valueOf(an.coord.T)), i, serie);
                        omexml.setLabelText(i.ToString(), i, serie);
                        if (an.Text != "")
                            omexml.setLabelText(an.Text, i, serie);
                        else
                            omexml.setLabelText(i.ToString(), i, serie);
                        ome.units.quantity.Length fl = new ome.units.quantity.Length(java.lang.Double.valueOf(an.fontSize), ome.units.UNITS.PIXEL);
                        omexml.setLabelFontSize(fl, i, serie);
                        ome.xml.model.primitives.Color col = new ome.xml.model.primitives.Color(an.strokeColor.R, an.strokeColor.G, an.strokeColor.B, an.strokeColor.A);
                        omexml.setLabelStrokeColor(col, i, serie);
                        ome.units.quantity.Length sw = new ome.units.quantity.Length(java.lang.Double.valueOf(an.strokeWidth), ome.units.UNITS.PIXEL);
                        omexml.setLabelStrokeWidth(sw, i, serie);
                        ome.xml.model.primitives.Color colf = new ome.xml.model.primitives.Color(an.fillColor.R, an.fillColor.G, an.fillColor.B, an.fillColor.A);
                        omexml.setLabelFillColor(colf, i, serie);
                    }
                    else
                    if (an.type == ROI.Type.Mask || an.roiMask != null)
                    {
                        if (an.id != "")
                            omexml.setMaskID(an.id, i, serie);
                        else
                            omexml.setMaskID("Shape:" + i + ":" + serie, i, serie);
                        omexml.setMaskX(java.lang.Double.valueOf(b.ToImageSpaceX(b.StageSizeX + (an.roiMask.X * an.roiMask.PhysicalSizeX))), i, serie);
                        omexml.setMaskY(java.lang.Double.valueOf(b.ToImageSpaceY(b.StageSizeY + (an.roiMask.Y * an.roiMask.PhysicalSizeY))), i, serie);
                        omexml.setMaskTheZ(new NonNegativeInteger(java.lang.Integer.valueOf(an.coord.Z)), i, serie);
                        omexml.setMaskTheC(new NonNegativeInteger(java.lang.Integer.valueOf(an.coord.C)), i, serie);
                        omexml.setMaskTheT(new NonNegativeInteger(java.lang.Integer.valueOf(an.coord.T)), i, serie);
                        omexml.setMaskWidth(new java.lang.Double(an.roiMask.Width * an.roiMask.PhysicalSizeX), i, serie);
                        omexml.setMaskHeight(new java.lang.Double(an.roiMask.Height * an.roiMask.PhysicalSizeY), i, serie);
                        if (an.Text != "")
                            omexml.setMaskText(an.Text, i, serie);
                        else
                            omexml.setMaskText(i.ToString(), i, serie);

                        ome.units.quantity.Length fl = new ome.units.quantity.Length(java.lang.Double.valueOf(an.fontSize), ome.units.UNITS.PIXEL);
                        omexml.setMaskFontSize(fl, i, serie);
                        ome.xml.model.primitives.Color col = new ome.xml.model.primitives.Color(an.strokeColor.R, an.strokeColor.G, an.strokeColor.B, an.strokeColor.A);
                        omexml.setMaskStrokeColor(col, i, serie);
                        ome.units.quantity.Length sw = new ome.units.quantity.Length(java.lang.Double.valueOf(an.strokeWidth), ome.units.UNITS.PIXEL);
                        omexml.setMaskStrokeWidth(sw, i, serie);
                        ome.xml.model.primitives.Color colf = new ome.xml.model.primitives.Color(an.fillColor.R, an.fillColor.G, an.fillColor.B, an.fillColor.A);
                        omexml.setMaskFillColor(colf, i, serie);
                        byte[] bts = an.roiMask.GetBytes();
                        omexml.setMaskBinData(bts, i, serie);
                        omexml.setMaskBinDataBigEndian(new java.lang.Boolean(!BitConverter.IsLittleEndian), i, serie);
                        omexml.setMaskBinDataLength(new NonNegativeLong(new java.lang.Long(bts.Length)), i, serie);
                        omexml.setMaskBinDataCompression(ome.xml.model.enums.Compression.NONE, i, serie);
                    }

                    i++;
                }

                if (b.Buffers[0].Plane != null && planes)
                    for (int bu = 0; bu < b.Buffers.Count; bu++)
                    {
                        //Correct order of parameters.
                        if (b.Buffers[bu].Plane.Delta != 0)
                        {
                            ome.units.quantity.Time t = new ome.units.quantity.Time(java.lang.Double.valueOf(b.Buffers[bu].Plane.Delta), ome.units.UNITS.MILLISECOND);
                            omexml.setPlaneDeltaT(t, serie, bu);
                        }
                        if (b.Buffers[bu].Plane.Exposure != 0)
                        {
                            ome.units.quantity.Time et = new ome.units.quantity.Time(java.lang.Double.valueOf(b.Buffers[bu].Plane.Exposure), ome.units.UNITS.MILLISECOND);
                            omexml.setPlaneExposureTime(et, serie, bu);
                        }
                        ome.units.quantity.Length lx = new ome.units.quantity.Length(java.lang.Double.valueOf(b.Buffers[bu].Plane.Location.X), ome.units.UNITS.MICROMETER);
                        ome.units.quantity.Length ly = new ome.units.quantity.Length(java.lang.Double.valueOf(b.Buffers[bu].Plane.Location.Y), ome.units.UNITS.MICROMETER);
                        ome.units.quantity.Length lz = new ome.units.quantity.Length(java.lang.Double.valueOf(b.Buffers[bu].Plane.Location.Z), ome.units.UNITS.MICROMETER);
                        omexml.setPlanePositionX(lx, serie, bu);
                        omexml.setPlanePositionY(ly, serie, bu);
                        omexml.setPlanePositionZ(lz, serie, bu);
                        omexml.setPlaneTheC(new NonNegativeInteger(java.lang.Integer.valueOf(b.Buffers[bu].Plane.Coordinate.C)), serie, bu);
                        omexml.setPlaneTheZ(new NonNegativeInteger(java.lang.Integer.valueOf(b.Buffers[bu].Plane.Coordinate.Z)), serie, bu);
                        omexml.setPlaneTheT(new NonNegativeInteger(java.lang.Integer.valueOf(b.Buffers[bu].Plane.Coordinate.T)), serie, bu);

                        omexml.setTiffDataPlaneCount(new NonNegativeInteger(java.lang.Integer.valueOf(1)), serie, bu);
                        omexml.setTiffDataIFD(new NonNegativeInteger(java.lang.Integer.valueOf(bu)), serie, bu);
                        omexml.setTiffDataFirstC(new NonNegativeInteger(java.lang.Integer.valueOf(b.Buffers[bu].Plane.Coordinate.C)), serie, bu);
                        omexml.setTiffDataFirstZ(new NonNegativeInteger(java.lang.Integer.valueOf(b.Buffers[bu].Plane.Coordinate.Z)), serie, bu);
                        omexml.setTiffDataFirstT(new NonNegativeInteger(java.lang.Integer.valueOf(b.Buffers[bu].Plane.Coordinate.T)), serie, bu);

                    }

            }
            writer = new ImageWriter();
            writer.setMetadataRetrieve(omexml);
            try
            {
                Status = "Saving OME Image Planes.";
                for (int i = 0; i < files.Length; i++)
                {
                    Console.WriteLine("Writer setId: " + files[i]);
                    writer.setId(f);
                    BioImage b = files[i];
                    progFile = files[i].Filename;
                    writer.setSeries(i);
                    for (int bu = 0; bu < b.Buffers.Count; bu++)
                    {
                        byte[] bts = b.Buffers[bu].GetSaveBytes(BitConverter.IsLittleEndian);
                        writer.saveBytes(bu, bts);
                        Progress = ((float)bu / (float)b.Buffers.Count) * 100;
                    }
                }
                if (!OperatingSystem.IsMacOS())
                {
                    Console.WriteLine("Closing writer.");
                    writer.close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            Recorder.Record(BioLib.Recorder.GetCurrentMethodInfo(), false, files, f, planes);
        }
        /// <summary>
        /// The function "GetBands" returns the number of color bands for a given pixel format in the
        /// AForge library.
        /// </summary>
        /// <param name="PixelFormat">The PixelFormat parameter is an enumeration that represents the
        /// format of a pixel in an image. It specifies the number of bits per pixel and the color space
        /// used by the pixel. The PixelFormat enumeration is defined in the AForge.Imaging
        /// namespace.</param>
        /// <returns>
        /// The method is returning the number of color bands for a given pixel format.
        /// </returns>
        public static int GetBands(PixelFormat format)
        {
            switch (format)
            {
                case AForge.PixelFormat.Format8bppIndexed: return 1;
                case AForge.PixelFormat.Format16bppGrayScale: return 1;
                case AForge.PixelFormat.Format24bppRgb: return 3;
                case AForge.PixelFormat.Format32bppArgb:
                case AForge.PixelFormat.Format32bppPArgb:
                case AForge.PixelFormat.Format32bppRgb:
                    return 4;
                case AForge.PixelFormat.Format48bppRgb:
                    return 3;
                default:
                    throw new NotSupportedException($"Unsupported pixel format: {format}");
            }
        }
        /// <summary>
        /// The function `SaveOMEPyramidal` saves a collection of BioImages as a pyramidal OME-TIFF
        /// file.
        /// </summary>
        /// <param name="bms">An array of BioImage objects representing the images to be saved.</param>
        /// <param name="file">The `file` parameter is a string that represents the file path where the
        /// OME Pyramidal TIFF file will be saved.</param>
        /// <param name="compression">The `compression` parameter is of type
        /// `Enums.ForeignTiffCompression` and it specifies the compression method to be used when
        /// saving the TIFF file.</param>
        public static void SaveOMEPyramidal(BioImage[] bms, string file, Enums.ForeignTiffCompression compression, int compressionLevel)
        {
            if (File.Exists(file))
                File.Delete(file);
            Status = "Saving OME Pyramidal";
            //We need to go through the images and find the ones belonging to each resolution.
            //As well we need to determine the dimensions of the tiles.
            Dictionary<double, List<BioImage>> bis = new Dictionary<double, List<BioImage>>();
            Dictionary<double, Point3D> min = new Dictionary<double, Point3D>();
            Dictionary<double, Point3D> max = new Dictionary<double, Point3D>();
            for (int i = 0; i < bms.Length; i++)
            {
                Resolution res = bms[i].Resolutions[bms[i].Level];
                if (bis.ContainsKey(res.PhysicalSizeX))
                {
                    bis[res.PhysicalSizeX].Add(bms[i]);
                    if (bms[i].StageSizeX < min[res.PhysicalSizeX].X || bms[i].StageSizeY < min[res.PhysicalSizeX].Y)
                    {
                        min[res.PhysicalSizeX] = bms[i].Volume.Location;
                    }
                    if (bms[i].StageSizeX > max[res.PhysicalSizeX].X || bms[i].StageSizeY > max[res.PhysicalSizeX].Y)
                    {
                        max[res.PhysicalSizeX] = bms[i].Volume.Location;
                    }
                }
                else
                {
                    bis.Add(res.PhysicalSizeX, new List<BioImage>());
                    min.Add(res.PhysicalSizeX, new Point3D(double.MaxValue, double.MaxValue, double.MaxValue));
                    max.Add(res.PhysicalSizeX, new Point3D(double.MinValue, double.MinValue, double.MinValue));
                    if (bms[i].StageSizeX < min[res.PhysicalSizeX].X || bms[i].StageSizeY < min[res.PhysicalSizeX].Y)
                    {
                        min[res.PhysicalSizeX] = bms[i].Volume.Location;
                    }
                    if (bms[i].StageSizeX > max[res.PhysicalSizeX].X || bms[i].StageSizeY > max[res.PhysicalSizeX].Y)
                    {
                        max[res.PhysicalSizeX] = bms[i].Volume.Location;
                    }
                    bis[res.PhysicalSizeX].Add(bms[i]);
                }
            }
            int s = 0;
            //We determine the sizes of each resolution.
            Dictionary<double, AForge.Size> ss = new Dictionary<double, AForge.Size>();
            int minx = int.MaxValue;
            int miny = int.MaxValue;
            double last = 0;
            foreach (double px in bis.Keys)
            {
                int xs = (1 + (int)Math.Ceiling((max[px].X - min[px].X) / bis[px][0].Resolutions[0].VolumeWidth)) * bis[px][0].SizeX;
                int ys = (1 + (int)Math.Ceiling((max[px].Y - min[px].Y) / bis[px][0].Resolutions[0].VolumeHeight)) * bis[px][0].SizeY;
                if (minx > xs)
                    minx = xs;
                if (miny > ys)
                    miny = ys;
                ss.Add(px, new AForge.Size(xs, ys));
                last = px;
            }
            s = 0;
            string met = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>" +
                "<OME xmlns=\"http://www.openmicroscopy.org/Schemas/OME/2016-06\" " +
                "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" " +
                "xsi:schemaLocation=\"http://www.openmicroscopy.org/Schemas/OME/2016-06 http://www.openmicroscopy.org/Schemas/OME/2016-06/ome.xsd\">";
            NetVips.Image img = null;
            int ib = 0;

            foreach (double px in bis.Keys)
            {
                int c = bis[px][s].SizeC;
                if (bis[px][s].Buffers[0].isRGB)
                    c = 3;
                string endian = (bis[px][s].Buffers[0].LittleEndian).ToString().ToLower();
                met +=
                "<Image ID=\"Image:" + ib + "\">" +
                "<Pixels BigEndian=\"" + endian + "\" DimensionOrder= \"XYCZT\" ID= \"Pixels:0\" Interleaved=\"true\" " +
                "PhysicalSizeX=\"" + bis[px][s].PhysicalSizeX + "\" PhysicalSizeXUnit=\"µm\" PhysicalSizeY=\"" + bis[px][s].PhysicalSizeY + "\" PhysicalSizeYUnit=\"µm\" SignificantBits=\"" + bis[px][s].bitsPerPixel + "\" " +
                "SizeC = \"" + c + "\" SizeT = \"" + bis[px][s].SizeT + "\" SizeX =\"" + ss[px].Width +
                "\" SizeY= \"" + ss[px].Height + "\" SizeZ=\"" + bis[px][s].SizeZ;
                if (bis[px][s].bitsPerPixel > 8) met += "\" Type= \"uint16\">";
                else met += "\" Type= \"uint8\">";
                int i = 0;
                foreach (Channel ch in bis[px][s].Channels)
                {
                    met += "<Channel ID=\"Channel:" + ib + ":" + i + "\" SamplesPerPixel=\"1\"></Channel>";
                    i++;
                }
                met += "</Pixels></Image>";
                ib++;
            }
            met += "</OME>";
            foreach (double px in bis.Keys)
            {
                PixelFormat pf = bis[px][0].Buffers[0].PixelFormat;
                Bitmap level = new Bitmap(ss[px].Width, ss[px].Height, pf);
                int bands = GetBands(pf);
                if (bis[px][0].bitsPerPixel > 8)
                    img = NetVips.Image.NewFromMemory(level.Data, (ulong)level.Length, level.Width, level.Height, bands, Enums.BandFormat.Ushort);
                else
                    img = NetVips.Image.NewFromMemory(level.Data, (ulong)level.Length, level.Width, level.Height, bands, Enums.BandFormat.Uchar);
                int i = 0;
                foreach (BioImage b in bis[px])
                {
                    Progress = ((float)i / (float)bis[px].Count) * 100;
                    AForge.Size si = ss[px];
                    double xs = (-(min[px].X - bis[px][i].Volume.Location.X) / bis[px][i].Resolutions[0].VolumeWidth) * bis[px][i].SizeX;
                    double ys = (-(min[px].Y - bis[px][i].Volume.Location.Y) / bis[px][i].Resolutions[0].VolumeHeight) * bis[px][i].SizeY;
                    NetVips.Image tile;
                    if (b.bitsPerPixel > 8)
                        tile = NetVips.Image.NewFromMemory(bis[px][i].Buffers[0].Data, (ulong)bis[px][i].Buffers[0].Length, bis[px][i].Buffers[0].Width, bis[px][i].Buffers[0].Height, bands, Enums.BandFormat.Ushort);
                    else
                        tile = NetVips.Image.NewFromMemory(bis[px][i].Buffers[0].Data, (ulong)bis[px][i].Buffers[0].Length, bis[px][i].Buffers[0].Width, bis[px][i].Buffers[0].Height, bands, Enums.BandFormat.Uchar);
                    img = img.Insert(tile, (int)xs, (int)ys);
                    i++;
                };
                using var mutated = img.Mutate(mutable =>
                {
                    // Set the ImageDescription tag
                    mutable.Set(GValue.GStrType, "image-description", met);
                    mutable.Set(GValue.GIntType, "page-height", ss[last].Height);
                });
                if (bis[px][0].bitsPerPixel > 8)
                    mutated.Tiffsave(file, compression, 1, Enums.ForeignTiffPredictor.None, true, ss[px].Width, ss[px].Height, true, false, 16,
                    Enums.ForeignTiffResunit.Cm, 1000 * bis[px][0].PhysicalSizeX, 1000 * bis[px][0].PhysicalSizeY, true, null, Enums.RegionShrink.Nearest,
                    compressionLevel, true, Enums.ForeignDzDepth.One, true, false, null, null, ss[px].Height);
                else
                    mutated.Tiffsave(file, compression, 1, Enums.ForeignTiffPredictor.None, true, ss[px].Width, ss[px].Height, true, false, 8,
                    Enums.ForeignTiffResunit.Cm, 1000 * bis[px][0].PhysicalSizeX, 1000 * bis[px][0].PhysicalSizeY, true, null, Enums.RegionShrink.Nearest,
                    compressionLevel, true, Enums.ForeignDzDepth.One, true, false, null, null, ss[px].Height);
                s++;
            }

        }

        /// The function "OpenOME" opens a bioimage file in the OME format and returns the first image
        /// in the series.
        /// 
        /// @param file The "file" parameter is a string that represents the file path or name of the
        /// OME file that you want to open.
        /// @param tab The "tab" parameter is a boolean value that determines whether the image is opened in a new tab.
        /// 
        /// @return The method is returning a BioImage object.
        public static BioImage OpenOME(string file, bool tab)
        {
            return OpenOMESeries(file, tab, true)[0];
        }
        /// > OpenOME(string file, int serie)
        /// 
        /// The first parameter is a string, the second is an integer
        /// 
        /// @param file the path to the file
        /// @param serie the image series to open
        /// 
        /// @return A BioImage object.
        public static BioImage OpenOME(string file, int serie)
        {
            return OpenOME(file, serie, true, false, false, 0, 0, 0, 0);
        }
        /// <summary>
        /// Converts a numpy array to BioImage.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static BioImage FromNumpy(string file)
        {
            BioImage bm = new BioImage(file);
            var (shape, dataType, data) = NumPy.ReadNpyFile(file);

            // Handle different shapes and cast the data into appropriate multi-dimensional arrays
            if (shape.Length == 5)
            {
                if (dataType == NumPy.NpyDataType.Float32)
                {
                    float[,,,,] fs = NumPy.ConvertToMultidimensional<float, float[,,,,]>(data, shape);
                    for (int z = 0; z < shape[2]; z++)
                    {
                        for (int c = 0; c < shape[1]; c++)
                        {
                            for (int t = 0; t < shape[0]; t++)
                            {
                                Bitmap b = new Bitmap(shape[3], shape[4], PixelFormat.Float);
                                for (int y = 0; y < shape[4]; y++)
                                {
                                    for (int x = 0; x < shape[3]; x++)
                                    {
                                        b.SetValue(x, y, fs[t, c, z, x, y]);
                                    }
                                }
                                bm.Buffers.Add(b);
                            }
                        }
                    }
                }
                else if (dataType == NumPy.NpyDataType.UInt8)
                {
                    uint[,,,,] fs = NumPy.ConvertToMultidimensional<uint, uint[,,,,]>(data, shape);
                    for (int z = 0; z < shape[2]; z++)
                    {
                        for (int c = 0; c < shape[1]; c++)
                        {
                            for (int t = 0; t < shape[0]; t++)
                            {
                                Bitmap b = new Bitmap(shape[3], shape[4], PixelFormat.Format8bppIndexed);
                                for (int y = 0; y < shape[4]; y++)
                                {
                                    for (int x = 0; x < shape[3]; x++)
                                    {
                                        b.SetValue(x, y, fs[t, c, z, x, y]);
                                    }
                                }
                                bm.Buffers.Add(b);
                            }
                        }
                    }
                }
                bm.sizeZ = shape[2];
                bm.sizeC = shape[1];
                bm.sizeT = shape[0];
            }
            else if (shape.Length == 4)
            {
                if (dataType == NumPy.NpyDataType.Float32)
                {
                    float[,,,] fs = NumPy.ConvertToMultidimensional<float, float[,,,]>(data, shape);
                    for (int z = 0; z < shape[1]; z++)
                    {
                        for (int c = 0; c < shape[0]; c++)
                        {
                            Bitmap b = new Bitmap(shape[2], shape[3], PixelFormat.Float);
                            for (int y = 0; y < shape[3]; y++)
                            {
                                for (int x = 0; x < shape[2]; x++)
                                {
                                    b.SetValue(x, y, fs[c, z, x, y]);
                                }
                            }
                            bm.Buffers.Add(b);
                        }
                    }
                }
                else if (dataType == NumPy.NpyDataType.UInt8)
                {
                    uint[,,,] fs = NumPy.ConvertToMultidimensional<uint, uint[,,,]>(data, shape);
                    for (int z = 0; z < shape[1]; z++)
                    {
                        for (int c = 0; c < shape[0]; c++)
                        {
                            Bitmap b = new Bitmap(shape[2], shape[3], PixelFormat.Format8bppIndexed);
                            for (int y = 0; y < shape[3]; y++)
                            {
                                for (int x = 0; x < shape[2]; x++)
                                {
                                    b.SetValue(x, y, fs[c, z, x, y]);
                                }
                            }
                            bm.Buffers.Add(b);
                        }
                    }
                }
                bm.sizeZ = shape[1];
                bm.sizeC = shape[0];
            }
            else if (shape.Length == 3)
            {
                if (dataType == NumPy.NpyDataType.Float32)
                {
                    float[,,] fs = NumPy.ConvertToMultidimensional<float, float[,,]>(data, shape);
                    for (int z = 0; z < shape[0]; z++)
                    {
                        Bitmap b = new Bitmap(shape[1], shape[2], PixelFormat.Float);
                        for (int y = 0; y < shape[1]; y++)
                        {
                            for (int x = 0; x < shape[2]; x++)
                            {
                                b.SetValue(x, y, fs[z, x, y]);
                            }
                        }
                        bm.Buffers.Add(b);
                    }
                }
                else if (dataType == NumPy.NpyDataType.UInt8)
                {
                    uint[,,] fs = NumPy.ConvertToMultidimensional<uint, uint[,,]>(data, shape);
                    for (int z = 0; z < shape[0]; z++)
                    {
                        Bitmap b = new Bitmap(shape[1], shape[2], PixelFormat.Format8bppIndexed);
                        for (int y = 0; y < shape[1]; y++)
                        {
                            for (int x = 0; x < shape[2]; x++)
                            {
                                b.SetValue(x, y, fs[z, x, y]);
                            }
                        }
                        bm.Buffers.Add(b);
                    }
                }
                bm.sizeZ = shape[0];
                bm.sizeC = 1;
            }
            else if (shape.Length == 2)
            {
                if (dataType == NumPy.NpyDataType.Float32)
                {
                    float[,] fs = NumPy.ConvertToMultidimensional<float, float[,]>(data, shape);
                    Bitmap b = new Bitmap(shape[0], shape[1], PixelFormat.Float);
                    for (int y = 0; y < shape[1]; y++)
                    {
                        for (int x = 0; x < shape[0]; x++)
                        {
                            b.SetValue(x, y, fs[x, y]);
                        }
                    }
                    bm.Buffers.Add(b);
                }
                else if (dataType == NumPy.NpyDataType.UInt8)
                {
                    uint[,] fs = NumPy.ConvertToMultidimensional<uint, uint[,]>(data, shape);
                    Bitmap b = new Bitmap(shape[0], shape[1], PixelFormat.Format8bppIndexed);
                    for (int y = 0; y < shape[1]; y++)
                    {
                        for (int x = 0; x < shape[0]; x++)
                        {
                            b.SetValue(x, y, fs[x, y]);
                        }
                    }
                    bm.Buffers.Add(b);
                }
                bm.sizeC = 1;
                bm.sizeZ = 1;
            }
            else
            {
                throw new InvalidOperationException("Unsupported array shape.");
            }

            for (int i = 0; i < bm.SizeC; i++)
            {
                if (bm.Buffers[0].PixelFormat == PixelFormat.Float || bm.Buffers[0].PixelFormat == PixelFormat.UInt || bm.Buffers[0].PixelFormat == PixelFormat.Int)
                    bm.Channels.Add(new Channel(i, 32, 1));
                else
                if (bm.Buffers[0].PixelFormat == PixelFormat.Format8bppIndexed)
                    bm.Channels.Add(new Channel(i, 8, 1));
            }
            bm.sizeT = 1;
            bm.Coords = new int[bm.SizeZ, bm.SizeC, bm.SizeT];
            bm.UpdateCoords();
            bm.bitsPerPixel = 8;
            if (dataType == NumPy.NpyDataType.Float32)
                bm.Resolutions.Add(new Resolution(bm.SizeX, bm.SizeY, PixelFormat.Float, 96 * (1 / 2.54) / 1000, 96 * (1 / 2.54) / 1000, 96 * (1 / 2.54) / 1000, 0, 0, 0));
            else
            if (dataType == NumPy.NpyDataType.UInt32)
                bm.Resolutions.Add(new Resolution(bm.SizeX, bm.SizeY, PixelFormat.UInt, 96 * (1 / 2.54) / 1000, 96 * (1 / 2.54) / 1000, 96 * (1 / 2.54) / 1000, 0, 0, 0));
            else
            if (dataType == NumPy.NpyDataType.Int32)
                bm.Resolutions.Add(new Resolution(bm.SizeX, bm.SizeY, PixelFormat.Int, 96 * (1 / 2.54) / 1000, 96 * (1 / 2.54) / 1000, 96 * (1 / 2.54) / 1000, 0, 0, 0));
            else
            if (dataType == NumPy.NpyDataType.UInt8)
                bm.Resolutions.Add(new Resolution(bm.SizeX, bm.SizeY, PixelFormat.Format8bppIndexed, 96 * (1 / 2.54) / 1000, 96 * (1 / 2.54) / 1000, 96 * (1 / 2.54) / 1000, 0, 0, 0));
            foreach (var item in bm.Buffers)
            {
                item.Stats = Statistics.FromBytes(item);
            }
            bm.Type = ImageType.stack;
            bm.Volume = new VolumeD(new Point3D(bm.StageSizeX, bm.StageSizeY, bm.StageSizeZ), new Point3D(bm.PhysicalSizeX * bm.SizeX, bm.PhysicalSizeY * bm.SizeY, bm.PhysicalSizeZ * bm.SizeZ));
            AutoThreshold(bm, false);
            if (bm.bitsPerPixel > 8)
                bm.StackThreshold(true);
            else
                bm.StackThreshold(false);
            bm.StackOrder = Order.ZCT;
            bm.ID = Path.GetFileName(file);
            bm.Filename = bm.ID;
            bm.file = file;
            Images.AddImage(bm);
            return bm;
        }
        /// It takes a list of files, and creates a new BioImage object with the first file in the list.
        /// Then it loops through the rest of the files, adding the buffers from each file to the new
        /// BioImage object. Finally, it updates the coordinates of the new BioImage object, and adds it
        /// to the Images list
        /// 
        /// @param files an array of file paths
        /// @param sizeZ number of slices in the stack
        /// @param sizeC number of channels
        /// @param sizeT number of time points
        /// 
        /// @return A BioImage object.
        public static BioImage FilesToStack(string[] files, int sizeZ, int sizeC, int sizeT)
        {
            BioImage b = new BioImage(files[0]);
            for (int i = 0; i < files.Length; i++)
            {
                BioImage bb = OpenFile(files[i], false);
                b.Buffers.AddRange(bb.Buffers);
            }
            b.UpdateCoords(sizeZ, sizeC, sizeT);
            Images.AddImage(b);
            return b;
        }
        /// It takes a folder of images and creates a stack from them
        /// 
        /// @param path the path to the folder containing the images
        /// 
        /// @return A BioImage object.
        public static BioImage FolderToStack(string path, bool tab)
        {
            string[] files = Directory.GetFiles(path);
            BioImage b = new BioImage(files[0]);
            int z = 0;
            int c = 0;
            int t = 0;
            BioImage bb = null;
            for (int i = 0; i < files.Length; i++)
            {
                string[] st = files[i].Split('_');
                if (st.Length > 3)
                {
                    z = int.Parse(st[1].Replace("Z", ""));
                    c = int.Parse(st[2].Replace("C", ""));
                    t = int.Parse(st[3].Replace("T", ""));
                }
                bb = OpenFile(files[i], tab);
                b.Buffers.AddRange(bb.Buffers);
            }
            if (z == 0)
            {
                /*TO DO
                ImagesToStack im = new ImagesToStack();
                if (im.ShowDialog() != DialogResult.OK)
                    return null;
                b.UpdateCoords(im.SizeZ, im.SizeC, im.SizeT);
                */
            }
            else
                b.UpdateCoords(z + 1, c + 1, t + 1);
            Images.AddImage(b);
            return b;
        }
        static bool vips = true;
        /// The function "OpenVips" takes a BioImage object and an integer representing the number of
        /// pages, and adds each page of the image file to the BioImage's vipPages list using the
        /// NetVips library.
        /// 
        /// @param BioImage The BioImage parameter is an object that represents a bio image. It likely
        /// contains information about the image file, such as the file path and other metadata.
        /// @param pagecount The parameter "pagecount" represents the number of pages in the TIFF file
        /// that needs to be loaded into the "vipPages" list of the "BioImage" object.
        public static void OpenVips(BioImage b)
        {
            try
            {
                List<NetVips.Image> ims = new List<NetVips.Image>();
                for (int i = 0; i < b.seriesCount; i++)
                {
                    b.vipPages.Add(NetVips.Image.Tiffload(b.file, i));
                }
                b.vipPages = SortImagesBySizeDescending(b.vipPages.ToArray()).ToList();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

        }
        private static NetVips.Image[] SortImagesBySizeDescending(NetVips.Image[] images)
        {
            if (images == null || images.Length == 0)
            {
                throw new ArgumentException("The input array of images cannot be null or empty.");
            }
            // Sort images by area (width * height) in descending order
            return images
                .OrderByDescending(img => img.Width * img.Height)
                .ToArray();
        }

        /// The function ExtractRegionFromTiledTiff takes a BioImage object, coordinates, width, height,
        /// and resolution as input, and returns a Bitmap object representing the extracted region from
        /// the tiled TIFF image.
        /// 
        /// @param BioImage The BioImage object represents an image file that contains multiple pages or
        /// resolutions. It contains information about the image file, such as the file path, the number
        /// of pages, and the format of each page.
        /// @param x The x-coordinate of the top-left corner of the region to extract from the tiled
        /// TIFF image.
        /// @param y The parameter "y" represents the starting y-coordinate of the region to be
        /// extracted from the tiled TIFF image.
        /// @param width The width parameter represents the width of the region to be extracted from the
        /// tiled TIFF image.
        /// @param height The height parameter represents the height of the region to be extracted from
        /// the tiled TIFF image.
        /// @param res The parameter "res" represents the resolution level of the tiled TIFF image. It
        /// is used to specify the level of detail or zoom level at which the image is being extracted.
        /// 
        /// @return The method is returning a Bitmap object.
        public static Bitmap ExtractRegionFromTiledTiff(BioImage b, int x, int y, int width, int height, int level)
        {
            try
            {
                // Validate inputs
                if (b == null) throw new ArgumentNullException(nameof(b));
                if (b.vipPages == null || b.vipPages.Count <= level) throw new ArgumentException("Invalid image level.");

                // Extract the region from the specified level
                NetVips.Image subImage = b.vipPages[level].ExtractArea(x, y, width, height);

                // Convert the NetVips.Image to Bitmap
                Bitmap bitmap = ConvertVipsImageToBitmap(subImage, b.bitsPerPixel);

                // Clean up
                subImage.Dispose();

                return bitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Converts a NetVips.Image to a Bitmap object based on the bit depth.
        /// </summary>
        private static Bitmap ConvertVipsImageToBitmap(NetVips.Image subImage, int bitsPerPixel)
        {
            // Convert to byte array with raw pixel data
            byte[] pixelData = subImage.WriteToMemory();
            int width = (int)subImage.Width;
            int height = (int)subImage.Height;

            Bitmap bitmap;
            if (bitsPerPixel == 8)
            {
                // 8-bit images -> 24bpp RGB
                bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

                // Ensure correct stride alignment
                int stride = bmpData.Stride;
                byte[] rowBuffer = new byte[stride];
                int bytesPerPixel = 3;

                for (int y = 0; y < height; y++)
                {
                    Array.Copy(pixelData, y * width * bytesPerPixel, rowBuffer, 0, width * bytesPerPixel);
                    Marshal.Copy(rowBuffer, 0, bmpData.Scan0 + y * stride, stride);
                }

                bitmap.UnlockBits(bmpData);
            }
            else if (bitsPerPixel == 16)
            {
                // 16-bit images -> 48bpp RGB
                bitmap = new Bitmap(width, height, PixelFormat.Format48bppRgb);
                BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format48bppRgb);

                int stride = bmpData.Stride;
                byte[] rowBuffer = new byte[stride];
                int bytesPerPixel = 6;

                for (int y = 0; y < height; y++)
                {
                    Array.Copy(pixelData, y * width * bytesPerPixel, rowBuffer, 0, width * bytesPerPixel);
                    Marshal.Copy(rowBuffer, 0, bmpData.Scan0 + y * stride, stride);
                }

                bitmap.UnlockBits(bmpData);
            }
            else
            {
                throw new NotSupportedException($"Unsupported bit depth: {bitsPerPixel}");
            }

            return bitmap;
        }

        /// The function "OpenOME" opens a bioimage file, with options to specify the series, whether to
        /// display it in a tab, whether to add it to existing images, whether to tile the image, and
        /// the tile size.
        /// 
        /// @param file The file parameter is a string that represents the file path or name of the OME
        /// (Open Microscopy Environment) file that you want to open.
        /// @param serie The "serie" parameter is an integer that represents the series number of the
        /// image to be opened.
        /// @param tab The "tab" parameter is a boolean value that determines whether the image should
        /// be opened in a new tab or not. If set to true, the image will be opened in a new tab. If set
        /// to false, the image will be opened in the current tab.
        /// @param addToImages The "addToImages" parameter is a boolean value that determines whether
        /// the opened image should be added to a collection of images. If set to true, the image will
        /// be added to the collection. If set to false, the image will not be added.
        /// @param tile The "tile" parameter is a boolean value that determines whether or not to tile
        /// the images. If set to true, the images will be tiled according to the specified tilex,
        /// tiley, tileSizeX, and tileSizeY parameters. If set to false, the images will not be tiled.
        /// @param tilex The parameter "tilex" is an integer that represents the starting x-coordinate
        /// of the tile.
        /// @param tiley The parameter "tiley" is used to specify the number of tiles in the y-direction
        /// when tiling the image.
        /// @param tileSizeX The tileSizeX parameter specifies the width of each tile in pixels when
        /// tiling the images.
        /// @param tileSizeY The tileSizeY parameter is the height of each tile in pixels when tiling
        /// the images. <summary>
        /// The function "OpenOME" opens a bioimage file, with options to specify the series, whether to
        public static BioImage OpenOME(string file, int serie, bool tab, bool addToImages, bool tile, int tilex, int tiley, int tileSizeX, int tileSizeY, bool useOpenSlide = true)
        {
            if (file == null || file == "")
                throw new InvalidDataException("File is empty or null");
            //We wait incase OME has not initialized.
            do
            {
                Thread.Sleep(10);
            } while (!initialized);
            Console.WriteLine("OpenOME " + file);
            reader = new ImageReader();
            if (tileSizeX == 0)
                tileSizeX = 1920;
            if (tileSizeY == 0)
                tileSizeY = 1080;
            progFile = file;
            BioImage b = new BioImage(file);
            b.Type = ImageType.stack;
            b.Loading = true;
            if (b.meta == null)
                b.meta = service.createOMEXMLMetadata();
            string f = file.Replace("\\", "/");
            string cf = reader.getCurrentFile();
            if (cf != null)
                cf = cf.Replace("\\", "/");
            if (cf != f)
            {
                reader.close();
                reader.setMetadataStore(b.meta);
                try
                {
                    Status = "Opening file " + b.Filename;
                    reader.setId(f);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    return null;
                }
            }

            //status = "Reading OME Metadata.";
            reader.setSeries(serie);
            int RGBChannelCount = reader.getRGBChannelCount();
            //OME reader.getBitsPerPixel(); sometimes returns incorrect bits per pixel, like when opening ImageJ images.
            //So we check the pixel type from xml metadata and if it fails we use the readers value.
            PixelFormat PixelFormat;
            try
            {
                PixelFormat = GetPixelFormat(RGBChannelCount, b.meta.getPixelsType(serie));
            }
            catch (Exception)
            {
                PixelFormat = GetPixelFormat(RGBChannelCount, reader.getBitsPerPixel());
            }

            b.id = file;
            b.file = file;
            int SizeX, SizeY;
            SizeX = reader.getSizeX();
            SizeY = reader.getSizeY();
            int SizeZ = reader.getSizeZ();
            b.sizeC = reader.getSizeC();
            b.sizeZ = reader.getSizeZ();
            b.sizeT = reader.getSizeT();
            b.littleEndian = reader.isLittleEndian();
            b.seriesCount = reader.getSeriesCount();
            b.imagesPerSeries = reader.getImageCount();
            b.imRead = reader;
            List<Resolution> ress = new List<Resolution>();
            if (PixelFormat == PixelFormat.Format8bppIndexed || PixelFormat == PixelFormat.Format24bppRgb || PixelFormat == PixelFormat.Format32bppArgb)
                b.bitsPerPixel = 8;
            else
                b.bitsPerPixel = 16;
            b.series = serie;
            string order = reader.getDimensionOrder();
            if (vips && tile)
                OpenVips(b);
            //Lets get the channels and initialize them
            int i = 0;
            int sumSamples = 0;
            while (true)
            {
                Status = "Reading channels.";
                bool def = false;
                try
                {
                    int s = b.meta.getChannelSamplesPerPixel(serie, i).getNumberValue().intValue();
                    Channel ch = new Channel(i, b.bitsPerPixel, s);
                    if (b.meta.getChannelSamplesPerPixel(serie, i) != null)
                    {
                        ch.SamplesPerPixel = s;
                        sumSamples += s;
                        def = true;
                        b.Channels.Add(ch);
                    }
                    if (i == 0)
                    {
                        b.rgbChannels[0] = 0;
                    }
                    else
                    if (i == 1)
                    {
                        b.rgbChannels[1] = 1;
                    }
                    else
                    if (i == 2)
                    {
                        b.rgbChannels[2] = 2;
                    }
                    //If this channel is not defined we have loaded all the channels in the file.
                    if (!def)
                        break;
                    if (b.meta.getChannelName(serie, i) != null)
                        ch.Name = b.meta.getChannelName(serie, i);
                    if (b.meta.getChannelAcquisitionMode(serie, i) != null)
                        ch.AcquisitionMode = b.meta.getChannelAcquisitionMode(serie, i).ToString();
                    if (b.meta.getChannelID(serie, i) != null)
                        ch.info.ID = b.meta.getChannelID(serie, i);
                    if (b.meta.getChannelFluor(serie, i) != null)
                        ch.Fluor = b.meta.getChannelFluor(serie, i);
                    if (b.meta.getChannelColor(serie, i) != null)
                    {
                        ome.xml.model.primitives.Color cc = b.meta.getChannelColor(serie, i);
                        ch.Color = Color.FromArgb(cc.getRed(), cc.getGreen(), cc.getBlue());
                    }
                    if (b.meta.getChannelIlluminationType(serie, i) != null)
                        ch.IlluminationType = b.meta.getChannelIlluminationType(serie, i).ToString();
                    if (b.meta.getChannelContrastMethod(serie, i) != null)
                        ch.ContrastMethod = b.meta.getChannelContrastMethod(serie, i).ToString();
                    if (b.meta.getChannelEmissionWavelength(serie, i) != null)
                        ch.Emission = b.meta.getChannelEmissionWavelength(serie, i).value().intValue();
                    if (b.meta.getChannelExcitationWavelength(serie, i) != null)
                        ch.Excitation = b.meta.getChannelExcitationWavelength(serie, i).value().intValue();
                    if (b.meta.getLightEmittingDiodePower(serie, i) != null)
                        ch.LightSourceIntensity = b.meta.getLightEmittingDiodePower(serie, i).value().doubleValue();
                    if (b.meta.getLightEmittingDiodeID(serie, i) != null)
                        ch.DiodeName = b.meta.getLightEmittingDiodeID(serie, i);
                    if (b.meta.getChannelLightSourceSettingsAttenuation(serie, i) != null)
                        ch.LightSourceAttenuation = b.meta.getChannelLightSourceSettingsAttenuation(serie, i).toString();


                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    if (!def)
                        break;
                }
                i++;
            }
            try
            {
                if (b.meta.getObjectiveNominalMagnification(serie, 0) != null)
                {
                    b.Magnification = b.meta.getObjectiveNominalMagnification(serie, 0).intValue();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            //If the file doens't have channels we initialize them.
            if (b.Channels.Count == 0)
            {
                b.Channels.Add(new Channel(0, b.bitsPerPixel, RGBChannelCount));
            }
            try
            {
                Status = "Reading wells.";
                int wells = b.meta.getWellCount(0);
                if (wells > 0)
                {
                    b.Type = ImageType.well;
                    b.Plate = new WellPlate(b);
                    tile = false;
                }
            }
            catch (Exception e)
            {
                //This file is not a well plate.
                Console.WriteLine(e.Message);
            }
            if (reader.getResolutionCount() > 0)
                ress.AddRange(GetResolutions(b));
            Console.WriteLine("Done reading resolutions.");
            reader.setSeries(serie);

            int pyramidCount = 0;
            int pyramidResolutions = 0;
            List<Tuple<int, int>> prs = new List<Tuple<int, int>>();
            Console.WriteLine("Determining pyramidal levels.");
            //We need to determine if this image is pyramidal or not.
            //We do this by seeing if the resolutions are downsampled or not.
            if (ress.Count > 1 && b.Type != ImageType.well)
            {
                if (ress[0].SizeX > ress[1].SizeX)
                {
                    b.Type = ImageType.pyramidal;
                    tile = true;
                    //We need to determine number of pyramids in this image and which belong to the series we are opening.
                    int? sr = null;
                    for (int r = 0; r < ress.Count - 1; r++)
                    {
                        if (ress[r].SizeX > ress[r + 1].SizeX && ress[r].PixelFormat == ress[r + 1].PixelFormat)
                        {
                            if (sr == null)
                            {
                                sr = r;
                                prs.Add(new Tuple<int, int>(r, 0));
                            }
                        }
                        else
                        {
                            if (ress[prs[prs.Count - 1].Item1].PixelFormat == ress[r].PixelFormat)
                                prs[prs.Count - 1] = new Tuple<int, int>(prs[prs.Count - 1].Item1, r);
                            sr = null;
                        }
                    }
                    pyramidCount = prs.Count;
                    for (int p = 0; p < prs.Count; p++)
                    {
                        pyramidResolutions += (prs[p].Item2 - prs[p].Item1) + 1;
                    }
                    if (prs[serie].Item2 == 0)
                    {
                        prs[serie] = new Tuple<int, int>(prs[serie].Item1, b.seriesCount - 1);
                    }
                }
            }
            if (b.Type == ImageType.pyramidal)
            {
                Console.WriteLine("Determining Label and Macro resolutions.");
                for (int p = 0; p < prs.Count; p++)
                {
                    for (int r = prs[p].Item1; r < prs[p].Item2 + 1; r++)
                    {
                        b.Resolutions.Add(ress[r]);
                    }
                }
                //If we have 2 resolutions that we're not added they are the label & macro resolutions so we add them to the image.
                if (ress.Count > b.Resolutions.Count)
                {
                    b.LabelResolution = ress.Count - 1;
                    b.Resolutions.Add(ress[ress.Count - 1]);
                    b.MacroResolution = ress.Count - 2;
                    b.Resolutions.Add(ress[ress.Count - 2]);
                }
            }
            if (b.Resolutions.Count == 0)
                b.Resolutions.AddRange(ress);
            try
            {
                string s = b.meta.getStageLabelName(serie);
                if (s != null)
                {
                    b.StageSizeX = b.meta.getStageLabelX(serie).value().doubleValue();
                    b.StageSizeY = b.meta.getStageLabelY(serie).value().doubleValue();
                    b.StageSizeZ = b.meta.getStageLabelZ(serie).value().doubleValue();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("No Stage Cooordinates");
            }

            b.Volume = new VolumeD(new Point3D(b.StageSizeX, b.StageSizeY, b.StageSizeZ), new Point3D(b.PhysicalSizeX * SizeX, b.PhysicalSizeY * SizeY, b.PhysicalSizeZ * SizeZ));
            int rc = b.meta.getROICount();
            for (int im = 0; im < rc; im++)
            {
                string roiID = b.meta.getROIID(im);
                string roiName = b.meta.getROIName(im);
                ZCT co = new ZCT(0, 0, 0);
                int scount = 1;
                try
                {
                    scount = b.meta.getShapeCount(im);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message.ToString());
                }
                for (int sc = 0; sc < scount; sc++)
                {
                    Status = "Reading ROI " + (sc + 1).ToString() + "/" + scount;
                    string typ = b.meta.getShapeType(im, sc);
                    ROI an = new ROI();
                    an.roiID = roiID;
                    an.roiName = roiName;
                    an.shapeIndex = sc;
                    if (typ == "Point")
                    {
                        an.type = ROI.Type.Point;
                        an.id = b.meta.getPointID(im, sc);
                        double dx = b.meta.getPointX(im, sc).doubleValue();
                        double dy = b.meta.getPointY(im, sc).doubleValue();
                        an.AddPoint(b.ToStageSpace(new PointD(dx, dy)));
                        an.coord = new ZCT();
                        ome.xml.model.primitives.NonNegativeInteger nz = b.meta.getPointTheZ(im, sc);
                        if (nz != null)
                            an.coord.Z = nz.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nc = b.meta.getPointTheC(im, sc);
                        if (nc != null)
                            an.coord.C = nc.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nt = b.meta.getPointTheT(im, sc);
                        if (nt != null)
                            an.coord.T = nt.getNumberValue().intValue();
                        an.Text = b.meta.getPointText(im, sc);
                        ome.units.quantity.Length fl = b.meta.getPointFontSize(im, sc);
                        if (fl != null)
                            an.fontSize = fl.value().intValue();
                        ome.xml.model.enums.FontFamily ff = b.meta.getPointFontFamily(im, sc);
                        if (ff != null)
                            an.family = ff.name();
                        ome.xml.model.primitives.Color col = b.meta.getPointStrokeColor(im, sc);
                        if (col != null)
                            an.strokeColor = Color.FromArgb(col.getAlpha(), col.getRed(), col.getGreen(), col.getBlue());
                        ome.units.quantity.Length fw = b.meta.getPointStrokeWidth(im, sc);
                        if (fw != null)
                            an.strokeWidth = (float)fw.value().floatValue();
                        ome.xml.model.primitives.Color colf = b.meta.getPointStrokeColor(im, sc);
                        if (colf != null)
                            an.fillColor = Color.FromArgb(colf.getAlpha(), colf.getRed(), colf.getGreen(), colf.getBlue());
                    }
                    else
                    if (typ == "Line")
                    {
                        an.type = ROI.Type.Line;
                        an.id = b.meta.getLineID(im, sc);
                        double px1 = b.meta.getLineX1(im, sc).doubleValue();
                        double py1 = b.meta.getLineY1(im, sc).doubleValue();
                        double px2 = b.meta.getLineX2(im, sc).doubleValue();
                        double py2 = b.meta.getLineY2(im, sc).doubleValue();
                        an.AddPoint(b.ToStageSpace(new PointD(px1, py1)));
                        an.AddPoint(b.ToStageSpace(new PointD(px2, py2)));
                        ome.xml.model.primitives.NonNegativeInteger nz = b.meta.getLineTheZ(im, sc);
                        if (nz != null)
                            co.Z = nz.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nc = b.meta.getLineTheC(im, sc);
                        if (nc != null)
                            co.C = nc.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nt = b.meta.getLineTheT(im, sc);
                        if (nt != null)
                            co.T = nt.getNumberValue().intValue();
                        an.coord = co;
                        an.Text = b.meta.getLineText(im, sc);
                        ome.units.quantity.Length fl = b.meta.getLineFontSize(im, sc);
                        if (fl != null)
                            an.fontSize = fl.value().intValue();
                        ome.xml.model.enums.FontFamily ff = b.meta.getLineFontFamily(im, sc);
                        if (ff != null)
                            an.family = ff.name();
                        ome.xml.model.primitives.Color col = b.meta.getLineStrokeColor(im, sc);
                        if (col != null)
                            an.strokeColor = Color.FromArgb(col.getAlpha(), col.getRed(), col.getGreen(), col.getBlue());
                        ome.units.quantity.Length fw = b.meta.getLineStrokeWidth(im, sc);
                        if (fw != null)
                            an.strokeWidth = (float)fw.value().floatValue();
                        ome.xml.model.primitives.Color colf = b.meta.getLineFillColor(im, sc);
                        if (colf != null)
                            an.fillColor = Color.FromArgb(colf.getAlpha(), colf.getRed(), colf.getGreen(), colf.getBlue());
                    }
                    else
                    if (typ == "Rectangle")
                    {
                        an.type = ROI.Type.Rectangle;
                        an.id = b.meta.getRectangleID(im, sc);
                        double px = b.meta.getRectangleX(im, sc).doubleValue();
                        double py = b.meta.getRectangleY(im, sc).doubleValue();
                        double pw = b.meta.getRectangleWidth(im, sc).doubleValue();
                        double ph = b.meta.getRectangleHeight(im, sc).doubleValue();
                        an.Rect = b.ToStageSpace(new RectangleD(px, py, pw, ph));
                        ome.xml.model.primitives.NonNegativeInteger nz = b.meta.getRectangleTheZ(im, sc);
                        if (nz != null)
                            co.Z = nz.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nc = b.meta.getRectangleTheC(im, sc);
                        if (nc != null)
                            co.C = nc.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nt = b.meta.getRectangleTheT(im, sc);
                        if (nt != null)
                            co.T = nt.getNumberValue().intValue();
                        an.coord = co;

                        an.Text = b.meta.getRectangleText(im, sc);
                        ome.units.quantity.Length fl = b.meta.getRectangleFontSize(im, sc);
                        if (fl != null)
                            an.fontSize = fl.value().intValue();
                        ome.xml.model.enums.FontFamily ff = b.meta.getRectangleFontFamily(im, sc);
                        if (ff != null)
                            an.family = ff.name();
                        ome.xml.model.primitives.Color col = b.meta.getRectangleStrokeColor(im, sc);
                        if (col != null)
                            an.strokeColor = Color.FromArgb(col.getAlpha(), col.getRed(), col.getGreen(), col.getBlue());
                        ome.units.quantity.Length fw = b.meta.getRectangleStrokeWidth(im, sc);
                        if (fw != null)
                            an.strokeWidth = (float)fw.value().floatValue();
                        ome.xml.model.primitives.Color colf = b.meta.getRectangleFillColor(im, sc);
                        if (colf != null)
                            an.fillColor = Color.FromArgb(colf.getAlpha(), colf.getRed(), colf.getGreen(), colf.getBlue());
                        ome.xml.model.enums.FillRule fr = b.meta.getRectangleFillRule(im, sc);

                    }
                    else
                    if (typ == "Ellipse")
                    {
                        an.type = ROI.Type.Ellipse;
                        an.id = b.meta.getEllipseID(im, sc);
                        double px = b.meta.getEllipseX(im, sc).doubleValue();
                        double py = b.meta.getEllipseY(im, sc).doubleValue();
                        double ew = b.meta.getEllipseRadiusX(im, sc).doubleValue();
                        double eh = b.meta.getEllipseRadiusY(im, sc).doubleValue();
                        //We convert the ellipse radius to Rectangle
                        double w = ew * 2;
                        double h = eh * 2;
                        double x = px - ew;
                        double y = py - eh;
                        an.Rect = b.ToStageSpace(new RectangleD(x, y, w, h));
                        ome.xml.model.primitives.NonNegativeInteger nz = b.meta.getEllipseTheZ(im, sc);
                        if (nz != null)
                            co.Z = nz.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nc = b.meta.getEllipseTheC(im, sc);
                        if (nc != null)
                            co.C = nc.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nt = b.meta.getEllipseTheT(im, sc);
                        if (nt != null)
                            co.T = nt.getNumberValue().intValue();
                        an.coord = co;
                        an.Text = b.meta.getEllipseText(im, sc);
                        ome.units.quantity.Length fl = b.meta.getEllipseFontSize(im, sc);
                        if (fl != null)
                            an.fontSize = fl.value().intValue();
                        ome.xml.model.enums.FontFamily ff = b.meta.getEllipseFontFamily(im, sc);
                        if (ff != null)
                            an.family = ff.name();
                        ome.xml.model.primitives.Color col = b.meta.getEllipseStrokeColor(im, sc);
                        if (col != null)
                            an.strokeColor = Color.FromArgb(col.getAlpha(), col.getRed(), col.getGreen(), col.getBlue());
                        ome.units.quantity.Length fw = b.meta.getEllipseStrokeWidth(im, sc);
                        if (fw != null)
                            an.strokeWidth = (float)fw.value().floatValue();
                        ome.xml.model.primitives.Color colf = b.meta.getEllipseFillColor(im, sc);
                        if (colf != null)
                            an.fillColor = Color.FromArgb(colf.getAlpha(), colf.getRed(), colf.getGreen(), colf.getBlue());
                    }
                    else
                    if (typ == "Polygon")
                    {
                        an.type = ROI.Type.Polygon;
                        an.id = b.meta.getPolygonID(im, sc);
                        an.closed = true;
                        string pxs = b.meta.getPolygonPoints(im, sc);
                        PointD[] pts = an.stringToPoints(pxs);
                        pts = b.ToStageSpace(pts);
                        if (pts.Length > 100)
                        {
                            an.type = ROI.Type.Freeform;
                        }
                        an.AddPoints(pts);
                        ome.xml.model.primitives.NonNegativeInteger nz = b.meta.getPolygonTheZ(im, sc);
                        if (nz != null)
                            co.Z = nz.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nc = b.meta.getPolygonTheC(im, sc);
                        if (nc != null)
                            co.C = nc.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nt = b.meta.getPolygonTheT(im, sc);
                        if (nt != null)
                            co.T = nt.getNumberValue().intValue();
                        an.coord = co;
                        an.Text = b.meta.getPolygonText(im, sc);
                        ome.units.quantity.Length fl = b.meta.getPolygonFontSize(im, sc);
                        if (fl != null)
                            an.fontSize = fl.value().intValue();
                        ome.xml.model.enums.FontFamily ff = b.meta.getPolygonFontFamily(im, sc);
                        if (ff != null)
                            an.family = ff.name();
                        ome.xml.model.primitives.Color col = b.meta.getPolygonStrokeColor(im, sc);
                        if (col != null)
                            an.strokeColor = Color.FromArgb(col.getAlpha(), col.getRed(), col.getGreen(), col.getBlue());
                        ome.units.quantity.Length fw = b.meta.getPolygonStrokeWidth(im, sc);
                        if (fw != null)
                            an.strokeWidth = (float)fw.value().floatValue();
                        ome.xml.model.primitives.Color colf = b.meta.getPolygonFillColor(im, sc);
                        if (colf != null)
                            an.fillColor = Color.FromArgb(colf.getAlpha(), colf.getRed(), colf.getGreen(), colf.getBlue());
                    }
                    else
                    if (typ == "Polyline")
                    {
                        an.type = ROI.Type.Polyline;
                        an.id = b.meta.getPolylineID(im, sc);
                        string pxs = b.meta.getPolylinePoints(im, sc);
                        PointD[] pts = an.stringToPoints(pxs);
                        for (int pi = 0; pi < pts.Length; pi++)
                        {
                            pts[pi] = b.ToStageSpace(pts[pi]);
                        }
                        an.AddPoints(an.stringToPoints(pxs));
                        ome.xml.model.primitives.NonNegativeInteger nz = b.meta.getPolylineTheZ(im, sc);
                        if (nz != null)
                            co.Z = nz.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nc = b.meta.getPolylineTheC(im, sc);
                        if (nc != null)
                            co.C = nc.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nt = b.meta.getPolylineTheT(im, sc);
                        if (nt != null)
                            co.T = nt.getNumberValue().intValue();
                        an.coord = co;
                        an.Text = b.meta.getPolylineText(im, sc);
                        ome.units.quantity.Length fl = b.meta.getPolylineFontSize(im, sc);
                        if (fl != null)
                            an.fontSize = fl.value().intValue();
                        ome.xml.model.enums.FontFamily ff = b.meta.getPolylineFontFamily(im, sc);
                        if (ff != null)
                            an.family = ff.name();
                        ome.xml.model.primitives.Color col = b.meta.getPolylineStrokeColor(im, sc);
                        if (col != null)
                            an.strokeColor = Color.FromArgb(col.getAlpha(), col.getRed(), col.getGreen(), col.getBlue());
                        ome.units.quantity.Length fw = b.meta.getPolylineStrokeWidth(im, sc);
                        if (fw != null)
                            an.strokeWidth = (float)fw.value().floatValue();
                        ome.xml.model.primitives.Color colf = b.meta.getPolylineFillColor(im, sc);
                        if (colf != null)
                            an.fillColor = Color.FromArgb(colf.getAlpha(), colf.getRed(), colf.getGreen(), colf.getBlue());
                    }
                    else
                    if (typ == "Label")
                    {
                        an.type = ROI.Type.Label;
                        an.id = b.meta.getLabelID(im, sc);

                        ome.xml.model.primitives.NonNegativeInteger nz = b.meta.getLabelTheZ(im, sc);
                        if (nz != null)
                            co.Z = nz.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nc = b.meta.getLabelTheC(im, sc);
                        if (nc != null)
                            co.C = nc.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nt = b.meta.getLabelTheT(im, sc);
                        if (nt != null)
                            co.T = nt.getNumberValue().intValue();
                        an.coord = co;

                        ome.units.quantity.Length fl = b.meta.getLabelFontSize(im, sc);
                        if (fl != null)
                            an.fontSize = fl.value().intValue();
                        ome.xml.model.enums.FontFamily ff = b.meta.getLabelFontFamily(im, sc);
                        if (ff != null)
                            an.family = ff.name();
                        ome.xml.model.primitives.Color col = b.meta.getLabelStrokeColor(im, sc);
                        if (col != null)
                            an.strokeColor = Color.FromArgb(col.getAlpha(), col.getRed(), col.getGreen(), col.getBlue());
                        ome.units.quantity.Length fw = b.meta.getLabelStrokeWidth(im, sc);
                        if (fw != null)
                            an.strokeWidth = (float)fw.value().floatValue();
                        ome.xml.model.primitives.Color colf = b.meta.getLabelFillColor(im, sc);
                        if (colf != null)
                            an.fillColor = Color.FromArgb(colf.getAlpha(), colf.getRed(), colf.getGreen(), colf.getBlue());
                        PointD p = new PointD(b.meta.getLabelX(im, sc).doubleValue(), b.meta.getLabelY(im, sc).doubleValue());
                        an.AddPoint(b.ToStageSpace(p));
                        an.Text = b.meta.getLabelText(im, sc);
                    }
                    else
                    if (typ == "Mask")
                    {
                        byte[] bts = b.meta.getMaskBinData(im, sc);
                        bool end = b.meta.getMaskBinDataBigEndian(im, sc).booleanValue();
                        double h = b.meta.getMaskHeight(im, sc).doubleValue();
                        double w = b.meta.getMaskWidth(im, sc).doubleValue();
                        double x = b.meta.getMaskX(im, sc).doubleValue();
                        double y = b.meta.getMaskY(im, sc).doubleValue();
                        an = ROI.CreateMask(co, bts, (int)Math.Round(w / b.PhysicalSizeX), (int)Math.Round(h / b.PhysicalSizeY), new PointD(x * b.PhysicalSizeX, y * b.PhysicalSizeY), b.PhysicalSizeX, b.PhysicalSizeY);
                        an.Text = b.meta.getMaskText(im, sc);
                        an.id = b.meta.getMaskID(im, sc);
                        an.Rect = new RectangleD(an.X, an.Y, an.W, an.H);
                        ome.xml.model.primitives.NonNegativeInteger nz = b.meta.getMaskTheZ(im, sc);
                        if (nz != null)
                            co.Z = nz.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nc = b.meta.getMaskTheC(im, sc);
                        if (nc != null)
                            co.C = nc.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nt = b.meta.getMaskTheT(im, sc);
                        if (nt != null)
                            co.T = nt.getNumberValue().intValue();
                        an.coord = co;

                        ome.units.quantity.Length fl = b.meta.getMaskFontSize(im, sc);
                        if (fl != null)
                            an.fontSize = fl.value().intValue();
                        ome.xml.model.enums.FontFamily ff = b.meta.getMaskFontFamily(im, sc);
                        if (ff != null)
                            an.family = ff.name();
                        ome.xml.model.primitives.Color col = b.meta.getMaskStrokeColor(im, sc);
                        if (col != null)
                            an.strokeColor = Color.FromArgb(col.getAlpha(), col.getRed(), col.getGreen(), col.getBlue());
                        ome.units.quantity.Length fw = b.meta.getMaskStrokeWidth(im, sc);
                        if (fw != null)
                            an.strokeWidth = (float)fw.value().floatValue();
                        ome.xml.model.primitives.Color colf = b.meta.getMaskFillColor(im, sc);
                        if (colf != null)
                            an.fillColor = Color.FromArgb(colf.getAlpha(), colf.getRed(), colf.getGreen(), colf.getBlue());
                    }
                    b.Annotations.Add(an);
                }
            }

            List<string> serFiles = new List<string>();
            serFiles.AddRange(reader.getSeriesUsedFiles());

            b.Buffers = new List<Bitmap>();
            if (b.Type == ImageType.pyramidal)
            {

                try
                {

                    string st = OpenSlideImage.DetectVendor(file);
                    if (st != null && !file.EndsWith("ome.tif") && useOpenSlide)
                    {
                        Status = "Opening file with OpenSlide.";
                        b.openSlideImage = OpenSlideImage.Open(file);
                        b.openslideBase = (OpenSlideBase)OpenSlideGTK.SlideSourceBase.Create(file, true);
                    }
                    else
                    {
                        Status = "Opening file with BioFormats.";
                        b.slideBase = new SlideBase(b, SlideImage.Open(b));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message.ToString());
                    b.slideBase = new SlideBase(b, SlideImage.Open(b));
                }
                tile = true;
            }
            // read the image data bytes
            int pages = reader.getImageCount();
            bool inter = reader.isInterleaved();
            int z = 0;
            int c = 0;
            int t = 0;
            if (!tile)
            {
                Status = "Reading image planes.";
                try
                {
                    for (int p = 0; p < pages; p++)
                    {
                        Progress = ((float)p / (float)pages) * 100;
                        Bitmap bf;
                        byte[] bytes = reader.openBytes(p);
                        bf = new Bitmap(file, SizeX, SizeY, PixelFormat, bytes, new ZCT(z, c, t), p, null, b.littleEndian, inter);
                        b.Buffers.Add(bf);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

            }
            else
            {
                Status = "Reading tiles.";
                b.imRead = reader;
                for (int p = 0; p < pages; p++)
                {
                    Progress = ((float)p / (float)pages) * 100;
                    b.Buffers.Add(GetTile(b, p, b.Level, tilex, tiley, tileSizeX, tileSizeY));
                }
            }
            int pls;
            try
            {
                pls = b.meta.getPlaneCount(serie);
            }
            catch (Exception)
            {
                pls = 0;
            }
            if (pls == b.Buffers.Count)
                for (int bi = 0; bi < b.Buffers.Count; bi++)
                {
                    Plane pl = new Plane();
                    pl.Coordinate = new ZCT();
                    double px = 0; double py = 0; double pz = 0;
                    if (b.meta.getPlanePositionX(serie, bi) != null)
                        px = b.meta.getPlanePositionX(serie, bi).value().doubleValue();
                    if (b.meta.getPlanePositionY(serie, bi) != null)
                        py = b.meta.getPlanePositionY(serie, bi).value().doubleValue();
                    if (b.meta.getPlanePositionZ(serie, bi) != null)
                        pz = b.meta.getPlanePositionZ(serie, bi).value().doubleValue();
                    pl.Location = new AForge.Point3D(px, py, pz);
                    int cc = 0; int zc = 0; int tc = 0;
                    if (b.meta.getPlaneTheC(serie, bi) != null)
                        cc = b.meta.getPlaneTheC(serie, bi).getNumberValue().intValue();
                    if (b.meta.getPlaneTheZ(serie, bi) != null)
                        zc = b.meta.getPlaneTheZ(serie, bi).getNumberValue().intValue();
                    if (b.meta.getPlaneTheT(serie, bi) != null)
                        tc = b.meta.getPlaneTheT(serie, bi).getNumberValue().intValue();
                    pl.Coordinate = new ZCT(zc, cc, tc);
                    if (b.meta.getPlaneDeltaT(serie, bi) != null)
                        pl.Delta = b.meta.getPlaneDeltaT(serie, bi).value().doubleValue();
                    if (b.meta.getPlaneExposureTime(serie, bi) != null)
                        pl.Exposure = b.meta.getPlaneExposureTime(serie, bi).value().doubleValue();
                    b.Buffers[bi].Plane = pl;
                }
            //Bioformats gives a size of 3 for C when saved in ImageJ as RGB. We need to correct for this as C should be 1 for RGB.
            if (RGBChannelCount >= 3)
            {
                b.sizeC = sumSamples / b.Channels[0].SamplesPerPixel;
            }
            string ord = reader.getDimensionOrder();
            if (ord == "XYZCT")
                b.UpdateCoords(b.SizeZ, b.SizeC, b.SizeT, Order.ZCT);
            else if (ord == "XYCZT")
                b.UpdateCoords(b.SizeZ, b.SizeC, b.SizeT, Order.CZT);
            else if (ord == "XYTCZ")
                b.UpdateCoords(b.SizeZ, b.SizeC, b.SizeT, Order.TCZ);
            AutoThreshold(b, true);
            if (b.bitsPerPixel > 8)
                b.StackThreshold(true);
            else
                b.StackThreshold(false);
            try
            {
                if (b.Type == ImageType.stack)
                    reader.close();
                if (addToImages)
                    Images.AddImage(b);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            b.Loading = false;
            Recorder.Record(BioLib.Recorder.GetCurrentMethodInfo(), false, file, serie, tab, addToImages, tile, tilex, tiley, tileSizeX, tileSizeY);
            return b;
        }

        public int GetFrameIndex(int z, int c, int t)
        {
            try
            {
                if (StackOrder == Order.ZCT)
                {
                    return Coords[z, c, t];
                }
                else if (StackOrder == Order.CZT)
                {
                    return Coords[c, z, t];
                }
                else
                    return Coords[t, c, z];
            }
            catch (Exception e)
            {
                return 0;
            }

        }
        public void SetFrameIndex(int z, int c, int t, int val)
        {
            Coords[z, c, t] = val;
        }
        public ImageReader imRead;
        public Tiff tifRead;
        public OpenSlideImage openSlideImage;
        static Bitmap bm;
        public enum BackEnd
        {
            OpenSlide,
            Bioformats,
            LibVips,
        }
        static bool useVips = true;
        static bool useGPU = true;
        /// It reads a tile from a file, and returns a bitmap
        /// 
        /// @param BioImage This is a class that contains the image file name, the image reader, and the
        /// coordinates of the image.
        /// @param ZCT Z, C, T
        /// @param serie the series number (0-based)
        /// @param tilex the x coordinate of the tile
        /// @param tiley the y coordinate of the tile
        /// @param tileSizeX the width of the tile
        /// @param tileSizeY the height of the tile
        /// 
        /// @return A Bitmap object.
        public static Bitmap GetTile(BioImage b, int index, int level, int tilex, int tiley, int tileSizeX, int tileSizeY)
        {
            if (b.Tag != null)
            {
                if (b.Tag.ToString() != "OMERO")
                    return null;
                //This is a OMERO file we need to update it.
                int i = 0;
                for (int z = 0; z < b.SizeZ; z++)
                {
                    for (int c = 0; c < b.SizeC; c++)
                    {
                        for (int t = 0; t < b.SizeT; t++)
                        {
                            if(i == index)
                                return OMERO.GetTile(b, new ZCT(z,c,t), tilex, tiley, tileSizeX, tileSizeY, level);
                            i++;
                        }
                    }
                }
            }
            if (vips && b.vipPages.Count > 0)
            {
                //We can get a tile faster with libvips rather than bioformats.
                Bitmap bmp = ExtractRegionFromTiledTiff(b, tilex, tiley, tileSizeX, tileSizeY, level);
                if (bmp != null)
                {
                    return bmp;
                }
            }
            //We check if we can open this with OpenSlide as this is faster than Bioformats with IKVM.
            if (b.openSlideImage != null)
            {
                byte[] bts = b.openSlideImage.ReadRegion(level, tilex, tiley, tileSizeX, tileSizeY);
                Bitmap bm = new Bitmap("", tileSizeX, tileSizeY, AForge.PixelFormat.Format32bppArgb, bts, new ZCT(), 0, null, true, true);
                return bm;
            }

            string curfile = b.imRead.getCurrentFile();
            if (curfile == null)
            {
                b.meta = (IMetadata)((OMEXMLService)factory.getInstance(typeof(OMEXMLService))).createOMEXMLMetadata();
                b.imRead.close();
                b.imRead.setMetadataStore(b.meta);
                b.imRead.setId(b.file);
            }
            else
            {
                string fi = b.file.Replace("\\", "/");
                string cf = curfile.Replace("\\", "/");
                if (cf != fi)
                {
                    b.imRead.close();
                    b.meta = (IMetadata)((OMEXMLService)factory.getInstance(typeof(OMEXMLService))).createOMEXMLMetadata();
                    b.imRead.setMetadataStore(b.meta);
                    b.imRead.setId(b.file);
                }
            }
            if (b.imRead.getSeries() != level)
                b.imRead.setSeries(level);
            int SizeX = b.imRead.getSizeX();
            int SizeY = b.imRead.getSizeY();
            bool flat = b.imRead.hasFlattenedResolutions();
            bool littleEndian = b.imRead.isLittleEndian();
            bool interleaved = b.imRead.isInterleaved();
            PixelFormat PixelFormat = b.Resolutions[level].PixelFormat;
            if (tilex < 0)
                tilex = 0;
            if (tiley < 0)
                tiley = 0;
            if (tilex >= SizeX)
                tilex = SizeX;
            if (tiley >= SizeY)
                tiley = SizeY;
            int sx = tileSizeX;
            if (tilex + tileSizeX > SizeX)
                sx -= (tilex + tileSizeX) - (SizeX);
            int sy = tileSizeY;
            if (tiley + tileSizeY > SizeY)
                sy -= (tiley + tileSizeY) - (SizeY);
            //For some reason calling openBytes with 1x1px area causes an exception. 
            if (sx <= 1)
                return null;
            if (sy <= 1)
                return null;
            try
            {
                byte[] bytesr = b.imRead.openBytes(index, tilex, tiley, sx, sy);
                return new Bitmap(b.file, sx, sy, PixelFormat, bytesr, new ZCT(), index, null, littleEndian, interleaved);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }
        public BioImage GetRegion(int x, int y, int w, int h)
        {
            string id = System.IO.Path.GetFileNameWithoutExtension(Filename);
            id.Replace(".ome", "");
            BioImage bm = Copy();
            bm.ID = id + ".ome.tif";
            bm.Filename = id + ".ome.tif";
            bm.Volume = new VolumeD(new Point3D(StageSizeX + (PhysicalSizeX * PyramidalOrigin.X), StageSizeY + (PhysicalSizeY * PyramidalOrigin.Y), StageSizeZ),
                new Point3D(w * PhysicalSizeX, h * PhysicalSizeY, SizeZ * PhysicalSizeZ));
            bm.Resolutions.Add(new Resolution(w, h, bm.Buffers[0].PixelFormat, PhysicalSizeX, PhysicalSizeY, PhysicalSizeZ, StageSizeX, StageSizeY, StageSizeZ));
            bm.Resolutions.RemoveAt(0);
            Recorder.AddLine("BioLib.Images.GetImage(\"" + bm.ID + "\").GetRegion(" + x + "," + y + "," + w + "," + h + ");", false);
            return bm;
        }
        /// This function sets the minimum and maximum values of the image to the minimum and maximum
        /// values of the stack
        /// 
        /// @param bit16 true = 16 bit, false = 8 bit
        public void StackThreshold(bool bit16)
        {
            if (bit16)
            {
                for (int ch = 0; ch < Channels.Count; ch++)
                {
                    for (int i = 0; i < Channels[ch].range.Length; i++)
                    {
                        Channels[ch].range[i].Min = (int)Channels[ch].stats[i].StackMin;
                        Channels[ch].range[i].Max = (int)Channels[ch].stats[i].StackMax;
                    }
                    Channels[ch].BitsPerPixel = 16;
                }
                bitsPerPixel = 16;
            }
            else
            {
                for (int ch = 0; ch < Channels.Count; ch++)
                {
                    for (int i = 0; i < Channels[ch].range.Length; i++)
                    {
                        Channels[ch].range[i].Min = (int)Channels[ch].stats[i].StackMin;
                        Channels[ch].range[i].Max = (int)Channels[ch].stats[i].StackMax;
                    }
                    Channels[ch].BitsPerPixel = 8;
                }
                bitsPerPixel = 8;
            }
            Recorder.AddLine("BioLib.Images.GetImage(\"" + id + "\").StackThreshold(" + bit16.ToString().ToLower() + ");", true);
        }
        /// > If the number is less than or equal to 255, then it's 8 bits. If it's less than or equal
        /// to 512, then it's 9 bits. If it's less than or equal to 1023, then it's 10 bits. If it's
        /// less than or equal to 2047, then it's 11 bits. If it's less than or equal to 4095, then it's
        /// 12 bits. If it's less than or equal to 8191, then it's 13 bits. If it's less than or equal
        /// to 16383, then it's 14 bits. If it's less than or equal to 32767, then it's 15 bits. If it's
        /// less than or equal to 65535, then it's 16 bits
        /// 
        /// @param bt The number of bits per pixel.
        /// 
        /// @return The number of bits per pixel.
        public static int GetBitsPerPixel(int bt)
        {
            if (bt <= 255)
                return 8;
            if (bt <= 512)
                return 9;
            else if (bt <= 1023)
                return 10;
            else if (bt <= 2047)
                return 11;
            else if (bt <= 4095)
                return 12;
            else if (bt <= 8191)
                return 13;
            else if (bt <= 16383)
                return 14;
            else if (bt <= 32767)
                return 15;
            else
                return 16;
        }
        /// It returns the maximum value of a bit.
        /// 
        /// @param bt bit depth
        /// 
        /// @return The maximum value of a bit.
        public static int GetBitMaxValue(int bt)
        {
            if (bt == 8)
                return 255;
            if (bt == 9)
                return 512;
            else if (bt == 10)
                return 1023;
            else if (bt == 11)
                return 2047;
            else if (bt == 12)
                return 4095;
            else if (bt == 13)
                return 8191;
            else if (bt == 14)
                return 16383;
            else if (bt == 15)
                return 32767;
            else
                return 65535;
        }
        /// <summary>
        /// The function takes a list of points in stage space and converts them to image space using
        /// the provided stage and physical size parameters.
        /// </summary>
        /// <param name="p">A list of PointD objects representing points in stage space.</param>
        /// <param name="stageSizeX">The width of the stage or canvas in pixels.</param>
        /// <param name="stageSizeY">The stageSizeY parameter represents the size of the stage or canvas
        /// in the Y-axis direction. It is used to calculate the Y-coordinate of each point in the p
        /// list in image space.</param>
        /// <param name="physicalSizeX">The physical size of the X-axis in the image space.</param>
        /// <param name="physicalSizeY">The physical size of the Y-axis in the coordinate system of the
        /// stage or image.</param>
        /// <returns>
        /// The method is returning an array of PointD objects.
        /// </returns>
        public static PointD[] ToImageSpace(List<PointD> p, double stageSizeX, double stageSizeY, double physicalSizeX, double physicalSizeY)
        {
            PointD[] ps = new PointD[p.Count];
            for (int i = 0; i < p.Count; i++)
            {
                PointD pp = new PointD();
                pp.X = ((p[i].X - stageSizeX) / physicalSizeX);
                pp.Y = ((p[i].Y - stageSizeY) / physicalSizeY);
                ps[i] = pp;
            }
            return ps;
        }
        /// If the bits per pixel is 8, then the pixel format is either 8bppIndexed, 24bppRgb, or
        /// 32bppArgb. If the bits per pixel is 16, then the pixel format is either 16bppGrayScale or
        /// 48bppRgb
        /// 
        /// @param rgbChannelCount The number of channels in the image. For example, a grayscale image
        /// has 1 channel, a color image has 3 channels (red, green, blue).
        /// @param bitsPerPixel 8 or 16
        /// 
        /// @return The PixelFormat of the image.
        public static PixelFormat GetPixelFormat(int rgbChannelCount, int bitsPerPixel)
        {
            if (bitsPerPixel == 8)
            {
                if (rgbChannelCount == 1)
                    return PixelFormat.Format8bppIndexed;
                else if (rgbChannelCount == 3)
                    return PixelFormat.Format24bppRgb;
                else if (rgbChannelCount == 4)
                    return PixelFormat.Format32bppArgb;
            }
            else
            {
                if (rgbChannelCount == 1)
                    return PixelFormat.Format16bppGrayScale;
                if (rgbChannelCount == 3)
                    return PixelFormat.Format48bppRgb;
            }
            throw new NotSupportedException("Not supported pixel format.");
        }
        /// The function returns the appropriate PixelFormat based on the number of RGB channels and the
        /// pixel type.
        /// 
        /// @param rgbChannelCount The `rgbChannelCount` parameter represents the number of channels in
        /// the RGB color model. It can have a value of either 1 (for grayscale images) or 3 (for RGB
        /// color images).
        /// @param px The parameter "px" is of type ome.xml.model.enums.PixelType. It represents the
        /// pixel type of the image, such as INT8, UINT8, INT16, or UINT16.
        /// 
        /// @return The method returns a PixelFormat value based on the input parameters.
        public static PixelFormat GetPixelFormat(int rgbChannelCount, ome.xml.model.enums.PixelType px)
        {
            if (rgbChannelCount == 1)
            {
                if (px == ome.xml.model.enums.PixelType.INT8 || px == ome.xml.model.enums.PixelType.UINT8)
                    return PixelFormat.Format8bppIndexed;
                else if (px == ome.xml.model.enums.PixelType.INT16 || px == ome.xml.model.enums.PixelType.UINT16)
                    return PixelFormat.Format16bppGrayScale;
            }
            else if (rgbChannelCount == 3)
            {
                if (px == ome.xml.model.enums.PixelType.INT8 || px == ome.xml.model.enums.PixelType.UINT8)
                    return PixelFormat.Format24bppRgb;
                else if (px == ome.xml.model.enums.PixelType.INT16 || px == ome.xml.model.enums.PixelType.UINT16)
                    return PixelFormat.Format48bppRgb;
            }
            else
                return PixelFormat.Format32bppArgb;
            throw new InvalidDataException("RGBChannels Count of " + rgbChannelCount + " not supported.");
        }
        /// It opens a file, checks if it's tiled, if it is, it opens it as a tiled image, if not, it
        /// opens it as a normal image
        /// 
        /// @param file the file path
        /// @param tab open in new tab
        /// @param addToImages add to images list.
        /// @return An array of BioImage objects.
        public static BioImage[] OpenOMESeries(string file, bool tab, bool addToImages)
        {
            //We wait incase OME has not initialized yet.
            if (!initialized)
                do
                {
                    Thread.Sleep(100);
                    //Application.DoEvents();
                } while (!Initialized);
            var meta = (IMetadata)((OMEXMLService)new ServiceFactory().getInstance(typeof(OMEXMLService))).createOMEXMLMetadata();
            reader.setMetadataStore((MetadataStore)meta);
            file = file.Replace("\\", "/");
            try
            {
                if (reader.getCurrentFile() != file)
                {
                    Status = "Opening OME Image: " + file;
                    file = file.Replace("\\", "/");
                    reader.setId(file);
                }
            }
            catch (Exception e)
            {
                return null;
            }
            int count = reader.getSeriesCount();
            BioImage[] bs = new BioImage[count];
            for (int i = 0; i < count; i++)
            {
                bs[i] = OpenOME(file, i, tab, addToImages, false, 0, 0, 0, 0);
                if (bs[i] == null)
                    return null;
            }
            return bs;
        }
        /// It opens a file in a new thread.
        /// 
        /// @param file The file to open
        public static async Task OpenAsync(string file, bool OME, bool newtab, bool images, int series)
        {
            openfile = file;
            omes = OME;
            tab = newtab;
            add = images;
            serie = series;
            await Task.Run(OpenThread);
        }
        /// It opens a file asynchronously
        /// 
        /// @param files The file(s) to open.
        public static async Task OpenAsync(string[] files, bool OME, bool tab, bool images)
        {
            foreach (string file in files)
            {
                await OpenAsync(file, OME, tab, images, 0);
            }
        }
        /// It opens a file
        /// 
        /// @param file The file to open.
        public static void Open(string file)
        {
            OpenFile(file);
        }
        /// It opens a file
        /// 
        /// @param files The files to open.
        public static void Open(string[] files)
        {
            foreach (string file in files)
            {
                Open(file);
            }
        }
        /// It takes a list of files, opens them, and then combines them into a single BioImage object
        /// 
        /// @param files an array of file paths
        /// 
        /// @return A BioImage object.
        public static BioImage ImagesToStack(string[] files, bool tab)
        {
            BioImage[] bs = new BioImage[files.Length];
            int z = 0;
            int c = 0;
            int t = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string str = Path.GetFileNameWithoutExtension(files[i]);
                str = str.Replace(".ome", "");
                string[] st = str.Split('_');
                if (st.Length > 3)
                {
                    z = int.Parse(st[1].Replace("Z", ""));
                    c = int.Parse(st[2].Replace("C", ""));
                    t = int.Parse(st[3].Replace("T", ""));
                }
                if (i == 0)
                    bs[0] = OpenOME(files[i], tab);
                else
                {
                    bs[i] = OpenFile(files[i], 0, tab, false);
                }
            }
            BioImage b = BioImage.CopyInfo(bs[0], true, true);
            for (int i = 0; i < files.Length; i++)
            {
                for (int bc = 0; bc < bs[i].Buffers.Count; bc++)
                {
                    b.Buffers.Add(bs[i].Buffers[bc]);
                }
            }
            b.UpdateCoords(z + 1, c + 1, t + 1);
            b.Volume = new VolumeD(bs[0].Volume.Location, new Point3D(bs[0].SizeX * bs[0].PhysicalSizeX, bs[0].SizeY * bs[0].PhysicalSizeY, (z + 1) * bs[0].PhysicalSizeZ));
            Recorder.Record(BioLib.Recorder.GetCurrentMethodInfo(), false, files, tab);
            return b;
        }
        /// The function takes a BioImage object, opens the file, and returns a updated BioImage object
        /// 
        /// @param BioImage This is the class that contains the image data.
        public static void Update(BioImage b)
        {
            b = OpenFile(b.file);
        }
        /// <summary>
        /// Updates the Buffers based on current pyramidal origin and resolution.
        /// </summary>
        public async Task UpdateBuffersPyramidal()
        {
            try
            {
                if (Type != ImageType.pyramidal)
                    return;
                for (int i = 0; i < Buffers.Count; i++)
                {
                    Buffers[i].Dispose();
                }
                Buffers.Clear();
                for (int z = 0; z < SizeZ; z++)
                {
                    for (int c = 0; c < SizeC; c++)
                    {
                        for (int t = 0; t < SizeT; t++)
                        {
                            ZCT co = new ZCT(z, c, t);
                            if (openSlideImage != null)
                            {
                            startos:
                                int lev = LevelFromResolution(this.Resolution);
                                openslideBase.SetSliceInfo(lev, PixelFormat.Format24bppRgb, co);
                                byte[] bts = openslideBase.GetSlice(new OpenSlideGTK.SliceInfo(PyramidalOrigin.X, PyramidalOrigin.Y, PyramidalSize.Width, PyramidalSize.Height, resolution));
                                if (bts == null)
                                {
                                    pyramidalOrigin = new PointD(0, 0);
                                    Resolution = GetUnitPerPixel(lev) * 1.1f;
                                    goto startos;
                                }
                                Buffers.Add(new Bitmap((int)Math.Round(OpenSlideBase.destExtent.Width), (int)Math.Round(OpenSlideBase.destExtent.Height), PixelFormat.Format24bppRgb, bts, co, ""));
                            }
                            else
                            {
                            start:
                                int lev = LevelFromResolution(this.Resolution);
                                byte[] bts = await slideBase.GetSlice(new BioLib.SliceInfo(Math.Round(PyramidalOrigin.X), Math.Round(PyramidalOrigin.Y), PyramidalSize.Width, PyramidalSize.Height, resolution, co));
                                if (bts == null)
                                {
                                    pyramidalOrigin = new PointD(0, 0);
                                    Resolution = GetUnitPerPixel(lev) * 1.1f;
                                    goto start;
                                }
                                Bitmap bmp = new Bitmap((int)Math.Round(SlideBase.destExtent.Width), (int)Math.Round(SlideBase.destExtent.Height), Resolutions[Level].PixelFormat, bts, co, "");
                                Buffers.Add(bmp);
                            }
                        }
                    }
                }
                BioImage.AutoThreshold(this, false);
                if (bitsPerPixel > 8)
                    StackThreshold(true);
                else
                    StackThreshold(false);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public void UpdateBuffersWells()
        {
            try
            {
                if (Type != ImageType.well)
                    return;
                for (int i = 0; i < Buffers.Count; i++)
                {
                    Buffers[i].Dispose();
                }
                Buffers.Clear();
                if (imRead.getSeries() != Level)
                    this.imRead.setSeries(Level);
                int w = imRead.getSizeX();
                int h = imRead.getSizeY();
                for (int z = 0; z < SizeZ; z++)
                {
                    for (int c = 0; c < SizeC; c++)
                    {
                        for (int t = 0; t < SizeT; t++)
                        {
                            byte[] bm = this.imRead.openBytes(Coords[z, c, t]);
                            Bitmap bmp = new Bitmap(w, h, Resolutions[Level].PixelFormat, bm, new ZCT(z, c, t), "");
                            bmp.Stats = Statistics.FromBytes(bmp);
                            Buffers.Add(bmp);
                        }
                    }
                }
                BioImage.AutoThreshold(this, false);
                if (bitsPerPixel > 8)
                    StackThreshold(true);
                else
                    StackThreshold(false);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        public async Task<Bitmap[]> GetSlice(int x, int y, int w, int h, double resolution)
        {
            List<Bitmap> Buffers = new List<Bitmap>();
            for (int i = 0; i < imagesPerSeries; i++)
            {
                if (openSlideImage != null)
                {
                    int lev = LevelFromResolution(Resolution);
                    openslideBase.SetSliceInfo(lev, Resolutions[lev].PixelFormat, Coordinate);
                    byte[] bts = openslideBase.GetSlice(new OpenSlideGTK.SliceInfo(x, y, w, h, resolution));
                    Buffers.Add(new Bitmap((int)Math.Round(OpenSlideBase.destExtent.Width), (int)Math.Round(OpenSlideBase.destExtent.Height), PixelFormat.Format24bppRgb, bts, new ZCT(), ""));
                }
                else
                {
                start:
                    byte[] bts = await slideBase.GetSlice(new SliceInfo(x, y, w, h, resolution, Coordinate));
                    if (bts == null)
                    {
                        if (x == 0 && y == 0)
                        {
                            Resolution = GetUnitPerPixel(0);
                        }
                        pyramidalOrigin = new PointD(0, 0);
                        goto start;
                    }
                    Buffers.Add(new Bitmap((int)Math.Round(SlideBase.destExtent.Width), (int)Math.Round(SlideBase.destExtent.Height), Resolutions[Level].PixelFormat, bts, new ZCT(), ""));
                }
            }
            Recorder.Record(BioLib.Recorder.GetCurrentMethodInfo(), true, x, y, w, h, resolution);
            return Buffers.ToArray();
        }
        /// > Update() is a function that calls the Update() function of the parent class
        public void Update()
        {
            Update(this);
        }

        public void Rename(string name)
        {
            this.Filename = Path.GetFileName(name);
            this.ID = name;
        }
        static string openfile;
        static bool omes, tab, add;
        static int serie;
        static void OpenThread()
        {
            if (omes)
                OpenOME(openfile, serie, tab, add, false, 0, 0, 0, 0);
            else
                OpenFile(openfile, serie, tab, add);
        }
        static string savefile, saveid;
        static bool some;
        static int sserie;
        static void SaveThread()
        {
            if (some)
                SaveOME(savefile, saveid);
            else
                SaveFile(savefile, saveid);
        }
        /// <summary>
        /// The SaveAsync function saves data to a file asynchronously.
        /// </summary>
        /// <param name="file">The file parameter is a string that represents the file path or name
        /// where the data will be saved.</param>
        /// <param name="id">The "id" parameter is a string that represents an identifier for the save
        /// operation. It could be used to uniquely identify the saved data or to specify a specific
        /// location or format for the saved file.</param>
        /// <param name="serie">The "serie" parameter is an integer that represents a series or sequence
        /// number. It is used as a parameter in the SaveAsync method.</param>
        /// <param name="ome">The "ome" parameter is a boolean value that determines whether or not to
        /// perform a specific action in the saving process.</param>
        public static async Task SaveAsync(string file, string id, int serie, bool ome)
        {
            savefile = file;
            saveid = id;
            some = ome;
            await Task.Run(SaveThread);
        }

        static List<string> sts = new List<string>();
        static void SaveSeriesThread()
        {
            if (omes)
            {
                BioImage[] bms = new BioImage[sts.Count];
                int i = 0;
                foreach (string st in sts)
                {
                    bms[i] = Images.GetImage(st);
                }
                SaveOMESeries(bms, savefile, Planes);
            }
            else
                SaveSeries(sts.ToArray(), savefile);
        }
        /// <summary>
        /// The function `SaveSeriesAsync` saves a series of `BioImage` objects to a file asynchronously.
        /// </summary>
        /// <param name="imgs">imgs is an array of BioImage objects.</param>
        /// <param name="file">The "file" parameter is a string that represents the file path where the
        /// series of BioImages will be saved.</param>
        /// <param name="ome">The "ome" parameter is a boolean flag that indicates whether the images
        /// should be saved in OME-TIFF format or not. If "ome" is set to true, the images will be saved
        /// in OME-TIFF format. If "ome" is set to false, the images will be</param>
        public static async Task SaveSeriesAsync(BioImage[] imgs, string file, bool ome)
        {
            sts.Clear();
            foreach (BioImage item in imgs)
            {
                sts.Add(item.ID);
            }
            savefile = file;
            some = ome;
            await Task.Run(SaveSeriesThread);
        }
        static Enums.ForeignTiffCompression comp;
        static int compLev = 0;
        static BioImage[] bms;
        static void SavePyramidalThread()
        {
            SaveOMEPyramidal(bms, savefile, comp, compLev);
        }
        /// <summary>
        /// The function `SavePyramidalAsync` saves an array of `BioImage` objects as a pyramidal TIFF
        /// file asynchronously.
        /// </summary>
        /// <param name="imgs">imgs is an array of BioImage objects.</param>
        /// <param name="file">The "file" parameter is a string that represents the file path where the
        /// pyramidal image will be saved.</param>
        /// <param name="com">The parameter "com" is of type Enums.ForeignTiffCompression, which is an
        /// enumeration representing different compression options for the TIFF file.</param>
        /// <param name="compLevel">The `compLevel` parameter is an integer that represents the
        /// compression level for the TIFF file. It is used to specify the level of compression to be
        /// applied to the image data when saving the pyramidal image. The higher the compression level,
        /// the smaller the file size but potentially lower image quality.</param>
        public static async Task SavePyramidalAsync(BioImage[] imgs, string file, Enums.ForeignTiffCompression com, int compLevel)
        {
            bms = imgs;
            savefile = file;
            comp = com;
            compLev = compLevel;
            await Task.Run(SavePyramidalThread);
        }
        private static List<string> openOMEfile = new List<string>();
        /// It opens the OME file.
        private static void OpenOME()
        {
            foreach (string f in openOMEfile)
            {
                OpenOME(f, true);
            }
            openOMEfile.Clear();
        }
        private static string saveOMEfile;
        private static string saveOMEID;
        static NetVips.Image netim;
        /// The function checks if a given file is supported by the Vips library and returns true if it
        /// is, false otherwise.
        /// 
        /// @param file The "file" parameter is a string that represents the file path of the TIFF image
        /// that you want to load using the NetVips library.
        /// 
        /// @return The method is returning a boolean value. If the try block is executed successfully,
        /// it will return true. If an exception is caught, it will return false.
        public static bool VipsSupport(string file)
        {
            
            try
            {
                if (isOME(file))
                {
                    return true;
                }
                netim = NetVips.Image.Tiffload(file);
                Settings.AddSettings("VipsSupport", "true");
                netim.Close();
                netim.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
            return true;
        }
        private static Stopwatch st = new Stopwatch();
        private static ServiceFactory factory;
        private static OMEXMLService service;
        private static ImageReader reader;
        private static ImageWriter writer;
        private loci.formats.meta.IMetadata meta;

        //We use UNIX type line endings since they are supported by ImageJ & BioGTK.
        public const char NewLine = '\n';
        public const string columns = "ROIID,ROINAME,TYPE,ID,SHAPEINDEX,TEXT,S,C,Z,T,X,Y,W,H,POINTS,STROKECOLOR,STROKECOLORW,FILLCOLOR,FONTSIZE\n";

        /// > Open the file, get the image description field, and return it as a string
        /// 
        /// @param file the path to the file
        /// 
        /// @return The image description of the tiff file.
        public static string OpenXML(string file)
        {
            if (!file.EndsWith(".tif"))
                return null;
            Tiff image = Tiff.Open(file, "r");
            FieldValue[] f = image.GetField(TiffTag.IMAGEDESCRIPTION);
            Recorder.Record(BioLib.Recorder.GetCurrentMethodInfo(), false, file);
            return f[0].ToString();
        }
        /// It reads the OME-XML file and converts the ROIs to a list of ROI objects
        /// 
        /// @param file the path to the OME-TIFF file
        /// @param series the series number of the image you want to open
        /// 
        /// @return A list of ROI objects.
        public static List<ROI> OpenOMEROIs(string file, int series)
        {
            List<ROI> Annotations = new List<ROI>();
            // create OME-XML metadata store
            ServiceFactory factory = new ServiceFactory();
            OMEXMLService service = (OMEXMLService)factory.getInstance(typeof(OMEXMLService));
            loci.formats.ome.OMEXMLMetadata meta = service.createOMEXMLMetadata();
            // create format reader
            ImageReader imageReader = new ImageReader();
            imageReader.setMetadataStore(meta);
            // initialize file
            file = file.Replace("\\", "/");
            imageReader.setId(file);
            int imageCount = imageReader.getImageCount();
            int seriesCount = imageReader.getSeriesCount();
            double physicalSizeX = 0;
            double physicalSizeY = 0;
            double physicalSizeZ = 0;
            double stageSizeX = 0;
            double stageSizeY = 0;
            double stageSizeZ = 0;
            int SizeX = imageReader.getSizeX();
            int SizeY = imageReader.getSizeY();
            int SizeZ = imageReader.getSizeY();
            try
            {
                bool hasPhysical = false;
                if (meta.getPixelsPhysicalSizeX(series) != null)
                {
                    physicalSizeX = meta.getPixelsPhysicalSizeX(series).value().doubleValue();
                    hasPhysical = true;
                }
                if (meta.getPixelsPhysicalSizeY(series) != null)
                {
                    physicalSizeY = meta.getPixelsPhysicalSizeY(series).value().doubleValue();
                }
                if (meta.getPixelsPhysicalSizeZ(series) != null)
                {
                    physicalSizeZ = meta.getPixelsPhysicalSizeZ(series).value().doubleValue();
                }
                else
                {
                    physicalSizeZ = 1;
                }
                if (meta.getStageLabelX(series) != null)
                    stageSizeX = meta.getStageLabelX(series).value().doubleValue();
                if (meta.getStageLabelY(series) != null)
                    stageSizeY = meta.getStageLabelY(series).value().doubleValue();
                if (meta.getStageLabelZ(series) != null)
                    stageSizeZ = meta.getStageLabelZ(series).value().doubleValue();
                else
                    stageSizeZ = 1;
            }
            catch (Exception e)
            {
                stageSizeX = 0;
                stageSizeY = 0;
                stageSizeZ = 1;
            }
            VolumeD volume = new VolumeD(new Point3D(stageSizeX, stageSizeY, stageSizeZ), new Point3D(physicalSizeX * SizeX, physicalSizeY * SizeY, physicalSizeZ * SizeZ));
            int rc = meta.getROICount();
            for (int im = 0; im < rc; im++)
            {
                string roiID = meta.getROIID(im);
                string roiName = meta.getROIName(im);
                ZCT co = new ZCT(0, 0, 0);
                int scount = 1;
                try
                {
                    scount = meta.getShapeCount(im);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message.ToString());
                }
                for (int sc = 0; sc < scount; sc++)
                {
                    string type = meta.getShapeType(im, sc);
                    ROI an = new ROI();
                    an.roiID = roiID;
                    an.roiName = roiName;
                    an.shapeIndex = sc;
                    if (type == "Point")
                    {
                        an.type = ROI.Type.Point;
                        an.id = meta.getPointID(im, sc);
                        double dx = meta.getPointX(im, sc).doubleValue();
                        double dy = meta.getPointY(im, sc).doubleValue();
                        an.AddPoint(ToStageSpace(new PointD(dx, dy), physicalSizeX, physicalSizeY, volume.Location.X, volume.Location.Y));
                        an.coord = new ZCT();
                        ome.xml.model.primitives.NonNegativeInteger nz = meta.getPointTheZ(im, sc);
                        if (nz != null)
                            an.coord.Z = nz.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nc = meta.getPointTheC(im, sc);
                        if (nc != null)
                            an.coord.C = nc.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nt = meta.getPointTheT(im, sc);
                        if (nt != null)
                            an.coord.T = nt.getNumberValue().intValue();
                        an.Text = meta.getPointText(im, sc);
                        ome.units.quantity.Length fl = meta.getPointFontSize(im, sc);
                        if (fl != null)
                            an.fontSize = fl.value().intValue();
                        an.family = meta.getPointFontFamily(im, sc).name();
                        ome.xml.model.primitives.Color col = meta.getPointStrokeColor(im, sc);
                        if (col != null)
                            an.strokeColor = Color.FromArgb(col.getAlpha(), col.getRed(), col.getGreen(), col.getBlue());
                        ome.units.quantity.Length fw = meta.getPointStrokeWidth(im, sc);
                        if (fw != null)
                            an.strokeWidth = (float)fw.value().floatValue();
                        ome.xml.model.primitives.Color colf = meta.getPointStrokeColor(im, sc);
                        if (colf != null)
                            an.fillColor = Color.FromArgb(colf.getAlpha(), colf.getRed(), colf.getGreen(), colf.getBlue());
                    }
                    else
                    if (type == "Line")
                    {
                        an.type = ROI.Type.Line;
                        an.id = meta.getLineID(im, sc);
                        double px1 = meta.getLineX1(im, sc).doubleValue();
                        double py1 = meta.getLineY1(im, sc).doubleValue();
                        double px2 = meta.getLineX2(im, sc).doubleValue();
                        double py2 = meta.getLineY2(im, sc).doubleValue();
                        an.AddPoint(ToStageSpace(new PointD(px1, py1), physicalSizeX, physicalSizeY, volume.Location.X, volume.Location.Y));
                        an.AddPoint(ToStageSpace(new PointD(px2, py2), physicalSizeX, physicalSizeY, volume.Location.X, volume.Location.Y));
                        ome.xml.model.primitives.NonNegativeInteger nz = meta.getLineTheZ(im, sc);
                        if (nz != null)
                            co.Z = nz.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nc = meta.getLineTheC(im, sc);
                        if (nc != null)
                            co.C = nc.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nt = meta.getLineTheT(im, sc);
                        if (nt != null)
                            co.T = nt.getNumberValue().intValue();
                        an.coord = co;
                        an.Text = meta.getLineText(im, sc);
                        ome.units.quantity.Length fl = meta.getLineFontSize(im, sc);
                        if (fl != null)
                            an.fontSize = fl.value().intValue();
                        an.family = meta.getPointFontFamily(im, sc).name();
                        ome.xml.model.primitives.Color col = meta.getLineStrokeColor(im, sc);
                        if (col != null)
                            an.strokeColor = Color.FromArgb(col.getAlpha(), col.getRed(), col.getGreen(), col.getBlue());
                        ome.units.quantity.Length fw = meta.getLineStrokeWidth(im, sc);
                        if (fw != null)
                            an.strokeWidth = (float)fw.value().floatValue();
                        ome.xml.model.primitives.Color colf = meta.getLineFillColor(im, sc);
                        if (colf != null)
                            an.fillColor = Color.FromArgb(colf.getAlpha(), colf.getRed(), colf.getGreen(), colf.getBlue());
                    }
                    else
                    if (type == "Rectangle")
                    {
                        an.type = ROI.Type.Rectangle;
                        an.id = meta.getRectangleID(im, sc);
                        double px = meta.getRectangleX(im, sc).doubleValue();
                        double py = meta.getRectangleY(im, sc).doubleValue();
                        double pw = meta.getRectangleWidth(im, sc).doubleValue();
                        double ph = meta.getRectangleHeight(im, sc).doubleValue();
                        an.Rect = ToStageSpace(new RectangleD(px, py, pw, ph), physicalSizeX, physicalSizeY, volume.Location.X, volume.Location.Y);
                        ome.xml.model.primitives.NonNegativeInteger nz = meta.getRectangleTheZ(im, sc);
                        if (nz != null)
                            co.Z = nz.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nc = meta.getRectangleTheC(im, sc);
                        if (nc != null)
                            co.C = nc.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nt = meta.getRectangleTheT(im, sc);
                        if (nt != null)
                            co.T = nt.getNumberValue().intValue();
                        an.coord = co;

                        an.Text = meta.getRectangleText(im, sc);
                        ome.units.quantity.Length fl = meta.getRectangleFontSize(im, sc);
                        if (fl != null)
                            an.fontSize = fl.value().intValue();
                        an.family = meta.getPointFontFamily(im, sc).name();
                        ome.xml.model.primitives.Color col = meta.getRectangleStrokeColor(im, sc);
                        if (col != null)
                            an.strokeColor = Color.FromArgb(col.getAlpha(), col.getRed(), col.getGreen(), col.getBlue());
                        ome.units.quantity.Length fw = meta.getRectangleStrokeWidth(im, sc);
                        if (fw != null)
                            an.strokeWidth = (float)fw.value().floatValue();
                        ome.xml.model.primitives.Color colf = meta.getRectangleFillColor(im, sc);
                        if (colf != null)
                            an.fillColor = Color.FromArgb(colf.getAlpha(), colf.getRed(), colf.getGreen(), colf.getBlue());
                        ome.xml.model.enums.FillRule fr = meta.getRectangleFillRule(im, sc);
                    }
                    else
                    if (type == "Ellipse")
                    {
                        an.type = ROI.Type.Ellipse;
                        an.id = meta.getEllipseID(im, sc);
                        double px = meta.getEllipseX(im, sc).doubleValue();
                        double py = meta.getEllipseY(im, sc).doubleValue();
                        double ew = meta.getEllipseRadiusX(im, sc).doubleValue();
                        double eh = meta.getEllipseRadiusY(im, sc).doubleValue();
                        //We convert the ellipse radius to Rectangle
                        double w = ew * 2;
                        double h = eh * 2;
                        double x = px - ew;
                        double y = py - eh;
                        an.Rect = ToStageSpace(new RectangleD(px, py, w, h), physicalSizeX, physicalSizeY, volume.Location.X, volume.Location.Y);
                        ome.xml.model.primitives.NonNegativeInteger nz = meta.getEllipseTheZ(im, sc);
                        if (nz != null)
                            co.Z = nz.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nc = meta.getEllipseTheC(im, sc);
                        if (nc != null)
                            co.C = nc.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nt = meta.getEllipseTheT(im, sc);
                        if (nt != null)
                            co.T = nt.getNumberValue().intValue();
                        an.coord = co;
                        an.Text = meta.getEllipseText(im, sc);
                        ome.units.quantity.Length fl = meta.getEllipseFontSize(im, sc);
                        if (fl != null)
                            an.fontSize = fl.value().intValue();
                        an.family = meta.getPointFontFamily(im, sc).name();
                        ome.xml.model.primitives.Color col = meta.getEllipseStrokeColor(im, sc);
                        if (col != null)
                            an.strokeColor = Color.FromArgb(col.getAlpha(), col.getRed(), col.getGreen(), col.getBlue());
                        ome.units.quantity.Length fw = meta.getEllipseStrokeWidth(im, sc);
                        if (fw != null)
                            an.strokeWidth = (float)fw.value().floatValue();
                        ome.xml.model.primitives.Color colf = meta.getEllipseFillColor(im, sc);
                        if (colf != null)
                            an.fillColor = Color.FromArgb(colf.getAlpha(), colf.getRed(), colf.getGreen(), colf.getBlue());
                    }
                    else
                    if (type == "Polygon")
                    {
                        an.type = ROI.Type.Polygon;
                        an.id = meta.getPolygonID(im, sc);
                        an.closed = true;
                        string pxs = meta.getPolygonPoints(im, sc);
                        PointD[] pts = an.stringToPoints(pxs);
                        pts = ToStageSpace(pts, physicalSizeX, physicalSizeY, volume.Location.X, volume.Location.Y);
                        if (pts.Length > 100)
                        {
                            an.type = ROI.Type.Freeform;
                        }
                        an.AddPoints(pts);
                        ome.xml.model.primitives.NonNegativeInteger nz = meta.getPolygonTheZ(im, sc);
                        if (nz != null)
                            co.Z = nz.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nc = meta.getPolygonTheC(im, sc);
                        if (nc != null)
                            co.C = nc.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nt = meta.getPolygonTheT(im, sc);
                        if (nt != null)
                            co.T = nt.getNumberValue().intValue();
                        an.coord = co;
                        an.Text = meta.getPolygonText(im, sc);
                        ome.units.quantity.Length fl = meta.getPolygonFontSize(im, sc);
                        if (fl != null)
                            an.fontSize = fl.value().intValue();
                        an.family = meta.getPointFontFamily(im, sc).name();
                        ome.xml.model.primitives.Color col = meta.getPolygonStrokeColor(im, sc);
                        if (col != null)
                            an.strokeColor = Color.FromArgb(col.getAlpha(), col.getRed(), col.getGreen(), col.getBlue());
                        ome.units.quantity.Length fw = meta.getPolygonStrokeWidth(im, sc);
                        if (fw != null)
                            an.strokeWidth = (float)fw.value().floatValue();
                        ome.xml.model.primitives.Color colf = meta.getPolygonFillColor(im, sc);
                        if (colf != null)
                            an.fillColor = Color.FromArgb(colf.getAlpha(), colf.getRed(), colf.getGreen(), colf.getBlue());
                    }
                    else
                    if (type == "Polyline")
                    {
                        an.type = ROI.Type.Polyline;
                        an.id = meta.getPolylineID(im, sc);
                        string pxs = meta.getPolylinePoints(im, sc);
                        PointD[] pts = an.stringToPoints(pxs);
                        for (int pi = 0; pi < pts.Length; pi++)
                        {
                            pts[pi] = ToStageSpace(pts[pi], physicalSizeX, physicalSizeY, volume.Location.X, volume.Location.Y);
                        }
                        an.AddPoints(an.stringToPoints(pxs));
                        ome.xml.model.primitives.NonNegativeInteger nz = meta.getPolylineTheZ(im, sc);
                        if (nz != null)
                            co.Z = nz.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nc = meta.getPolylineTheC(im, sc);
                        if (nc != null)
                            co.C = nc.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nt = meta.getPolylineTheT(im, sc);
                        if (nt != null)
                            co.T = nt.getNumberValue().intValue();
                        an.coord = co;
                        an.Text = meta.getPolylineText(im, sc);
                        ome.units.quantity.Length fl = meta.getPolylineFontSize(im, sc);
                        if (fl != null)
                            an.fontSize = fl.value().intValue();
                        an.family = meta.getPointFontFamily(im, sc).name();
                        ome.xml.model.primitives.Color col = meta.getPolylineStrokeColor(im, sc);
                        if (col != null)
                            an.strokeColor = Color.FromArgb(col.getAlpha(), col.getRed(), col.getGreen(), col.getBlue());
                        ome.units.quantity.Length fw = meta.getPolylineStrokeWidth(im, sc);
                        if (fw != null)
                            an.strokeWidth = (float)fw.value().floatValue();
                        ome.xml.model.primitives.Color colf = meta.getPolylineFillColor(im, sc);
                        if (colf != null)
                            an.fillColor = Color.FromArgb(colf.getAlpha(), colf.getRed(), colf.getGreen(), colf.getBlue());
                    }
                    else
                    if (type == "Label")
                    {
                        an.type = ROI.Type.Label;
                        an.id = meta.getLabelID(im, sc);

                        ome.xml.model.primitives.NonNegativeInteger nz = meta.getLabelTheZ(im, sc);
                        if (nz != null)
                            co.Z = nz.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nc = meta.getLabelTheC(im, sc);
                        if (nc != null)
                            co.C = nc.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nt = meta.getLabelTheT(im, sc);
                        if (nt != null)
                            co.T = nt.getNumberValue().intValue();
                        an.coord = co;

                        ome.units.quantity.Length fl = meta.getLabelFontSize(im, sc);
                        if (fl != null)
                            an.fontSize = fl.value().intValue();
                        an.family = meta.getPointFontFamily(im, sc).name();
                        ome.xml.model.primitives.Color col = meta.getLabelStrokeColor(im, sc);
                        if (col != null)
                            an.strokeColor = Color.FromArgb(col.getAlpha(), col.getRed(), col.getGreen(), col.getBlue());
                        ome.units.quantity.Length fw = meta.getLabelStrokeWidth(im, sc);
                        if (fw != null)
                            an.strokeWidth = (float)fw.value().floatValue();
                        ome.xml.model.primitives.Color colf = meta.getLabelFillColor(im, sc);
                        if (colf != null)
                            an.fillColor = Color.FromArgb(colf.getAlpha(), colf.getRed(), colf.getGreen(), colf.getBlue());
                        PointD p = new PointD(meta.getLabelX(im, sc).doubleValue(), meta.getLabelY(im, sc).doubleValue());
                        an.AddPoint(ToStageSpace(p, physicalSizeX, physicalSizeY, volume.Location.X, volume.Location.Y));
                        an.Text = meta.getLabelText(im, sc);
                    }
                    else
                    if (type == "Mask")
                    {
                        byte[] bts = meta.getMaskBinData(im, sc);
                        bool end = meta.getMaskBinDataBigEndian(im, sc).booleanValue();
                        an = ROI.CreateMask(co, bts, (int)(an.W * physicalSizeX), (int)(an.H * physicalSizeY), new PointD(stageSizeX, stageSizeY), physicalSizeX, physicalSizeY);
                        an.Text = meta.getMaskText(im, sc);
                        an.id = meta.getMaskID(im, sc);
                        ome.xml.model.primitives.NonNegativeInteger nz = meta.getMaskTheZ(im, sc);
                        if (nz != null)
                            co.Z = nz.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nc = meta.getMaskTheC(im, sc);
                        if (nc != null)
                            co.C = nc.getNumberValue().intValue();
                        ome.xml.model.primitives.NonNegativeInteger nt = meta.getMaskTheT(im, sc);
                        if (nt != null)
                            co.T = nt.getNumberValue().intValue();
                        an.coord = co;

                        ome.units.quantity.Length fl = meta.getMaskFontSize(im, sc);
                        if (fl != null)
                            an.fontSize = fl.value().intValue();
                        ome.xml.model.enums.FontFamily ff = meta.getMaskFontFamily(im, sc);
                        if (ff != null)
                            an.family = ff.name();
                        ome.xml.model.primitives.Color col = meta.getMaskStrokeColor(im, sc);
                        if (col != null)
                            an.strokeColor = Color.FromArgb(col.getAlpha(), col.getRed(), col.getGreen(), col.getBlue());
                        ome.units.quantity.Length fw = meta.getMaskStrokeWidth(im, sc);
                        if (fw != null)
                            an.strokeWidth = (float)fw.value().floatValue();
                        ome.xml.model.primitives.Color colf = meta.getMaskFillColor(im, sc);
                        if (colf != null)
                            an.fillColor = Color.FromArgb(colf.getAlpha(), colf.getRed(), colf.getGreen(), colf.getBlue());
                    }
                }
            }

            imageReader.close();
            Recorder.Record(BioLib.Recorder.GetCurrentMethodInfo(), false, file, series);
            return Annotations;
        }
        /// It takes a list of ROI objects and returns a string of all the ROI objects in the list
        /// 
        /// @param Annotations List of ROI objects
        /// 
        /// @return A string of the ROI's
        public static string ROIsToString(List<ROI> Annotations)
        {
            string s = "";
            for (int i = 0; i < Annotations.Count; i++)
            {
                s += ROIToString(Annotations[i]);
            }
            return s;
        }
        /// This function takes an ROI object and returns a string that contains all the information
        /// about the ROI
        /// 
        /// @param ROI The ROI object
        /// 
        /// @return A string
        public static string ROIToString(ROI an)
        {
            PointD[] points = an.GetPoints();
            string pts = "";
            for (int j = 0; j < points.Length; j++)
            {
                if (j == points.Length - 1)
                    pts += points[j].X.ToString(CultureInfo.InvariantCulture) + "," + points[j].Y.ToString(CultureInfo.InvariantCulture);
                else
                    pts += points[j].X.ToString(CultureInfo.InvariantCulture) + "," + points[j].Y.ToString(CultureInfo.InvariantCulture) + " ";
            }
            char sep = (char)34;
            string sColor = sep.ToString() + an.strokeColor.A.ToString() + ',' + an.strokeColor.R.ToString() + ',' + an.strokeColor.G.ToString() + ',' + an.strokeColor.B.ToString() + sep.ToString();
            string bColor = sep.ToString() + an.fillColor.A.ToString() + ',' + an.fillColor.R.ToString() + ',' + an.fillColor.G.ToString() + ',' + an.fillColor.B.ToString() + sep.ToString();

            string line = an.roiID + ',' + an.roiName + ',' + an.type.ToString() + ',' + an.id + ',' + an.shapeIndex.ToString() + ',' +
                an.Text + ',' + an.serie + ',' + an.coord.Z.ToString() + ',' + an.coord.C.ToString() + ',' + an.coord.T.ToString() + ',' + an.X.ToString(CultureInfo.InvariantCulture) + ',' + an.Y.ToString(CultureInfo.InvariantCulture) + ',' +
                an.W.ToString(CultureInfo.InvariantCulture) + ',' + an.H.ToString(CultureInfo.InvariantCulture) + ',' + sep.ToString() + pts + sep.ToString() + ',' + sColor + ',' + an.strokeWidth.ToString(CultureInfo.InvariantCulture) + ',' + bColor + ',' + an.fontSize.ToString(CultureInfo.InvariantCulture) + ',' + NewLine;
            return line;
        }
        /// It takes a string and returns an ROI object
        /// 
        /// @param sts the string that contains the ROI data
        /// 
        /// @return A ROI object.
        public static ROI StringToROI(string sts)
        {
            //Works with either comma or tab separated values.
            if (sts.StartsWith("<?xml") || sts.StartsWith("{"))
                return null;
            ROI an = new ROI();
            string val = "";
            bool inSep = false;
            int col = 0;
            double x = 0;
            double y = 0;
            double w = 0;
            double h = 0;
            string line = sts;
            bool points = false;
            char sep = '"';
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == sep)
                {
                    if (!inSep)
                    {
                        inSep = true;
                    }
                    else
                        inSep = false;
                    continue;
                }

                if ((c == ',' || c == '\t') && (!inSep || points))
                {
                    //ROIID,ROINAME,TYPE,ID,SHAPEINDEX,TEXT,C,Z,T,X,Y,W,H,POINTS,STROKECOLOR,STROKECOLORW,FILLCOLOR,FONTSIZE
                    if (col == 0)
                    {
                        //ROIID
                        an.roiID = val;
                    }
                    else
                    if (col == 1)
                    {
                        //ROINAME
                        an.roiName = val;
                    }
                    else
                    if (col == 2)
                    {
                        //TYPE
                        an.type = (ROI.Type)Enum.Parse(typeof(ROI.Type), val);
                        if (an.type == ROI.Type.Freeform || an.type == ROI.Type.Polygon)
                            an.closed = true;
                    }
                    else
                    if (col == 3)
                    {
                        //ID
                        an.id = val;
                    }
                    else
                    if (col == 4)
                    {
                        //SHAPEINDEX/
                        an.shapeIndex = int.Parse(val);
                    }
                    else
                    if (col == 5)
                    {
                        //TEXT/
                        an.Text = val;
                    }
                    else
                    if (col == 6)
                    {
                        an.serie = int.Parse(val);
                    }
                    else
                    if (col == 7)
                    {
                        an.coord.Z = int.Parse(val);
                    }
                    else
                    if (col == 8)
                    {
                        an.coord.C = int.Parse(val);
                    }
                    else
                    if (col == 9)
                    {
                        an.coord.T = int.Parse(val);
                    }
                    else
                    if (col == 10)
                    {
                        x = double.Parse(val, CultureInfo.InvariantCulture);
                    }
                    else
                    if (col == 11)
                    {
                        y = double.Parse(val, CultureInfo.InvariantCulture);
                    }
                    else
                    if (col == 12)
                    {
                        w = double.Parse(val, CultureInfo.InvariantCulture);
                    }
                    else
                    if (col == 13)
                    {
                        h = double.Parse(val, CultureInfo.InvariantCulture);
                    }
                    else
                    if (col == 14)
                    {
                        //POINTS
                        an.AddPoints(an.stringToPoints(val));
                        points = false;
                        an.Rect = new RectangleD(x, y, w, h);
                    }
                    else
                    if (col == 15)
                    {
                        //STROKECOLOR
                        string[] st = val.Split(',');
                        an.strokeColor = Color.FromArgb(int.Parse(st[0]), int.Parse(st[1]), int.Parse(st[2]), int.Parse(st[3]));
                    }
                    else
                    if (col == 16)
                    {
                        //STROKECOLORW
                        an.strokeWidth = double.Parse(val, CultureInfo.InvariantCulture);
                    }
                    else
                    if (col == 17)
                    {
                        //FILLCOLOR
                        string[] st = val.Split(',');
                        an.fillColor = Color.FromArgb(int.Parse(st[0]), int.Parse(st[1]), int.Parse(st[2]), int.Parse(st[3]));
                    }
                    else
                    if (col == 18)
                    {
                        //FONTSIZE
                        double s = double.Parse(val, CultureInfo.InvariantCulture);
                        an.fontSize = (float)s;
                        an.family = "Times New Roman";
                    }
                    col++;
                    val = "";
                }
                else
                    val += c;
            }

            return an;
        }
        /// This function takes a list of ROIs and writes them to a CSV file
        /// 
        /// @param filename the name of the file to be saved
        /// @param Annotations List of ROI objects
        public static void ExportROIsCSV(string filename, List<ROI> Annotations)
        {
            string con = columns;
            con += ROIsToString(Annotations);
            File.WriteAllText(filename, con);
            Recorder.Record(BioLib.Recorder.GetCurrentMethodInfo(), false, filename, Annotations);
        }
        /// It reads the CSV file and converts each line into a ROI object
        /// 
        /// @param filename The path to the CSV file.
        /// 
        /// @return A list of ROI objects.
        public static List<ROI> ImportROIsCSV(string filename)
        {
            List<ROI> list = new List<ROI>();
            if (!File.Exists(filename))
                return list;
            string[] sts = File.ReadAllLines(filename);
            //We start reading from line 1.
            for (int i = 1; i < sts.Length; i++)
            {
                list.Add(StringToROI(sts[i]));
            }
            Recorder.Record(BioLib.Recorder.GetCurrentMethodInfo(), false, filename);
            return list;
        }
        /// ExportROIFolder(path, filename)
        /// 
        /// This function takes a folder path and a filename as input and exports all the ROIs in the
        /// folder as CSV files
        /// 
        /// @param path the path to the folder containing the OMERO ROI files
        /// @param filename the name of the file you want to export
        public static void ExportROIFolder(string path, string filename)
        {
            string[] fs = Directory.GetFiles(path);
            int i = 0;
            foreach (string f in fs)
            {
                List<ROI> annotations = OpenOMEROIs(f, 0);
                string ff = Path.GetFileNameWithoutExtension(f);
                ExportROIsCSV(path + "//" + ff + "-" + i.ToString() + ".csv", annotations);
                i++;
            }
            Recorder.Record(BioLib.Recorder.GetCurrentMethodInfo(), false, path, filename);
        }

        private static BioImage bstats = null;
        private static bool update = false;
        /// It takes a BioImage object, and calculates the mean histogram for each channel, and for the
        /// entire image. 
        /// 
        /// The BioImage object is a class that contains a list of buffers, each of which contains a
        /// byte array of pixel data. 
        /// 
        /// The function also calculates the mean histogram for each buffer, and stores it in the
        /// buffer's stats property. 
        /// 
        /// The function also calculates the mean histogram for each channel, and stores it in the
        /// channel's stats property. 
        /// 
        /// The function also calculates the mean histogram for the entire image, and stores it in the
        /// image's statistics property. 
        /// 
        /// The mean histogram is calculated by adding up the histograms of each buffer, and then
        /// dividing by the number of buffers. 
        /// 
        /// The histogram is calculated by looping through the byte array of pixel data, and
        /// incrementing the value of the histogram at the index of the pixel value. 
        /// 
        /// The histogram
        /// 
        /// @param BioImage This is the image object that contains the image data.
        /// @param updateImageStats if true, the image stats will be updated.
        public static void AutoThreshold(BioImage b, bool updateImageStats)
        {
            bstats = b;
            Statistics statistics = null;
            if (b.bitsPerPixel > 8)
                statistics = new Statistics(true);
            else
                statistics = new Statistics(false);
            for (int i = 0; i < b.Buffers.Count; i++)
            {
                if (b.Buffers[i].Stats == null || updateImageStats)
                    b.Buffers[i].Stats = Statistics.FromBytes(b.Buffers[i]);
                if (b.Buffers[i].RGBChannelsCount == 1)
                    statistics.AddStatistics(b.Buffers[i].Stats[0]);
                else
                {
                    for (int r = 0; r < b.Buffers[i].RGBChannelsCount; r++)
                    {
                        statistics.AddStatistics(b.Buffers[i].Stats[r]);
                    }
                }
            }
            for (int c = 0; c < b.Channels.Count; c++)
            {
                Statistics[] sts = new Statistics[b.Buffers[0].RGBChannelsCount];
                for (int i = 0; i < b.Buffers[0].RGBChannelsCount; i++)
                {
                    if (b.bitsPerPixel > 8)
                    {
                        sts[i] = new Statistics(true);
                    }
                    else
                        sts[i] = new Statistics(false);
                }
                for (int z = 0; z < b.SizeZ; z++)
                {
                    for (int t = 0; t < b.SizeT; t++)
                    {
                        int ind;
                        if (b.Channels.Count > b.SizeC)
                        {
                            ind = b.GetFrameIndex(z, 0, t);
                        }
                        else
                            ind = b.GetFrameIndex(z, c, t);
                        if (b.Buffers[ind].RGBChannelsCount == 1)
                            sts[0].AddStatistics(b.Buffers[ind].Stats[0]);
                        else
                        {
                            sts[0].AddStatistics(b.Buffers[ind].Stats[0]);
                            sts[1].AddStatistics(b.Buffers[ind].Stats[1]);
                            sts[2].AddStatistics(b.Buffers[ind].Stats[2]);
                            if (b.Buffers[ind].RGBChannelsCount == 4)
                                sts[3].AddStatistics(b.Buffers[ind].Stats[3]);
                        }
                    }
                }
                if (b.RGBChannelCount == 1)
                    sts[0].MeanHistogram();
                else
                {
                    sts[0].MeanHistogram();
                    sts[1].MeanHistogram();
                    sts[2].MeanHistogram();
                    if (b.Buffers[0].RGBChannelsCount == 4)
                        sts[3].MeanHistogram();
                }
                b.Channels[c].stats = sts;
            }
            statistics.MeanHistogram();
            b.statistics = statistics;
            Recorder.Record(BioLib.Recorder.GetCurrentMethodInfo(), true, b, updateImageStats);
        }
        /// It takes the current image, and finds the best threshold value for it
        public static void AutoThreshold()
        {
            AutoThreshold(bstats, update);
        }
        /// It creates a new thread that calls the AutoThreshold function
        /// 
        /// @param BioImage This is a class that holds the image data and some other information.
        public static void AutoThresholdThread(BioImage b)
        {
            bstats = b;
            Thread th = new Thread(AutoThreshold);
            th.Start();
        }
        /// The function finds the focus of a given BioImage at a specific channel and time by
        /// calculating the focus quality of each Z-plane and returning the coordinate with the highest
        /// focus quality.
        /// 
        /// @param BioImage A BioImage object that contains the image data.
        /// @param Channel The channel of the BioImage to analyze. A BioImage can have multiple
        /// channels, each representing a different fluorescent label or imaging modality.
        /// @param Time The time point at which the focus is being calculated.
        /// 
        /// @return an integer value which represents the coordinate of the image with the highest focus
        /// quality in a given channel and time.
        public static int FindFocus(BioImage im, int Channel, int Time)
        {
            long mf = 0;
            int fr = 0;
            List<double> dt = new List<double>();
            ZCT c = new ZCT(0, 0, 0);
            for (int i = 0; i < im.SizeZ; i++)
            {
                long f = CalculateFocusQuality(im.Buffers[im.GetFrameIndex(i, Channel, Time)]);
                dt.Add(f);
                if (f > mf)
                {
                    mf = f;
                    fr = im.GetFrameIndex(i, Channel, Time);
                }
            }

            Recorder.Record(BioLib.Recorder.GetCurrentMethodInfo(), false, im, Channel, Time);
            return fr;
        }
        /// The function calculates the focus quality of a given bitmap image.
        /// 
        /// @param Bitmap A class representing a bitmap image, which contains information about the
        /// image's size, pixel data, and color channels.
        /// 
        /// @return The method is returning a long value which represents the calculated focus quality
        /// of the input Bitmap image.
        public static long CalculateFocusQuality(Bitmap b)
        {
            if (b.RGBChannelsCount == 1)
            {
                long sum = 0;
                long sumOfSquaresR = 0;
                for (int y = 0; y < b.SizeY; y++)
                    for (int x = 0; x < b.SizeX; x++)
                    {
                        ColorS pixel = b.GetPixel(x, y);
                        sum += pixel.R;
                        sumOfSquaresR += pixel.R * pixel.R;
                    }
                return sumOfSquaresR * b.SizeX * b.SizeY - sum * sum;
            }
            else
            {
                long sum = 0;
                long sumOfSquares = 0;
                for (int y = 0; y < b.SizeY; y++)
                    for (int x = 0; x < b.SizeX; x++)
                    {
                        ColorS pixel = b.GetPixel(x, y);
                        int p = (pixel.R + pixel.G + pixel.B) / 3;
                        sum += p;
                        sumOfSquares += p * p;
                    }
                Recorder.Record(BioLib.Recorder.GetCurrentMethodInfo(), true, b);
                return sumOfSquares * b.SizeX * b.SizeY - sum * sum;
            }
        }
        /// It disposes of all the buffers and channels in the image, removes the image from the Images
        /// list, and then forces the garbage collector to run
        public void Dispose()
        {
            for (int i = 0; i < Buffers.Count; i++)
            {
                Buffers[i].Dispose();
            }
            for (int i = 0; i < Channels.Count; i++)
            {
                Channels[i].Dispose();
            }
            for (int i = 0; i < Annotations.Count; i++)
            {
                Annotations[i].Dispose();
            }
        }
        /// This function returns the filename of the object, and the location of the object in the 3D
        /// space
        /// 
        /// @return The filename, and the location of the volume.
        public override string ToString()
        {
            return "BioLib.Images.GetImage(\"" + id + "\")";
        }
        public static int GetSeriesCount(string file)
        {
            ImageReader r = new ImageReader();
            r.setId(file);
            return r.getSeriesCount();
        }

        /// This function divides each pixel in the image by a constant value
        /// 
        /// @param BioImage a class that contains a list of buffers (which are 2D arrays of floats)
        /// @param b the value to divide by
        /// 
        /// @return The image is being returned.
        public static BioImage operator /(BioImage a, float b)
        {
            for (int i = 0; i < a.Buffers.Count; i++)
            {
                a.Buffers[i] = a.Buffers[i] / b;
            }
            return a;
        }
        public static BioImage operator *(BioImage a, float b)
        {
            for (int i = 0; i < a.Buffers.Count; i++)
            {
                a.Buffers[i] = a.Buffers[i] / b;
            }
            return a;
        }
        /// This function adds a constant value to each pixel in the image
        /// 
        /// @param BioImage a class that contains a list of buffers (float[])
        /// @param b the value to add to the image
        /// 
        /// @return The image itself.
        public static BioImage operator +(BioImage a, float b)
        {
            for (int i = 0; i < a.Buffers.Count; i++)
            {
                a.Buffers[i] = a.Buffers[i] + b;
            }
            return a;
        }
        /// Subtracts a scalar value from each pixel in the image
        /// 
        /// @param BioImage a class that contains a list of buffers (which are 2D arrays of floats)
        /// @param b the value to subtract from the image
        /// 
        /// @return The BioImage object is being returned.
        public static BioImage operator -(BioImage a, float b)
        {
            for (int i = 0; i < a.Buffers.Count; i++)
            {
                a.Buffers[i] = a.Buffers[i] - b;
            }
            return a;
        }


        /// This function divides each pixel in the image by the value of the color
        /// 
        /// @param BioImage a class that contains a list of ColorS objects.
        /// @param ColorS a struct that contains a byte for each color channel (R, G, B, A)
        /// 
        /// @return A BioImage object.
        public static BioImage operator /(BioImage a, ColorS b)
        {
            for (int i = 0; i < a.Buffers.Count; i++)
            {
                a.Buffers[i] = a.Buffers[i] / b;
            }
            return a;
        }
        /// This function takes a BioImage object and a ColorS object and returns a BioImage object
        /// 
        /// @param BioImage a class that contains a list of ColorS objects.
        /// @param ColorS a struct that contains a byte for each color channel (R, G, B, A)
        /// 
        /// @return A BioImage object
        public static BioImage operator *(BioImage a, ColorS b)
        {
            for (int i = 0; i < a.Buffers.Count; i++)
            {
                a.Buffers[i] = a.Buffers[i] * b;
            }
            return a;
        }
        /// It takes a BioImage object and a ColorS object and adds the ColorS object to each buffer in
        /// the BioImage object
        /// 
        /// @param BioImage a class that contains a list of ColorS objects.
        /// @param ColorS a struct that contains a byte for each color channel (R, G, B, A)
        /// 
        /// @return A BioImage object
        public static BioImage operator +(BioImage a, ColorS b)
        {
            for (int i = 0; i < a.Buffers.Count; i++)
            {
                a.Buffers[i] = a.Buffers[i] + b;
            }
            return a;
        }
        /// The function subtracts a color from each pixel in the image
        /// 
        /// @param BioImage a class that contains a list of ColorS objects.
        /// @param ColorS a struct that contains a byte for each color channel (R, G, B, A)
        /// 
        /// @return The image itself.
        public static BioImage operator -(BioImage a, ColorS b)
        {
            for (int i = 0; i < a.Buffers.Count; i++)
            {
                a.Buffers[i] = a.Buffers[i] - b;
            }
            return a;
        }
    }
}
