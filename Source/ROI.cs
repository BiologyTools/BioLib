// SelectionMoveTool.cs
// GTK# selection + move tool example — all rendering via SkiaSharp (SkiaSharp.Views.Gtk SKWidget)
// Targets: Gtk# (Gtk 3) / C# (.NET 6/7/8) with SkiaSharp and SkiaSharp.Views.Gtk

using AForge;
using BioLib;
using Gdk;
using Gtk;
using omero.api;
using omero.constants;
using omero.gateway;
using omero.gateway.facility;
using omero.gateway.model;
using omero.model;
using SkiaSharp;
using SkiaSharp.Views.Gtk;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Color = AForge.Color;

namespace BioLib
{
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
        public RectangleD BoundingBox
        {
            get; set;
        }
        public double X
        {
            get
            {
                if (type == Type.Mask)
                {
                    return roiMask.X * roiMask.PhysicalSizeX;
                }
                return BoundingBox.X;
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
                return BoundingBox.Y;
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
                    return ROI.selectBoxSize;
                else
                    return BoundingBox.W;
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
                    return ROI.selectBoxSize;
                else
                    return BoundingBox.H;
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
        public PointD Point
        {
            get
            {
                if (Points.Count > 0)
                    return Points[0];
                else
                    return new PointD(X, Y);

            }
        }
        public static SKPaint SelectBoxColor = new() { Style = SKPaintStyle.Stroke, Color = new SKColor(255, 0, 0) };
        public static float selectBoxSize = 14f;
        private List<PointD> pos = new List<PointD>();
        public List<PointD> Points 
        { 
            get 
            {
                if (pos.Count == 0)
                    Validate();
                return pos;
            } 
            set 
            {
                pos = value;
                UpdateBoundingBox();
            } 
        }
        public void Validate()
        {
            // Ensure pos exists
            if (pos == null)
                pos = new List<PointD>();
            else
                pos.Clear();

            switch (type)
            {
                case Type.Rectangle:
                case Type.Ellipse:
                    // 4 corners derived from the bounding box
                    pos.Add(new PointD(BoundingBox.X, BoundingBox.Y));
                    pos.Add(new PointD(BoundingBox.X + BoundingBox.W, BoundingBox.Y));
                    pos.Add(new PointD(BoundingBox.X, BoundingBox.Y + BoundingBox.H));
                    pos.Add(new PointD(BoundingBox.X + BoundingBox.W, BoundingBox.Y + BoundingBox.H));
                    break;

                case Type.Point:
                case Type.Label:
                    // Only the anchor point (top-left of bounding box)
                    pos.Add(new PointD(BoundingBox.X, BoundingBox.Y));
                    break;
                default:
                    // Other ROI types keep their existing point data
                    // (Freeform, Polyline, Polygon, Mask, etc.)
                    break;
            }
        }

        public bool Contains(PointD p)
        {
            return (p.X >= X &&
                    p.X <= X + W &&
                    p.Y >= Y &&
                    p.Y <= Y + H);
        }

        private List<RectangleD> selectBoxs = new List<RectangleD>();
        public List<int> selectedPoints = new List<int>();
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
        /*
        public ROI(BioImage b, Widget v)
        {
            coord = new ZCT(0, 0, 0);
            strokeColor = Color.Yellow;
            BoundingBox = new RectangleD(0, 0, 1, 1);
            Convert.Initialize(b, v);
        }
        */
        public ROI()
        {
            coord = new ZCT(0, 0, 0);
            strokeColor = Color.Yellow;
            BoundingBox = new RectangleD(0, 0, 1, 1);
        }
        public SKBitmap ByteArrayToBitmap(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return null;

            using var ms = new SKMemoryStream(imageBytes);
            return SKBitmap.Decode(ms);
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
                pts[0] = Points[0];
                pts[1] = new PointD(Points[0].X + BoundingBox.W, Points[0].Y);
                pts[2] = new PointD(Points[0].X + BoundingBox.W, Points[0].Y + BoundingBox.H);
                pts[3] = new PointD(Points[0].X, Points[0].Y + BoundingBox.H);
                return BioImage.ToImageSpace(pts.ToList(), res.StageSizeX, res.StageSizeY, res.PhysicalSizeX, res.PhysicalSizeY);
            }
            else
                return BioImage.ToImageSpace(Points, res.StageSizeX, res.StageSizeY, res.PhysicalSizeX, res.PhysicalSizeY);
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
            int c = Points.Count;
            if (type != Type.Mask)
            {
                for (int i = 0; i < c; i++)
                {
                    selectBoxs.Add(new RectangleD((float)(Points[i].X - f), (float)(Points[i].Y - f), (float)s, (float)s));
                }
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
            an.BoundingBox = new RectangleD(x, y, selectBoxSize, selectBoxSize);
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
            an.Points = new List<PointD>();
            an.coord = coord;
            an.type = Type.Line;
            an.Points.Add(x1);
            an.Points.Add(x2);
            an.UpdateBoundingBox();
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
            an.BoundingBox = new RectangleD(x, y, w, h);
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
            an.BoundingBox = new RectangleD(x, y, w, h);
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
            an.Points = new List<PointD>();
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
            an.BoundingBox = new RectangleD(loc.X + (an.roiMask.X * an.roiMask.PhysicalSizeX), loc.Y + (an.roiMask.Y * an.roiMask.PhysicalSizeY), width, height);
            return an;
        }
        public static ROI CreateMask(ZCT coord, Byte[] mask, int width, int height, PointD loc, double physicalX, double physicalY)
        {
            ROI an = new ROI();
            an.coord = coord;
            an.type = Type.Mask;
            an.roiMask = new Mask(mask, width, height, physicalX, physicalY, loc.X, loc.Y);
            an.AddPoint(loc);
            an.BoundingBox = new RectangleD(loc.X + (an.roiMask.X * an.roiMask.PhysicalSizeX), loc.Y + (an.roiMask.Y * an.roiMask.PhysicalSizeY), width, height);
            return an;
        }
        // Calculate the center point of the ROI
        public PointD GetCenter()
        {
            return new PointD(BoundingBox.X + (BoundingBox.W / 2.0), BoundingBox.Y + (BoundingBox.H / 2.0));
        }
        public static int AddROIsToOMERO(long imageId, long user, BioImage b)
        {
            try
            {
                var gateway = OMERO.gateway;
                var roiService = gateway.getFacility(typeof(ROIFacility)) as ROIFacility;
                var browse = gateway.getFacility(typeof(BrowseFacility)) as BrowseFacility;
                var ctx = OMERO.sc;
                ImageData image = browse.getImage(ctx, imageId);
                int count = 0;
                ROI[] rois = b.Annotations.ToArray();

                foreach (ROI an in rois)
                {
                    ROIData roi = new ROIData();
                    ShapeData shape = null;

                    switch (an.type)
                    {
                        case ROI.Type.Point:
                            {
                                var pt = new omero.gateway.model.PointData(
                                    b.ToImageSpaceX(an.X),
                                    b.ToImageSpaceY(an.Y)
                                );
                                pt.setZ(an.coord.Z);
                                pt.setC(an.coord.C);
                                pt.setT(an.coord.T);
                                pt.setText(string.IsNullOrEmpty(an.Text) ? an.id : an.Text);
                                shape = pt;
                                break;
                            }

                        case ROI.Type.Rectangle:
                            {
                                var r = new RectangleData(
                                    b.ToImageSpaceX(an.BoundingBox.X),
                                    b.ToImageSpaceY(an.BoundingBox.Y),
                                    b.ToImageSizeX(an.W),
                                    b.ToImageSizeY(an.H)
                                );
                                r.setZ(an.coord.Z);
                                r.setC(an.coord.C);
                                r.setT(an.coord.T);
                                r.setText(string.IsNullOrEmpty(an.Text) ? an.id : an.Text);
                                shape = r;
                                break;
                            }

                        case ROI.Type.Ellipse:
                            {
                                double rx = b.ToImageSizeX(an.W / 2.0);
                                double ry = b.ToImageSizeY(an.H / 2.0);
                                double cx = b.ToImageSpaceX(an.Points[0].X + an.W / 2.0);
                                double cy = b.ToImageSpaceY(an.Points[0].Y + an.H / 2.0);

                                var e = new EllipseData(cx, cy, rx, ry);
                                e.setZ(an.coord.Z);
                                e.setC(an.coord.C);
                                e.setT(an.coord.T);
                                e.setText(string.IsNullOrEmpty(an.Text) ? an.id : an.Text);
                                shape = e;
                                break;
                            }

                        case ROI.Type.Line:
                            {
                                var l = new LineData(
                                    b.ToImageSpaceX(an.GetPoint(0).X),
                                    b.ToImageSpaceY(an.GetPoint(0).Y),
                                    b.ToImageSpaceX(an.GetPoint(1).X),
                                    b.ToImageSpaceY(an.GetPoint(1).Y)
                                );
                                l.setZ(an.coord.Z);
                                l.setC(an.coord.C);
                                l.setT(an.coord.T);
                                l.setText(string.IsNullOrEmpty(an.Text) ? an.id : an.Text);
                                shape = l;
                                break;
                            }

                        case ROI.Type.Polygon:
                        case ROI.Type.Freeform:
                            {
                                var pts = new List<PointD>();
                                foreach (var p in an.Points)
                                    pts.Add(new PointD(
                                        b.ToImageSpaceX(p.X),
                                        b.ToImageSpaceY(p.Y)
                                    ));

                                var poly = new PolygonData(BioPointToOmeroPoint(an,b));
                                poly.setZ(an.coord.Z);
                                poly.setC(an.coord.C);
                                poly.setT(an.coord.T);
                                poly.setText(string.IsNullOrEmpty(an.Text) ? an.id : an.Text);
                                shape = poly;
                                break;
                            }

                        case ROI.Type.Polyline:
                            {
                                var pts = new List<PointD>();
                                foreach (var p in an.Points)
                                    pts.Add(new PointD(
                                        b.ToImageSpaceX(p.X),
                                        b.ToImageSpaceY(p.Y)
                                    ));

                                var pl = new PolylineData(BioPointToOmeroPoint(an, b));
                                pl.setZ(an.coord.Z);
                                pl.setC(an.coord.C);
                                pl.setT(an.coord.T);
                                pl.setText(string.IsNullOrEmpty(an.Text) ? an.id : an.Text);
                                shape = pl;
                                break;
                            }

                        case ROI.Type.Label:
                            {
                                var t = new TextData(
                                    string.IsNullOrEmpty(an.Text) ? "Label" : an.Text,
                                    b.ToImageSpaceX(an.BoundingBox.X),
                                    b.ToImageSpaceY(an.BoundingBox.Y)
                                );
                                //t.setFontSize(new omero.model.LengthI(an.fontSize, omero.model.enums.UnitsLength.PIXEL));
                                t.setZ(an.coord.Z);
                                t.setC(an.coord.C);
                                t.setT(an.coord.T);
                                shape = t;
                                break;
                            }

                        case ROI.Type.Mask:
                            {
                                if (an.roiMask == null)
                                    continue;

                                var m = new MaskData(
                                    an.roiMask.X,
                                    an.roiMask.Y,
                                    an.roiMask.Width,
                                    an.roiMask.Height,
                                    an.roiMask.GetBytes()
                                );

                                m.setZ(an.coord.Z);
                                m.setC(an.coord.C);
                                m.setT(an.coord.T);
                                m.setText(string.IsNullOrEmpty(an.Text) ? an.id : an.Text);
                                shape = m;
                                break;
                            }
                    }

                    if (shape == null)
                        continue;
                    // Apply stroke/fill
                    shape.getShapeSettings().setStroke(new java.awt.Color(
                        an.strokeColor.A,
                        an.strokeColor.R,
                        an.strokeColor.G,
                        an.strokeColor.B
                    ));

                    shape.getShapeSettings().setFill(new java.awt.Color(
                        an.fillColor.A,
                        an.fillColor.R,
                        an.fillColor.G,
                        an.fillColor.B
                    ));

                    shape.getShapeSettings().setStrokeWidth(
                        new omero.model.LengthI(an.strokeWidth, omero.model.enums.UnitsLength.PIXEL)
                    );

                    roi.addShapeData(shape);

                    // ----- Save ROI to OMERO -----
                    java.util.List roiList = new java.util.ArrayList();
                    roiList.add(roi);

                    var saved = roiService.saveROIs(ctx, imageId, user, roiList);
                    count++;

                }

                Console.WriteLine($"Added {count} ROIs to OMERO image {imageId}");
                return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error adding ROIs: " + ex);
                return 0;
            }
        }
        public static java.util.List BioPointToOmeroPoint(ROI r, BioImage b)
        {
            // Create Java list, NOT C# list
            java.util.List javaPts = new java.util.ArrayList();
            foreach (var p in r.Points)
            {
                // Create Java Point2D.Double for OMERO
                var jp = new java.awt.geom.Point2D.Double(
                    b.ToImageSpaceX(p.X),
                    b.ToImageSpaceY(p.Y)
                );
                javaPts.add(jp);
            }
            return javaPts;
        }
        /*
        public void AddRoisToOmero(long imageId,BioImage b, ServiceFactoryPrx sf)
        {
            for (int i = 0; i < b.Annotations.Count; i++)
            {
                ROI an = b.Annotations[i];
                // 1. Get services
                var roiService = sf.getRoiService();
                var updateService = sf.getUpdateService();

                // 2. Create ROI and link to image
                var roi = new omero.model.RoiI();
                roi.setImage(new omero.model.ImageI(imageId, false));

                ROIData roid = new ROIData();
                ShapeData shape = null;

                switch (an.type)
                {
                    case ROI.Type.Point:
                        {
                            var pt = new omero.gateway.model.PointData(
                                b.ToImageSpaceX(an.X),
                                b.ToImageSpaceY(an.Y)
                            );
                            pt.setZ(an.coord.Z);
                            pt.setC(an.coord.C);
                            pt.setT(an.coord.T);
                            pt.setText(string.IsNullOrEmpty(an.Text) ? an.id : an.Text);
                            shape = pt;
                            break;
                        }

                    case ROI.Type.Rectangle:
                        {
                            var r = new RectangleData(
                                b.ToImageSpaceX(an.BoundingBox.X),
                                b.ToImageSpaceY(an.BoundingBox.Y),
                                b.ToImageSizeX(an.W),
                                b.ToImageSizeY(an.H)
                            );
                            r.setZ(an.coord.Z);
                            r.setC(an.coord.C);
                            r.setT(an.coord.T);
                            r.setText(string.IsNullOrEmpty(an.Text) ? an.id : an.Text);
                            shape = r;
                            break;
                        }

                    case ROI.Type.Ellipse:
                        {
                            double rx = b.ToImageSizeX(an.W / 2.0);
                            double ry = b.ToImageSizeY(an.H / 2.0);
                            double cx = b.ToImageSpaceX(an.Points[0].X + an.W / 2.0);
                            double cy = b.ToImageSpaceY(an.Points[0].Y + an.H / 2.0);

                            var e = new EllipseData(cx, cy, rx, ry);
                            e.setZ(an.coord.Z);
                            e.setC(an.coord.C);
                            e.setT(an.coord.T);
                            e.setText(string.IsNullOrEmpty(an.Text) ? an.id : an.Text);
                            shape = e;
                            break;
                        }

                    case ROI.Type.Line:
                        {
                            var l = new LineData(
                                b.ToImageSpaceX(an.GetPoint(0).X),
                                b.ToImageSpaceY(an.GetPoint(0).Y),
                                b.ToImageSpaceX(an.GetPoint(1).X),
                                b.ToImageSpaceY(an.GetPoint(1).Y)
                            );
                            l.setZ(an.coord.Z);
                            l.setC(an.coord.C);
                            l.setT(an.coord.T);
                            l.setText(string.IsNullOrEmpty(an.Text) ? an.id : an.Text);
                            shape = l;
                            break;
                        }

                    case ROI.Type.Polygon:
                    case ROI.Type.Freeform:
                        {
                            var pts = new List<PointD>();
                            foreach (var p in an.Points)
                                pts.Add(new PointD(
                                    b.ToImageSpaceX(p.X),
                                    b.ToImageSpaceY(p.Y)
                                ));

                            var poly = new PolygonData(BioPointToOmeroPoint(an, b));
                            poly.setZ(an.coord.Z);
                            poly.setC(an.coord.C);
                            poly.setT(an.coord.T);
                            poly.setText(string.IsNullOrEmpty(an.Text) ? an.id : an.Text);
                            shape = poly;
                            break;
                        }

                    case ROI.Type.Polyline:
                        {
                            var pts = new List<PointD>();
                            foreach (var p in an.Points)
                                pts.Add(new PointD(
                                    b.ToImageSpaceX(p.X),
                                    b.ToImageSpaceY(p.Y)
                                ));

                            var pl = new PolylineData(BioPointToOmeroPoint(an, b));
                            pl.setZ(an.coord.Z);
                            pl.setC(an.coord.C);
                            pl.setT(an.coord.T);
                            pl.setText(string.IsNullOrEmpty(an.Text) ? an.id : an.Text);
                            shape = pl;
                            break;
                        }

                    case ROI.Type.Label:
                        {
                            var t = new TextData(
                                string.IsNullOrEmpty(an.Text) ? "Label" : an.Text,
                                b.ToImageSpaceX(an.BoundingBox.X),
                                b.ToImageSpaceY(an.BoundingBox.Y)
                            );
                            //t.setFontSize(new omero.model.LengthI(an.fontSize, omero.model.enums.UnitsLength.PIXEL));
                            t.setZ(an.coord.Z);
                            t.setC(an.coord.C);
                            t.setT(an.coord.T);
                            shape = t;
                            break;
                        }

                    case ROI.Type.Mask:
                        {
                            if (an.roiMask == null)
                                break;

                            var m = new MaskData(
                                an.roiMask.X,
                                an.roiMask.Y,
                                an.roiMask.Width,
                                an.roiMask.Height,
                                an.roiMask.GetBytes()
                            );

                            m.setZ(an.coord.Z);
                            m.setC(an.coord.C);
                            m.setT(an.coord.T);
                            m.setText(string.IsNullOrEmpty(an.Text) ? an.id : an.Text);
                            shape = m;
                            break;
                        }
                }

                var awtColor = new java.awt.Color(an.strokeColor.R, an.strokeColor.G, an.strokeColor.B, an.strokeColor.A);
                // Apply stroke/fill
                shape.getShapeSettings().setStroke(awtColor);

                shape.getShapeSettings().setFill(awtColor);
                // 5. Add shape to ROI
                roid.addShapeData(shape);

                // 6. Save ROI using UpdateService (the correct service)
                var saved = updateService.saveAndReturnObject(roi);

                Console.WriteLine("ROI saved with ID: " + saved.getId().getValue());
            }
        }
        */
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
        }
        /// This function returns the point at the specified index
        /// 
        /// @param i The index of the point to get.
        /// 
        /// @return The point at index i in the Points array.
        public PointD GetPoint(int i)
        {
            if (i < 0 || Points.Count == 0)
                return new PointD();
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
            if(Points == null)
                Points = new List<PointD>();
            Points.Add(p);
            UpdateBoundingBox();
        }
        /// > Adds a range of points to the Points collection and updates the bounding box
        /// 
        /// @param p The points to add to the polygon
        public void AddPoints(PointD[] p)
        {
            if (Points == null)
                Points = new List<PointD>();
            Points.AddRange(p);
            UpdateBoundingBox();
        }
        /// > Adds a range of integer points to the Points collection and updates the bounding box
        /// 
        /// @param p The points to add to the polygon
        public void AddPoints(int[] xp, int[] yp)
        {
            if (Points == null)
                Points = new List<PointD>();
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
            if (Points == null)
                Points = new List<PointD>();
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
            // Safety check
            if ((Points == null || Points.Count == 0) && type != Type.Mask)
            {
                BoundingBox = new RectangleD(0, 0, 0, 0);
                return;
            }

            // -----------------------------
            // 1) MASK ROI MODE
            // -----------------------------
            if (type == Type.Mask && roiMask != null)
            {
                double minX = double.PositiveInfinity;
                double minY = double.PositiveInfinity;
                double maxX = double.NegativeInfinity;
                double maxY = double.NegativeInfinity;

                for (int y = 0; y < roiMask.Height; y++)
                {
                    for (int x = 0; x < roiMask.Width; x++)
                    {
                        if (!roiMask.IsSelected(x, y))
                            continue;

                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }

                // No selected pixels
                if (double.IsInfinity(minX))
                {
                    BoundingBox = new RectangleD(0, 0, 0, 0);
                    return;
                }

                // Mask bounding box IN PHYSICAL COORDINATES
                double physX = roiMask.X + minX * roiMask.PhysicalSizeX;
                double physY = roiMask.Y + minY * roiMask.PhysicalSizeY;

                double physW = (maxX - minX + 1) * roiMask.PhysicalSizeX;
                double physH = (maxY - minY + 1) * roiMask.PhysicalSizeY;

                BoundingBox = new RectangleD(physX, physY, physW, physH);
                return;
            }

            // -----------------------------
            // 2) POINT-BASED ROI MODE
            // -----------------------------
            double minPX = double.PositiveInfinity;
            double minPY = double.PositiveInfinity;
            double maxPX = double.NegativeInfinity;
            double maxPY = double.NegativeInfinity;

            foreach (var p in Points)
            {
                if (p.X < minPX) minPX = p.X;
                if (p.Y < minPY) minPY = p.Y;
                if (p.X > maxPX) maxPX = p.X;
                if (p.Y > maxPY) maxPY = p.Y;
            }

            // Build bounding rectangle
            BoundingBox = new RectangleD(
                minPX,
                minPY,
                maxPX - minPX,
                maxPY - minPY
            );
        }
        public override string ToString()
        {
            return type + " X:" + W + " Y:" + H + " W:" + W + " H:" + H + ", ZCT:" + coord.Z + "," + coord.C + "," + coord.T + " Text: " + Text;
        }
        public void Dispose()
        {
            if (roiMask != null)
            {
                roiMask.Dispose();
            }
        }

    }

}
