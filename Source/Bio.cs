using AForge;
using AForge.Imaging;
using AForge.Imaging.Filters;
using BitMiracle.LibTiff.Classic;
using loci.common.services;
using loci.formats;
using loci.formats.services;
using ome.xml.model.primitives;
using loci.formats.meta;
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

namespace BioLib
{
    /* A class declaration. */
    public static class Images
    {
        public static List<BioImage> images = new List<BioImage>();
        /// <summary>
        /// The function `GetImage` returns a `BioImage` object from a list of images based on a given
        /// ID or file name.
        /// </summary>
        /// <param name="ids">The parameter "ids" is a string that represents the ID or file name of an
        /// image.</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
        public static BioImage GetImage(string ids)
        {
            for (int i = 0; i < images.Count; i++)
            {
                if (images[i].ID == ids || images[i].file == ids)
                    return images[i];
            }
            return null;
        }

        /// <summary>
        /// The function `AddImage` adds a `BioImage` object to a collection of images if it is not
        /// already present.
        /// </summary>
        /// <param name="BioImage">The BioImage parameter is an object that represents an image in a
        /// biological context. It likely contains properties such as the image file name, ID, and
        /// possibly other metadata related to the image.</param>
        /// <param name="newtab">The "newtab" parameter is a boolean value that determines whether the
        /// image should be added to a new tab or not. If "newtab" is set to true, the image will be
        /// added to a new tab. If "newtab" is set to false, the image will be added</param>
        /// <returns>
        /// If the condition `if (images.Contains(im))` is true, then the method will return and nothing
        /// will be returned explicitly.
        /// </returns>
        public static void AddImage(BioImage im, bool newtab)
        {
            if (images.Contains(im)) return;
            im.Filename = GetImageName(im.ID);
            im.ID = im.Filename;
            images.Add(im);

            //App.Image = im;
            //NodeView.viewer.AddTab(im);
        }

        /// <summary>
        /// The function `GetImageCountByName` returns the count of images with a specific name.
        /// </summary>
        /// <param name="s">The parameter "s" is a string that represents the name of an image
        /// file.</param>
        /// <returns>
        /// The method is returning the count of images with the given name.
        /// </returns>
        public static int GetImageCountByName(string s)
        {
            int i = 0;
            string name = Path.GetFileNameWithoutExtension(s);
            for (int im = 0; im < images.Count; im++)
            {
                if (images[im].ID == s)
                    i++;
            }
            return i;
        }

        /// <summary>
        /// The GetImageName function generates a unique image name based on the input string.
        /// </summary>
        /// <param name="s">The parameter "s" is a string that represents the file path of an
        /// image.</param>
        /// <returns>
        /// The method returns a string that represents the unique name for an image.
        /// </returns>
        public static string GetImageName(string s)
        {
            //Here we create a unique ID for an image.
            int i = Images.GetImageCountByName(s);
            if (i == 0)
                return Path.GetFileName(s);
            string name = Path.GetFileNameWithoutExtension(s);
            string ext = Path.GetExtension(s);
            int sti = name.LastIndexOf("-");
            if (sti == -1)
            {
                return name + "-" + i + ext;

            }
            else
            {
                string stb = name.Substring(0, sti);
                string sta = name.Substring(sti + 1, name.Length - sti - 1);
                int ind;
                if (int.TryParse(sta, out ind))
                {
                    return stb + "-" + (ind + 1).ToString() + ext;
                }
                else
                    return name + "-" + i + ext;
            }
        }

        /// <summary>
        /// The function "RemoveImage" takes a BioImage object as a parameter and removes the image with
        /// the corresponding ID.
        /// </summary>
        /// <param name="BioImage">The BioImage is a class that represents an image in a biological
        /// context. It likely contains properties such as ID, name, file path, and other relevant
        /// information about the image.</param>
        public static void RemoveImage(BioImage im)
        {
            RemoveImage(im.ID);
        }

        /// <summary>
        /// The RemoveImage function removes an image from a collection and disposes of it.
        /// </summary>
        /// <param name="id">The "id" parameter is a string that represents the unique identifier of the
        /// image that needs to be removed.</param>
        /// <returns>
        /// If the `im` object is `null`, then nothing is being returned. The method will simply exit
        /// and no further code will be executed.
        /// </returns>
        public static void RemoveImage(string id)
        {
            BioImage im = GetImage(id);
            if (im == null)
                return;
            images.Remove(im);
            im.Dispose();
            im = null;
        }

