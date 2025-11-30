// SelectionMoveTool.cs
// GTK# selection + move tool example — all rendering via SkiaSharp (SkiaSharp.Views.Gtk SKWidget)
// Targets: Gtk# (Gtk 3) / C# (.NET 6/7/8) with SkiaSharp and SkiaSharp.Views.Gtk

using AForge;
using Gdk;
using Gtk;
using javax.swing.text.html;
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

        public static SKPaint SelectBoxColor = new() { Style = SKPaintStyle.Stroke, Color = new SKColor(255, 0, 0) };
        public static float selectBoxSize = 14f;
        public List<PointD> Points { get; set; }
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
            //if (i == Points.Count - 1)
                UpdateBoundingBox();
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
            if (Points == null)
            {
                if (type == Type.Rectangle || type == Type.Ellipse)
                {
                    Points = new List<PointD>();
                    Points.Add(new PointD(BoundingBox.X, BoundingBox.Y));
                    Points.Add(new PointD(BoundingBox.X + BoundingBox.W, BoundingBox.Y));
                    Points.Add(new PointD(BoundingBox.X, BoundingBox.Y + BoundingBox.H));
                    Points.Add(new PointD(BoundingBox.X + BoundingBox.W, BoundingBox.Y + BoundingBox.H));
                }
                else if(type == Type.Line)
                {
                    Points = new List<PointD>();
                    Points.Add(new PointD(BoundingBox.X, BoundingBox.Y));
                    Points.Add(new PointD(BoundingBox.X + BoundingBox.W, BoundingBox.Y + BoundingBox.H));
                }
                else if (type == Type.Point || type == Type.Label)
                {
                    Points = new List<PointD>();
                    Points.Add(new PointD(BoundingBox.X, BoundingBox.Y));
                }
            }
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
                BoundingBox = new RectangleD(Points[0].X + (minx * roiMask.PhysicalSizeX), Points[0].Y + (miny * roiMask.PhysicalSizeY),
                    (maxx - minx) * roiMask.PhysicalSizeX, (maxy - miny) * roiMask.PhysicalSizeY);
                return;
            }
            else
            {
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