        /// <summary>
        /// The function `UpdateImage` updates an image in a list of images based on its ID.
        /// </summary>
        /// <param name="BioImage">BioImage is a class that represents an image object. It likely has
        /// properties such as ID, image data, and other relevant information.</param>
        /// <returns>
        /// The method is returning nothing (void).
        /// </returns>
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
        /* The above code is defining a property called "SizeInBytes" in C#. This property returns the
        size of an image in bytes based on its pixel format. */
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
        /* The above code is defining a constructor for a class called "Resolution". The constructor
        takes in several parameters including the width and height of an image, the number of pixels
        per unit of physical measurement, the number of bits per pixel, the physical dimensions of
        the image, and the stage dimensions of the image. The constructor assigns these values to
        the corresponding properties of the class. */
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
        /* The above code is defining a constructor for a class called "Resolution". The constructor
        takes in several parameters including width (w), height (h), pixel format (f), physical
        dimensions (physX, physY, physZ), and stage dimensions (stageX, stageY, stageZ). The
        constructor assigns the parameter values to corresponding class variables. */
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
            return "(" + x + ", " + y + ") " + format.ToString() + " " + (SizeInBytes / 1000) + " KB";
        }
    }
    /* Declaring a struct named RectangleD. */
    public struct RectangleD
    {
        private double x;
        private double y;
        private double w;
        private double h;
        public double X { get { return x; } set { x = value; } }
        public double Y { get { return y; } set { y = value; } }
        public double W { get { return w; } set { w = value; } }
        public double H { get { return h; } set { h = value; } }

        /* The above code is defining a constructor for a class called RectangleD in C#. The
        constructor takes four parameters: X, Y, W, and H, which represent the x-coordinate,
        y-coordinate, width, and height of the rectangle, respectively. Inside the constructor, the
        values of these parameters are assigned to the corresponding instance variables x, y, w, and
        h. */
        public RectangleD(double X, double Y, double W, double H)
        {
            x = X;
            y = Y;
            w = W;
            h = H;
        }
        /// <summary>
        /// The function converts the coordinates and dimensions of a rectangle from float to integer
        /// values.
        /// </summary>
        /// <returns>
        /// The method is returning a new instance of the Rectangle class, with the X, Y, W, and H
        /// values casted to integers.
        /// </returns>
        public Rectangle ToRectangleInt()
        {
            return new Rectangle((int)X, (int)Y, (int)W, (int)H);
        }

        /// <summary>
        /// The function checks if a given rectangle intersects with another rectangle.
        /// </summary>
        /// <param name="RectangleD">The RectangleD is a custom data type that represents a rectangle in
        /// 2D space. It has four properties: X (the x-coordinate of the top-left corner), Y (the
        /// y-coordinate of the top-left corner), W (the width of the rectangle), and H (the height of
        /// the</param>
        /// <returns>
        /// The method is returning a boolean value. If the rectangles intersect, it returns true. If
        /// they do not intersect, it returns false.
        /// </returns>
        public bool IntersectsWith(RectangleD rect1)
        {
            if (rect1.X + rect1.W < X || X + W < rect1.X ||
        rect1.Y + rect1.H < Y || Y + H < rect1.Y)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// The function checks if a given point intersects with another point.
        /// </summary>
        /// <param name="PointD">The PointD class represents a point in a two-dimensional space. It
        /// typically has two properties: X and Y, which represent the coordinates of the point on the
        /// x-axis and y-axis, respectively.</param>
        /// <returns>
        /// The method is returning a boolean value.
        /// </returns>
        public bool IntersectsWith(PointD p)
        {
            return IntersectsWith(p.X, p.Y);
        }

        /// <summary>
        /// The function checks if a given point (x, y) intersects with a rectangle defined by its
        /// top-left corner (X, Y) and its width (W) and height (H).
        /// </summary>
        /// <param name="x">The x-coordinate of the point to check for intersection.</param>
        /// <param name="y">The y parameter represents the y-coordinate of a point.</param>
        /// <returns>
        /// The method is returning a boolean value. It returns true if the given coordinates (x, y)
        /// intersect with the rectangle defined by the X, Y, W, and H properties. Otherwise, it returns
        /// false.
        /// </returns>
        public bool IntersectsWith(double x, double y)
        {
            if (X <= x && (X + W) >= x && Y <= y && (Y + H) >= y)
                return true;
            else
                return false;
        }

        /// <summary>
        /// The function converts the values of X, Y, W, and H into a RectangleF object and returns it.
        /// </summary>
        /// <returns>
        /// The method is returning a new instance of the RectangleF class, with the X, Y, W, and H
        /// values converted to float.
        /// </returns>
        public RectangleF ToRectangleF()
        {
            return new RectangleF((float)X, (float)Y, (float)W, (float)H);
        }

        /// <summary>
        /// The ToString() function returns a string representation of the object's properties, rounded
        /// to two decimal places.
        /// </summary>
        /// <returns>
        /// The method is returning a string representation of the object's properties, specifically the
        /// values of X, Y, W, and H. The values are rounded to two decimal places using the
        /// MidpointRounding.ToZero rounding method. The values are concatenated with commas and spaces
        /// to form the returned string.
        /// </returns>
        public override string ToString()
        {
            double w = Math.Round(W, 2, MidpointRounding.ToZero);
            double h = Math.Round(H, 2, MidpointRounding.ToZero);
            double x = Math.Round(X, 2, MidpointRounding.ToZero);
            double y = Math.Round(Y, 2, MidpointRounding.ToZero);
            return x + ", " + y + ", " + w + ", " + h;
        }

    }

    /* The ROI class is a class that contains a list of points, a bounding box, and a type */
    public class ROI
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
            Label
        }
        /* A property of a class. */
        public PointD Point
        {
            get
            {
                if (Points.Count == 0)
                    return new PointD(0, 0);
                if (type == Type.Line || type == Type.Ellipse || type == Type.Label || type == Type.Freeform)
                    return new PointD(BoundingBox.X, BoundingBox.Y);
                return Points[0];
            }
            set
            {
                if (Points.Count == 0)
                {
                    AddPoint(value);
                }
                else
                    UpdatePoint(value, 0);
                UpdateBoundingBox();
            }
        }
        /* The above code is defining a property called "Rect" of type "RectangleD". */
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
                else
                    return new RectangleD(Points[0].X, Points[0].Y, 1, 1);
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
            return BioImage.ToImageSpace(PointsD,(int)res.StageSizeX, (int)res.StageSizeY, (int)res.PhysicalSizeX, (int)res.PhysicalSizeY);
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
        public bool selected = false;
        public bool subPixel = false;

        /*
        public Size TextSize
        {
            get
            {
                return TextRenderer.MeasureText(text, font);
            }
        }
        */
        /// <summary>
        /// The Copy function creates a deep copy of an ROI object.
        /// </summary>
        /// <returns>
        /// The method is returning a copy of the ROI object.
        /// </returns>
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
            copy.selected = selected;
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
        /// <summary>
        /// The function "Copy" creates a deep copy of an ROI object, including all its properties and
        /// coordinates.
        /// </summary>
        /// <param name="ZCT">The ZCT parameter is of type ZCT and is used to pass the coordinates of
        /// the ROI.</param>
        /// <returns>
        /// The method is returning a copy of the ROI object.
        /// </returns>
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
            copy.selected = selected;
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

        /// <summary>
        /// The function returns a new RectangleD object with adjusted dimensions based on the given
        /// scale.
        /// </summary>
        /// <param name="scale">The scale parameter represents the scaling factor that will be applied
        /// to the rectangle's dimensions.</param>
        /// <returns>
        /// The method is returning a RectangleD object.
        /// </returns>
        public RectangleD GetSelectBound(double scale)
        {
            double f = scale / 2;
            return new RectangleD(BoundingBox.X - f, BoundingBox.Y - f, BoundingBox.W + scale, BoundingBox.H + scale);
        }
        /* The above code is defining a constructor for a class called ROI. The constructor initializes
        the coord variable with a new instance of the ZCT class, passing in the values 0, 0, and 0
        as parameters. It also sets the strokeColor variable to the Color.Yellow value.
        Additionally, it creates a new instance of the RectangleD class, passing in the values 0, 0,
        1, and 1 as parameters, and assigns it to the BoundingBox variable. */
        public ROI()
        {
            coord = new ZCT(0, 0, 0);
            strokeColor = Color.Yellow;
            BoundingBox = new RectangleD(0, 0, 1, 1);
        }

        /// <summary>
        /// The function takes a size parameter and returns an array of rectangles with that size,
        /// centered around each point in a list.
        /// </summary>
        /// <param name="s">The parameter "s" represents the size of the select boxes.</param>
        /// <returns>
        /// The method is returning an array of RectangleD objects.
        /// </returns>
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

        /// <summary>
        /// The function "CreatePoint" creates a new ROI (Region of Interest) object with a specified
        /// coordinate and adds a point to it.
        /// </summary>
        /// <param name="ZCT">ZCT is an object that represents the Z, C, and T coordinates of a point in
        /// a three-dimensional space. It is used to specify the position of the point in a stack or
        /// image sequence.</param>
        /// <param name="x">The x-coordinate of the point.</param>
        /// <param name="y">The parameter "y" is a double value representing the y-coordinate of the
        /// point.</param>
        /// <returns>
        /// The method is returning an object of type ROI.
        /// </returns>
        public static ROI CreatePoint(ZCT coord, double x, double y)
        {
            ROI an = new ROI();
            an.coord = coord;
            an.AddPoint(new PointD(x, y));
            an.type = Type.Point;
            //Recorder.AddLine("ROI.CreatePoint(new ZCT(" + coord.Z + "," + coord.C + "," + coord.T + "), " + x + "," + y + ");");
            return an;
        }

        /// <summary>
        /// The function CreateLine creates a line region of interest (ROI) with specified coordinates
        /// and points.
        /// </summary>
        /// <param name="ZCT">ZCT is a custom class that represents the coordinates of a point in a 3D
        /// space. It has three properties: Z, C, and T, which represent the Z-axis, C-axis, and T-axis
        /// coordinates respectively.</param>
        /// <param name="PointD">PointD is a class representing a point in a two-dimensional space. It
        /// has two properties: X and Y, which represent the coordinates of the point on the X and Y
        /// axes, respectively.</param>
        /// <param name="PointD">PointD is a class representing a point in a two-dimensional space. It
        /// has two properties: X and Y, which represent the coordinates of the point on the X and Y
        /// axes, respectively.</param>
        /// <returns>
        /// The method is returning an object of type ROI.
        /// </returns>
        public static ROI CreateLine(ZCT coord, PointD x1, PointD x2)
        {
            ROI an = new ROI();
            an.coord = coord;
            an.type = Type.Line;
            an.AddPoint(x1);
            an.AddPoint(x2);
            //Recorder.AddLine("ROI.CreateLine(new ZCT(" + coord.Z + "," + coord.C + "," + coord.T + "), new PointD(" + x1.X + "," + x1.Y + "), new PointD(" + x2.X + "," + x2.Y + "));");
            return an;
        }

        /// <summary>
        /// The function CreateRectangle creates a new ROI (Region of Interest) object with a specified
        /// coordinate, type, and rectangle dimensions.
        /// </summary>
        /// <param name="ZCT">ZCT is an object that represents the coordinates of a point in a 3D space.
        /// It has three properties: Z, C, and T, which represent the Z-axis, C-axis, and T-axis
        /// coordinates respectively.</param>
        /// <param name="x">The x-coordinate of the top-left corner of the rectangle.</param>
        /// <param name="y">The parameter "y" represents the y-coordinate of the top-left corner of the
        /// rectangle.</param>
        /// <param name="w">The parameter "w" represents the width of the rectangle.</param>
        /// <param name="h">The parameter "h" represents the height of the rectangle.</param>
        /// <returns>
        /// The method is returning an object of type ROI.
        /// </returns>
        public static ROI CreateRectangle(ZCT coord, double x, double y, double w, double h)
        {
            ROI an = new ROI();
            an.coord = coord;
            an.type = Type.Rectangle;
            an.Rect = new RectangleD(x, y, w, h);
            //Recorder.AddLine("ROI.CreateRectangle(new ZCT(" + coord.Z + "," + coord.C + "," + coord.T + "), new RectangleD(" + x + "," + y + "," + w + "," + h + ");");
            return an;
        }

        /// <summary>
        /// The function CreateEllipse creates an ROI (Region of Interest) object with an ellipse shape,
        /// given the coordinates, width, and height.
        /// </summary>
        /// <param name="ZCT">ZCT is a custom class that represents the coordinates of a point in a 3D
        /// space. It has three properties: Z, C, and T, which represent the Z-axis, C-axis, and T-axis
        /// coordinates respectively.</param>
        /// <param name="x">The x-coordinate of the center of the ellipse.</param>
        /// <param name="y">The parameter "y" represents the y-coordinate of the center of the
        /// ellipse.</param>
        /// <param name="w">The width of the ellipse.</param>
        /// <param name="h">The parameter "h" represents the height of the ellipse.</param>
        /// <returns>
        /// The method is returning an object of type ROI.
        /// </returns>
        public static ROI CreateEllipse(ZCT coord, double x, double y, double w, double h)
        {
            ROI an = new ROI();
            an.coord = coord;
            an.type = Type.Ellipse;
            an.Rect = new RectangleD(x, y, w, h);
            //Recorder.AddLine("ROI.CreateEllipse(new ZCT(" + coord.Z + "," + coord.C + "," + coord.T + "), new RectangleD(" + x + "," + y + "," + w + "," + h + ");");
            return an;
        }

        /// <summary>
        /// The function "CreatePolygon" creates a polygon region of interest (ROI) with the given
        /// coordinates and points.
        /// </summary>
        /// <param name="ZCT">ZCT is a data type that represents the coordinate system used for the
        /// polygon. It could be a specific coordinate system like WGS84 or a custom coordinate system
        /// defined by the application.</param>
        /// <param name="pts">The "pts" parameter is an array of PointD objects. Each PointD object
        /// represents a point in the polygon.</param>
        /// <returns>
        /// The method is returning an instance of the ROI class.
        /// </returns>
        public static ROI CreatePolygon(ZCT coord, PointD[] pts)
        {
            ROI an = new ROI();
            an.coord = coord;
            an.type = Type.Polygon;
            an.AddPoints(pts);
            an.closed = true;
            return an;
        }

        /// <summary>
        /// The function creates a freeform region of interest (ROI) with the given coordinates and
        /// points.
        /// </summary>
        /// <param name="ZCT">The ZCT parameter is a data structure that represents the coordinate
        /// system in which the points are defined. It may contain information such as the zoom level,
        /// the center point, and the transformation matrix for converting between screen coordinates
        /// and world coordinates.</param>
        /// <param name="pts">The "pts" parameter is an array of PointD objects. Each PointD object
        /// represents a point in a coordinate system.</param>
        /// <returns>
        /// The method is returning an instance of the ROI class.
        /// </returns>
        public static ROI CreateFreeform(ZCT coord, PointD[] pts)
        {
            ROI an = new ROI();
            an.coord = coord;
            an.type = Type.Freeform;
            an.AddPoints(pts);
            an.closed = true;
            return an;
        }

        /// <summary>
        /// The function updates a specific point in a list of points and then updates the bounding box.
        /// </summary>
        /// <param name="PointD">The parameter "p" is of type PointD, which represents a point in a
        /// two-dimensional space. It likely has properties such as X and Y to store the coordinates of
        /// the point.</param>
        /// <param name="i">The parameter "i" is an integer that represents the index of the point in
        /// the list of points. It is used to determine which point in the list should be updated with
        /// the new point "p".</param>
        public void UpdatePoint(PointD p, int i)
        {
            if (i < Points.Count)
            {
                Points[i] = p;
            }
            UpdateBoundingBox();
        }

        /// <summary>
        /// The function "GetPoint" returns a PointD object at the specified index from an array of
        /// PointD objects.
        /// </summary>
        /// <param name="i">The parameter "i" is an integer that represents the index of the point you
        /// want to retrieve from the "Points" array.</param>
        /// <returns>
        /// The method is returning a PointD object.
        /// </returns>
        public PointD GetPoint(int i)
        {
            return Points[i];
        }

        /// <summary>
        /// The function returns an array of PointD objects.
        /// </summary>
        /// <returns>
        /// An array of PointD objects.
        /// </returns>
        public PointD[] GetPoints()
        {
            return Points.ToArray();
        }

        /// <summary>
        /// The function converts a list of points to an array of PointF objects in C#.
        /// </summary>
        /// <returns>
        /// The method is returning an array of PointF objects.
        /// </returns>
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

        /// <summary>
        /// The function adds a PointD object to a list of points and updates the bounding box.
        /// </summary>
        /// <param name="PointD">The parameter "p" is of type PointD, which is likely a custom class
        /// representing a point in a two-dimensional space.</param>
        public void AddPoint(PointD p)
        {
            Points.Add(p);
            UpdateBoundingBox();
        }

        /// <summary>
        /// The function adds an array of PointD objects to a list and updates the bounding box.
        /// </summary>
        /// <param name="p">The parameter "p" is an array of PointD objects.</param>
        public void AddPoints(PointD[] p)
        {
            Points.AddRange(p);
            UpdateBoundingBox();
        }

        /// <summary>
        /// The function takes two arrays of integers representing x and y coordinates and adds them as
        /// points to a list, then updates the bounding box.
        /// </summary>
        /// <param name="xp">The xp parameter is an array of integers representing the x-coordinates of
        /// the points to be added.</param>
        /// <param name="yp">The parameter `yp` is an array of integers representing the y-coordinates
        /// of the points to be added.</param>
        public void AddPoints(int[] xp, int[] yp)
        {
            for (int i = 0; i < xp.Length; i++)
            {
                Points.Add(new PointD(xp[i], yp[i]));
            }
            UpdateBoundingBox();
        }

        /// <summary>
        /// The function `AddPoints` takes in two arrays of floats representing x and y coordinates and
        /// adds them as points to a list, then updates the bounding box.
        /// </summary>
        /// <param name="xp">An array of x-coordinates for the points to be added.</param>
        /// <param name="yp">The parameter `yp` is an array of `float` values representing the
        /// y-coordinates of the points to be added.</param>
        public void AddPoints(float[] xp, float[] yp)
        {
            for (int i = 0; i < xp.Length; i++)
            {
                Points.Add(new PointD(xp[i], yp[i]));
            }
            UpdateBoundingBox();
        }

        /// <summary>
        /// The function removes points from a list based on their indices and updates the bounding box.
        /// </summary>
        /// <param name="indexs">The parameter "indexs" is an array of integers that represents the
        /// indices of the points to be removed from the "Points" list.</param>
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

        /// <summary>
        /// The function returns the count of points in a collection.
        /// </summary>
        /// <returns>
        /// The method is returning the count of points in the Points collection.
        /// </returns>
        public int GetPointCount()
        {
            return Points.Count;
        }

        /// <summary>
        /// The function takes a string of coordinates and converts them into an array of PointD
        /// objects.
        /// </summary>
        /// <param name="s">The parameter `s` is a string that represents a series of points. Each point
        /// is represented by a pair of coordinates separated by a space, tab, or comma. The x and y
        /// coordinates are separated by either a tab or a comma.</param>
        /// <returns>
        /// The method is returning an array of PointD objects.
        /// </returns>
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

        /// <summary>
        /// The function "PointsToString" takes a BioImage object and converts its points to a string
        /// representation in image space.
        /// </summary>
        /// <param name="BioImage">The BioImage parameter is an object of type BioImage. It is used to
        /// convert the points from the image space to the desired format.</param>
        /// <returns>
        /// The method is returning a string representation of the points in the BioImage object.
        /// </returns>
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

        /// <summary>
        /// The function calculates the bounding box of a set of points by finding the minimum and
        /// maximum x and y coordinates, and then creates a rectangle with those coordinates.
        /// </summary>
        public void UpdateBoundingBox()
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

        public override string ToString()
        {
            double w = Math.Round(W, 2, MidpointRounding.ToZero);
            double h = Math.Round(H, 2, MidpointRounding.ToZero);
            double x = Math.Round(Point.X, 2, MidpointRounding.ToZero);
            double y = Math.Round(Point.Y, 2, MidpointRounding.ToZero);
            return type.ToString() + ", " + Text + " (" + w + ", " + h + ") " + " (" + x + ", " + y + ") " + coord.ToString();
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

        /// <summary>
        /// The function "GetFilter" returns a filter object from a list of filters based on the given
        /// name.
        /// </summary>
        /// <param name="name">The name of the filter that you want to retrieve.</param>
        /// <returns>
        /// The method is returning an object of type Filt.
        /// </returns>
        public static Filt GetFilter(string name)
        {
            foreach (Filt item in filters)
            {
                if (item.name == name)
                    return item;
            }
            return null;
        }

        /// <summary>
        /// The function "GetFilter" returns a filter based on the given type and index.
        /// </summary>
        /// <param name="type">The type parameter is an integer that represents the type of filter. It
        /// is used to determine which array to access in order to retrieve the filter.</param>
        /// <param name="index">The index parameter is used to specify the position of the filter within
        /// the filters array.</param>
        /// <returns>
        /// The method is returning a filter object.
        /// </returns>
        public static Filt GetFilter(int type, int index)
        {
            return filters[indexs[type, index]];
        }
        public static List<Filt> filters = new List<Filt>();

        /// <summary>
        /// The Base function applies a filter to a BioImage object and returns the modified image.
        /// </summary>
        /// <param name="id">The id parameter is a string that represents the unique identifier of the
        /// image. It is used to retrieve the image from a collection or database.</param>
        /// <param name="name">The "name" parameter is a string that represents the name of the filter
        /// to be applied to the image.</param>
        /// <param name="inPlace">The "inPlace" parameter is a boolean value that determines whether the
        /// filter should be applied to the original image or to a copy of the image. If "inPlace" is
        /// set to true, the filter will be applied to the original image. If "inPlace" is set to
        /// false,</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
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
                    Images.AddImage(img, true);
                }
                //Recorder.AddLine("Filters.Base(" + '"' + id +
                //    '"' + "," + '"' + name + '"' + "," + inPlace.ToString().ToLower() + ");");
            }
            catch (Exception e)
            {
                MessageDialog dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, e.Message);
                dialog.Show();
            }
            return img;
        }

        /// <summary>
        /// The Base2 function applies a filter to an image, using another image as an overlay, and
        /// returns the resulting image.
        /// </summary>
        /// <param name="id">The parameter "id" is a string that represents the ID of the first
        /// image.</param>
        /// <param name="id2">The parameter "id2" is a string that represents the ID of the second
        /// image.</param>
        /// <param name="name">The "name" parameter is a string that represents the name of the filter
        /// to be applied to the images.</param>
        /// <param name="inPlace">The "inPlace" parameter is a boolean value that determines whether the
        /// filter should be applied to the image in-place or create a new image with the filtered
        /// result. If "inPlace" is set to true, the filter will be applied directly to the input image
        /// and modify it. If "in</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
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
                    Images.AddImage(img, true);
                }
                //Recorder.AddLine("Filters.Base2(" + '"' + id + '"' + "," +
                //   '"' + id2 + '"' + "," + '"' + name + '"' + "," + inPlace.ToString().ToLower() + ");");
                return img;
            }
            catch (Exception e)
            {
                MessageDialog dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, e.Message);
                dialog.Show();
            }
            return img;
        }

        /// <summary>
        /// The function takes an image ID, filter name, and a boolean indicating whether to apply the
        /// filter in place or create a copy, and applies the filter to the image.
        /// </summary>
        /// <param name="id">The "id" parameter is a string that represents the unique identifier of the
        /// image. It is used to retrieve the image from the Images collection.</param>
        /// <param name="name">The "name" parameter is a string that represents the name of the filter
        /// to be applied to the image.</param>
        /// <param name="inPlace">The "inPlace" parameter is a boolean value that determines whether the
        /// filter should be applied in place or create a new copy of the image. If "inPlace" is true,
        /// the filter will be applied directly to the image buffers. If "inPlace" is false, a copy of
        /// the</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
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
                    Images.AddImage(img, true);
                }
                //Recorder.AddLine("Filters.InPlace(" + '"' + id +
                //    '"' + "," + '"' + name + '"' + "," + inPlace.ToString().ToLower() + ");");
                return img;
            }
            catch (Exception e)
            {
                MessageDialog dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, e.Message);
                dialog.Show();
            }
            return img;
        }

        /// <summary>
        /// The function "InPlace2" applies a filter to an image, either in place or by creating a copy,
        /// and returns the resulting image.
        /// </summary>
        /// <param name="id">The parameter "id" is a string that represents the ID of the first
        /// image.</param>
        /// <param name="id2">The parameter "id2" is a string that represents the ID of the second
        /// image.</param>
        /// <param name="name">The "name" parameter is a string that represents the name of the filter
        /// to be applied to the images.</param>
        /// <param name="inPlace">The "inPlace" parameter is a boolean value that determines whether the
        /// filter should be applied in place or not. If "inPlace" is true, the filter will be applied
        /// directly to the input image ("img") without creating a copy. If "inPlace" is false, a copy
        /// of</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
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
                    Images.AddImage(img, true);
                }
                //Recorder.AddLine("Filters.InPlace2(" + '"' + id + '"' + "," +
                 //  '"' + id2 + '"' + "," + '"' + name + '"' + "," + inPlace.ToString().ToLower() + ");");
                return img;
            }
            catch (Exception e)
            {
                MessageDialog dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, e.Message);
                dialog.Show();
            }
            return img;
        }

        /// <summary>
        /// The function takes an image ID, filter name, and a boolean flag indicating whether to apply
        /// the filter in place or create a copy, and applies the filter to the image buffers
        /// accordingly.
        /// </summary>
        /// <param name="id">The id parameter is a string that represents the unique identifier of the
        /// image. It is used to retrieve the image from the Images collection.</param>
        /// <param name="name">The "name" parameter is a string that represents the name of the filter
        /// to be applied to the image.</param>
        /// <param name="inPlace">The "inPlace" parameter is a boolean value that determines whether the
        /// filter should be applied in place or create a new copy of the image. If "inPlace" is true,
        /// the filter will be applied directly to the image buffers. If "inPlace" is false, a copy of
        /// the</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
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
                    Images.AddImage(img, true);
                }
                //Recorder.AddLine("Filters.InPlacePartial(" + '"' + id +
                //    '"' + "," + '"' + name + '"' + "," + inPlace.ToString().ToLower() + ");");
                return img;
            }
            catch (Exception e)
            {
                MessageDialog dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, e.Message);
                dialog.Show();
            }
            return img;
        }

        /// <summary>
        /// The function resizes a BioImage object using a specified filter and returns the resized
        /// image.
        /// </summary>
        /// <param name="id">The id parameter is a string that represents the unique identifier of the
        /// image. It is used to retrieve the image from the Images collection.</param>
        /// <param name="name">The "name" parameter is a string that represents the name of the filter
        /// to be applied to the image.</param>
        /// <param name="inPlace">The "inPlace" parameter is a boolean value that determines whether the
        /// image should be resized in place or if a new copy of the image should be created. If
        /// "inPlace" is set to true, the image will be resized directly, modifying the original image.
        /// If "inPlace" is</param>
        /// <param name="w">The parameter "w" in the Resize method represents the desired width of the
        /// resized image.</param>
        /// <param name="h">The parameter "h" in the code represents the desired height of the resized
        /// image.</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
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
                    Images.AddImage(img, true);
                }
                //Recorder.AddLine("Filters.Resize(" + '"' + id +
                //    '"' + "," + '"' + name + '"' + "," + inPlace.ToString().ToLower() + "," + w + "," + h + ");");
            }
            catch (Exception e)
            {
                MessageDialog dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, e.Message);
                dialog.Show();
            }
            return img;
        }

        /// <summary>
        /// The function takes an image, rotates it by a specified angle, and returns the rotated image.
        /// </summary>
        /// <param name="id">The id parameter is a string that represents the unique identifier of the
        /// image.</param>
        /// <param name="name">The "name" parameter is a string that represents the name of the filter
        /// to be applied to the image.</param>
        /// <param name="inPlace">The "inPlace" parameter determines whether the rotation should be
        /// applied to the original image or a copy of it. If "inPlace" is set to true, the rotation
        /// will be applied to the original image. If "inPlace" is set to false, a copy of the image
        /// will be</param>
        /// <param name="angle">The angle parameter is the amount of rotation in degrees that you want
        /// to apply to the image.</param>
        /// <param name="a">The parameter "a" represents the alpha value of the fill color. Alpha value
        /// determines the transparency of the color, with 0 being fully transparent and 255 being fully
        /// opaque.</param>
        /// <param name="r">The parameter "r" represents the red component of the fill color. It is an
        /// integer value ranging from 0 to 255, where 0 represents no red and 255 represents maximum
        /// red intensity.</param>
        /// <param name="g">The parameter "g" in the Rotate method represents the green component of the
        /// fill color used in the rotation operation. It is an integer value ranging from 0 to 255,
        /// where 0 represents no green and 255 represents maximum green intensity.</param>
        /// <param name="b">The parameter "b" represents the blue component of the fill color used in
        /// the rotation operation.</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
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
                    Images.AddImage(img, true);
                }
                //Recorder.AddLine("Filters.Rotate(" + '"' + id +
                //    '"' + "," + '"' + name + '"' + "," + inPlace.ToString().ToLower() + "," + angle.ToString() + "," +
                //    a + "," + r + "," + g + "," + b + ");");
            }
            catch (Exception e)
            {
                MessageDialog dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, e.Message);
                dialog.Show();
            }
            return img;

        }

        /// <summary>
        /// The function takes an image ID, a filter name, a boolean indicating whether the
        /// transformation should be applied in place, and an angle, and applies the specified
        /// transformation filter to the image.
        /// </summary>
        /// <param name="id">The id parameter is a string that represents the unique identifier of the
        /// bioimage.</param>
        /// <param name="name">The "name" parameter is a string that represents the name of the
        /// transformation filter to be applied to the image.</param>
        /// <param name="inPlace">The "inPlace" parameter is a boolean value that determines whether the
        /// transformation should be applied to the original image or a copy of it. If "inPlace" is set
        /// to true, the transformation will be applied to the original image. If it is set to false, a
        /// copy of the image</param>
        /// <param name="angle">The angle parameter is a float value that represents the rotation angle
        /// in degrees. It is used to specify the amount of rotation to be applied to the image during
        /// the transformation.</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
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
                    Images.AddImage(img, true);
                }
                //Recorder.AddLine("Filters.Transformation(" + '"' + id +
                //        '"' + "," + '"' + name + '"' + "," + inPlace.ToString().ToLower() + "," + angle + ");");
            }
            catch (Exception e)
            {
                MessageDialog dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, e.Message);
                dialog.Show();
            }
            return img;
        }

        /// <summary>
        /// The function `Copy` takes an image ID, a filter name, and a boolean flag indicating whether
        /// to perform the operation in place, and returns a copy of the image with the specified filter
        /// applied.
        /// </summary>
        /// <param name="id">The id parameter is a string that represents the unique identifier of the
        /// bioimage.</param>
        /// <param name="name">The "name" parameter is a string that represents the name of the filter
        /// to be applied to the image.</param>
        /// <param name="inPlace">The "inPlace" parameter is a boolean value that determines whether the
        /// image processing operation should be performed on the original image or on a copy of the
        /// image. If "inPlace" is set to true, the operation will be performed on the original image.
        /// If "inPlace" is set to</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
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
                    Images.AddImage(img, true);
                }
                //Recorder.AddLine("Filters.Copy(" + '"' + id +
                //        '"' + "," + '"' + name + '"' + "," + inPlace.ToString().ToLower() + ");");
            }
            catch (Exception e)
            {
                MessageDialog dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, e.Message);
                dialog.Show();
            }
            return img;
        }

        /// <summary>
        /// The Crop function takes an image ID, coordinates, and dimensions, crops the image based on
        /// those parameters, applies thresholding and stacking filters, and returns the cropped image.
        /// </summary>
        /// <param name="id">The id parameter is a string that represents the identifier of the
        /// bioimage.</param>
        /// <param name="x">The x-coordinate of the top-left corner of the crop area.</param>
        /// <param name="y">The parameter "y" in the Crop method represents the y-coordinate of the
        /// top-left corner of the rectangle to be cropped from the image.</param>
        /// <param name="w">The parameter "w" in the Crop method represents the width of the rectangle
        /// to be cropped from the image.</param>
        /// <param name="h">The parameter "h" in the Crop method represents the height of the rectangle
        /// to be cropped from the image.</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
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
            //Recorder.AddLine("Filters.Crop(" + '"' + id + '"' + "," + x + "," + y + "," + w + "," + h + ");");
            //App.tabsView.AddTab(img);
            return img;
        }

        /// <summary>
        /// The Crop function takes an image ID and a rectangle, and returns a cropped version of the
        /// image.
        /// </summary>
        /// <param name="id">The "id" parameter is a string that represents the identifier of the bio
        /// image that you want to crop.</param>
        /// <param name="RectangleD">The RectangleD is a class that represents a rectangle with double
        /// precision coordinates. It has four properties:</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
        public static BioImage Crop(string id, RectangleD r)
        {
            return Crop(id, r.X, r.Y, r.W, r.H);
        }


        /// <summary>
        /// The Init() function initializes a list of filters with their corresponding names and types.
        /// </summary>
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

       /// <summary>
       /// The Copy function creates a new ImageInfo object and copies the values of the properties from
       /// the current object to the new object.
       /// </summary>
       /// <returns>
       /// The method is returning an instance of the ImageInfo class.
       /// </returns>
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
    public class BioImage : IDisposable
    {
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

        private string id;
        public List<Channel> Channels = new List<Channel>();
        public List<Resolution> Resolutions = new List<Resolution>();
        public List<AForge.Bitmap> Buffers = new List<AForge.Bitmap>();
        public List<NetVips.Image> vipPages = new List<NetVips.Image>();
        public int Resolution
        {
            get { return resolution; }
            set { resolution = value; }
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
        public int[] rgbChannels = new int[3];
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
        private bool ispyramidal = false;
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
        private int resolution = 0;
        ImageInfo imageInfo = new ImageInfo();
        /// <summary>
        /// The Copy function creates a deep copy of a BioImage object, including its annotations,
        /// buffers, channels, and other properties.
        /// </summary>
        /// <param name="BioImage">A class representing a bioimage, which contains various properties
        /// and data related to the image.</param>
        /// <param name="rois">A boolean value indicating whether to copy the ROIs (Region of Interest)
        /// from the original BioImage or not.</param>
        /// <returns>
        /// The method is returning a copy of the BioImage object.
        /// </returns>
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
            bi.Resolutions = b.Resolutions;
            bi.statistics = b.statistics;
            return bi;
        }

        /// <summary>
        /// The function "Copy" in C# takes a BioImage object as input and returns a copy of it.
        /// </summary>
        /// <param name="BioImage">The BioImage class is a data structure that represents an image in a
        /// biological context. It may contain information such as pixel data, metadata, and other
        /// properties related to the image.</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
        public static BioImage Copy(BioImage b)
        {
            return Copy(b, true);
        }

        /// <summary>
        /// The function "Copy" creates a copy of a BioImage object, optionally including the regions of
        /// interest (ROIs).
        /// </summary>
        /// <param name="rois">The "rois" parameter is a boolean value that determines whether or not to
        /// include the regions of interest (ROIs) in the copied BioImage. If "rois" is set to true, the
        /// ROIs will be included in the copied BioImage. If "rois" is set</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
        public BioImage Copy(bool rois)
        {
            return BioImage.Copy(this, rois);
        }

        /// <summary>
        /// The function "Copy" returns a new instance of the BioImage class by making a deep copy of
        /// the current instance.
        /// </summary>
        /// <returns>
        /// The method is returning a copy of the BioImage object.
        /// </returns>
        public BioImage Copy()
        {
            return BioImage.Copy(this, true);
        }

        /// <summary>
        /// The function CopyInfo creates a new BioImage object and copies the properties and data from
        /// the input BioImage object, optionally including annotations and channels.
        /// </summary>
        /// <param name="BioImage">The BioImage class represents an image object that contains various
        /// properties and data related to an image.</param>
        /// <param name="copyAnnotations">A boolean value indicating whether to copy the annotations
        /// from the original BioImage to the new BioImage.</param>
        /// <param name="copyChannels">A boolean value indicating whether to copy the channels of the
        /// BioImage object.</param>
        /// <returns>
        /// The method is returning a copy of the BioImage object with the specified properties and data
        /// copied from the original BioImage object.
        /// </returns>
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
            bi.Resolutions = b.Resolutions;
            bi.Coordinate = b.Coordinate;
            bi.file = b.file;
            bi.Filename = b.Filename;
            bi.ID = Images.GetImageName(b.file);
            bi.statistics = b.statistics;
            return bi;
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
            get { return Resolutions[Resolution].PhysicalSizeX; }
        }
        public double PhysicalSizeY
        {
            get { return Resolutions[Resolution].PhysicalSizeY; }
        }
        public double PhysicalSizeZ
        {
            get { return Resolutions[Resolution].PhysicalSizeZ; }
        }
        public double StageSizeX
        {
            get
            {
                return Resolutions[Resolution].StageSizeX;
            }
            set { imageInfo.StageSizeX = value; }
        }
        public double StageSizeY
        {
            get { return Resolutions[Resolution].StageSizeY; }
            set { imageInfo.StageSizeY = value; }
        }
        public double StageSizeZ
        {
            get { return Resolutions[Resolution].StageSizeZ; }
            set { imageInfo.StageSizeZ = value; }
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
        public Channel RChannel
        {
            get
            {
                if (Channels[0].range.Length == 1)
                    return Channels[rgbChannels[0]];
                else
                    return Channels[0];
            }
        }
        /* The above code is defining a property called "GChannel" in C#. This property returns a
        Channel object. */
        public Channel GChannel
        {
            get
            {
                if (Channels[0].range.Length == 1)
                    return Channels[rgbChannels[1]];
                else
                    return Channels[0];
            }
        }
        /* The above code is defining a property called "BChannel" in C#. This property returns a
        Channel object. */
        public Channel BChannel
        {
            get
            {
                if (Channels[0].range.Length == 1)
                    return Channels[rgbChannels[2]];
                else
                    return Channels[0];
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

            /// <summary>
            /// The function "FromImage" takes a BioImage object and returns an ImageJDesc object with
            /// various properties set based on the properties of the BioImage object.
            /// </summary>
            /// <param name="BioImage">BioImage is a class that represents an image in a biological
            /// context. It contains information about the image such as the number of channels, slices,
            /// frames, and the physical properties of the image (e.g., frame interval, spacing). It
            /// also contains information about the intensity range of each channel.</param>
            /// <returns>
            /// The method is returning an instance of the ImageJDesc class.
            /// </returns>
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

            /// <summary>
            /// The GetString function returns a string containing the values of various variables.
            /// </summary>
            /// <returns>
            /// The method is returning a string that contains the values of various variables
            /// concatenated together.
            /// </returns>
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

            /// <summary>
            /// The function `SetString` takes a string input and parses it to set various properties
            /// based on the key-value pairs in the string.
            /// </summary>
            /// <param name="desc">The `desc` parameter is a string that contains multiple lines of
            /// text. Each line represents a key-value pair, where the key and value are separated by an
            /// equals sign (=). The method `SetString` splits the `desc` string into individual lines
            /// and then processes each line to set the corresponding</param>
            /// <returns>
            /// If the value of `i` is greater than or equal to `maxlen`, the method will return and
            /// exit without returning any value.
            /// </returns>
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
                return Buffers[Coords[Coordinate.Z, Coordinate.C, Coordinate.T]];
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
                return ispyramidal;
            }
            set
            {
                ispyramidal = value;
            }
        }
        public string file;
        static int progress = 0;
        public static int progressValue
        {
            get
            {
                return progress;
            }
            set
            {
                progress = value;
            }
        }
        public static string status;
        public static string progFile;
        /* The above code is defining a public static property called "Initialized" in C#. The property
        has a getter method that returns the value of a private static boolean variable called
        "initialized". */
        public static bool Initialized
        {
            get
            {
                return initialized;
            }
        }

        /// <summary>
        /// The function converts an image to 8-bit format, either by converting from 48-bit or 24-bit
        /// RGB format, or by converting from 16-bit grayscale format.
        /// </summary>
        /// <returns>
        /// The method does not return anything. It is a void method.
        /// </returns>
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
                    GC.Collect();
                    Statistics.CalcStatistics(br);
                    Statistics.CalcStatistics(bg);
                    Statistics.CalcStatistics(bb);
                    bfs.Add(br);
                    bfs.Add(bg);
                    bfs.Add(bb);
                    index += 3;
                }
                Buffers = bfs;
                UpdateCoords(SizeZ, 3, SizeT);
            }
            else if (px == PixelFormat.Format24bppRgb)
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
                    GC.Collect();
                    Statistics.CalcStatistics(br);
                    Statistics.CalcStatistics(bg);
                    Statistics.CalcStatistics(bb);
                    bfs.Add(br);
                    bfs.Add(bg);
                    bfs.Add(bb);
                    index += 3;
                }
                Buffers = bfs;
                UpdateCoords(SizeZ, 3, SizeT);
            }
            else
            {
                for (int i = 0; i < Buffers.Count; i++)
                {
                    Bitmap b = AForge.Imaging.Image.Convert16bppTo8bpp(Buffers[i]);
                    Buffers[i] = b;
                    Statistics.CalcStatistics(Buffers[i]);
                }
                for (int c = 0; c < Channels.Count; c++)
                {
                    Channels[c].BitsPerPixel = 8;
                    for (int i = 0; i < Channels[c].range.Length; i++)
                    {
                        Channels[c].range[i].Min = (int)(((float)Channels[c].range[i].Min / (float)ushort.MaxValue) * byte.MaxValue);
                        Channels[c].range[i].Max = (int)(((float)Channels[c].range[i].Max / (float)ushort.MaxValue) * byte.MaxValue);
                    }
                }

            }
            //We wait for threshold image statistics calculation
            do
            {
                Thread.Sleep(100);
            } while (Buffers[Buffers.Count - 1].Stats == null);
            Statistics.ClearCalcBuffer();
            AutoThreshold(this, false);
            bitsPerPixel = 8;
        }

        /// <summary>
        /// The function converts an image to 16-bit format and performs thresholding on the image.
        /// </summary>
        /// <returns>
        /// The method does not have a return type, so it does not return anything.
        /// </returns>
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
                    Buffers[i].Image = AForge.Imaging.Image.Convert8bppTo16bpp(Buffers[i]);
                    Statistics.CalcStatistics(Buffers[i]);
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
            }
            else if (Buffers[0].PixelFormat == PixelFormat.Format24bppRgb)
            {
                List<Bitmap> bfs = new List<Bitmap>();
                int index = 0;
                for (int i = 0; i < Buffers.Count; i++)
                {
                    Bitmap[] bs = Bitmap.RGB24To8(Buffers[i]);
                    Bitmap br = new Bitmap(ID, bs[2].Image, new ZCT(Buffers[i].Coordinate.Z, 0, Buffers[i].Coordinate.T), index, Buffers[i].Plane);
                    Bitmap bg = new Bitmap(ID, bs[1].Image, new ZCT(Buffers[i].Coordinate.Z, 1, Buffers[i].Coordinate.T), index + 1, Buffers[i].Plane);
                    Bitmap bb = new Bitmap(ID, bs[0].Image, new ZCT(Buffers[i].Coordinate.Z, 2, Buffers[i].Coordinate.T), index + 2, Buffers[i].Plane);
                    for (int b = 0; b < 3; b++)
                    {
                        bs[b].Dispose();
                        bs[b] = null;
                    }
                    bs = null;
                    GC.Collect();
                    br.To16Bit();
                    bg.To16Bit();
                    bb.To16Bit();
                    Statistics.CalcStatistics(br);
                    Statistics.CalcStatistics(bg);
                    Statistics.CalcStatistics(bb);
                    bfs.Add(br);
                    bfs.Add(bg);
                    bfs.Add(bb);
                    index += 3;
                }
                Buffers = bfs;
                UpdateCoords(SizeZ, 3, SizeT);
                for (int c = 0; c < Channels.Count; c++)
                {
                    for (int i = 0; i < Channels[c].range.Length; i++)
                    {
                        Channels[c].range[i].Min = (int)(((float)Channels[c].range[i].Min / (float)byte.MaxValue) * ushort.MaxValue);
                        Channels[c].range[i].Max = (int)(((float)Channels[c].range[i].Max / (float)byte.MaxValue) * ushort.MaxValue);
                    }
                    Channels[c].BitsPerPixel = 16;
                }
            }
            //We wait for threshold image statistics calculation
            do
            {
                Thread.Sleep(100);
            } while (Buffers[Buffers.Count - 1].Stats == null);
            Statistics.ClearCalcBuffer();
            AutoThreshold(this, false);
            StackThreshold(true);
        }

        /// <summary>
        /// The function converts the pixel format of the image buffers to 24-bit RGB format.
        /// </summary>
        /// <returns>
        /// If the PixelFormat of the first buffer in the Buffers list is already Format24bppRgb, then
        /// the method returns without performing any further actions.
        /// </returns>
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
                    Buffers[i].Image = AForge.Imaging.Image.Convert16bppTo8bpp(Buffers[i]);
                }
            }
            else
            if (Buffers[0].PixelFormat == PixelFormat.Format16bppGrayScale || Buffers[0].PixelFormat == PixelFormat.Format8bppIndexed)
            {
                if (Buffers[0].PixelFormat == PixelFormat.Format16bppGrayScale)
                {
                    for (int i = 0; i < Buffers.Count; i++)
                    {
                        Buffers[i].Image = AForge.Imaging.Image.Convert16bppTo8bpp(Buffers[i]);
                    }
                    for (int c = 0; c < Channels.Count; c++)
                    {
                        for (int i = 0; i < Channels[c].range.Length; i++)
                        {
                            Channels[c].range[i].Min = (int)(((float)Channels[c].range[i].Min / (float)ushort.MaxValue) * byte.MaxValue);
                            Channels[c].range[i].Max = (int)(((float)Channels[c].range[i].Max / (float)ushort.MaxValue) * byte.MaxValue);
                        }
                        Channels[c].BitsPerPixel = 8;
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
                        bs[2] = new Bitmap(ID, SizeX, SizeY, Buffers[i].PixelFormat, Buffers[i].Bytes, new ZCT(Buffers[i].Coordinate.Z, 0, Buffers[i].Coordinate.T), i, Buffers[i].Plane);
                        bs[1] = new Bitmap(ID, SizeX, SizeY, Buffers[i + 1].PixelFormat, Buffers[i + 1].Bytes, new ZCT(Buffers[i + 1].Coordinate.Z, 0, Buffers[i + 1].Coordinate.T), i + 1, Buffers[i + 1].Plane);
                        if (Channels.Count > 2)
                            bs[0] = new Bitmap(ID, SizeX, SizeY, Buffers[i + 2].PixelFormat, Buffers[i + 2].Bytes, new ZCT(Buffers[i + 2].Coordinate.Z, 0, Buffers[i + 2].Coordinate.T), i + 2, Buffers[i + 2].Plane);
                        Bitmap bbs = Bitmap.RGB8To24(bs);
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
            }

            AutoThreshold(this, true);
            StackThreshold(false);
        }

        /// <summary>
        /// The function converts the pixel format of the image buffers to 32-bit ARGB format and
        /// applies an auto threshold.
        /// </summary>
        /// <returns>
        /// If the pixel format of the first buffer is already Format32bppArgb, then nothing is returned
        /// and the method exits.
        /// </returns>
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

        /// <summary>
        /// The function converts the image to a 48-bit format and performs various calculations and
        /// operations on the image.
        /// </summary>
        /// <returns>
        /// The method does not have a return type, so nothing is being returned.
        /// </returns>
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
                        Statistics.CalcStatistics(bbs);
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
                            bs[2] = new Bitmap(ID, SizeX, SizeY, Buffers[i + 2].PixelFormat, Buffers[i + 2].Bytes, new ZCT(Buffers[i + 2].Coordinate.Z, 0, Buffers[i + 2].Coordinate.T), i + 2, Buffers[i + 2].Plane);
                        Bitmap bbs = Bitmap.RGB16To48(bs);
                        for (int b = 0; b < 3; b++)
                        {
                            if (bs[b] != null)
                                bs[b].Dispose();
                            bs[b] = null;
                        }
                        Statistics.CalcStatistics(bbs);
                        bfs.Add(bbs);
                    }
                GC.Collect();
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
            if (Buffers[0].PixelFormat == PixelFormat.Format24bppRgb)
            {
                for (int i = 0; i < Buffers.Count; i++)
                {
                    Buffers[i].Image = AForge.Imaging.Image.Convert8bppTo16bpp(Buffers[i]);
                    Statistics.CalcStatistics(Buffers[i]);
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
                    Statistics.CalcStatistics(inf);
                    for (int b = 0; b < 3; b++)
                    {
                        bf[b].Dispose();
                    }
                    index++;
                }
                Buffers = buffers;
                UpdateCoords(SizeZ, 1, SizeT);
            }
            //We wait for threshold image statistics calculation
            do
            {
                Thread.Sleep(50);
            } while (Buffers[Buffers.Count - 1].Stats == null);
            Statistics.ClearCalcBuffer();
            bitsPerPixel = 16;
            AutoThreshold(this, false);
            StackThreshold(true);
        }

        /// <summary>
        /// The RotateFlip function rotates and flips the images in the Buffers list and updates the
        /// Volume property.
        /// </summary>
        /// <param name="rot">The "rot" parameter is of type AForge.RotateFlipType, which is an
        /// enumeration that specifies the type of rotation or flip operation to be performed on the
        /// image.</param>
        public void RotateFlip(AForge.RotateFlipType rot)
        {
            for (int i = 0; i < Buffers.Count; i++)
            {
                Buffers[i].RotateFlip(rot);
            }
            Volume = new VolumeD(new Point3D(StageSizeX, StageSizeY, StageSizeZ), new Point3D(PhysicalSizeX * SizeX, PhysicalSizeY * SizeY, PhysicalSizeZ * SizeZ));
        }

        /// <summary>
        /// The Bake function takes in minimum and maximum values for red, green, and blue color
        /// channels and calls another Bake function with IntRange parameters.
        /// </summary>
        /// <param name="rmin">The minimum value for the red color component.</param>
        /// <param name="rmax">The maximum value for the red component of the color range.</param>
        /// <param name="gmin">The parameter "gmin" represents the minimum value for the green component
        /// in the RGB color model.</param>
        /// <param name="gmax">The parameter "gmax" represents the maximum value for the green component
        /// of a color.</param>
        /// <param name="bmin">The parameter "bmin" represents the minimum value for the blue component
        /// in the color range.</param>
        /// <param name="bmax">The parameter "bmax" represents the maximum value for the blue component
        /// in the RGB color model.</param>
        public void Bake(int rmin, int rmax, int gmin, int gmax, int bmin, int bmax)
        {
            Bake(new IntRange(rmin, rmax), new IntRange(gmin, gmax), new IntRange(bmin, bmax));
        }

        /// <summary>
        /// The function "Bake" creates a new BioImage object, copies information from the current
        /// object, applies filters to the image buffers, calculates statistics for the filtered images,
        /// sets the coordinate values for the new BioImage object, adds the filtered images to the new
        /// BioImage object, sets the range values for each channel in the new BioImage object, waits
        /// for threshold image statistics calculation, applies auto thresholding to the new BioImage
        /// object, clears the calculation buffer for statistics, and adds the new BioImage object to
        /// the Images collection.
        /// </summary>
        /// <param name="IntRange">The IntRange class represents a range of integer values. It has two
        /// properties: Min and Max, which define the minimum and maximum values of the range.</param>
        /// <param name="IntRange">The IntRange class represents a range of integer values. It has two
        /// properties: Min and Max, which define the minimum and maximum values of the range.</param>
        /// <param name="IntRange">The IntRange class represents a range of integer values. It has two
        /// properties: Min and Max, which define the minimum and maximum values of the range.</param>
        public void Bake(IntRange rf, IntRange gf, IntRange bf)
        {
            BioImage bm = new BioImage(Images.GetImageName(ID));
            bm = CopyInfo(this, true, true);
            for (int i = 0; i < Buffers.Count; i++)
            {
                ZCT co = Buffers[i].Coordinate;
                UnmanagedImage b = GetFiltered(i, rf, gf, bf);
                Bitmap inf = new Bitmap(bm.ID, b, co, i);
                Statistics.CalcStatistics(inf);
                bm.Coords[co.Z, co.C, co.T] = i;
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
            //We wait for threshold image statistics calculation
            do
            {
                Thread.Sleep(50);
            } while (Buffers[Buffers.Count - 1].Stats == null);
            AutoThreshold(bm, false);
            Statistics.ClearCalcBuffer();
            Images.AddImage(bm, true);
        }

        /// <summary>
        /// The function "UpdateCoords" assigns coordinates to each element in a three-dimensional array
        /// based on the size of the array and the number of elements in a list.
        /// </summary>
        public void UpdateCoords()
        {
            int z = 0;
            int c = 0;
            int t = 0;
            Coords = new int[SizeZ, SizeC, SizeT];
            for (int im = 0; im < Buffers.Count; im++)
            {
                ZCT co = new ZCT(z, c, t);
                Coords[co.Z, co.C, co.T] = im;
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

        /// <summary>
        /// The function "UpdateCoords" updates the coordinates of a multi-dimensional array based on
        /// the given sizes and assigns each element of the array a corresponding index from a list of
        /// buffers.
        /// </summary>
        /// <param name="sz">The parameter `sz` represents the size of the Z dimension in the `Coords`
        /// array. It determines the number of elements in the Z dimension.</param>
        /// <param name="sc">The parameter "sc" represents the size of the second dimension of the
        /// Coords array. It determines the number of columns in the three-dimensional array.</param>
        /// <param name="st">The parameter "st" represents the size of the third dimension in the Coords
        /// array. It determines the number of elements in the third dimension of the array.</param>
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
                Coords[co.Z, co.C, co.T] = im;
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

        /// <summary>
        /// The function "UpdateCoords" updates the coordinates of a multi-dimensional array based on
        /// the specified order.
        /// </summary>
        /// <param name="sz">The parameter `sz` represents the size of the Z dimension in the Coords
        /// array.</param>
        /// <param name="sc">The parameter "sc" represents the size of the second dimension of the
        /// Coords array. It determines the number of columns in the Coords array.</param>
        /// <param name="st">The parameter "st" represents the size of the T dimension in the Coords
        /// array. It determines the number of elements in the T dimension.</param>
        /// <param name="order">The "order" parameter determines the order in which the coordinates are
        /// updated. It can have two possible values: "XYCZT" or "XYZCT".</param>
        public void UpdateCoords(int sz, int sc, int st, string order)
        {
            int z = 0;
            int c = 0;
            int t = 0;
            sizeZ = sz;
            sizeC = sc;
            sizeT = st;
            Coords = new int[sz, sc, st];
            if (order == "XYCZT")
            {
                for (int im = 0; im < Buffers.Count; im++)
                {
                    ZCT co = new ZCT(z, c, t);
                    Coords[co.Z, co.C, co.T] = im;
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
            else if (order == "XYZCT")
            {
                for (int im = 0; im < Buffers.Count; im++)
                {
                    ZCT co = new ZCT(z, c, t);
                    Coords[co.Z, co.C, co.T] = im;
                    Buffers[im].Coordinate = co;
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
        }

        /// <summary>
        /// The function converts a value from a physical size to an image size in the X dimension.
        /// </summary>
        /// <param name="d">The parameter "d" represents a value that needs to be converted to image
        /// size in the X direction.</param>
        /// <returns>
        /// The method is returning a double value.
        /// </returns>
        public double ToImageSizeX(double d)
        {
            return d / PhysicalSizeX;
        }

        /// <summary>
        /// The function converts a value from a physical size to an image size in the Y-axis.
        /// </summary>
        /// <param name="d">The parameter "d" represents the size of an object in the physical
        /// world.</param>
        /// <returns>
        /// The method is returning the value of `d` divided by the value of `PhysicalSizeY`.
        /// </returns>
        public double ToImageSizeY(double d)
        {
            return d / PhysicalSizeY;
        }

        /// <summary>
        /// The function converts a given x-coordinate from stage space to image space.
        /// </summary>
        /// <param name="x">The parameter "x" represents a coordinate value in some coordinate
        /// space.</param>
        /// <returns>
        /// The method is returning a double value.
        /// </returns>
        public double ToImageSpaceX(double x)
        {
            if (isPyramidal)
                return x;
            return (float)((x - StageSizeX) / PhysicalSizeX);
        }

        /// <summary>
        /// The function converts a given y-coordinate from stage space to image space.
        /// </summary>
        /// <param name="y">The parameter "y" represents the coordinate value in the image
        /// space.</param>
        /// <returns>
        /// The method is returning a double value.
        /// </returns>
        public double ToImageSpaceY(double y)
        {
            if (isPyramidal)
                return y;
            return (float)((y - StageSizeY) / PhysicalSizeY);
        }

        /// <summary>
        /// The function converts a point from stage space to image space using given stage and physical
        /// sizes.
        /// </summary>
        /// <param name="PointD">The PointD class represents a point in a two-dimensional space. It has
        /// two properties, X and Y, which represent the coordinates of the point.</param>
        /// <returns>
        /// The method is returning a PointD object.
        /// </returns>
        public PointD ToImageSpace(PointD p)
        {
            PointD pp = new PointD();
            pp.X = (float)((p.X - StageSizeX) / PhysicalSizeX);
            pp.Y = (float)((p.Y - StageSizeY) / PhysicalSizeY);
            return pp;
        }

        /// <summary>
        /// The function takes a list of PointD objects and converts their coordinates from stage space
        /// to image space.
        /// </summary>
        /// <param name="p">A list of PointD objects representing points in some coordinate
        /// system.</param>
        /// <returns>
        /// The method is returning an array of PointD objects.
        /// </returns>
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
        public static PointD[] ToImageSpace(List<PointD> p, int stageSizeX,int stageSizeY, int physicalSizeX,int physicalSizeY)
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

        /// <summary>
        /// The function converts an array of points from stage space to image space.
        /// </summary>
        /// <param name="p">An array of PointF objects representing points in some coordinate
        /// space.</param>
        /// <returns>
        /// The method is returning an array of PointF objects.
        /// </returns>
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

        /// <summary>
        /// The function converts a rectangle from a coordinate space with physical dimensions to a
        /// coordinate space with image dimensions.
        /// </summary>
        /// <param name="RectangleD">A rectangle with double precision coordinates (X, Y, Width,
        /// Height).</param>
        /// <returns>
        /// The method is returning a RectangleF object.
        /// </returns>
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

        /// <summary>
        /// The function converts a point from a coordinate space to a stage space.
        /// </summary>
        /// <param name="PointD">A structure representing a point in 2D space, with X and Y
        /// coordinates.</param>
        /// <returns>
        /// The method is returning a PointD object, which represents a point in stage space.
        /// </returns>
        public PointD ToStageSpace(PointD p)
        {
            PointD pp = new PointD();
            if (isPyramidal)
            {
                pp.X = ((p.X * Resolutions[resolution].PhysicalSizeX) + Volume.Location.X);
                pp.Y = ((p.Y * Resolutions[resolution].PhysicalSizeY) + Volume.Location.Y);
                return pp;
            }
            else
            {
                pp.X = ((p.X * PhysicalSizeX) + Volume.Location.X);
                pp.Y = ((p.Y * PhysicalSizeY) + Volume.Location.Y);
                return pp;
            }
        }

        /// <summary>
        /// The function converts a point from a coordinate system with a given resolution to a stage
        /// space coordinate system.
        /// </summary>
        /// <param name="PointD">A structure representing a point in 2D space, with X and Y
        /// coordinates.</param>
        /// <param name="resolution">The resolution parameter is an integer that represents the
        /// resolution level. It is used to access the corresponding PhysicalSizeX and PhysicalSizeY
        /// values from the Resolutions array.</param>
        /// <returns>
        /// The method is returning a PointD object, which represents a point in stage space.
        /// </returns>
        public PointD ToStageSpace(PointD p, int resolution)
        {
            PointD pp = new PointD();
            pp.X = ((p.X * Resolutions[resolution].PhysicalSizeX) + Volume.Location.X);
            pp.Y = ((p.Y * Resolutions[resolution].PhysicalSizeY) + Volume.Location.Y);
            return pp;
        }

        /// <summary>
        /// The function converts a point from a coordinate system with physical sizes and volumes to a
        /// stage space coordinate system.
        /// </summary>
        /// <param name="PointD">A structure representing a point in 2D space, with X and Y
        /// coordinates.</param>
        /// <param name="physicalSizeX">The physical size of the X-axis in the stage space.</param>
        /// <param name="physicalSizeY">The physical size of the Y-axis in the stage space.</param>
        /// <param name="volumeX">The volumeX parameter represents the offset or displacement in the
        /// X-axis of the stage space. It is used to adjust the position of the point in the stage
        /// space.</param>
        /// <param name="volumeY">The parameter "volumeY" represents the offset or displacement in the
        /// Y-axis direction in the stage space. It is used to adjust the position of the point in the
        /// Y-axis direction after scaling and translating it from the physical space to the stage
        /// space.</param>
        /// <returns>
        /// The method is returning a PointD object.
        /// </returns>
        public static PointD ToStageSpace(PointD p, double physicalSizeX, double physicalSizeY, double volumeX, double volumeY)
        {
            PointD pp = new PointD();
            pp.X = ((p.X * physicalSizeX) + volumeX);
            pp.Y = ((p.Y * physicalSizeY) + volumeY);
            return pp;
        }

        /// <summary>
        /// The function converts a rectangle from a normalized coordinate space to a stage coordinate
        /// space.
        /// </summary>
        /// <param name="RectangleD">The RectangleD is a custom data structure that represents a
        /// rectangle in 2D space. It has four properties: X (the x-coordinate of the top-left corner),
        /// Y (the y-coordinate of the top-left corner), W (the width of the rectangle), and H (the
        /// height of the</param>
        /// <returns>
        /// The method is returning a RectangleD object.
        /// </returns>
        public RectangleD ToStageSpace(RectangleD p)
        {
            RectangleD r = new RectangleD();
            r.X = ((p.X * PhysicalSizeX) + Volume.Location.X);
            r.Y = ((p.Y * PhysicalSizeY) + Volume.Location.Y);
            r.W = (p.W * PhysicalSizeX);
            r.H = (p.H * PhysicalSizeY);
            return r;
        }

        /// <summary>
        /// The function converts a rectangle from a coordinate space with physical dimensions to a
        /// coordinate space with volume dimensions.
        /// </summary>
        /// <param name="RectangleD">A custom data structure representing a rectangle with double
        /// precision coordinates and dimensions. It has properties X, Y, W, and H representing the
        /// x-coordinate, y-coordinate, width, and height of the rectangle, respectively.</param>
        /// <param name="physicalSizeX">The physical size of the X-axis in the stage space.</param>
        /// <param name="physicalSizeY">The physical size of the Y-axis in the stage space.</param>
        /// <param name="volumeX">The volumeX parameter represents the offset or displacement in the
        /// x-axis of the rectangle in stage space. It is used to adjust the position of the rectangle
        /// in the stage space.</param>
        /// <param name="volumeY">The parameter "volumeY" represents the offset or displacement in the
        /// Y-axis direction in the stage space. It is used to adjust the position of the rectangle in
        /// the stage space.</param>
        /// <returns>
        /// The method is returning a RectangleD object.
        /// </returns>
        public static RectangleD ToStageSpace(RectangleD p, double physicalSizeX, double physicalSizeY, double volumeX, double volumeY)
        {
            RectangleD r = new RectangleD();
            r.X = ((p.X * physicalSizeX) + volumeX);
            r.Y = ((p.Y * physicalSizeY) + volumeY);
            r.W = (p.W * physicalSizeX);
            r.H = (p.H * physicalSizeY);
            return r;
        }

        /// <summary>
        /// The function takes an array of PointD objects and converts their coordinates to stage space
        /// by scaling them and adding an offset.
        /// </summary>
        /// <param name="p">An array of PointD objects representing points in some coordinate
        /// system.</param>
        /// <returns>
        /// The method is returning an array of PointD objects.
        /// </returns>
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

        /// <summary>
        /// The function converts an array of points from a normalized coordinate system to a stage
        /// space coordinate system.
        /// </summary>
        /// <param name="p">An array of PointD objects representing points in some coordinate
        /// system.</param>
        /// <param name="physicalSizeX">The physical size of the X-axis in the stage space.</param>
        /// <param name="physicalSizeY">The physical size of the Y-axis in the stage space.</param>
        /// <param name="volumeX">The parameter volumeX represents the offset or translation in the
        /// x-axis of the stage space. It determines how much the x-coordinate of each point in the
        /// input array should be shifted in the stage space.</param>
        /// <param name="volumeY">The parameter "volumeY" represents the offset or displacement in the
        /// Y-axis direction in the stage space. It determines the position of the points in the Y-axis
        /// direction after converting them to stage space.</param>
        /// <returns>
        /// The method is returning an array of PointD objects.
        /// </returns>
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
        /* The above code is defining a constructor for the BioImage class in C#. The constructor takes
        a string parameter called "file". */
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

        /// <summary>
        /// The Substack function takes a BioImage object and extracts a substack of images based on
        /// specified parameters, including series, z-slices, channels, and timepoints.
        /// </summary>
        /// <param name="BioImage">The BioImage object that represents the original image.</param>
        /// <param name="ser">The parameter "ser" stands for series, which represents the series number
        /// of the image stack.</param>
        /// <param name="zs">The parameter "zs" represents the starting z-index of the substack. It
        /// determines the first z-slice to be included in the substack.</param>
        /// <param name="ze">ze is the ending z-index of the substack. It represents the last z-slice
        /// that will be included in the substack.</param>
        /// <param name="cs">The parameter "cs" represents the starting channel index. It indicates the
        /// index of the first channel to be included in the substack.</param>
        /// <param name="ce">The parameter "ce" represents the ending channel index. It is used to
        /// specify the last channel to include in the substack operation.</param>
        /// <param name="ts">The parameter "ts" stands for "start time point". It represents the
        /// starting time point of the substack in the original BioImage.</param>
        /// <param name="te">The parameter "te" represents the end time point of the substack. It is
        /// used to specify the last time point to include in the substack.</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
        public static BioImage Substack(BioImage orig, int ser, int zs, int ze, int cs, int ce, int ts, int te)
        {
            BioImage b = CopyInfo(orig, false, false);
            b.ID = Images.GetImageName(orig.ID);
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
                        int ind = orig.Coords[zs + zi, cs + ci, ts + ti];
                        Bitmap bf = new Bitmap(Images.GetImageName(orig.id), orig.SizeX, orig.SizeY, orig.Buffers[0].PixelFormat, orig.Buffers[ind].Bytes, new ZCT(zi, ci, ti), i);
                        Statistics.CalcStatistics(bf);
                        b.Buffers.Add(bf);
                        b.Coords[zi, ci, ti] = i;
                        i++;
                    }
                }
            }
            for (int ci = cs; ci < ce; ci++)
            {
                b.Channels.Add(orig.Channels[ci]);
            }
            //We wait for threshold image statistics calculation
            do
            {
                Thread.Sleep(100);
            } while (b.Buffers[b.Buffers.Count - 1].Stats == null);
            Statistics.ClearCalcBuffer();
            AutoThreshold(b, false);
            if (b.bitsPerPixel > 8)
                b.StackThreshold(true);
            else
                b.StackThreshold(false);
            Images.AddImage(b, true);
            //Recorder.AddLine("Bio.BioImage.Substack(" + '"' + orig.Filename + '"' + "," + ser + "," + zs + "," + ze + "," + cs + "," + ce + "," + ts + "," + te + ");");
            return b;
        }

        /// <summary>
        /// The MergeChannels function takes two BioImage objects, b2 and b, and merges their channels
        /// into a new BioImage object, res.
        /// </summary>
        /// <param name="BioImage">The BioImage class represents an image in a biological context. It
        /// contains information about the image, such as its ID, series, size (Z, C, T), bits per
        /// pixel, image info, little endian flag, series count, and images per series. It also contains
        /// buffers for storing the</param>
        /// <param name="BioImage">The BioImage class represents an image in a biological context. It
        /// contains information about the image, such as its ID, series, size (Z, C, T), bits per
        /// pixel, image info, little endian flag, series count, and images per series. It also contains
        /// buffers for storing the</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
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

            int i = 0;
            int cc = 0;
            for (int ti = 0; ti < res.SizeT; ti++)
            {
                for (int zi = 0; zi < res.SizeZ; zi++)
                {
                    for (int ci = 0; ci < res.SizeC; ci++)
                    {
                        ZCT co = new ZCT(zi, ci, ti);
                        if (ci < cOrig)
                        {
                            //If this channel is part of the image b1 we add planes from it.
                            Bitmap copy = new Bitmap(b2.id, b2.SizeX, b2.SizeY, b2.Buffers[0].PixelFormat, b2.Buffers[i].Bytes, co, i);
                            if (b2.littleEndian)
                                copy.RotateFlip(AForge.RotateFlipType.Rotate180FlipNone);
                            res.Coords[zi, ci, ti] = i;
                            res.Buffers.Add(b2.Buffers[i]);
                            res.Buffers.Add(copy);
                            //Lets copy the ROI's from the original image.
                            List<ROI> anns = b2.GetAnnotations(zi, ci, ti);
                            if (anns.Count > 0)
                                res.Annotations.AddRange(anns);
                        }
                        else
                        {
                            //This plane is not part of b1 so we add the planes from b2 channels.
                            Bitmap copy = new Bitmap(b.id, b.SizeX, b.SizeY, b.Buffers[0].PixelFormat, b.Buffers[i].Bytes, co, i);
                            if (b2.littleEndian)
                                copy.RotateFlip(AForge.RotateFlipType.Rotate180FlipNone);
                            res.Coords[zi, ci, ti] = i;
                            res.Buffers.Add(b.Buffers[i]);
                            res.Buffers.Add(copy);

                            //Lets copy the ROI's from the original image.
                            List<ROI> anns = b.GetAnnotations(zi, cc, ti);
                            if (anns.Count > 0)
                                res.Annotations.AddRange(anns);
                        }
                        i++;
                    }
                }
            }
            for (int ci = 0; ci < res.SizeC; ci++)
            {
                if (ci < cOrig)
                {
                    res.Channels.Add(b2.Channels[ci].Copy());
                }
                else
                {
                    res.Channels.Add(b.Channels[cc].Copy());
                    res.Channels[cOrig + cc].Index = ci;
                    cc++;
                }
            }
            Images.AddImage(res, true);
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
            //Recorder.AddLine("Bio.BioImage.MergeChannels(" + '"' + b.ID + '"' + "," + '"' + b2.ID + '"' + ");");
            return res;
        }

        /// <summary>
        /// The function "MergeChannels" takes in two image names as input, retrieves the corresponding
        /// images, and merges the channels of the images into a single BioImage object.
        /// </summary>
        /// <param name="bname">The parameter "bname" is a string that represents the name of the first
        /// BioImage that you want to merge channels from.</param>
        /// <param name="b2name">The parameter "b2name" is a string that represents the name of the
        /// second BioImage that you want to merge channels with.</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
        public static BioImage MergeChannels(string bname, string b2name)
        {
            BioImage b = Images.GetImage(bname);
            BioImage b2 = Images.GetImage(b2name);
            return MergeChannels(b, b2);
        }

        /// <summary>
        /// The MergeZ function merges multiple images in the Z dimension and applies thresholding to
        /// the resulting image.
        /// </summary>
        /// <param name="BioImage">The BioImage class represents an image in a biological context. It
        /// contains information about the image such as its size, channels, and time points. It also
        /// contains a list of buffers, which are the actual image data.</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
        public static BioImage MergeZ(BioImage b)
        {
            BioImage bi = BioImage.CopyInfo(b, true, true);
            int ind = 0;
            for (int c = 0; c < b.SizeC; c++)
            {
                for (int t = 0; t < b.sizeT; t++)
                {
                    Merge m = new Merge(b.Buffers[b.Coords[0, c, t]]);
                    Bitmap bm = new Bitmap(b.SizeX, b.SizeY, b.Buffers[0].PixelFormat);
                    for (int i = 1; i < b.sizeZ; i++)
                    {
                        m.OverlayImage = bm;
                        bm = m.Apply(b.Buffers[b.Coords[i, c, t]]);
                    }
                    Bitmap bf = new Bitmap(b.file, bm, new ZCT(0, c, t), ind);
                    bi.Buffers.Add(bf);
                    Statistics.CalcStatistics(bf);
                    ind++;
                }
            }
            Images.AddImage(bi, true);
            bi.UpdateCoords(1, b.SizeC, b.SizeT);
            bi.Coordinate = new ZCT(0, 0, 0);
            //We wait for threshold image statistics calculation
            do
            {
                Thread.Sleep(100);
            } while (bi.Buffers[bi.Buffers.Count - 1].Stats == null);
            Statistics.ClearCalcBuffer();
            AutoThreshold(bi, false);
            if (bi.bitsPerPixel > 8)
                bi.StackThreshold(true);
            else
                bi.StackThreshold(false);
            //Recorder.AddLine("Bio.BioImage.MergeZ(" + '"' + b.ID + '"' + ");");
            return bi;
        }

        /// <summary>
        /// The MergeT function merges multiple images in a BioImage object and applies thresholding to
        /// the resulting image.
        /// </summary>
        /// <param name="BioImage">The BioImage class represents an image with biological data. It
        /// contains information about the image size, channels, slices, and time points, as well as the
        /// pixel data stored in buffers.</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
        public static BioImage MergeT(BioImage b)
        {
            BioImage bi = BioImage.CopyInfo(b, true, true);
            int ind = 0;
            for (int c = 0; c < b.SizeC; c++)
            {
                for (int z = 0; z < b.sizeZ; z++)
                {
                    Merge m = new Merge(b.Buffers[b.Coords[z, c, 0]]);
                    Bitmap bm = new Bitmap(b.SizeX, b.SizeY, b.Buffers[0].PixelFormat);
                    for (int i = 1; i < b.sizeT; i++)
                    {
                        m.OverlayImage = bm;
                        bm = m.Apply(b.Buffers[b.Coords[z, c, i]]);
                    }
                    Bitmap bf = new Bitmap(b.file, bm, new ZCT(z, c, 0), ind);
                    bi.Buffers.Add(bf);
                    Statistics.CalcStatistics(bf);
                    ind++;
                }
            }
            Images.AddImage(bi, true);
            bi.UpdateCoords(1, b.SizeC, b.SizeT);
            bi.Coordinate = new ZCT(0, 0, 0);
            //We wait for threshold image statistics calculation
            do
            {
                Thread.Sleep(100);
            } while (bi.Buffers[bi.Buffers.Count - 1].Stats == null);
            Statistics.ClearCalcBuffer();
            AutoThreshold(bi, false);
            if (bi.bitsPerPixel > 8)
                bi.StackThreshold(true);
            else
                bi.StackThreshold(false);
            //Recorder.AddLine("Bio.BioImage.MergeT(" + '"' + b.ID + '"' + ");");
            return bi;
        }

        /// <summary>
        /// The SplitChannels function splits an image into separate color channels and returns an array
        /// of BioImage objects representing each channel.
        /// </summary>
        /// <returns>
        /// The method is returning an array of BioImage objects.
        /// </returns>
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
                        Statistics.CalcStatistics(bfs[0]);
                        Statistics.CalcStatistics(bfs[1]);
                        Statistics.CalcStatistics(bfs[2]);
                        ri.Coords[Buffers[i].Coordinate.Z, Buffers[i].Coordinate.C, Buffers[i].Coordinate.T] = i;
                        gi.Coords[Buffers[i].Coordinate.Z, Buffers[i].Coordinate.C, Buffers[i].Coordinate.T] = i;
                        bi.Coords[Buffers[i].Coordinate.Z, Buffers[i].Coordinate.C, Buffers[i].Coordinate.T] = i;
                    }
                    else
                    {

                        Bitmap rImage = extractR.Apply(Buffers[i]);
                        Bitmap rbf = new Bitmap(ri.ID, rImage, Buffers[i].Coordinate, ind++);
                        Statistics.CalcStatistics(rbf);
                        ri.Buffers.Add(rbf);
                        ri.Coords[Buffers[i].Coordinate.Z, Buffers[i].Coordinate.C, Buffers[i].Coordinate.T] = i;

                        Bitmap gImage = extractG.Apply(Buffers[i]);
                        Bitmap gbf = new Bitmap(gi.ID, gImage, Buffers[i].Coordinate, ind++);
                        Statistics.CalcStatistics(gbf);
                        gi.Buffers.Add(gbf);
                        gi.Coords[Buffers[i].Coordinate.Z, Buffers[i].Coordinate.C, Buffers[i].Coordinate.T] = i;

                        Bitmap bImage = extractB.Apply(Buffers[i]);
                        //Clipboard.SetImage(bImage);
                        Bitmap bbf = new Bitmap(bi.ID, bImage, Buffers[i].Coordinate, ind++);
                        Statistics.CalcStatistics(bbf);
                        bi.Buffers.Add(bbf);
                        bi.Coords[Buffers[i].Coordinate.Z, Buffers[i].Coordinate.C, Buffers[i].Coordinate.T] = i;

                    }
                }
                //We wait for threshold image statistics calculation
                do
                {
                    Thread.Sleep(100);
                } while (bi.Buffers[bi.Buffers.Count - 1].Stats == null);

                ri.Channels.Add(Channels[0].Copy());
                gi.Channels.Add(Channels[0].Copy());
                bi.Channels.Add(Channels[0].Copy());
                AutoThreshold(ri, false);
                AutoThreshold(gi, false);
                AutoThreshold(bi, false);
                Images.AddImage(ri, true);
                Images.AddImage(gi, true);
                Images.AddImage(bi, true);
                Statistics.ClearCalcBuffer();
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

            Statistics.ClearCalcBuffer();
            //Recorder.AddLine("Bio.BioImage.SplitChannels(" + '"' + Filename + '"' + ");");
            return bms;
        }

        /// <summary>
        /// The function "SplitChannels" takes a BioImage object as input and returns an array of
        /// BioImage objects, each representing a different channel of the original image.
        /// </summary>
        /// <param name="BioImage">The BioImage class represents an image in a biological context,
        /// typically used in bioinformatics or biomedical research. It may contain various properties
        /// and methods related to image processing and analysis.</param>
        /// <returns>
        /// The method is returning an array of BioImage objects.
        /// </returns>
        public static BioImage[] SplitChannels(BioImage bb)
        {
            return bb.SplitChannels();
        }
        /// <summary>
        /// The function "SplitChannels" takes in the name of an image and returns an array of BioImage
        /// objects representing the individual color channels of the image.
        /// </summary>
        /// <param name="name">The name parameter is a string that represents the name of the image
        /// file.</param>
        /// <returns>
        /// The method is returning an array of BioImage objects.
        /// </returns>
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


        /// <summary>
        /// The function returns a Bitmap image based on the given coordinates.
        /// </summary>
        /// <param name="z">The z parameter represents the z-coordinate of the image. It is used to
        /// access the correct z-slice of the image stack.</param>
        /// <param name="c">The parameter "c" likely represents the column index of the coordinate in
        /// the Coords array.</param>
        /// <param name="t">The parameter "t" represents the index of the time dimension. It is used to
        /// specify which time point or frame of the image to retrieve.</param>
        /// <returns>
        /// The method is returning a Bitmap object.
        /// </returns>
        public Bitmap GetImageByCoord(int z, int c, int t)
        {
            return Buffers[Coords[z, c, t]];
        }

        /// <summary>
        /// The function "GetBitmap" returns a Bitmap object based on the provided parameters.
        /// </summary>
        /// <param name="z">The parameter "z" represents the z-coordinate of the desired bitmap. It is
        /// used to access the appropriate index in the Coords array.</param>
        /// <param name="c">The parameter "c" likely represents the index of the channel or color
        /// component in the image.</param>
        /// <param name="t">The parameter "t" represents the index of the buffer in the "Buffers"
        /// array.</param>
        /// <returns>
        /// The method is returning a Bitmap object.
        /// </returns>
        public Bitmap GetBitmap(int z, int c, int t)
        {
            return Buffers[Coords[z, c, t]];
        }

        /// <summary>
        /// The function returns the index of a pixel in a 2D image based on its x and y coordinates.
        /// </summary>
        /// <param name="ix">The parameter "ix" represents the x-coordinate of a pixel in an
        /// image.</param>
        /// <param name="iy">The parameter "iy" represents the y-coordinate of a pixel in an
        /// image.</param>
        /// <returns>
        /// The method is returning an integer value.
        /// </returns>
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
        /// <summary>
        /// The function returns the index of a pixel in an image buffer based on its x and y
        /// coordinates and the index of the color channel.
        /// </summary>
        /// <param name="ix">The parameter "ix" represents the x-coordinate of the pixel in the
        /// image.</param>
        /// <param name="iy">The parameter "iy" represents the y-coordinate of the pixel in the
        /// image.</param>
        /// <param name="index">The `index` parameter represents the color channel index. It is used to
        /// calculate the index of the pixel in the image buffer based on the given x and y
        /// coordinates.</param>
        /// <returns>
        /// The method returns the index of the RGB value in the image buffer based on the given x and y
        /// coordinates and the specified index.
        /// </returns>
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

        /// <summary>
        /// The function `GetValue` returns the value at a given coordinate, taking into account the RGB
        /// channels if applicable.
        /// </summary>
        /// <param name="ZCTXY">ZCTXY is a custom data structure that represents a coordinate in a 3D
        /// space. It has the following properties:</param>
        /// <returns>
        /// The method is returning a ushort value.
        /// </returns>
        public ushort GetValue(ZCTXY coord)
        {
            if (coord.X < 0 || coord.Y < 0 || coord.X > SizeX || coord.Y > SizeY)
            {
                return 0;
            }
            if (isRGB)
            {
                if (coord.C == 0)
                    return GetValueRGB(coord, 0);
                else if (coord.C == 1)
                    return GetValueRGB(coord, 1);
                else if (coord.C == 2)
                    return GetValueRGB(coord, 2);
            }
            else
                return GetValueRGB(coord, 0);
            return 0;
        }

        /// <summary>
        /// The function `GetValueRGB` returns the value of the red, green, or blue component of a pixel
        /// at a given coordinate.
        /// </summary>
        /// <param name="ZCTXY">ZCTXY is a custom data structure that represents the coordinates of a
        /// pixel in a multi-dimensional image. It has the following properties:</param>
        /// <param name="index">The `index` parameter is an integer that represents the color channel
        /// index. It can have the values 0, 1, or 2, where 0 represents the red channel, 1 represents
        /// the green channel, and 2 represents the blue channel.</param>
        /// <returns>
        /// The method is returning a ushort value, which represents the red, green, or blue component
        /// of a pixel at the specified coordinates.
        /// </returns>
        public ushort GetValueRGB(ZCTXY coord, int index)
        {
            int ind = 0;
            if (coord.C >= SizeC)
            {
                coord.C = 0;
            }
            ind = Coords[coord.Z, coord.C, coord.T];
            ColorS c = Buffers[ind].GetPixel(coord.X, coord.Y);
            if (index == 0)
                return c.R;
            else
            if (index == 1)
                return c.G;
            else
            if (index == 2)
                return c.B;
            throw new IndexOutOfRangeException();
        }

        /// <summary>
        /// The function "GetValue" returns a ushort value by calling the "GetValueRGB" function with a
        /// modified coordinate.
        /// </summary>
        /// <param name="ZCT">ZCT is a custom data structure that represents the coordinates of a point
        /// in a 3D space. It has three components: Z (representing the depth or height), C
        /// (representing the channel or color), and T (representing the time).</param>
        /// <param name="x">The parameter "x" represents the x-coordinate of the pixel in the
        /// image.</param>
        /// <param name="y">The parameter "y" represents the y-coordinate of a point in a
        /// two-dimensional space. It is used to specify the vertical position of the point.</param>
        /// <returns>
        /// The method is returning a value of type ushort.
        /// </returns>
        public ushort GetValue(ZCT coord, int x, int y)
        {
            return GetValueRGB(new ZCTXY(coord.Z, coord.C, coord.T, x, y), 0);
        }

        /// <summary>
        /// The function `GetValue` takes in five integer parameters and returns an unsigned short
        /// value.
        /// </summary>
        /// <param name="z">The parameter "z" represents the Z coordinate.</param>
        /// <param name="c">The parameter "c" represents the channel number.</param>
        /// <param name="t">The parameter "t" represents the time value.</param>
        /// <param name="x">The parameter "x" represents the x-coordinate of the value you want to
        /// retrieve.</param>
        /// <param name="y">The parameter "y" represents the y-coordinate of a point in a 2D
        /// space.</param>
        /// <returns>
        /// The method is returning a value of type `ushort`.
        /// </returns>
        public ushort GetValue(int z, int c, int t, int x, int y)
        {
            return GetValue(new ZCTXY(z, c, t, x, y));
        }

        /// <summary>
        /// The function `GetValueRGB` returns the RGB value at a given coordinate and index if the
        /// image is in RGB format, otherwise it returns the value at the given coordinate.
        /// </summary>
        /// <param name="ZCT">ZCT is a coordinate system that represents the position of a pixel in a 3D
        /// image. It consists of three components: Z (depth), C (channel), and T (time).</param>
        /// <param name="x">The x parameter represents the x-coordinate of the pixel in the
        /// image.</param>
        /// <param name="y">The parameter "y" represents the y-coordinate of the pixel in the
        /// image.</param>
        /// <param name="RGBindex">RGBindex is an integer representing the index of the RGB channel. It
        /// is used to specify which channel's value to retrieve from the given coordinates (x, y) and
        /// ZCT (Z, C, T) values.</param>
        /// <returns>
        /// The method is returning a value of type ushort.
        /// </returns>
        public ushort GetValueRGB(ZCT coord, int x, int y, int RGBindex)
        {
            ZCTXY c = new ZCTXY(coord.Z, coord.C, coord.T, x, y);
            if (isRGB)
            {
                return GetValueRGB(c, RGBindex);
            }
            else
                return GetValue(coord, x, y);
        }
        /// <summary>
        /// The function `GetValueRGB` returns the RGB value at a specific position in a 3D image
        /// volume.
        /// </summary>
        /// <param name="z">The z parameter represents the z-coordinate of the pixel in a 3D image
        /// volume. It indicates the position of the pixel along the z-axis.</param>
        /// <param name="c">The parameter "c" represents the channel index. In an image or data set with
        /// multiple channels, each channel contains different information. The "c" parameter allows you
        /// to specify which channel you want to retrieve the value from.</param>
        /// <param name="t">The parameter "t" represents the time value. It is used to specify a
        /// particular time frame or moment in a dataset or image sequence.</param>
        /// <param name="x">The x parameter represents the x-coordinate of the pixel in the
        /// image.</param>
        /// <param name="y">The parameter "y" represents the y-coordinate of the pixel in the
        /// image.</param>
        /// <param name="RGBindex">The RGBindex parameter represents the index of the color channel in
        /// the RGB color space. In a typical RGB color space, the index 0 represents the red channel,
        /// index 1 represents the green channel, and index 2 represents the blue channel.</param>
        /// <returns>
        /// The method is returning a value of type ushort.
        /// </returns>
        public ushort GetValueRGB(int z, int c, int t, int x, int y, int RGBindex)
        {
            return GetValueRGB(new ZCT(z, c, t), x, y, RGBindex);
        }

       /// <summary>
       /// The function sets a value at a specific coordinate in a multi-dimensional array.
       /// </summary>
       /// <param name="ZCTXY">ZCTXY is a custom data structure that represents a coordinate in a
       /// three-dimensional space. It has the following properties:</param>
       /// <param name="value">The value parameter is of type ushort, which represents an unsigned short
       /// integer. It is the value that you want to set at the specified coordinates.</param>
        public void SetValue(ZCTXY coord, ushort value)
        {
            int i = Coords[coord.Z, coord.C, coord.T];
            Buffers[i].SetValue(coord.X, coord.Y, value);
        }
        /// <summary>
        /// The function SetValue sets a value at a specific position in a buffer.
        /// </summary>
        /// <param name="x">The x parameter represents the x-coordinate of the value to be set in the
        /// buffer.</param>
        /// <param name="y">The parameter "y" represents the y-coordinate of the position where the
        /// value should be set.</param>
        /// <param name="ind">The parameter "ind" is an index that represents the buffer in which the
        /// value will be set.</param>
        /// <param name="value">The value parameter is of type ushort, which stands for unsigned short.
        /// It is used to specify the value that will be set at the specified position (x, y) in the
        /// Buffers array.</param>
        public void SetValue(int x, int y, int ind, ushort value)
        {
            Buffers[ind].SetValue(x, y, value);
        }

        /// <summary>
        /// The function SetValue sets a value at a specific coordinate in a three-dimensional array.
        /// </summary>
        /// <param name="x">An integer representing the x-coordinate of the location where the value
        /// will be set.</param>
        /// <param name="y">The parameter "y" represents the y-coordinate of the position where the
        /// value will be set.</param>
        /// <param name="ZCT">ZCT is an enum that represents three dimensions: Z, C, and T. These
        /// dimensions are used to access a specific element in a three-dimensional array called
        /// Coords.</param>
        /// <param name="value">The value parameter is of type ushort, which stands for unsigned short.
        /// It is used to specify the value that will be set at the specified coordinates (x, y,
        /// coord).</param>
        public void SetValue(int x, int y, ZCT coord, ushort value)
        {
            SetValue(x, y, Coords[coord.Z, coord.C, coord.T], value);
        }

        /// <summary>
        /// The function SetValueRGB sets the RGB value at a specific coordinate in a buffer.
        /// </summary>
        /// <param name="ZCTXY">ZCTXY is a custom data structure that represents the coordinates of a
        /// pixel in a 3D image. It has the following properties:</param>
        /// <param name="RGBindex">RGBindex is an integer representing the index of the RGB value to be
        /// set. It is used to specify which RGB value (red, green, or blue) should be set in the
        /// SetValueRGB method.</param>
        /// <param name="value">The "value" parameter is an unsigned short (ushort) that represents the
        /// value to be set for the specified RGB index at the given coordinate (X, Y) in the
        /// buffer.</param>
        public void SetValueRGB(ZCTXY coord, int RGBindex, ushort value)
        {
            int ind = Coords[coord.Z, coord.C, coord.T];
            Buffers[ind].SetValueRGB(coord.X, coord.Y, RGBindex, value);
        }

        /// <summary>
        /// The function "GetBitmap" returns a Bitmap object based on the provided ZCT coordinates.
        /// </summary>
        /// <param name="ZCT">The ZCT parameters represent the coordinates of a specific image in a
        /// three-dimensional space.</param>
        /// <returns>
        /// The method is returning a Bitmap object.
        /// </returns>
        public Bitmap GetBitmap(ZCT coord)
        {
            return (Bitmap)GetImageByCoord(coord.Z, coord.C, coord.T);
        }

        /// <summary>
        /// The function takes in a ZCT coordinate and three IntRange parameters for the red, green, and
        /// blue channels, and returns a filtered Bitmap image.
        /// </summary>
        /// <param name="ZCT">ZCT refers to the coordinates of a pixel in a 3D image volume.</param>
        /// <param name="IntRange">An IntRange is a range of integer values. It typically consists of a
        /// minimum and maximum value. In this context, the IntRange parameters r, g, and b are used to
        /// specify the range of values for the red, green, and blue color channels of a pixel.</param>
        /// <param name="IntRange">An IntRange is a class that represents a range of integer values. It
        /// typically has two properties: Min and Max, which define the minimum and maximum values of
        /// the range. In the context of the GetFiltered method, the IntRange parameters r, g, and b are
        /// used to specify the range</param>
        /// <param name="IntRange">An IntRange is a class that represents a range of integer values. It
        /// typically has two properties: Min and Max, which define the minimum and maximum values of
        /// the range. In this context, the IntRange parameters r, g, and b are used to specify the
        /// range of values for the red</param>
        /// <returns>
        /// The method is returning a Bitmap object.
        /// </returns>
        public Bitmap GetFiltered(ZCT coord, IntRange r, IntRange g, IntRange b)
        {
            int index = Coords[coord.Z, coord.C, coord.T];
            return GetFiltered(index, r, g, b);
        }

        /// <summary>
        /// The function takes an index and three IntRange parameters for red, green, and blue values,
        /// and returns a filtered Bitmap image based on the specified ranges.
        /// </summary>
        /// <param name="ind">The "ind" parameter is an integer that represents the index of the buffer
        /// to apply the filter on.</param>
        /// <param name="IntRange">An IntRange is a class that represents a range of integer values. It
        /// has two properties: Min and Max, which define the minimum and maximum values of the range.
        /// In the context of this code, the IntRange parameters r, g, and b are used to specify the
        /// range of values for</param>
        /// <param name="IntRange">An IntRange is a class that represents a range of integer values. It
        /// has two properties: Min and Max, which define the minimum and maximum values of the range.
        /// In the context of this code, the IntRange parameters r, g, and b are used to specify the
        /// range of values for</param>
        /// <param name="IntRange">An IntRange is a class that represents a range of integer values. It
        /// has two properties: Min and Max, which define the minimum and maximum values of the range.
        /// In the context of this code, the IntRange parameters r, g, and b are used to specify the
        /// range of values for</param>
        /// <returns>
        /// The method is returning a Bitmap object.
        /// </returns>
        public Bitmap GetFiltered(int ind, IntRange r, IntRange g, IntRange b)
        {
            if (Buffers[ind].BitsPerPixel > 8)
            {
                BioImage.filter16.InRed = r;
                BioImage.filter16.InGreen = g;
                BioImage.filter16.InBlue = b;
                return BioImage.filter16.Apply(Buffers[ind]);
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

        /// <summary>
        /// The function `GetChannelImage` returns an unmanaged image of a specific channel (R, G, or B)
        /// from a bitmap buffer.
        /// </summary>
        /// <param name="ind">The parameter "ind" is an integer that represents the index of the
        /// buffer.</param>
        /// <param name="s">The parameter "s" is an integer value that represents the channel to extract
        /// from the image. A value of 0 represents the red channel, a value of 1 represents the green
        /// channel, and a value of 2 represents the blue channel.</param>
        /// <returns>
        /// The method is returning an UnmanagedImage object.
        /// </returns>
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

        /// <summary>
        /// The function `GetEmission` returns a bitmap image based on the given coordinates and color
        /// range values.
        /// </summary>
        /// <param name="ZCT">ZCT is a coordinate object that represents the position of a pixel in a 3D
        /// image. It has three properties: Z (the z-coordinate), C (the channel index), and T (the time
        /// index).</param>
        /// <param name="IntRange">An IntRange is a class that represents a range of integer values. It
        /// typically has two properties: Min and Max, which define the minimum and maximum values of
        /// the range. In this context, the IntRange parameters rf, gf, and bf are used to specify the
        /// range of red, green,</param>
        /// <param name="IntRange">An IntRange is a class that represents a range of integer values. It
        /// typically has two properties: Min and Max, which define the minimum and maximum values of
        /// the range. In this context, the IntRange parameters rf, gf, and bf are used to specify the
        /// range of red, green,</param>
        /// <param name="IntRange">An IntRange is a class that represents a range of integer values. It
        /// typically has two properties: Min and Max, which define the minimum and maximum values of
        /// the range. In this context, the IntRange parameters rf, gf, and bf are used to specify the
        /// range of red, green,</param>
        /// <returns>
        /// The method returns a Bitmap object.
        /// </returns>
        public Bitmap GetEmission(ZCT coord, IntRange rf, IntRange gf, IntRange bf)
        {
            if (RGBChannelCount == 1)
            {
                Bitmap[] bs = new Bitmap[Channels.Count];
                for (int c = 0; c < Channels.Count; c++)
                {
                    int index = Coords[coord.Z, c, coord.T];
                    bs[c] = Buffers[index];
                }
                Bitmap bm = (Bitmap)Bitmap.GetEmissionBitmap(bs, Channels.ToArray());
                return bm;
            }
            else
            {
                int index = Coords[coord.Z, coord.C, coord.T];
                return Buffers[index];
            }
        }

        /// <summary>
        /// The function `GetRGBBitmap` returns a bitmap with specified RGB channel ranges based on the
        /// given coordinates and buffer indices.
        /// </summary>
        /// <param name="ZCT">ZCT is a coordinate object that represents the position of a pixel in a 3D
        /// image. It has three properties: Z (the z-axis coordinate), C (the channel coordinate), and T
        /// (the time coordinate).</param>
        /// <param name="IntRange">An IntRange is a range of integer values. It is typically used to
        /// specify a range of values for a particular parameter. In this case, the IntRange parameters
        /// rf, gf, and bf are used to specify the range of values for the red, green, and blue channels
        /// respectively.</param>
        /// <param name="IntRange">An IntRange is a range of integer values. It is typically used to
        /// specify a range of values for a particular parameter. In this case, the IntRange parameters
        /// rf, gf, and bf are used to specify the range of values for the red, green, and blue channels
        /// respectively.</param>
        /// <param name="IntRange">An IntRange is a range of integer values. It is typically used to
        /// specify a range of values for a particular parameter. In this case, the IntRange parameters
        /// rf, gf, and bf are used to specify the range of values for the red, green, and blue channels
        /// respectively.</param>
        /// <returns>
        /// The method returns a Bitmap object.
        /// </returns>
        public Bitmap GetRGBBitmap(ZCT coord, IntRange rf, IntRange gf, IntRange bf)
        {
            int index = Coords[coord.Z, coord.C, coord.T];
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
                return Buffers[index];
        }

        /// <summary>
        /// The function `GetBitmapRGB` takes in the width, height, pixel format, and byte array of an
        /// image and returns a Bitmap object with the RGB values of the image.
        /// </summary>
        /// <param name="w">The width of the bitmap in pixels.</param>
        /// <param name="h">The parameter "h" represents the height of the bitmap in pixels.</param>
        /// <param name="PixelFormat">The PixelFormat parameter specifies the format of the pixels in
        /// the byte array. It can have the following values:</param>
        /// <param name="bts">The parameter "bts" is a byte array that contains the pixel data of an
        /// image. The format of the pixel data depends on the specified PixelFormat "px". The method
        /// "GetBitmapRGB" takes this byte array and converts it into a Bitmap object with the specified
        /// width and height. The</param>
        /// <returns>
        /// The method returns a Bitmap object.
        /// </returns>
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

        /// <summary>
        /// The function "GetAnnotations" returns a list of ROI objects that have a specific coordinate.
        /// </summary>
        /// <param name="ZCT">ZCT is a data type that represents the coordinates of a point in a
        /// three-dimensional space. It likely has three properties: X, Y, and Z, which represent the
        /// coordinates along the X, Y, and Z axes respectively.</param>
        /// <returns>
        /// The method is returning a list of ROI objects.
        /// </returns>
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

        /// <summary>
        /// The function "GetAnnotations" returns a list of ROI objects that match the specified Z, C,
        /// and T coordinates.
        /// </summary>
        /// <param name="Z">Z represents the Z-coordinate of the annotation. It is used to filter the
        /// annotations based on their Z-coordinate value.</param>
        /// <param name="C">The parameter C represents the channel number. It is used to filter the
        /// annotations based on the channel they belong to.</param>
        /// <param name="T">T represents the time index of the annotations. It is used to filter the
        /// annotations based on the specific time frame.</param>
        /// <returns>
        /// The method is returning a list of ROI objects.
        /// </returns>
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
        /// The Initialize function initializes OME on a separate thread to allow the user to view
        /// images without waiting for initialization.
        /// </summary>
        /// <returns>
        /// If the condition in the if statement is true (OMESupport() returns false), then nothing is
        /// being returned. The method will simply exit and no further code will be executed.
        /// </returns>
        public static void Initialize()
        {
            if (!OMESupport())
                return;
            //We initialize OME on a seperate thread so the user doesn't have to wait for initialization to
            //view images. 
            System.Threading.Thread t = new System.Threading.Thread(new System.Threading.ThreadStart(InitOME));
            t.Start();
        }
        /// <summary>
        /// The function initializes the OME XML service by creating instances of the ServiceFactory,
        /// OMEXMLService, ImageReader, and ImageWriter classes.
        /// </summary>
        private static void InitOME()
        {
            factory = new ServiceFactory();
            service = (OMEXMLService)factory.getInstance(typeof(OMEXMLService));
            reader = new ImageReader();
            writer = new ImageWriter();
            initialized = true;
        }

        /// <summary>
        /// The SaveFile function saves a file with the given file name and ID.
        /// </summary>
        /// <param name="file">The file parameter is a string that represents the file path or file name
        /// where the data will be saved.</param>
        /// <param name="ID">The ID parameter is a string that represents the identifier of the file
        /// being saved.</param>
        public static void SaveFile(string file, string ID)
        {
            string[] sts = new string[1];
            sts[0] = ID;
            SaveSeries(sts, file);
        }

        /// <summary>
        /// The function `SaveSeries` saves a series of images to a TIFF file, including image metadata
        /// and annotations.
        /// </summary>
        /// <param name="IDs">An array of strings representing the IDs of the images to be
        /// saved.</param>
        /// <param name="file">The "file" parameter is a string that represents the file path where the
        /// series of images will be saved.</param>
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
                //Progress pr = new //Progress(file, "Saving");
                //pr.Show();
                //Application.DoEvents();
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
                            //pr.Update//ProgressF((float)im / (float)b.ImageCount);
                            //Application.DoEvents();
                            im++;
                        }
                    }
                }
                //pr.Close();
            }
            image.Dispose();

        }

        /// <summary>
        /// The function `OpenSeries` opens a series of bioimages stored in a TIFF file and returns an
        /// array of `BioImage` objects.
        /// </summary>
        /// <param name="file">The file parameter is a string that represents the file path of the TIFF
        /// image file that you want to open.</param>
        /// <param name="tab">The "tab" parameter is a boolean value that determines whether the image
        /// series should be opened in a new tab or not. If the value is true, each image in the series
        /// will be opened in a new tab. If the value is false, the images will be opened in the same
        /// tab.</param>
        /// <returns>
        /// The method is returning an array of BioImage objects.
        /// </returns>
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

        /// <summary>
        /// The function "OpenFile" in C# opens a bioimage file with the specified file path and returns
        /// a BioImage object.
        /// </summary>
        /// <param name="file">The file parameter is a string that represents the path or name of the
        /// file that you want to open.</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
        public static BioImage OpenFile(string file)
        {
            return OpenFile(file, 0, true, true);
        }
       /// <summary>
       /// The function "OpenFile" opens a bio image file and returns a BioImage object.
       /// </summary>
       /// <param name="file">The file parameter is a string that represents the file path or file name
       /// of the file to be opened.</param>
       /// <param name="tab">The "tab" parameter is a boolean value that determines whether the file
       /// should be opened in a new tab or not. If the value is true, the file will be opened in a new
       /// tab. If the value is false, the file will be opened in the current tab.</param>
       /// <returns>
       /// The method is returning a BioImage object.
       /// </returns>
        public static BioImage OpenFile(string file, bool tab)
        {
            return OpenFile(file, 0, tab, true);
        }

        /// <summary>
        /// The function "OpenFile" opens a bioimage file and returns a BioImage object, with options to
        /// specify the series, whether to open in a new tab, and whether to add the image to a
        /// collection.
        /// </summary>
        /// <param name="file">The file parameter is a string that represents the file path or file name
        /// of the image file you want to open.</param>
        /// <param name="series">The "series" parameter is an integer that represents the series number
        /// of the image file to be opened. It is used to specify which series of images to open if the
        /// file contains multiple series.</param>
        /// <param name="tab">The "tab" parameter is a boolean value that determines whether the file
        /// should be opened in a new tab or not. If set to true, the file will be opened in a new tab;
        /// if set to false, the file will be opened in the current tab.</param>
        /// <param name="addToImages">The "addToImages" parameter is a boolean value that determines
        /// whether the opened BioImage should be added to a collection of images. If set to true, the
        /// BioImage will be added to the collection. If set to false, the BioImage will not be added to
        /// the collection.</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
        public static BioImage OpenFile(string file, int series, bool tab, bool addToImages)
        {
            return OpenFile(file, series, tab, addToImages, false, 0, 0, 0, 0);
        }
        /// <summary>
        /// The function determines if a given image file is a TIFF file and if it is tiled.
        /// </summary>
        /// <param name="imagePath">A string representing the file path of the image file.</param>
        /// <returns>
        /// The method is returning a boolean value.
        /// </returns>
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
        /// <summary>
        /// The function initializes the directory resolution for a BioImage object based on the
        /// resolution information from a Tiff image.
        /// </summary>
        /// <param name="BioImage">A BioImage object that represents an image in a biological
        /// context.</param>
        /// <param name="Tiff">The "Tiff" parameter is an object of the Tiff class. It represents a TIFF
        /// image file and provides methods and properties to access and manipulate the image
        /// data.</param>
        /// <param name="ImageJDesc">ImageJDesc is a class that contains information about the image
        /// dimensions and resolution in ImageJ format. It has the following properties:</param>
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
                if (jdesc != null)
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

        /// <summary>
        /// The function `OpenFile` opens a bioimage file, reads its metadata, and loads the image data
        /// into a `BioImage` object.
        /// </summary>
        /// <param name="file">The file path of the bioimage to be opened.</param>
        /// <param name="series">The series parameter is an integer that specifies the series number of
        /// the image to open.</param>
        /// <param name="tab">The "tab" parameter is a boolean value that determines whether the
        /// BioImage should be opened in a new tab or not. If set to true, the BioImage will be opened
        /// in a new tab. If set to false, the BioImage will be opened in the current tab.</param>
        /// <param name="addToImages">A boolean value indicating whether to add the opened BioImage to
        /// the list of Images.</param>
        /// <param name="tile">A boolean flag indicating whether the image should be tiled or not. If
        /// set to true, the image will be divided into tiles for faster loading and processing.</param>
        /// <param name="tileX">The `tileX` parameter is an integer that represents the X-coordinate of
        /// the top-left corner of the tile to be opened in the image.</param>
        /// <param name="tileY">The parameter "tileY" is used to specify the starting Y coordinate of
        /// the tile when opening a tiled image. It determines the position of the tile within the
        /// image.</param>
        /// <param name="tileSizeX">The tileSizeX parameter is the width of each tile in pixels when
        /// opening a tiled image.</param>
        /// <param name="tileSizeY">The parameter "tileSizeY" is the height of each tile in pixels when
        /// the image is tiled. It determines the size of the individual tiles that the image is divided
        /// into for efficient loading and processing.</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
        public static BioImage OpenFile(string file, int series, bool tab, bool addToImages, bool tile, int tileX, int tileY, int tileSizeX, int tileSizeY)
        {
            string fs = file.Replace("\\", "/");
            vips = VipsSupport(file);
            Console.WriteLine("Opening BioImage: " + file);
            bool ome = isOME(file);
            if (ome) return OpenOME(file, series, tab, addToImages, tile, tileX, tileY, tileSizeX, tileSizeY);
            bool tiled = IsTiffTiled(file);
            Console.WriteLine("IsTiled=" + tiled.ToString());
            tile = tiled;

            Stopwatch st = new Stopwatch();
            st.Start();
            status = "Opening Image";
            progFile = file;
            progressValue = 0;
            BioImage b = new BioImage(file);
            if (tiled && file.EndsWith(".tif") && !file.EndsWith(".ome.tif"))
            {
                //To open this we need libvips
                vips = VipsSupport(b.file);
            }
            b.series = series;
            b.file = file;
            b.ispyramidal = tiled;
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
                int str = image.ScanlineSize();
                bool inter = true;
                if (stride != str)
                    inter = false;
                InitDirectoryResolution(b, image, imDesc);
                if (tiled)
                {
                    Console.WriteLine("Opening tiles.");
                    if (vips)
                        OpenVips(b, b.Resolutions.Count);
                    for (int t = 0; t < b.SizeT; t++)
                    {
                        for (int c = 0; c < b.SizeC; c++)
                        {
                            for (int z = 0; z < b.SizeZ; z++)
                            {
                                Bitmap bmp = GetTile(b, new ZCT(z, c, t), series, tileX, tileY, tileSizeX, tileSizeY);
                                b.Buffers.Add(bmp);
                                Statistics.CalcStatistics(bmp);
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
                        Statistics.CalcStatistics(inf);
                        progressValue = (int)(p / (float)(series + 1) * pages);
                    }
                }
                image.Close();
                b.UpdateCoords();
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
            if (ome && OmeSupport)
            {
                b.Annotations.AddRange(OpenOMEROIs(file, series));
            }

            //We wait for histogram image statistics calculation
            do
            {
                Thread.Sleep(50);
            } while (b.Buffers[b.Buffers.Count - 1].Stats == null);
            Statistics.ClearCalcBuffer();
            AutoThreshold(b, false);
            if (b.bitsPerPixel > 8)
                b.StackThreshold(true);
            else
                b.StackThreshold(false);
            //Recorder.AddLine("Bio.BioImage.Open(" + '"' + file + '"' + ");");
            if (addToImages)
                Images.AddImage(b, tab);
            //pr.Close();
            //pr.Dispose();
            st.Stop();
            b.loadTimeMS = st.ElapsedMilliseconds;
            Console.WriteLine("BioImage loaded " + b.ToString());
            return b;
        }

        /// <summary>
        /// The function `isTiffSeries` checks if a TIFF image file is part of a series by reading the
        /// image description and checking for a specific format.
        /// </summary>
        /// <param name="file">The "file" parameter is a string that represents the file path of the
        /// TIFF image file that you want to check if it is part of a series.</param>
        /// <returns>
        /// The method is returning a boolean value.
        /// </returns>
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
        /// <summary>
        /// The function isOME checks if a given file is an OME file based on its extension or by
        /// reading its image description.
        /// </summary>
        /// <param name="file">The parameter "file" is a string that represents the file name or path of
        /// the file that needs to be checked.</param>
        /// <returns>
        /// The method is returning a boolean value.
        /// </returns>
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
            if (!(file.EndsWith("png") || file.EndsWith("PNG") || file.EndsWith("jpg") || file.EndsWith("JPG") ||
                file.EndsWith("jpeg") || file.EndsWith("JPEG") || file.EndsWith("bmp") || file.EndsWith("BMP")))
            {
                return true;
            }
            else return false;
        }

        /// <summary>
        /// The function isOMESeries checks if a given file is an OME series by using an ImageReader and
        /// OMEXMLMetadata.
        /// </summary>
        /// <param name="file">The "file" parameter is a string that represents the file path of an
        /// image file.</param>
        /// <returns>
        /// The method is returning a boolean value, which indicates whether the given file is an OME
        /// series or not.
        /// </returns>
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
        /// <summary>
        /// The function "SaveOME" saves a single BioImage with a given ID to a specified file using the
        /// OME file format.
        /// </summary>
        /// <param name="file">The "file" parameter is a string that represents the file path where the
        /// OME series will be saved.</param>
        /// <param name="ID">The ID parameter is a string that represents the unique identifier of an
        /// image.</param>
        /// <returns>
        /// If the `OMESupport()` method returns `false`, then nothing is being returned. The `return`
        /// statement will cause the method to exit without returning any value.
        /// </returns>
        public static void SaveOME(string file, string ID)
        {
            if (!OMESupport())
                return;
            BioImage[] sts = new BioImage[1];
            sts[0] = Images.GetImage(ID);
            SaveOMESeries(sts, file, BioImage.Planes);
        }

        /// <summary>
        /// The function saves a BioImage object as an OME file.
        /// </summary>
        /// <param name="BioImage">The BioImage parameter is an object that represents an image in a
        /// biological context. It likely contains information such as pixel data, metadata, and other
        /// properties related to the image.</param>
        /// <param name="file">The "file" parameter is a string that represents the file path where the
        /// OME (Open Microscopy Environment) file will be saved.</param>
        /// <returns>
        /// If the `OMESupport()` method returns `false`, then nothing is being returned. The method
        /// will simply exit and no further code will be executed.
        /// </returns>
        public static void SaveOME(BioImage image, string file)
        {
            if (!OMESupport())
                return;
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
            if (!OMESupport())
                return;
            if (File.Exists(f))
                File.Delete(f);
            loci.formats.meta.IMetadata omexml = service.createOMEXMLMetadata();
            progressValue = 0;
            status = "Saving OME Image Metadata.";
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
                int samples = 1;
                if (b.isRGB)
                    samples = 3;
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
                omexml.setPixelsPhysicalSizeZ(p3, serie);
                ome.units.quantity.Length s1 = new ome.units.quantity.Length(java.lang.Double.valueOf(b.Volume.Location.X), ome.units.UNITS.MICROMETER);
                omexml.setStageLabelX(s1, serie);
                ome.units.quantity.Length s2 = new ome.units.quantity.Length(java.lang.Double.valueOf(b.Volume.Location.Y), ome.units.UNITS.MICROMETER);
                omexml.setStageLabelY(s2, serie);
                ome.units.quantity.Length s3 = new ome.units.quantity.Length(java.lang.Double.valueOf(b.Volume.Location.Z), ome.units.UNITS.MICROMETER);
                omexml.setStageLabelZ(s3, serie);
                omexml.setStageLabelName("StageLabel:" + serie, serie);

                for (int channel = 0; channel < b.Channels.Count; channel++)
                {
                    Channel c = b.Channels[channel];
                    for (int r = 0; r < c.range.Length; r++)
                    {
                        omexml.setChannelID("Channel:" + channel + ":" + serie, serie, channel + r);
                        omexml.setChannelSamplesPerPixel(new PositiveInteger(java.lang.Integer.valueOf(1)), serie, channel + r);
                        if (c.LightSourceWavelength != 0)
                        {
                            omexml.setChannelLightSourceSettingsID("LightSourceSettings:" + channel, serie, channel + r);
                            ome.units.quantity.Length lw = new ome.units.quantity.Length(java.lang.Double.valueOf(c.LightSourceWavelength), ome.units.UNITS.NANOMETER);
                            omexml.setChannelLightSourceSettingsWavelength(lw, serie, channel + r);
                            omexml.setChannelLightSourceSettingsAttenuation(PercentFraction.valueOf(c.LightSourceAttenuation), serie, channel + r);
                        }
                        omexml.setChannelName(c.Name, serie, channel + r);
                        if (c.Color != null)
                        {
                            ome.xml.model.primitives.Color col = new ome.xml.model.primitives.Color(c.Color.Value.R, c.Color.Value.G, c.Color.Value.B, c.Color.Value.A);
                            omexml.setChannelColor(col, serie, channel + r);
                        }
                        if (c.Emission != 0)
                        {
                            ome.units.quantity.Length em = new ome.units.quantity.Length(java.lang.Double.valueOf(c.Emission), ome.units.UNITS.NANOMETER);
                            omexml.setChannelEmissionWavelength(em, serie, channel + r);
                            ome.units.quantity.Length ex = new ome.units.quantity.Length(java.lang.Double.valueOf(c.Excitation), ome.units.UNITS.NANOMETER);
                            omexml.setChannelExcitationWavelength(ex, serie, channel + r);
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
                        omexml.setChannelFluor(c.Fluor, serie, channel + r);
                        if (c.LightSourceIntensity != 0)
                        {
                            ome.units.quantity.Power pw = new ome.units.quantity.Power(java.lang.Double.valueOf(c.LightSourceIntensity), ome.units.UNITS.VOLT);
                            omexml.setLightEmittingDiodePower(pw, serie, channel + r);
                            omexml.setLightEmittingDiodeID(c.DiodeName, serie, channel + r);
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
                    omexml.setROIName(an.roiName, i);
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
            writer.setMetadataRetrieve(omexml);
            f = f.Replace("\\", "/");
            writer.setId(f);
            status = "Saving OME Image Planes.";
            for (int i = 0; i < files.Length; i++)
            {
                BioImage b = files[i];
                writer.setSeries(i);
                for (int bu = 0; bu < b.Buffers.Count; bu++)
                {
                    writer.saveBytes(bu, b.Buffers[bu].GetSaveBytes(BitConverter.IsLittleEndian));
                }
            }
            bool stop = false;
            do
            {
                try
                {
                    writer.close();
                    stop = true;
                }
                catch (Exception e)
                {
                    //Scripting.LogLine(e.Message);
                }

            } while (!stop);
        }
        /// <summary>
        /// The function "OpenOME" opens a bioimage file in the OME format and returns the first image
        /// in the series.
        /// </summary>
        /// <param name="file">The "file" parameter is a string that represents the file path or name of
        /// the OME file that you want to open.</param>
        /// <param name="tab">The "tab" parameter is a boolean value that determines whether the OME
        /// metadata should be displayed in a tabular format. If the value is true, the metadata will be
        /// displayed in a tabular format. If the value is false, the metadata will be displayed in a
        /// non-tabular format.</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
        public static BioImage OpenOME(string file, bool tab)
        {
            if (!OMESupport())
                return null;
            return OpenOMESeries(file, tab, true)[0];
        }

        /// <summary>
        /// The function "OpenOME" opens a BioImage file with the specified file path and series number,
        /// and returns the BioImage object.
        /// </summary>
        /// <param name="file">The "file" parameter is a string that represents the file path or name of
        /// the OME file that you want to open.</param>
        /// <param name="serie">The "serie" parameter is an integer that represents the series number of
        /// the OME file. It is used to specify which series within the file should be opened.</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
        public static BioImage OpenOME(string file, int serie)
        {
            if (!OMESupport())
                return null;
            //Recorder.AddLine("Bio.BioImage.OpenOME(\"" + file + "\"," + serie + ");");
            return OpenOME(file, serie, true, false, false, 0, 0, 0, 0);
        }

        /// <summary>
        /// The function takes an array of file paths, along with the dimensions of a stack, and returns
        /// a BioImage object that represents the stack of images.
        /// </summary>
        /// <param name="files">An array of file paths representing the bioimage files to be
        /// processed.</param>
        /// <param name="sizeZ">The sizeZ parameter represents the number of image slices or planes in
        /// the Z dimension of the image stack.</param>
        /// <param name="sizeC">The parameter "sizeC" represents the number of channels in the image.
        /// Channels refer to different color or fluorescence channels in an image. For example, if an
        /// image has red, green, and blue channels, the value of "sizeC" would be 3.</param>
        /// <param name="sizeT">The sizeT parameter represents the number of time points in the image
        /// stack. It determines the number of frames or time points in the resulting image
        /// stack.</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
        public static BioImage FilesToStack(string[] files, int sizeZ, int sizeC, int sizeT)
        {
            BioImage b = new BioImage(files[0]);
            for (int i = 0; i < files.Length; i++)
            {
                BioImage bb = OpenFile(files[i], false);
                b.Buffers.AddRange(bb.Buffers);
            }
            b.UpdateCoords(sizeZ, sizeC, sizeT);
            Images.AddImage(b, true);
            return b;
        }

        /// <summary>
        /// The function "FolderToStack" takes a path as input and returns a BioImage object that
        /// represents a stack of images from the specified folder.
        /// </summary>
        /// <param name="path">The path parameter is a string that represents the directory path where
        /// the image files are located.</param>
        /// <param name="tab">The "tab" parameter is a boolean value that determines whether the
        /// BioImage should be opened in a new tab or not. If "tab" is set to true, the BioImage will be
        /// opened in a new tab. If "tab" is set to false, the BioImage will be opened</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
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
            Images.AddImage(b, tab);
            //Recorder.AddLine("BioImage.FolderToStack(\"" + path + "\");");
            return b;
        }

        static bool vips = false;
        /// <summary>
        /// The function "OpenVips" takes a BioImage object and an integer representing the number of
        /// pages, and adds each page of a TIFF image to the BioImage's vipPages list.
        /// </summary>
        /// <param name="BioImage">The BioImage parameter is an object that represents a bio image. It
        /// likely contains information about the image file, such as the file path and other
        /// metadata.</param>
        /// <param name="pagecount">The parameter "pagecount" represents the number of pages in the TIFF
        /// image file that needs to be loaded.</param>
        public static void OpenVips(BioImage b, int pagecount)
        {
            try
            {
                for (int i = 0; i < pagecount; i++)
                {
                    b.vipPages.Add(NetVips.Image.Tiffload(b.file, i));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

        }
        /// <summary>
        /// The function `ExtractRegionFromTiledTiff` takes a BioImage object, coordinates, width,
        /// height, and resolution as input, and returns a Bitmap object representing the extracted
        /// region from the tiled TIFF image.
        /// </summary>
        /// <param name="BioImage">The BioImage object represents an image file that contains multiple
        /// pages or resolutions. It contains information about the image file, such as the file path,
        /// the number of pages, and the resolutions available.</param>
        /// <param name="x">The x-coordinate of the top-left corner of the region to extract from the
        /// tiled TIFF image.</param>
        /// <param name="y">The parameter "y" represents the starting y-coordinate of the region to be
        /// extracted from the tiled TIFF image.</param>
        /// <param name="width">The width parameter represents the width of the region to be extracted
        /// from the tiled TIFF image.</param>
        /// <param name="height">The height parameter represents the height of the region that you want
        /// to extract from the tiled TIFF image.</param>
        /// <param name="res">The parameter "res" represents the resolution level of the tiled TIFF
        /// image. It is used to specify the level of detail or zoom level at which the region is
        /// extracted.</param>
        /// <returns>
        /// The method is returning a Bitmap object.
        /// </returns>
        public static Bitmap ExtractRegionFromTiledTiff(BioImage b, int x, int y, int width, int height, int res)
        {
            try
            {
                NetVips.Image subImage = b.vipPages[res].Crop(x, y, width, height);
                if (b.vipPages[res].Format == Enums.BandFormat.Uchar)
                {
                    Bitmap bm;
                    byte[] imageData = subImage.WriteToMemory();
                    if (b.Resolutions[res].RGBChannelsCount == 3)
                        bm = new Bitmap(width, height, PixelFormat.Format24bppRgb, imageData, new ZCT(), b.file);
                    else
                        bm = new Bitmap(width, height, PixelFormat.Format8bppIndexed, imageData, new ZCT(), b.file);
                    return bm;

                }
                else if (b.vipPages[res].Format == Enums.BandFormat.Ushort)
                {
                    Bitmap bm;
                    byte[] imageData = subImage.WriteToMemory();
                    if (b.Resolutions[res].RGBChannelsCount == 3)
                        bm = new Bitmap(width, height, PixelFormat.Format48bppRgb, imageData, new ZCT(), b.file);
                    else
                        bm = new Bitmap(width, height, PixelFormat.Format16bppGrayScale, imageData, new ZCT(), b.file);
                    return bm;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return null;
            }
            return null;
        }
        /* Reading the OME-XML metadata and creating a BioImage object. */
        public static BioImage OpenOME(string file, int serie, bool tab, bool addToImages, bool tile, int tilex, int tiley, int tileSizeX, int tileSizeY)
        {
            if (tileSizeX == 0)
                tileSizeX = 1920;
            if (tileSizeY == 0)
                tileSizeY = 1080;
            do
            {
                Thread.Sleep(50);
            } while (!initialized);
            if (file == null || file == "")
                throw new InvalidDataException("File is empty or null");
            progressValue = 0;
            progFile = file;
            BioImage b = new BioImage(file);
            b.Loading = true;
            if (b.meta == null)
                b.meta = (IMetadata)((OMEXMLService)new ServiceFactory().getInstance(typeof(OMEXMLService))).createOMEXMLMetadata();
            string f = file.Replace("\\", "/");
            string cf = reader.getCurrentFile();
            if (cf != null)
                cf = cf.Replace("\\", "/");
            if (cf != f)
            {
                status = "Opening OME Image.";
                reader.close();
                reader.setMetadataStore(b.meta);
                file = file.Replace("\\", "/");
                reader.setId(file);
            }
            status = "Reading OME Metadata.";
            reader.setSeries(serie);
            int RGBChannelCount = reader.getRGBChannelCount();
            if (reader.getOptimalTileWidth() != reader.getSizeX())
            {
                b.ispyramidal = true;
                tile = true;
            }
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
            b.bitsPerPixel = reader.getBitsPerPixel();
            b.series = serie;
            string order = reader.getDimensionOrder();

            //Lets get the channels and initialize them
            int i = 0;
            while (true)
            {
                Channel ch = new Channel(i, b.bitsPerPixel, 1);
                bool def = false;
                try
                {
                    if (b.meta.getChannelSamplesPerPixel(serie, i) != null)
                    {
                        int s = b.meta.getChannelSamplesPerPixel(serie, i).getNumberValue().intValue();
                        ch.SamplesPerPixel = s;
                        def = true;
                    }
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
                }
                //If this channel is not defined we have loaded all the channels in the file.
                if (!def)
                    break;
                else
                    b.Channels.Add(ch);
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
                i++;
            }

            //If the file doens't have channels we initialize them.
            if (b.Channels.Count == 0)
            {
                b.Channels.Add(new Channel(0, b.bitsPerPixel, RGBChannelCount));
            }

            //Bioformats gives a size of 3 for C when saved in ImageJ as RGB. We need to correct for this as C should be 1 for RGB.
            if ((PixelFormat == PixelFormat.Format24bppRgb || PixelFormat == PixelFormat.Format32bppArgb || PixelFormat == PixelFormat.Format48bppRgb) && b.SizeC == 3)
            {
                b.sizeC = 1;
            }
            b.Coords = new int[b.SizeZ, b.SizeC, b.SizeT];

            int resc = reader.getResolutionCount();
            for (int s = 0; s < b.seriesCount; s++)
            {
                reader.setSeries(s);
                for (int r = 0; r < reader.getResolutionCount(); r++)
                {
                    Resolution res = new Resolution();
                    try
                    {
                        int rgbc = reader.getRGBChannelCount();
                        int bps = reader.getBitsPerPixel();
                        PixelFormat px;
                        try
                        {
                            px = GetPixelFormat(rgbc, b.meta.getPixelsType(s));
                        }
                        catch (Exception)
                        {
                            px = GetPixelFormat(rgbc, bps);
                        }
                        res.PixelFormat = px;
                        res.SizeX = reader.getSizeX();
                        res.SizeY = reader.getSizeY();
                        if (b.meta.getPixelsPhysicalSizeX(s) != null)
                        {
                            res.PhysicalSizeX = b.meta.getPixelsPhysicalSizeX(s).value().doubleValue();
                        }
                        else
                            res.PhysicalSizeX = (96 / 2.54) / 1000;
                        if (b.meta.getPixelsPhysicalSizeY(s) != null)
                        {
                            res.PhysicalSizeY = b.meta.getPixelsPhysicalSizeY(s).value().doubleValue();
                        }
                        else
                            res.PhysicalSizeY = (96 / 2.54) / 1000;

                        if (b.meta.getStageLabelX(s) != null)
                            res.StageSizeX = b.meta.getStageLabelX(s).value().doubleValue();
                        if (b.meta.getStageLabelY(s) != null)
                            res.StageSizeY = b.meta.getStageLabelY(s).value().doubleValue();
                        if (b.meta.getStageLabelZ(s) != null)
                            res.StageSizeZ = b.meta.getStageLabelZ(s).value().doubleValue();
                        else
                            res.StageSizeZ = 1;
                        if (b.meta.getPixelsPhysicalSizeZ(s) != null)
                        {
                            res.PhysicalSizeZ = b.meta.getPixelsPhysicalSizeZ(s).value().doubleValue();
                        }
                        else
                        {
                            res.PhysicalSizeZ = 1;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("No Stage Coordinates. PhysicalSize:(" + res.PhysicalSizeX + "," + res.PhysicalSizeY + "," + res.PhysicalSizeZ + ")");
                    }
                    b.Resolutions.Add(res);
                }
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
                    string type = b.meta.getShapeType(im, sc);
                    ROI an = new ROI();
                    an.roiID = roiID;
                    an.roiName = roiName;
                    an.shapeIndex = sc;
                    if (type == "Point")
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
                        continue;
                    }
                    else
                    if (type == "Line")
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
                        continue;
                    }
                    else
                    if (type == "Rectangle")
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
                        continue;
                    }
                    else
                    if (type == "Ellipse")
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
                    if (type == "Polygon")
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
                    if (type == "Polyline")
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
                    if (type == "Label")
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
                    if (b.Volume.Intersects(new PointD(an.BoundingBox.X, an.BoundingBox.Y)))
                        b.Annotations.Add(an);
                }
            }

            List<string> serFiles = new List<string>();
            serFiles.AddRange(reader.getSeriesUsedFiles());

            b.Buffers = new List<Bitmap>();
            // read the image data bytes
            int pages = reader.getImageCount();
            bool inter = reader.isInterleaved();
            int z = 0;
            int c = 0;
            int t = 0;
            status = "Reading OME Image Planes";
            if (!tile)
                for (int p = 0; p < pages; p++)
                {
                    Bitmap bf;
                    progressValue = (int)p / pages;
                    byte[] bytes = reader.openBytes(p);
                    bf = new Bitmap(file, SizeX, SizeY, PixelFormat, bytes, new ZCT(z, c, t), p, null, b.littleEndian, inter);
                    b.Buffers.Add(bf);
                    Statistics.CalcStatistics(b.Buffers.Last());
                }
            else
            {
                b.Buffers.Add(GetTile(b, new ZCT(z, c, t), serie, tilex, tiley, tileSizeX, tileSizeY));
                Statistics.CalcStatistics(b.Buffers.Last());
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
                    if (b.meta.getPlaneTheC(serie, bi) != null)
                        zc = b.meta.getPlaneTheZ(serie, bi).getNumberValue().intValue();
                    if (b.meta.getPlaneTheC(serie, bi) != null)
                        tc = b.meta.getPlaneTheT(serie, bi).getNumberValue().intValue();
                    pl.Coordinate = new ZCT(zc, cc, tc);
                    if (b.meta.getPlaneDeltaT(serie, bi) != null)
                        pl.Delta = b.meta.getPlaneDeltaT(serie, bi).value().doubleValue();
                    if (b.meta.getPlaneExposureTime(serie, bi) != null)
                        pl.Exposure = b.meta.getPlaneExposureTime(serie, bi).value().doubleValue();
                    b.Buffers[bi].Plane = pl;
                }

            b.UpdateCoords(b.SizeZ, b.SizeC, b.SizeT, order);
            //We wait for histogram image statistics calculation
            do
            {
                Thread.Sleep(50);
            } while (b.Buffers[b.Buffers.Count - 1].Stats == null);
            Statistics.ClearCalcBuffer();

            AutoThreshold(b, true);
            if (b.bitsPerPixel > 8)
                b.StackThreshold(true);
            else
                b.StackThreshold(false);
            if (addToImages)
                Images.AddImage(b, tab);
            //We use a try block to close the reader as sometimes this will cause an error.
            bool stop = false;
            do
            {
                try
                {
                    reader.close();
                    stop = true;
                }
                catch (Exception)
                {

                }
            } while (!stop);
            b.Loading = false;
            return b;
        }
        public ImageReader imRead;
        public Tiff tifRead;
        static Bitmap bm;
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
        public static Bitmap GetTile(BioImage b, ZCT coord, int serie, int tilex, int tiley, int tileSizeX, int tileSizeY)
        {
            if ((!OMESupport() && b.file.EndsWith("ome.tif") && vips) || (b.file.EndsWith(".tif") && vips))
            {
                //We can get a tile faster with libvips rather than bioformats.
                //and incase we are on mac we can't use bioformats due to IKVM not supporting mac.
                return ExtractRegionFromTiledTiff(b, tilex, tiley, tileSizeX, tileSizeY, serie);
            }
            string curfile = reader.getCurrentFile();
            if (curfile == null)
            {
                b.meta = (IMetadata)((OMEXMLService)factory.getInstance(typeof(OMEXMLService))).createOMEXMLMetadata();
                reader.close();
                reader.setMetadataStore(b.meta);
                b.file = b.file.Replace("\\", "/");
                reader.setId(b.file);
            }
            else
            {
                if (curfile != b.file)
                {
                    reader.close();
                    b.meta = (IMetadata)((OMEXMLService)factory.getInstance(typeof(OMEXMLService))).createOMEXMLMetadata();
                    reader.setMetadataStore(b.meta);
                    b.file = b.file.Replace("\\", "/");
                    reader.setId(b.file);
                }
            }
            if (reader.getSeries() != serie)
                reader.setSeries(serie);
            int SizeX = reader.getSizeX();
            int SizeY = reader.getSizeY();
            int p = b.Coords[coord.Z, coord.C, coord.T];
            bool littleEndian = reader.isLittleEndian();
            PixelFormat PixelFormat = b.Resolutions[serie].PixelFormat;
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
            byte[] bytesr = reader.openBytes(b.Coords[coord.Z, coord.C, coord.T], tilex, tiley, sx, sy);
            bool interleaved = reader.isInterleaved();
            if (bm != null)
                bm.Dispose();
            bm = new Bitmap(b.file, sx, sy, PixelFormat, bytesr, coord, p, null, littleEndian, interleaved);
            if (bm.isRGB && !interleaved)
                bm.SwitchRedBlue();
            return bm;
        }

        public static void SaveOMEStiched(BioImage[] bms, string file, string compression)
        {
            if (File.Exists(file))
                File.Delete(file);
            loci.formats.meta.IMetadata omexml = service.createOMEXMLMetadata();
            //We need to go through the images and find the ones belonging to each resolution.
            //As well we need to determine the dimensions of the tiles.
            Dictionary<double, List<BioImage>> bis = new Dictionary<double, List<BioImage>>();
            Dictionary<double, Point3D> min = new Dictionary<double, Point3D>();
            Dictionary<double, Point3D> max = new Dictionary<double, Point3D>();
            for (int i = 0; i < bms.Length; i++)
            {
                Resolution res = bms[i].Resolutions[bms[i].Resolution];
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
                    min.Add(res.PhysicalSizeX, new Point3D(res.StageSizeX, res.StageSizeY, res.StageSizeZ));
                    max.Add(res.PhysicalSizeX, new Point3D(res.StageSizeX, res.StageSizeY, res.StageSizeZ));
                    bis[res.PhysicalSizeX].Add(bms[i]);
                }
            }
            int s = 0;
            foreach (double px in bis.Keys)
            {
                int xi = 1 + (int)Math.Ceiling((max[px].X - min[px].X) / bis[px][0].Resolutions[bis[px][0].Resolution].VolumeWidth);
                int yi = 1 + (int)Math.Ceiling((max[px].Y - min[px].Y) / bis[px][0].Resolutions[bis[px][0].Resolution].VolumeHeight);
                BioImage b = bis[px][0];
                int serie = s;
                // create OME-XML metadata store.
                omexml.setImageID("Image:" + serie, serie);
                omexml.setPixelsID("Pixels:" + serie, serie);
                omexml.setPixelsInterleaved(java.lang.Boolean.TRUE, serie);
                omexml.setPixelsDimensionOrder(ome.xml.model.enums.DimensionOrder.XYCZT, serie);
                if (b.bitsPerPixel > 8)
                    omexml.setPixelsType(ome.xml.model.enums.PixelType.UINT16, serie);
                else
                    omexml.setPixelsType(ome.xml.model.enums.PixelType.UINT8, serie);
                omexml.setPixelsSizeX(new PositiveInteger(java.lang.Integer.valueOf(b.SizeX * xi)), serie);
                omexml.setPixelsSizeY(new PositiveInteger(java.lang.Integer.valueOf(b.SizeY * yi)), serie);
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
                omexml.setPixelsPhysicalSizeZ(p3, serie);
                ome.units.quantity.Length s1 = new ome.units.quantity.Length(java.lang.Double.valueOf(b.Volume.Location.X), ome.units.UNITS.MICROMETER);
                omexml.setStageLabelX(s1, serie);
                ome.units.quantity.Length s2 = new ome.units.quantity.Length(java.lang.Double.valueOf(b.Volume.Location.Y), ome.units.UNITS.MICROMETER);
                omexml.setStageLabelY(s2, serie);
                ome.units.quantity.Length s3 = new ome.units.quantity.Length(java.lang.Double.valueOf(b.Volume.Location.Z), ome.units.UNITS.MICROMETER);
                omexml.setStageLabelZ(s3, serie);
                omexml.setStageLabelName("StageLabel:" + serie, serie);

                for (int channel = 0; channel < b.Channels.Count; channel++)
                {
                    Channel c = b.Channels[channel];
                    for (int r = 0; r < c.range.Length; r++)
                    {
                        omexml.setChannelID("Channel:" + channel + ":" + serie, serie, channel + r);
                        omexml.setChannelSamplesPerPixel(new PositiveInteger(java.lang.Integer.valueOf(1)), serie, channel + r);
                        if (c.LightSourceWavelength != 0)
                        {
                            omexml.setChannelLightSourceSettingsID("LightSourceSettings:" + channel, serie, channel + r);
                            ome.units.quantity.Length lw = new ome.units.quantity.Length(java.lang.Double.valueOf(c.LightSourceWavelength), ome.units.UNITS.NANOMETER);
                            omexml.setChannelLightSourceSettingsWavelength(lw, serie, channel + r);
                            omexml.setChannelLightSourceSettingsAttenuation(PercentFraction.valueOf(c.LightSourceAttenuation), serie, channel + r);
                        }
                        omexml.setChannelName(c.Name, serie, channel + r);
                        if (c.Color != null)
                        {
                            ome.xml.model.primitives.Color col = new ome.xml.model.primitives.Color(c.Color.Value.R, c.Color.Value.G, c.Color.Value.B, c.Color.Value.A);
                            omexml.setChannelColor(col, serie, channel + r);
                        }
                        if (c.Emission != 0)
                        {
                            ome.units.quantity.Length em = new ome.units.quantity.Length(java.lang.Double.valueOf(c.Emission), ome.units.UNITS.NANOMETER);
                            omexml.setChannelEmissionWavelength(em, serie, channel + r);
                            ome.units.quantity.Length ex = new ome.units.quantity.Length(java.lang.Double.valueOf(c.Excitation), ome.units.UNITS.NANOMETER);
                            omexml.setChannelExcitationWavelength(ex, serie, channel + r);
                        }
                        omexml.setChannelContrastMethod((ome.xml.model.enums.ContrastMethod)Enum.Parse(typeof(ome.xml.model.enums.ContrastMethod), c.ContrastMethod), serie, channel + r);
                        omexml.setChannelFluor(c.Fluor, serie, channel + r);
                        omexml.setChannelIlluminationType((ome.xml.model.enums.IlluminationType)Enum.Parse(typeof(ome.xml.model.enums.IlluminationType), c.IlluminationType), serie, channel + r);

                        if (c.LightSourceIntensity != 0)
                        {
                            ome.units.quantity.Power pw = new ome.units.quantity.Power(java.lang.Double.valueOf(c.LightSourceIntensity), ome.units.UNITS.VOLT);
                            omexml.setLightEmittingDiodePower(pw, serie, channel + r);
                            omexml.setLightEmittingDiodeID(c.DiodeName, serie, channel + r);
                        }
                        if (c.AcquisitionMode != null)
                            omexml.setChannelAcquisitionMode((ome.xml.model.enums.AcquisitionMode)Enum.Parse(typeof(ome.xml.model.enums.AcquisitionMode),c.AcquisitionMode), serie, channel + r);
                    }
                }

                int i = 0;
                foreach (ROI an in b.Annotations)
                {
                    if (an.roiID == "")
                        omexml.setROIID("ROI:" + i.ToString() + ":" + serie, i);
                    else
                        omexml.setROIID(an.roiID, i);
                    omexml.setROIName(an.roiName, i);
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
                    i++;
                }
                /*
                if (b.Buffers[0].Plane != null)
                    for (int bu = 0; bu < b.Buffers.Count; bu++)
                    {
                        //Correct order of parameters.
                        if (b.Buffers[bu].Plane.Delta != 0)
                        {
                            ome.units.quantity.Time t = new ome.units.quantity.Time(java.lang.Double.valueOf(b.Buffers[bu].Plane.Delta), ome.units.UNITS.MILLISECOND);
                            omexml.setPlaneDeltaT(t, bu, serie);
                        }
                        if (b.Buffers[bu].Plane.Exposure != 0)
                        {
                            ome.units.quantity.Time et = new ome.units.quantity.Time(java.lang.Double.valueOf(b.Buffers[bu].Plane.Exposure), ome.units.UNITS.MILLISECOND);
                            omexml.setPlaneExposureTime(et, bu, serie);
                        }
                        ome.units.quantity.Length lx = new ome.units.quantity.Length(java.lang.Double.valueOf(b.Buffers[bu].Plane.Location.X), ome.units.UNITS.MICROMETER);
                        ome.units.quantity.Length ly = new ome.units.quantity.Length(java.lang.Double.valueOf(b.Buffers[bu].Plane.Location.Y), ome.units.UNITS.MICROMETER);
                        ome.units.quantity.Length lz = new ome.units.quantity.Length(java.lang.Double.valueOf(b.Buffers[bu].Plane.Location.Z), ome.units.UNITS.MICROMETER);
                        omexml.setPlanePositionX(lx, bu, serie);
                        omexml.setPlanePositionY(ly, bu, serie);
                        omexml.setPlanePositionZ(lz, bu, serie);
                        omexml.setPlaneTheC(new NonNegativeInteger(java.lang.Integer.valueOf(b.Buffers[bu].Plane.Coordinate.C)), bu, serie);
                        omexml.setPlaneTheZ(new NonNegativeInteger(java.lang.Integer.valueOf(b.Buffers[bu].Plane.Coordinate.Z)), bu, serie);
                        omexml.setPlaneTheT(new NonNegativeInteger(java.lang.Integer.valueOf(b.Buffers[bu].Plane.Coordinate.T)), bu, serie);

                        omexml.setTiffDataPlaneCount(new NonNegativeInteger(java.lang.Integer.valueOf(1)), bu, serie);
                        omexml.setTiffDataIFD(new NonNegativeInteger(java.lang.Integer.valueOf(bu)), bu, serie);
                        omexml.setTiffDataFirstC(new NonNegativeInteger(java.lang.Integer.valueOf(b.Buffers[bu].Plane.Coordinate.C)), bu, serie);
                        omexml.setTiffDataFirstZ(new NonNegativeInteger(java.lang.Integer.valueOf(b.Buffers[bu].Plane.Coordinate.Z)), bu, serie);
                        omexml.setTiffDataFirstT(new NonNegativeInteger(java.lang.Integer.valueOf(b.Buffers[bu].Plane.Coordinate.T)), bu, serie);

                    }
                */
                s++;
            }
            writer.setMetadataRetrieve(omexml);
            file = file.Replace("\\", "/");
            writer.setId(file);
            writer.setCompression(compression);
            s = 0;
            foreach (double px in bis.Keys)
            {
                writer.setSeries(s);
                PointD p = new PointD(max[px].X - min[px].X, max[px].Y - min[px].Y);
                for (int i = 0; i < bis[px].Count; i++)
                {
                    BioImage b = bis[px][i];
                    writer.setTileSizeX(b.SizeX);
                    writer.setTileSizeY(b.SizeY);
                    double dx = Math.Ceiling((bis[px][i].StageSizeX - min[px].X) / bis[px][i].Resolutions[bis[px][i].Resolution].VolumeWidth);
                    double dy = Math.Ceiling((bis[px][i].StageSizeY - min[px].Y) / bis[px][i].Resolutions[bis[px][i].Resolution].VolumeHeight);
                    for (int bu = 0; bu < b.Buffers.Count; bu++)
                    {
                        byte[] bt = b.Buffers[bu].GetSaveBytes(BitConverter.IsLittleEndian);
                        writer.saveBytes(bu, bt, (int)dx * b.SizeX, (int)dy * b.SizeY, b.SizeX, b.SizeY);
                    }
                    progress = (int)((float)i / bis[px].Count)*100;
                }
            }
            bool stop = false;
            do
            {
                try
                {
                    writer.close();
                    stop = true;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            } while (!stop);
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
        private static int GetBands(PixelFormat format)
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
            //We need to go through the images and find the ones belonging to each resolution.
            //As well we need to determine the dimensions of the tiles.
            Dictionary<double, List<BioImage>> bis = new Dictionary<double, List<BioImage>>();
            Dictionary<double, Point3D> min = new Dictionary<double, Point3D>();
            Dictionary<double, Point3D> max = new Dictionary<double, Point3D>();
            for (int i = 0; i < bms.Length; i++)
            {
                Resolution res = bms[i].Resolutions[bms[i].Resolution];
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
                    mutated.Tiffsave(file, compression, 1, Enums.ForeignTiffPredictor.None, null,ss[px].Width, ss[px].Height, true, false, 16,
                    Enums.ForeignTiffResunit.Cm, 1000 * bis[px][0].PhysicalSizeX, 1000 * bis[px][0].PhysicalSizeY, true, null, Enums.RegionShrink.Nearest,
                    compressionLevel, true, Enums.ForeignDzDepth.One, true, false, null, null, ss[px].Height);
                else
                    mutated.Tiffsave(file, compression, 1, Enums.ForeignTiffPredictor.None, null, ss[px].Width, ss[px].Height, true, false, 8,
                    Enums.ForeignTiffResunit.Cm, 1000 * bis[px][0].PhysicalSizeX, 1000 * bis[px][0].PhysicalSizeY, true, null, Enums.RegionShrink.Nearest,
                    compressionLevel, true, Enums.ForeignDzDepth.One, true, false, null, null, ss[px].Height);
                s++;
            }

        }

        /// <summary>
        /// The function `StackThreshold` adjusts the minimum and maximum values of each channel's range
        /// based on the stack's minimum and maximum values, and sets the bits per pixel value
        /// accordingly.
        /// </summary>
        /// <param name="bit16">The parameter "bit16" is a boolean value that determines whether the
        /// stack threshold should be set for 16-bit or 8-bit channels. If "bit16" is true, the
        /// threshold will be set for 16-bit channels. If "bit16" is false, the threshold will
        /// be</param>
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
        }

        /// <summary>
        /// The function "GetBitsPerPixel" returns the number of bits per pixel based on the given input
        /// value "bt".
        /// </summary>
        /// <param name="bt">The parameter "bt" represents the number of bits in a color value.</param>
        /// <returns>
        /// The method is returning the number of bits per pixel based on the input parameter "bt".
        /// </returns>
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

        /// <summary>
        /// The function returns the maximum value that can be represented by a given number of bits.
        /// </summary>
        /// <param name="bt">The parameter "bt" represents the number of bits.</param>
        /// <returns>
        /// The method returns the maximum value that can be represented by a given number of bits.
        /// </returns>
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
        /// The function returns the appropriate PixelFormat based on the number of RGB channels and
        /// bits per pixel.
        /// </summary>
        /// <param name="rgbChannelCount">The number of color channels in the pixel format. This can be
        /// either 1 (grayscale) or 3 (RGB).</param>
        /// <param name="bitsPerPixel">The bitsPerPixel parameter represents the number of bits used to
        /// represent each pixel in the image. It determines the color depth or the number of colors
        /// that can be represented in the image.</param>
        /// <returns>
        /// The method returns a PixelFormat value based on the input parameters.
        /// </returns>
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
        /// <summary>
        /// The function `GetPixelFormat` returns the appropriate `PixelFormat` based on the number of
        /// RGB channels and the pixel type.
        /// </summary>
        /// <param name="rgbChannelCount">The `rgbChannelCount` parameter represents the number of
        /// channels in the image. It can be either 1 (for grayscale images) or 3 (for RGB color
        /// images).</param>
        /// <param name="px">The parameter "px" is of type ome.xml.model.enums.PixelType, which is an
        /// enumeration representing different pixel types. It can have values like INT8, UINT8, INT16,
        /// UINT16, etc.</param>
        /// <returns>
        /// The method returns a PixelFormat value based on the input parameters.
        /// </returns>
        public static PixelFormat GetPixelFormat(int rgbChannelCount, ome.xml.model.enums.PixelType px)
        {
            if (rgbChannelCount == 1)
            {
                if (px == ome.xml.model.enums.PixelType.INT8 || px == ome.xml.model.enums.PixelType.UINT8)
                    return PixelFormat.Format8bppIndexed;
                else if (px == ome.xml.model.enums.PixelType.INT16 || px == ome.xml.model.enums.PixelType.UINT16)
                    return PixelFormat.Format16bppGrayScale;
            }
            else
            {
                if (px == ome.xml.model.enums.PixelType.INT8 || px == ome.xml.model.enums.PixelType.UINT8)
                    return PixelFormat.Format24bppRgb;
                else if (px == ome.xml.model.enums.PixelType.INT16 || px == ome.xml.model.enums.PixelType.UINT16)
                    return PixelFormat.Format48bppRgb;
            }
            throw new NotSupportedException("Not supported pixel format.");
        }

        /// <summary>
        /// The function `OpenOMESeries` opens an OME series file, retrieves metadata, and returns an
        /// array of BioImage objects.
        /// </summary>
        /// <param name="file">The file path of the OME series to be opened.</param>
        /// <param name="tab">The "tab" parameter is a boolean value that determines whether the opened
        /// image should be displayed in a new tab or not. If set to true, the image will be displayed
        /// in a new tab; if set to false, the image will be displayed in the current tab.</param>
        /// <param name="addToImages">The `addToImages` parameter is a boolean value that determines
        /// whether the opened BioImage should be added to the collection of images. If set to `true`,
        /// the BioImage will be added to the collection; if set to `false`, it will not be
        /// added.</param>
        /// <returns>
        /// The method is returning an array of BioImage objects.
        /// </returns>
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
                    status = "Opening OME Image.";
                    file = file.Replace("\\", "/");
                    reader.setId(file);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }

            bool tile = false;
            if (reader.getOptimalTileWidth() != reader.getSizeX())
                tile = true;
            int count = reader.getSeriesCount();
            BioImage[] bs = null;
            if (tile)
            {
                bs = new BioImage[1];
                bs[0] = OpenOME(file, 0, tab, addToImages, true, 0, 0, 1920, 1080);
                bs[0].isPyramidal = true;
                Images.AddImage(bs[0], tab);
                return bs;
            }
            else
                bs = new BioImage[count];
            reader.close();
            for (int i = 0; i < count; i++)
            {
                bs[i] = OpenOME(file, i, tab, addToImages, false, 0, 0, 0, 0);
                if (bs[i] == null)
                    return null;
            }
            return bs;
        }

        /// <summary>
        /// The OpenAsync function opens a file in a separate thread.
        /// </summary>
        /// <param name="file">The "file" parameter is a string that represents the file that needs to
        /// be opened asynchronously.</param>
        public static void OpenAsync(string file)
        {
            Thread t = new Thread(OpenThread);
            t.Name = file;
            t.Start();
        }

        /// <summary>
        /// The function OpenAsync takes an array of file names as input and opens each file
        /// asynchronously.
        /// </summary>
        /// <param name="files">The parameter "files" is an array of strings that represents the file
        /// names that need to be opened.</param>
        public static void OpenAsync(string[] files)
        {
            foreach (string file in files)
            {
                OpenAsync(file);
            }
        }

        /// <summary>
        /// The AddAsync function creates a new thread and starts it, passing in the file name as the
        /// thread's name.
        /// </summary>
        /// <param name="file">The file parameter is a string that represents the file that will be
        /// opened asynchronously.</param>
        public static void AddAsync(string file)
        {
            Thread t = new Thread(OpenThread);
            t.Name = file;
            t.Start();
        }

        /// <summary>
        /// The function `AddAsync` takes an array of file names and asynchronously opens each file.
        /// </summary>
        /// <param name="files">The parameter "files" is an array of strings that represents the file
        /// names that need to be processed.</param>
        public static void AddAsync(string[] files)
        {
            foreach (string file in files)
            {
                OpenAsync(file);
            }
        }

        /// <summary>
        /// The function "Open" opens a file.
        /// </summary>
        /// <param name="file">The "file" parameter is a string that represents the file path or file
        /// name that you want to open.</param>
        public static void Open(string file)
        {
            OpenFile(file);
        }

        /// <summary>
        /// The function "Open" takes an array of file names as input and opens each file.
        /// </summary>
        /// <param name="files">An array of strings representing the file names that need to be
        /// opened.</param>
        public static void Open(string[] files)
        {
            foreach (string file in files)
            {
                Open(file);
            }
        }

        /// <summary>
        /// The function "ImagesToStack" takes an array of file paths, opens each file, and combines
        /// them into a single BioImage stack.
        /// </summary>
        /// <param name="files">An array of file paths to the images that need to be converted into a
        /// stack.</param>
        /// <param name="tab">The "tab" parameter is a boolean value that determines whether the image
        /// files are in tab-delimited format or not. If it is set to true, it means the image files are
        /// in tab-delimited format. If it is set to false, it means the image files are not in
        /// tab-del</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
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
            return b;
        }
        /// <summary>
        /// The function "Update" takes a BioImage object as a parameter and updates it by opening the
        /// file specified in the object's "file" property.
        /// </summary>
        /// <param name="BioImage">The BioImage class is a data structure that represents an image file
        /// with associated metadata. It likely contains properties such as the file path, image
        /// dimensions, and other relevant information about the image.</param>
        public static void Update(BioImage b)
        {
            b = OpenFile(b.file);
        }
        /// The Update function calls the Update method on the current object.
        /// </summary>
        public void Update()
        {
            Update(this);
        }
        public static bool OmeSupport = false;
        static bool supportDialog = false;
        /// <summary>
        /// The function determines if the current operating system and architecture support OME (One
        /// More Thing) and returns a boolean value indicating the support.
        /// </summary>
        /// <returns>
        /// The method is returning a boolean value. If the condition `RuntimeInformation.OSArchitecture
        /// == Architecture.Arm64 && isMacOS` is true, then it returns `false`. Otherwise, it returns
        /// `true`.
        /// </returns>
        public static bool OMESupport()
        {
            bool isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            if (RuntimeInformation.OSArchitecture == Architecture.Arm64 && isMacOS)
            {
                OmeSupport = false;
                return false;
            }
            else
            {
                OmeSupport = true;
                return true;
            }
        }
        static NetVips.Image netim;
        /// <summary>
        /// The function checks if a given file is supported by the Vips library and returns true if it
        /// is, false otherwise.
        /// </summary>
        /// <param name="file">The "file" parameter is a string that represents the file path of the
        /// TIFF image that you want to load using the NetVips library.</param>
        /// <returns>
        /// The method is returning a boolean value. If the try block is executed successfully, it will
        /// return true. If an exception is caught, it will return false.
        /// </returns>
        public static bool VipsSupport(string file)
        {
            try
            {
                netim = NetVips.Image.Tiffload(file);
                Settings.AddSettings("VipsSupport", "true");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
            netim.Close();
            netim.Dispose();
            return true;
        }
        private static Stopwatch st = new Stopwatch();
        private static ServiceFactory factory;
        private static OMEXMLService service;
        private static ImageReader reader;
        private static ImageWriter writer;
        private loci.formats.meta.IMetadata meta;

        //We use UNIX type line endings since they are supported by ImageJ & Bio.
        public const char NewLine = '\n';
        public const string columns = "ROIID,ROINAME,TYPE,ID,SHAPEINDEX,TEXT,S,C,Z,T,X,Y,W,H,POINTS,STROKECOLOR,STROKECOLORW,FILLCOLOR,FONTSIZE\n";

        /// <summary>
        /// The OpenXML function opens a TIFF file and returns the value of the IMAGEDESCRIPTION field.
        /// </summary>
        /// <param name="file">The "file" parameter is a string that represents the file path of the
        /// TIFF image file that you want to open and extract information from.</param>
        /// <returns>
        /// The method is returning a string value.
        /// </returns>
        public static string OpenXML(string file)
        {
            if (!file.EndsWith(".tif"))
                return null;
            Tiff image = Tiff.Open(file, "r");
            FieldValue[] f = image.GetField(TiffTag.IMAGEDESCRIPTION);
            return f[0].ToString();
        }

        public static List<ROI> OpenOMEROIs(string file, int series)
        {
            if (!OMESupport())
                return null;
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
                }
            }

            imageReader.close();
            return Annotations;
        }

        /// <summary>
        /// The function takes a list of ROI objects and converts them to a string representation.
        /// </summary>
        /// <param name="Annotations">Annotations is a List of ROI objects.</param>
        /// <returns>
        /// The method is returning a string representation of a list of ROI objects.
        /// </returns>
        public static string ROIsToString(List<ROI> Annotations)
        {
            string s = "";
            for (int i = 0; i < Annotations.Count; i++)
            {
                s += ROIToString(Annotations[i]);
            }
            return s;
        }

        /// <summary>
        /// The function "ROIToString" converts a ROI object into a string representation.
        /// </summary>
        /// <param name="ROI">The ROI parameter is an object of type ROI. It contains information about
        /// a region of interest, such as its ID, name, type, coordinates, shape index, text, series,
        /// color, stroke width, fill color, and font size.</param>
        /// <returns>
        /// The method is returning a string that represents the properties of an ROI (Region of
        /// Interest) object.
        /// </returns>
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
        /// <summary>
        /// The function `StringToROI` takes a string as input and parses it to create an instance of
        /// the `ROI` class.
        /// </summary>
        /// <param name="sts">The parameter "sts" is a string that represents a line of data containing
        /// comma or tab-separated values. It is used to extract the values and assign them to the
        /// properties of the ROI object.</param>
        /// <returns>
        /// The method is returning an object of type ROI.
        /// </returns>
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

        /// <summary>
        /// The function exports a list of ROI annotations to a CSV file.
        /// </summary>
        /// <param name="filename">The filename parameter is a string that represents the name of the
        /// file that will be created or overwritten with the exported data.</param>
        /// <param name="Annotations">The "Annotations" parameter is a List of ROI objects. Each ROI
        /// object represents a region of interest in an image or a video frame.</param>
        public static void ExportROIsCSV(string filename, List<ROI> Annotations)
        {
            string con = columns;
            con += ROIsToString(Annotations);
            File.WriteAllText(filename, con);
        }

        /// <summary>
        /// The function ImportROIsCSV reads a CSV file containing ROI data and returns a list of ROI
        /// objects.
        /// </summary>
        /// <param name="filename">The filename parameter is a string that represents the name or path
        /// of the CSV file that contains the ROI data.</param>
        /// <returns>
        /// The method is returning a List of ROI objects.
        /// </returns>
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
            return list;
        }

        /// <summary>
        /// The function exports a folder of ROI files to CSV format.
        /// </summary>
        /// <param name="path">The path parameter is the directory path where the ROIs (Region of
        /// Interest) files are located.</param>
        /// <param name="filename">The `filename` parameter is a string that represents the name of the
        /// file that will be exported.</param>
        /// <returns>
        /// If the OMESupport() method returns false, then nothing is being returned. The method will
        /// exit and no further code will be executed.
        /// </returns>
        public static void ExportROIFolder(string path, string filename)
        {
            if (!OMESupport())
                return;
            string[] fs = Directory.GetFiles(path);
            int i = 0;
            foreach (string f in fs)
            {
                List<ROI> annotations = OpenOMEROIs(f, 0);
                string ff = Path.GetFileNameWithoutExtension(f);
                ExportROIsCSV(path + "//" + ff + "-" + i.ToString() + ".csv", annotations);
                i++;
            }
        }

        private static BioImage bstats = null;
        private static bool update = false;

        /// <summary>
        /// The AutoThreshold function calculates statistics and histograms for each channel of a
        /// BioImage object and assigns them to the corresponding channel's stats property.
        /// </summary>
        /// <param name="BioImage">The BioImage parameter represents an image object that contains
        /// multiple image buffers and channels. It is used to perform thresholding operations on the
        /// image data.</param>
        /// <param name="updateImageStats">The parameter "updateImageStats" is a boolean value that
        /// determines whether the image statistics should be updated or not. If it is set to true, the
        /// image statistics will be updated for each buffer. If it is set to false, the image
        /// statistics will only be updated if they are null.</param>
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
                        int ind = b.Coords[z, c, t];
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

        }

        /// <summary>
        /// The function AutoThreshold is a C# method that takes in two parameters, bstats and update,
        /// and calls another method with the same name and parameters.
        /// </summary>
        public static void AutoThreshold()
        {
            AutoThreshold(bstats, update);
        }

        /// <summary>
        /// The function AutoThresholdThread starts a new thread to run the AutoThreshold function on a
        /// BioImage object.
        /// </summary>
        /// <param name="BioImage">The BioImage parameter is an object that represents an image in a
        /// biological context. It likely contains information about the image, such as its dimensions,
        /// pixel values, and any associated metadata.</param>
        public static void AutoThresholdThread(BioImage b)
        {
            bstats = b;
            Thread th = new Thread(AutoThreshold);
            th.Start();
        }

        /// <summary>
        /// The function FindFocus takes a BioImage object, a channel number, and a time value as input,
        /// and returns the coordinate of the image with the highest focus quality in the specified
        /// channel and time.
        /// </summary>
        /// <param name="BioImage">The BioImage parameter represents an image object that contains
        /// multiple channels and time points. It has a property called Buffers, which is a 3D array
        /// that stores the pixel values of the image. The Coords property is a 3D array that stores the
        /// coordinates of each pixel in the image</param>
        /// <param name="Channel">The "Channel" parameter represents the specific channel of the
        /// BioImage that you want to analyze. It is used to access the corresponding buffer in the
        /// BioImage's "Buffers" array.</param>
        /// <param name="Time">The "Time" parameter represents the time point or frame number in a
        /// time-lapse sequence.</param>
        /// <returns>
        /// The method is returning the index of the buffer with the highest focus quality in the given
        /// BioImage.
        /// </returns>
        public static int FindFocus(BioImage im, int Channel, int Time)
        {
            long mf = 0;
            int fr = 0;
            List<double> dt = new List<double>();
            ZCT c = new ZCT(0, 0, 0);
            for (int i = 0; i < im.SizeZ; i++)
            {
                long f = CalculateFocusQuality(im.Buffers[im.Coords[i, Channel, Time]]);
                dt.Add(f);
                if (f > mf)
                {
                    mf = f;
                    fr = im.Coords[i, Channel, Time];
                }
            }
            return fr;
        }

        /// <summary>
        /// The function calculates the focus quality of a given bitmap image.
        /// </summary>
        /// <param name="Bitmap">The parameter `b` is a Bitmap object, which represents an
        /// image.</param>
        /// <returns>
        /// The method CalculateFocusQuality returns a long value.
        /// </returns>
        static long CalculateFocusQuality(Bitmap b)
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
                return sumOfSquares * b.SizeX * b.SizeY - sum * sum;
            }
        }


        static string openfile;
        static bool omes, tab, add;
        static int serie;
        static void OpenThread()
        {
            OpenFile(openfile, serie, tab, add);
        }
        static string savefile, saveid;
        static bool some;
        static int sserie;
        static void SaveThread()
        {
            if (omes)
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

        /// <summary>
        /// The Dispose function is used to release resources, such as buffers and channels, and remove
        /// the image from the Images collection.
        /// </summary>
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
            Images.RemoveImage(this);
        }

        /// <summary>
        /// The ToString function returns a string representation of the Filename and Volume Location.
        /// </summary>
        /// <returns>
        /// The method is returning a string representation of an object. The returned string consists
        /// of the Filename, followed by the Volume's Location coordinates in the format "(X, Y, Z)".
        /// </returns>
        public override string ToString()
        {
            return Filename.ToString() + ", (" + Volume.Location.X + ", " + Volume.Location.Y + ", " + Volume.Location.Z + ")";
        }

        
        /// <summary>
        /// The above function overloads the division operator (/) for the BioImage class, allowing
        /// division of each element in the BioImage's buffers by a float value.
        /// </summary>
        /// <param name="BioImage">BioImage is a class that represents an image. It contains a list of
        /// buffers, which are used to store the pixel data of the image.</param>
        /// <param name="b">The parameter "b" is a float value that is used to divide each element in
        /// the buffers of the BioImage object "a".</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
        public static BioImage operator /(BioImage a, float b)
        {
            for (int i = 0; i < a.Buffers.Count; i++)
            {
                a.Buffers[i] = a.Buffers[i] / b;
            }
            return a;
        }

        
        /// <summary>
        /// The function overloads the "+" operator for the BioImage class, allowing it to add a float
        /// value to each element in its list of buffers.
        /// </summary>
        /// <param name="BioImage">BioImage is a class that represents an image. It contains a list of
        /// buffers, which are used to store pixel data.</param>
        /// <param name="b">The parameter "b" is a float value that is being added to each element in
        /// the BioImage's Buffers list.</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
        public static BioImage operator +(BioImage a, float b)
        {
            for (int i = 0; i < a.Buffers.Count; i++)
            {
                a.Buffers[i] = a.Buffers[i] + b;
            }
            return a;
        }
        /// <summary>
        /// The above function overloads the subtraction operator for the BioImage class, subtracting a
        /// float value from each element in the Buffers list.
        /// </summary>
        /// <param name="BioImage">BioImage is a class that represents an image. It contains a list of
        /// buffers, which are used to store the pixel values of the image.</param>
        /// <param name="b">The parameter "b" is a float value that is subtracted from each element in
        /// the BioImage's Buffers list.</param>
        /// <returns>
        /// The BioImage object is being returned.
        /// </returns>
        public static BioImage operator -(BioImage a, float b)
        {
            for (int i = 0; i < a.Buffers.Count; i++)
            {
                a.Buffers[i] = a.Buffers[i] - b;
            }
            return a;
        }

        /// <summary>
        /// The function divides each buffer in a BioImage object by a ColorS object and returns the
        /// modified BioImage object.
        /// </summary>
        /// <param name="BioImage">BioImage is a class that represents an image. It contains a list of
        /// buffers, which are used to store pixel data for different color channels or image
        /// layers.</param>
        /// <param name="ColorS">ColorS is a data type that represents a color in the RGB color space.
        /// It typically contains three components: red, green, and blue, each represented by a
        /// floating-point value between 0 and 1.</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
        public static BioImage operator /(BioImage a, ColorS b)
        {
            for (int i = 0; i < a.Buffers.Count; i++)
            {
                a.Buffers[i] = a.Buffers[i] / b;
            }
            return a;
        }

        /// <summary>
        /// The function multiplies each buffer in a BioImage object by a ColorS object and returns the
        /// modified BioImage object.
        /// </summary>
        /// <param name="BioImage">BioImage is a class that represents an image. It contains a list of
        /// buffers, which are used to store pixel data for different color channels or image
        /// layers.</param>
        /// <param name="ColorS">ColorS is a data type that represents a color in the RGB color space.
        /// It typically contains three components: red, green, and blue, each represented by a
        /// floating-point value between 0 and 1.</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
        public static BioImage operator *(BioImage a, ColorS b)
        {
            for (int i = 0; i < a.Buffers.Count; i++)
            {
                a.Buffers[i] = a.Buffers[i] * b;
            }
            return a;
        }

        /// <summary>
        /// The function overloads the "+" operator to add a ColorS object to each buffer in a BioImage
        /// object.
        /// </summary>
        /// <param name="BioImage">BioImage is a class that represents an image. It contains a list of
        /// buffers, which are used to store pixel data for different color channels or image
        /// layers.</param>
        /// <param name="ColorS">ColorS is a class or struct representing a color in an image. It likely
        /// contains properties or fields for the red, green, blue, and alpha values of the
        /// color.</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
        public static BioImage operator +(BioImage a, ColorS b)
        {
            for (int i = 0; i < a.Buffers.Count; i++)
            {
                a.Buffers[i] = a.Buffers[i] + b;
            }
            return a;
        }

        /// <summary>
        /// The function subtracts a ColorS object from each element in a BioImage object's Buffers
        /// list.
        /// </summary>
        /// <param name="BioImage">BioImage is a class that represents an image. It contains a list of
        /// buffers, which are arrays of pixels that make up the image.</param>
        /// <param name="ColorS">ColorS is a data type that represents a color in the RGB color space.
        /// It typically contains three components: red, green, and blue, each represented by a
        /// floating-point value between 0 and 1.</param>
        /// <returns>
        /// The method is returning a BioImage object.
        /// </returns>
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
