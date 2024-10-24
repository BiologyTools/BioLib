using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using CSScripting;
using Gtk;
using AForge;

namespace BioLib
{
    public static class ImageJ
    {
        public static string ImageJPath;
        public static List<Process> processes = new List<Process>();
        private static Random rng = new Random();
        /// <summary>
        /// The function "RunMacro" runs a macro file with specified parameters using the ImageJ
        /// software.
        /// </summary>
        /// <param name="file">The "file" parameter is a string that represents the path to the macro
        /// file that you want to run. It should include the file extension (e.g., ".ijm" for ImageJ
        /// macro files).</param>
        /// <param name="param">The "param" parameter is a string that represents the additional
        /// parameters or arguments that you want to pass to the macro file specified by the "file"
        /// parameter. These parameters can be used by the macro file to modify its behavior or perform
        /// specific actions.</param>
        public static void RunMacro(string file, string param)
        {
            file.Replace("/", "\\");
            Process pr = new Process();
            pr.StartInfo.FileName = ImageJPath;
            pr.StartInfo.Arguments = "-macro " + file + " " + param;
            pr.Start();
            processes.Add(pr);
            //Recorder.AddLine("ImageJ.RunMacro(" + file + "," + '"' + param + '"' + ");");
        }

        /// <summary>
        /// The function `RunString` executes an ImageJ macro script with the given parameters, either
        /// in headless mode or with a graphical interface.
        /// </summary>
        /// <param name="con">The "con" parameter is a string that represents the content of a file. It
        /// will be written to a temporary file and used as input for the ImageJ macro.</param>
        /// <param name="param">The "param" parameter is a string that represents the additional
        /// arguments or parameters that you want to pass to the ImageJ application when running the
        /// macro. These arguments can be used to customize the behavior of the macro or provide input
        /// data to it.</param>
        /// <param name="headless">The "headless" parameter is a boolean value that determines whether
        /// the ImageJ process should run in headless mode or not. Headless mode means that the ImageJ
        /// interface will not be displayed, and the process will run in the background without any user
        /// interaction. If the "headless" parameter</param>
        public static void RunString(string con, string param, bool headless)
        {
            Process pr = new Process();
            pr.StartInfo.FileName = ImageJPath;
            string te = rng.Next(0, 9999999).ToString();
            string p = Path.GetDirectoryName(Environment.ProcessPath) + "/" + te;
            if (OperatingSystem.IsMacOS())
            {
                Console.WriteLine(p);
                pr.StartInfo.UseShellExecute = true;
            }
            File.WriteAllText(p, con);
            if (headless)
                pr.StartInfo.Arguments = "--headless -macro " + p + " " + param;
            else
                pr.StartInfo.Arguments = "-macro " + p + " " + param;
            pr.Start();
            string donedir = Path.GetDirectoryName(Environment.ProcessPath);
            donedir = donedir.Replace("\\", "/");
            File.Delete(Path.GetDirectoryName(Environment.ProcessPath) + "/done.txt");
            processes.Add(pr);
            do
            {
                if (File.Exists(donedir + "/done.txt"))
                {
                    do
                    {
                        try
                        {
                            File.Delete(donedir + "/done.txt");
                        }
                        catch (Exception)
                        {

                        }
                    } while (File.Exists(donedir + "/done.txt"));
                    pr.Kill();
                    break;
                }
            } while (!pr.HasExited);
            File.Delete(p);
        }

        /// <summary>
        /// The function "RunOnImage" takes a BioImage object, a string of commands, an index, and
        /// several boolean parameters, and performs various operations on the image.
        /// </summary>
        /// <param name="BioImage">The BioImage object represents an image file. It contains information
        /// about the image file such as the file path, filename, and ID.</param>
        /// <param name="con">The "con" parameter is a string that represents the ImageJ macro code that
        /// you want to run on the image. It is passed as an argument to the "run" function in the
        /// ImageJ macro language.</param>
        /// <param name="index">The index parameter is used to specify the index of the image series to
        /// be processed. It is used when opening multi-series images, such as multi-channel or
        /// time-lapse images, where each series represents a different set of images. By specifying the
        /// index, you can select a specific series to be processed</param>
        /// <param name="headless">The "headless" parameter is a boolean value that determines whether
        /// the image processing should be performed in headless mode. Headless mode means that the
        /// processing is done without any user interface or graphical display.</param>
        /// <param name="onTab">The "onTab" parameter determines whether the image processing should be
        /// performed on the current tab or on a new tab. If it is set to true, the processing will be
        /// done on the current tab. If it is set to false, a new tab will be created for the processed
        /// image.</param>
        /// <param name="bioformats">A boolean flag indicating whether to use Bio-Formats for importing
        /// and exporting the image. If set to true, Bio-Formats will be used. If set to false, the
        /// image will be opened and saved using the default ImageJ functions.</param>
        /// <param name="resultInNewTab">A boolean value indicating whether the result should be
        /// displayed in a new tab or not.</param>
        /// <returns>
        /// The method does not return any value. It is a void method.
        /// </returns>
        public static void RunOnImage(BioImage bi,string con, int index, bool headless, bool onTab, bool bioformats, bool resultInNewTab)
        {
            if (OperatingSystem.IsMacOS())
                bioformats = false;
            string filename = "";
            string dir = Path.GetDirectoryName(bi.file);
            dir = dir.Replace("\\", "/");
            if (bi.ID.EndsWith(".ome.tif"))
            {
                filename = Path.GetFileNameWithoutExtension(bi.ID);
                filename = filename.Remove(filename.Length - 4, 4);
            }
            else
                filename = Path.GetFileNameWithoutExtension(bi.ID);
            string file = dir + "/" + filename + "-temp.ome.tif";
            if(!bioformats)
                file = dir + "/" + filename + ".tif";
            string donepath = Path.GetDirectoryName(Environment.ProcessPath);
            donepath = donepath.Replace("\\", "/");
            string st =
            "run(\"Bio-Formats Importer\", \"open=" + dir + "/" + bi.ID + " autoscale color_mode=Default open_all_series display_rois rois_import=[ROI manager] view=Hyperstack stack_order=XYCZT\"); " + con +
            "run(\"Bio-Formats Exporter\", \"save=" + file + " export compression=Uncompressed\"); " +
            "dir = \"" + donepath + "\"" +
            "File.saveString(\"done\", dir + \"/done.txt\");";
            if (!bioformats)
                st =
                "open(getArgument); " + con +
                "saveAs(\"Tiff\",\"" + file + "\"); " +
                "dir = \"" + donepath + "\"" +
                "File.saveString(\"done\", dir + \"/done.txt\");";
            if (File.Exists(file) && bioformats)
                File.Delete(file);
            RunString(st, dir + "/" + bi.ID, headless);
            if (!File.Exists(file))
                return;
            
            string s = filename;
            if (bioformats)
                s += "-temp.ome.tif";
            else
                s += ".tif";
            string f = dir + "/" + s;
            f = f.Replace("\\", "/");
            string fn = filename + ".tif";
            if (bioformats)
                fn = filename + ".ome.tif";
            //If not in images we add it to a new tab.
            if (Images.GetImage(fn) == null)
            {
                BioImage bm = BioImage.OpenFile(f, index, false, false);
                bm.Filename = fn;
                bm.ID = fn;
                bm.file = dir + "/" + fn;
                Images.AddImage(bm);
            }
            else
            {
                BioImage b = BioImage.OpenFile(f, index, false, false);
                b.ID = bi.ID;
                b.Filename = bi.ID;
                b.file = dir + "/" + fn;
                Images.UpdateImage(b);
            }
            //If using bioformats we delete the temp file.
            if(bioformats)
            File.Delete(f);;
            //Recorder.AddLine("ImageJ.RunOnImage(\"" + con + "\"," + headless + "," + onTab + "," + bioformats + "," + resultInNewTab + ");");
        }

        /// <summary>
        /// The function "RunOnImage" takes in a BioImage object, a string, and several boolean values
        /// as parameters and executes a specific action based on those parameters.
        /// </summary>
        /// <param name="BioImage">The BioImage parameter is an object that represents an image in a
        /// bioinformatics context. It likely contains information about the image, such as its
        /// dimensions, pixel values, and metadata.</param>
        /// <param name="con">The "con" parameter is a string that represents the command to be executed
        /// on the image. It could be any valid ImageJ command or macro.</param>
        /// <param name="headless">The "headless" parameter is a boolean value that determines whether
        /// the image processing operation should be performed in headless mode. Headless mode means
        /// that the operation will be performed without any graphical user interface (GUI) being
        /// displayed.</param>
        /// <param name="onTab">The "onTab" parameter determines whether the image processing should be
        /// performed on the current active tab or on a new tab. If "onTab" is set to true, the
        /// processing will be performed on the current active tab. If "onTab" is set to false, a new
        /// tab will</param>
        /// <param name="bioformats">The "bioformats" parameter is a boolean value that determines
        /// whether to use the Bio-Formats plugin for opening the image. If set to true, the Bio-Formats
        /// plugin will be used to open the image, allowing for support of various file formats. If set
        /// to false, the default image opening</param>
        /// <param name="resultInNewTab">A boolean value indicating whether the result should be
        /// displayed in a new tab or not.</param>
        public static void RunOnImage(BioImage b, string con, bool headless, bool onTab, bool bioformats, bool resultInNewTab)
        {
            RunOnImage(b, con,0,headless,onTab,bioformats,resultInNewTab);
            //Recorder.AddLine("ImageJ.RunOnImage(\"" + con + "\"," + 0 + "," + headless + "," + onTab + "," + bioformats + "," + resultInNewTab + ");");
        }

        /// <summary>
        /// The Initialize function sets the ImageJPath variable to the specified path.
        /// </summary>
        /// <param name="path">The path parameter is a string that represents the file path where the
        /// ImageJ software is located.</param>
        public static void Initialize(string path)
        {
            ImageJPath = path;
        }

        public class RoiDecoder
        {
            #region Params
            // offsets
            public static int VERSION_OFFSET = 4;
            public static int TYPE = 6;
            public static int TOP = 8;
            public static int LEFT = 10;
            public static int BOTTOM = 12;
            public static int RIGHT = 14;
            public static int N_COORDINATES = 16;
            public static int X1 = 18;
            public static int Y1 = 22;
            public static int X2 = 26;
            public static int Y2 = 30;
            public static int XD = 18;
            public static int YD = 22;
            public static int WIDTHD = 26;
            public static int HEIGHTD = 30;
            public static int SIZE = 18;
            public static int STROKE_WIDTH = 34;
            public static int SHAPE_ROI_SIZE = 36;
            public static int STROKE_COLOR = 40;
            public static int FILL_COLOR = 44;
            public static int SUBTYPE = 48;
            public static int OPTIONS = 50;
            public static int ARROW_STYLE = 52;
            public static int FLOAT_PARAM = 52; //ellipse ratio or rotated rect width
            public static int POINT_TYPE = 52;
            public static int ARROW_HEAD_SIZE = 53;
            public static int ROUNDED_RECT_ARC_SIZE = 54;
            public static int POSITION = 56;
            public static int HEADER2_OFFSET = 60;
            public static int COORDINATES = 64;
            // header2 offsets
            public static int C_POSITION = 4;
            public static int Z_POSITION = 8;
            public static int T_POSITION = 12;
            public static int NAME_OFFSET = 16;
            public static int NAME_LENGTH = 20;
            public static int OVERLAY_LABEL_COLOR = 24;
            public static int OVERLAY_FONT_SIZE = 28; //short
            public static int GROUP = 30;  //byte
            public static int IMAGE_OPACITY = 31;  //byte
            public static int IMAGE_SIZE = 32;  //int
            public static int FLOAT_STROKE_WIDTH = 36;  //float
            public static int ROI_PROPS_OFFSET = 40;
            public static int ROI_PROPS_LENGTH = 44;
            public static int COUNTERS_OFFSET = 48;

            // subtypes
            public static int TEXT = 1;
            public static int ARROW = 2;
            public static int ELLIPSE = 3;
            public static int IMAGE = 4;
            public static int ROTATED_RECT = 5;

            // options
            public static int SPLINE_FIT = 1;
            public static int DOUBLE_HEADED = 2;
            public static int OUTLINE = 4;
            public static int OVERLAY_LABELS = 8;
            public static int OVERLAY_NAMES = 16;
            public static int OVERLAY_BACKGROUNDS = 32;
            public static int OVERLAY_BOLD = 64;
            public static int SUB_PIXEL_RESOLUTION = 128;
            public static int DRAW_OFFSET = 256;
            public static int ZERO_TRANSPARENT = 512;
            public static int SHOW_LABELS = 1024;
            public static int SCALE_LABELS = 2048;
            public static int PROMPT_BEFORE_DELETING = 4096; //points
            public static int SCALE_STROKE_WIDTH = 8192;

            // types
            private int polygon = 0, rect = 1, oval = 2, line = 3, freeline = 4, polyline = 5, noRoi = 6,
                freehand = 7, traced = 8, angle = 9, point = 10;

            private byte[] data;
            private string path;
            private MemoryStream ins;
            private string name;
            private int size;
            #endregion

            /** Constructs an RoiDecoder using a file path. */
            public RoiDecoder(string path)
            {
                this.path = path;
            }

            /* The above code is defining a constructor for a class called RoiDecoder in C#. The
            constructor takes in two parameters: a byte array called bytes and a string called name. */
            public RoiDecoder(byte[] bytes, string name)
            {
                ins = new MemoryStream(bytes);
                this.name = name;
                this.size = bytes.Length;
            }

            /// <summary>
            /// The function "open" takes a file path and a BioImage object as input, decodes the ROI
            /// from the file at the given path, and returns the ROI.
            /// </summary>
            /// <param name="path">The path parameter is a string that represents the file path of the
            /// ROI file that you want to open.</param>
            /// <param name="BioImage">The BioImage parameter is an object that represents an image in a
            /// biological context. It likely contains information about the image, such as its
            /// dimensions, pixel values, and any associated metadata.</param>
            /// <returns>
            /// The method is returning an instance of the BioGTK.ROI class.
            /// </returns>
            public static BioLib.ROI open(string path, BioImage image)
            {
                BioLib.ROI roi = null;
                RoiDecoder rd = new RoiDecoder(path);
                roi = rd.getRoi(image);
                return roi;
            }

            /// <summary>
            /// The function `getRoi` reads and parses an ImageJ ROI file and returns a `BioGTK.ROI`
            /// object representing the region of interest.
            /// </summary>
            /// <param name="BioImage">The `BioImage` parameter represents an image object in the BioGTK
            /// library.</param>
            /// <returns>
            /// The method is returning an instance of the BioGTK.ROI class.
            /// </returns>
            public BioLib.ROI getRoi(BioImage bi)
            {
                BioLib.ROI roi = new BioLib.ROI();
                data = File.ReadAllBytes(path);
                size = data.Length;
                if (getByte(0) != 73 || getByte(1) != 111)  //"Iout"
                    throw new IOException("This is not an ImageJ ROI");
                int version = getShort(VERSION_OFFSET);
                int type = getByte(TYPE);
                int subtype = getShort(SUBTYPE);
                int top = getShort(TOP);
                int left = getShort(LEFT);
                int bottom = getShort(BOTTOM);
                int right = getShort(RIGHT);
                int width = right - left;
                int height = bottom - top;
                int n = getUnsignedShort(N_COORDINATES);
                if (n == 0)
                    n = getInt(SIZE);
                int options = getShort(OPTIONS);
                int position = getInt(POSITION);
                int hdr2Offset = getInt(HEADER2_OFFSET);
                int channel = 0, slice = 0, frame = 0;
                int overlayLabelColor = 0;
                int overlayFontSize = 0;
                int group = 0;
                int imageOpacity = 0;
                int imageSize = 0;
                bool subPixelResolution = (options & SUB_PIXEL_RESOLUTION) != 0 && version >= 222;
                bool drawOffset = subPixelResolution && (options & DRAW_OFFSET) != 0;
                bool scaleStrokeWidth = true;
                if (version >= 228)
                    scaleStrokeWidth = (options & SCALE_STROKE_WIDTH) != 0;

                bool subPixelRect = version >= 223 && subPixelResolution && (type == rect || type == oval);
                double xd = 0.0, yd = 0.0, widthd = 0.0, heightd = 0.0;
                if (subPixelRect) {
                    xd = getFloat(XD);
                    yd = getFloat(YD);
                    widthd = getFloat(WIDTHD);
                    heightd = getFloat(HEIGHTD);
                    roi.subPixel = true;
                }

                if (hdr2Offset > 0 && hdr2Offset + IMAGE_SIZE + 4 <= size)
                {
                    channel = getInt(hdr2Offset + C_POSITION);
                    slice = getInt(hdr2Offset + Z_POSITION);
                    frame = getInt(hdr2Offset + T_POSITION);
                    overlayLabelColor = getInt(hdr2Offset + OVERLAY_LABEL_COLOR);
                    overlayFontSize = getShort(hdr2Offset + OVERLAY_FONT_SIZE);
                    imageOpacity = getByte(hdr2Offset + IMAGE_OPACITY);
                    imageSize = getInt(hdr2Offset + IMAGE_SIZE);
                    group = getByte(hdr2Offset + GROUP);
                }

                if (name != null && name.EndsWith(".roi"))
                    name = name.Substring(0, name.Length - 4);
                bool isComposite = getInt(SHAPE_ROI_SIZE) > 0;


                /*
                if (isComposite)
                {
                    roi = getShapeRoi();
                    if (version >= 218)
                        getStrokeWidthAndColor(roi, hdr2Offset, scaleStrokeWidth);
                    roi.coord.Z = position;
                    if (channel > 0 || slice > 0 || frame > 0)
                    {
                        roi.coord.C = channel; roi.coord.Z = slice; roi.coord.T = frame;
                    }
                    decodeOverlayOptions(roi, version, options, overlayLabelColor, overlayFontSize);
                    if (version >= 224)
                    {
                        string props = getRoiProps();
                        if (props != null)
                            roi.properties = props;
                    }
                    if (version >= 228 && group > 0)
                        roi.serie = group;
                    return roi;
                }
                */
                switch (type)
                {
                    case 1: //Rect
                        if (subPixelRect)
                            roi = BioLib.ROI.CreateRectangle(new AForge.ZCT(slice-1, channel - 1, frame - 1), xd, yd, widthd, heightd);
                        else
                            roi = BioLib.ROI.CreateRectangle(new AForge.ZCT(slice - 1, channel - 1, frame - 1), left, top, width, height);
                        int arcSize = getShort(ROUNDED_RECT_ARC_SIZE);
                        if (arcSize > 0)
                            throw new NotSupportedException("Type rounded rectangle not supported.");
                        break;
                    case 2: //Ellipse
                        if (subPixelRect)
                            roi = BioLib.ROI.CreateEllipse(new AForge.ZCT(slice - 1, channel - 1, frame - 1), xd, yd, widthd, heightd);
                        else
                            roi = BioLib.ROI.CreateEllipse(new AForge.ZCT(slice - 1, channel - 1, frame - 1), left, top, width, height);
                        break;
                    case 3: //Line
                        float x1 = getFloat(X1);
                        float y1 = getFloat(Y1);
                        float x2 = getFloat(X2);
                        float y2 = getFloat(Y2);

                        if (subtype == ARROW)
                        {
                            throw new NotSupportedException("Type arrow not supported.");
                            /*
                            roi = new Arrow(x1, y1, x2, y2);
                            ((Arrow)roi).setDoubleHeaded((options & DOUBLE_HEADED) != 0);
                            ((Arrow)roi).setOutline((options & OUTLINE) != 0);
                            int style = getByte(ARROW_STYLE);
                            if (style >= Arrow.FILLED && style <= Arrow.BAR)
                                ((Arrow)roi).setStyle(style);
                            int headSize = getByte(ARROW_HEAD_SIZE);
                            if (headSize >= 0 && style <= 30)
                                ((Arrow)roi).setHeadSize(headSize);
                            */
                        }
                        else
                        {
                            roi = ROI.CreateLine(new AForge.ZCT(slice, channel, frame), new AForge.PointD(x1, y1), new AForge.PointD(x2, y2));
                            //roi.setDrawOffset(drawOffset);
                        }

                        break;
                    case 0:
                    case 5:
                    case 6:
                    case 7:
                    case 8:
                    case 9:
                    case 10:
                        //IJ.log("type: "+type);
                        //IJ.log("n: "+n);
                        //ij.IJ.log("rect: "+left+","+top+" "+width+" "+height);
                        if (n == 0 || n < 0) break;
                        int[] x = new int[n];
                        int[] y = new int[n];
                        float[] xf = null;
                        float[] yf = null;
                        int base1 = COORDINATES;
                        int base2 = base1 + 2 * n;
                        int xtmp, ytmp;
                        for (int i = 0; i < n; i++)
                        {
                            xtmp = getShort(base1 + i * 2);
                            if (xtmp < 0) xtmp = 0;
                            ytmp = getShort(base2 + i * 2);
                            if (ytmp < 0) ytmp = 0;
                            x[i] = left + xtmp;
                            y[i] = top + ytmp;
                        }
                        if (subPixelResolution)
                        {
                            xf = new float[n];
                            yf = new float[n];
                            base1 = COORDINATES + 4 * n;
                            base2 = base1 + 4 * n;
                            for (int i = 0; i < n; i++)
                            {
                                xf[i] = getFloat(base1 + i * 4);
                                yf[i] = getFloat(base2 + i * 4);
                            }
                        }
                        if (type == point)
                        {
                            //TODO implement non subpizel ROI
                            if (subPixelResolution)
                            {
                                roi.AddPoints(xf, yf);
                            }
                            else
                                roi.AddPoints(x, y);
                            if (version >= 226)
                            {
                                //((PointRoi)roi).setPointType(getByte(POINT_TYPE));
                                roi.strokeWidth = getShort(STROKE_WIDTH);
                            }
                            //if ((options & SHOW_LABELS) != 0 && !ij.Prefs.noPointLabels)
                            //    ((PointRoi)roi).setShowLabels(true);
                            //if ((options & PROMPT_BEFORE_DELETING) != 0)
                            //    ((PointRoi)roi).promptBeforeDeleting(true);
                            roi.type = ROI.Type.Point;
                            break;
                        }
                        if (type == polygon)
                            roi.type = ROI.Type.Polygon;
                        else if (type == freehand)
                        {
                            roi.type = ROI.Type.Freeform;
                            if (subtype == ELLIPSE || subtype == ROTATED_RECT)
                            {
                                throw new NotSupportedException("ROI type not supported.");
                                /*
                                double ex1 = getFloat(X1);
                                double ey1 = getFloat(Y1);
                                double ex2 = getFloat(X2);
                                double ey2 = getFloat(Y2);
                                double param = getFloat(FLOAT_PARAM);
                                if (subtype == ROTATED_RECT)
                                    roi = new RotatedRectRoi(ex1, ey1, ex2, ey2, param);
                                else
                                    roi = new EllipseRoi(ex1, ey1, ex2, ey2, param);
                                break;
                                */
                            }
                        }
                        else if (type == traced)
                            roi.type = ROI.Type.Polyline;
                        else if (type == polyline)
                            roi.type = ROI.Type.Polyline;
                        else if (type == freeline)
                            roi.type = ROI.Type.Polyline;
                        else if (type == angle)
                            roi.type = ROI.Type.Point;
                        else
                            roi.type = ROI.Type.Freeform;
                        if (subPixelResolution)
                        {
                            roi.AddPoints(xf, yf);
                            //roi = new PolygonRoi(xf, yf, n, roiType);
                            //roi.setDrawOffset(drawOffset);
                        }
                        else
                            roi.AddPoints(x, y);
                        break;
                    default:
                        throw new IOException("Unrecognized ROI type: " + type);
                }
                if (roi == null)
                    return null;
                roi.roiName = getRoiName();

                // read stroke width, stroke color and fill color (1.43i or later)
                if (version >= 218)
                {
                    getStrokeWidthAndColor(roi, hdr2Offset, scaleStrokeWidth);
                    /*
                    if (type == point)
                        roi.setStrokeWidth(0);
                    bool splineFit = (options & SPLINE_FIT) != 0;
                    if (splineFit && roi instanceof PolygonRoi)
				            ((PolygonRoi)roi).fitSpline();
                    */
                }

                if (version >= 218 && subtype == TEXT)
                {
                    getTextRoi(roi, version);
                    roi.type = ROI.Type.Label;
                }
                /*
                if (version >= 221 && subtype == IMAGE)
                    roi = getImageRoi(roi, imageOpacity, imageSize, options);
                
                if (version >= 224)
                {
                    string props = getRoiProps();
                    if (props != null)
                        roi.setProperties(props);
                }

                if (version >= 227)
                {
                    int[] counters = getPointCounters(n);
                    if (counters != null && (roi instanceof PointRoi))
				            ((PointRoi)roi).setCounters(counters);
                }
                */
                // set group (1.52t or later)
                if (version >= 228 && group > 0)
                    roi.serie = group;

                roi.coord.Z = position;
                if (channel > 0 || slice > 0 || frame > 0)
                    roi.coord = new AForge.ZCT(slice - 1, channel - 1, frame - 1); //-1 because our ROI coordinates are 0 based
                //decodeOverlayOptions(roi, version, options, overlayLabelColor, overlayFontSize);

                //We convert pixel to subpixel
                if (!roi.subPixel)
                {
                    for (int i = 0; i < roi.PointsD.Count; i++)
                    {
                        AForge.PointD pd = bi.ToStageSpace(roi.PointsD[i]);
                        roi.PointsD[i] = pd;
                        roi.UpdateBoundingBox();
                    }
                }
                if (roi.type == ROI.Type.Polygon || roi.type == ROI.Type.Freeform)
                    roi.closed = true;
                return roi;
            }
            /*
            void decodeOverlayOptions(BioGTK.ROI roi, int version, int options, int color, int fontSize)
            {
                
                Overlay proto = new Overlay();
                proto.drawLabels((options & OVERLAY_LABELS) != 0);
                proto.drawNames((options & OVERLAY_NAMES) != 0);
                proto.drawBackgrounds((options & OVERLAY_BACKGROUNDS) != 0);
                if (version >= 220 && color != 0)
                    proto.setLabelColor(new Color(color));
                bool bold = (options & OVERLAY_BOLD) != 0;
                bool scalable = (options & SCALE_LABELS) != 0;
                if (fontSize > 0 || bold || scalable)
                {
                    proto.setLabelFont(new Font("SansSerif", bold ? Font.BOLD : Font.PLAIN, fontSize), scalable);
                }
                roi.setPrototypeOverlay(proto);
                
            }
            */
            /// <summary>
            /// The function "getStrokeWidthAndColor" sets the stroke width and color properties of a
            /// BioGTK.ROI object based on values retrieved from a data source.
            /// </summary>
            /// <param name="roi">The "roi" parameter is an object of type BioGTK.ROI, which represents
            /// a region of interest. It likely contains properties such as "strokeWidth",
            /// "strokeColor", and "fillColor" that need to be set based on the values obtained from
            /// other methods.</param>
            /// <param name="hdr2Offset">The `hdr2Offset` parameter is an integer value that represents
            /// the offset in bytes from the start of the `BioGTK.ROI` object to the location of the
            /// stroke width value in the object's header.</param>
            /// <param name="scaleStrokeWidth">The parameter "scaleStrokeWidth" is a boolean variable
            /// that determines whether the stroke width should be scaled or not. If it is set to true,
            /// the stroke width will be scaled according to some predefined rules. If it is set to
            /// false, the stroke width will not be scaled.</param>
            void getStrokeWidthAndColor(BioLib.ROI roi, int hdr2Offset, bool scaleStrokeWidth)
            {
                double strokeWidth = getShort(STROKE_WIDTH);
                if (hdr2Offset > 0)
                {
                    double strokeWidthD = getFloat(hdr2Offset + FLOAT_STROKE_WIDTH);
                    if (strokeWidthD > 0.0)
                        strokeWidth = strokeWidthD;
                }
                if (strokeWidth > 0.0)
                {
                    roi.strokeWidth = strokeWidth;
                }
                int strokeColor = getInt(STROKE_COLOR);
                if (strokeColor != 0)
                {
                    byte[] bts = BitConverter.GetBytes(strokeColor);
                    AForge.Color c = AForge.Color.FromArgb(bts[0], bts[1], bts[2], bts[3]);
                    roi.strokeColor = c;
                }
                int fillColor = getInt(FILL_COLOR);
                if (fillColor != 0)
                {
                    byte[] bts = BitConverter.GetBytes(strokeColor);
                    AForge.Color c = AForge.Color.FromArgb(bts[0], bts[1], bts[2], bts[3]);
                    roi.fillColor = c;
                }
            }
            /*
            public BioGTK.ROI getShapeRoi()
            {
		        int type = getByte(TYPE);
		        if (type!=rect)
			        throw new NotSupportedException("Invalid composite ROI type");
                int top = getShort(TOP);
                int left = getShort(LEFT);
                int bottom = getShort(BOTTOM);
                int right = getShort(RIGHT);
                int width = right - left;
                int height = bottom - top;
                int n = getInt(SHAPE_ROI_SIZE);

                BioGTK.ROI roi = new ROI();
                float[] shapeArray = new float[n];
                int bas = COORDINATES;
                for (int i = 0; i < n; i++)
                {
                    shapeArray[i] = getFloat(bas);
                    bas += 4;
                }
                roi = new ShapeRoi(shapeArray);
                roi.setName(getRoiName());
                return roi;
            }
	        */
            /// <summary>
            /// The function `getTextRoi` extracts information from a BioGTK.ROI object and assigns it
            /// to various properties of the object.
            /// </summary>
            /// <param name="roi">The `roi` parameter is an object of type `BioGTK.ROI`, which
            /// represents a region of interest in an image. It contains information about the bounding
            /// box, text, font style, and other properties of the region.</param>
            /// <param name="version">The version parameter is an integer that represents the version of
            /// the software being used. It is used to determine whether to read the angle value or not.
            /// If the version is greater than or equal to 225, the angle value is read from the header;
            /// otherwise, it is set to 0.</param>
            void getTextRoi(BioLib.ROI roi, int version)
            {
                AForge.Rectangle r = roi.BoundingBox.ToRectangleInt();
                int hdrSize = 64;
                int size = getInt(hdrSize);
                int styleAndJustification = getInt(hdrSize + 4);
                int style = styleAndJustification & 255;
                int justification = (styleAndJustification >> 8) & 3;
                bool drawStringMode = (styleAndJustification & 1024) != 0;
                int nameLength = getInt(hdrSize + 8);
                int textLength = getInt(hdrSize + 12);
                char[] name = new char[nameLength];
                char[] text = new char[textLength];
                for (int i = 0; i < nameLength; i++)
                    name[i] = (char)getShort(hdrSize + 16 + i * 2);
                for (int i = 0; i < textLength; i++)
                    text[i] = (char)getShort(hdrSize + 16 + nameLength * 2 + i * 2);
                double angle = version >= 225 ? getFloat(hdrSize + 16 + nameLength * 2 + textLength * 2) : 0f;
                //Font font = new Font(new string(name), style, size);
                roi.family = new string(name);
                roi.Text = new string(text);
                roi.slant = Cairo.FontSlant.Normal;
                roi.weight = Cairo.FontWeight.Normal;
                roi.fontSize = size;
                /*
                if (roi.subPixel)
                {
                    RectangleD fb = roi.Rect;
                    roi2 = new TextRoi(fb.getX(), fb.getY(), fb.getWidth(), fb.getHeight(), new string(text), font);
                }
                else
                    roi2 = new TextRoi(r.x, r.y, r.width, r.height, new string(text), font);

                roi.strokeColor
                roi2.setFillColor(roi.getFillColor());
                roi2.setName(getRoiName());
                roi2.setJustification(justification);
                roi2.setDrawStringMode(drawStringMode);
                roi2.setAngle(angle);
                return roi2;
                */
            }

            /// <summary>
            /// The function `getRoiName` retrieves the name of a region of interest (ROI) from a file.
            /// </summary>
            /// <returns>
            /// a string, which is the name of the ROI (Region of Interest).
            /// </returns>
            string getRoiName()
            {
                string fileName = name;
                int hdr2Offset = getInt(HEADER2_OFFSET);
                if (hdr2Offset == 0)
                    return fileName;
                int offset = getInt(hdr2Offset + NAME_OFFSET);
                int Length = getInt(hdr2Offset + NAME_LENGTH);
                if (offset == 0 || Length == 0)
                    return fileName;
                if (offset + Length * 2 > size)
                    return fileName;
                char[] namem = new char[Length];
                for (int i = 0; i < Length; i++)
                    namem[i] = (char)getShort(offset + i * 2);
                return new string(namem);
            }

            /// <summary>
            /// The function "getRoiProps" retrieves ROI properties from a specific offset in a given
            /// data structure.
            /// </summary>
            /// <returns>
            /// The method is returning a string that represents the ROI (Region of Interest)
            /// properties.
            /// </returns>
            string getRoiProps()
            {
                int hdr2Offset = getInt(HEADER2_OFFSET);
                if (hdr2Offset == 0)
                    return null;
                int offset = getInt(hdr2Offset + ROI_PROPS_OFFSET);
                int Length = getInt(hdr2Offset + ROI_PROPS_LENGTH);
                if (offset == 0 || Length == 0)
                    return null;
                if (offset + Length * 2 > size)
                    return null;
                char[] props = new char[Length];
                for (int i = 0; i < Length; i++)
                    props[i] = (char)getShort(offset + i * 2);
                return new string(props);
            }

            /// <summary>
            /// The function "getPointCounters" returns an array of integers representing point
            /// counters, based on a given input value.
            /// </summary>
            /// <param name="n">The parameter "n" represents the number of counters to retrieve.</param>
            /// <returns>
            /// The method is returning an array of integers.
            /// </returns>
            int[] getPointCounters(int n)
            {
                int hdr2Offset = getInt(HEADER2_OFFSET);
                if (hdr2Offset == 0)
                    return null;
                int offset = getInt(hdr2Offset + COUNTERS_OFFSET);
                if (offset == 0)
                    return null;
                if (offset + n * 4 > data.Length)
                    return null;
                int[] counters = new int[n];
                for (int i = 0; i < n; i++)
                    counters[i] = getInt(offset + i * 4);
                return counters;
            }


            /// <summary>
            /// The function "getByte" returns the value of the byte at the specified index in the
            /// "data" array.
            /// </summary>
            /// <param name="bas">The parameter "bas" is an integer that represents the base index of
            /// the data array.</param>
            /// <returns>
            /// the value of the byte at the specified index in the "data" array.
            /// </returns>
            int getByte(int bas)
            {
                return data[bas] & 255;
            }

            /// <summary>
            /// The function "getShort" takes an integer "bas" as input and returns a short value by
            /// combining two bytes from a data array.
            /// </summary>
            /// <param name="bas">The parameter "bas" is an integer that represents the base index of
            /// the data array.</param>
            /// <returns>
            /// an integer value.
            /// </returns>
            int getShort(int bas)
            {
                int b0 = data[bas] & 255;
                int b1 = data[bas + 1] & 255;
                int n = (short)((b0 << 8) + b1);
                if (n < -5000)
                    n = (b0 << 8) + b1; // assume n>32767 and unsigned
                return n;
            }

            /// <summary>
            /// The function "getUnsignedShort" takes an integer "bas" as input and returns the unsigned
            /// short value obtained by combining two bytes from the "data" array.
            /// </summary>
            /// <param name="bas">The parameter "bas" is an integer that represents the base index of
            /// the data array.</param>
            /// <returns>
            /// an unsigned short value.
            /// </returns>
            int getUnsignedShort(int bas)
            {
                int b0 = data[bas] & 255;
                int b1 = data[bas + 1] & 255;
                return (b0 << 8) + b1;
            }

            /// <summary>
            /// The function takes an index as input and returns an integer value by combining four
            /// bytes from a data array.
            /// </summary>
            /// <param name="bas">The parameter "bas" is an integer that represents the base index of
            /// the data array.</param>
            /// <returns>
            /// an integer value.
            /// </returns>
            int getInt(int bas)
            {
                int b0 = data[bas] & 255;
                int b1 = data[bas + 1] & 255;
                int b2 = data[bas + 2] & 255;
                int b3 = data[bas + 3] & 255;
                return ((b0 << 24) + (b1 << 16) + (b2 << 8) + b3);
            }

            /// <summary>
            /// The function "getFloat" converts an integer to a floating-point number.
            /// </summary>
            /// <param name="bas">The parameter "bas" is an integer value that is used as the base for
            /// converting the integer value to a floating-point value.</param>
            /// <returns>
            /// a float value.
            /// </returns>
            float getFloat(int bas)
            {
                return BitConverter.Int32BitsToSingle(getInt(bas));
            }

            /// <summary>
            /// The function "openFromByteArray" takes a BioImage object and a byte array as input, and
            /// returns a BioGTK.ROI object by decoding the byte array using a RoiDecoder.
            /// </summary>
            /// <param name="BioImage">The BioImage parameter is an object of type BioImage, which is
            /// likely a class representing an image in a biological context. It could contain
            /// properties and methods related to image processing and analysis.</param>
            /// <param name="bytes">The "bytes" parameter is a byte array that contains the data of a
            /// serialized BioGTK.ROI object.</param>
            /// <returns>
            /// The method is returning an instance of the BioGTK.ROI class.
            /// </returns>
            public static BioLib.ROI openFromByteArray(BioImage bi,byte[] bytes)
            {
                BioLib.ROI roi = null;
                if (bytes == null || bytes.Length == 0)
                    return roi;
                try
                {
                    RoiDecoder decoder = new RoiDecoder(bytes, null);
                    roi = decoder.getRoi(bi);
                }
                catch (IOException e)
                {
                    return null;
                }
                return roi;
            }

        }

        /// <summary>
        /// The function `GetImageJType` takes in a `ROI` object and returns an integer representing the
        /// type of the ROI in ImageJ.
        /// </summary>
        /// <param name="ROI">The ROI parameter is an object of type ROI, which represents a region of
        /// interest in an image. It contains information about the type of ROI, such as rectangle,
        /// point, line, polygon, etc. The GetImageJType method is used to convert the ROI type into an
        /// integer value that corresponds</param>
        /// <returns>
        /// The method is returning an integer value that represents the ImageJ type of the given ROI.
        /// </returns>
        static int GetImageJType(ROI roi)
        {
            //private int polygon = 0, rect = 1, oval = 2, line = 3, freeline = 4, polyline = 5, noRoi = 6, freehand = 7,
            //    traced = 8, angle = 9, point = 10;
            switch (roi.type)
            {
                case ROI.Type.Rectangle:
                    return 1;
                case ROI.Type.Point:
                    return 10;
                case ROI.Type.Line:
                    return 3;
                case ROI.Type.Polygon:
                    return 0;
                case ROI.Type.Polyline:
                    return 5;
                case ROI.Type.Freeform:
                    return 7;
                case ROI.Type.Ellipse:
                    return 2;
                case ROI.Type.Label:
                default:
                    return 0;
            }
        }

        /// <summary>
        /// The function "GetPointsXY" takes a BioImage and ROI as input and returns the x and y
        /// coordinates of the points in the ROI.
        /// </summary>
        /// <param name="BioImage">The BioImage parameter is an object that represents an image in a
        /// biological context. It likely contains information about the image, such as pixel values,
        /// dimensions, and metadata.</param>
        /// <param name="ROI">ROI is an object that represents a region of interest in an image. It
        /// contains a list of points (PointsD) that define the boundary of the region.</param>
        /// <param name="xp">An output parameter that will contain the x-coordinates of the points in
        /// the ROI.</param>
        /// <param name="yp">The `yp` parameter is an output parameter of type `int[]`. It is used to
        /// store the Y-coordinates of the points in the `roi` object after converting them to image
        /// space using the `bi` object.</param>
        static void GetPointsXY(BioImage bi,ROI roi, out int[] xp, out int[] yp)
        {
            int[] x = new int[roi.PointsD.Count];
            int[] y = new int[roi.PointsD.Count];
            for (int i = 0; i < roi.PointsD.Count; i++)
            {
                PointD pd = bi.ToImageSpace(roi.PointsD[i]);
                x[i] = (int)pd.X;
                y[i] = (int)pd.Y;
            }
            xp = x;
            yp = y;

        }

        /// <summary>
        /// The function "GetXY" takes a BioImage and ROI as input and returns the X and Y coordinates
        /// of the ROI in the image space.
        /// </summary>
        /// <param name="BioImage">The BioImage parameter is an object that represents an image in a
        /// biological context. It likely contains information about the image, such as pixel data,
        /// dimensions, and metadata.</param>
        /// <param name="ROI">The ROI parameter is an object of type ROI, which represents a region of
        /// interest in an image. It contains properties such as X and Y, which represent the
        /// coordinates of the top-left corner of the ROI.</param>
        /// <param name="x">An output parameter that will store the X-coordinate of the ROI in the image
        /// space.</param>
        /// <param name="y">The "y" parameter is an output parameter of type float. It is used to store
        /// the y-coordinate of a point in image space.</param>
        static void GetXY(BioImage bi, ROI roi,out float x, out float y)
        {
            PointD pd = bi.ToImageSpace(new PointD(roi.X,roi.Y));
            x = (float)pd.X;
            y = (float)pd.Y;
        }
        /// <summary>
        /// The function "GetWH" takes a BioImage and ROI as input parameters and returns the width and
        /// height of the ROI in terms of image size.
        /// </summary>
        /// <param name="BioImage">BioImage is a class that represents an image in a biological context.
        /// It likely contains information about the image's size, resolution, and other
        /// properties.</param>
        /// <param name="ROI">The ROI (Region of Interest) is a rectangular area within the BioImage
        /// (bi) that represents a specific region or object of interest. It has properties such as
        /// width (W) and height (H) that define its size.</param>
        /// <param name="w">The "w" parameter is an output parameter that will store the width of the
        /// ROI in the image.</param>
        /// <param name="h">The 'h' parameter is an output parameter of type float. It is used to store
        /// the height of the ROI (Region of Interest) in the image.</param>
        static void GetWH(BioImage bi, ROI roi, out float w, out float h)
        {
            w = (float)bi.ToImageSizeX(roi.W);
            h = (float)bi.ToImageSizeY(roi.H);
        }
        /// <summary>
        /// The function rightMove takes an integer value and a position as input, and returns the value
        /// after performing a right shift operation by the specified position.
        /// </summary>
        /// <param name="value">The value is an integer that represents the number you want to perform a
        /// right move on.</param>
        /// <param name="pos">The "pos" parameter represents the number of positions to move the bits to
        /// the right.</param>
        /// <returns>
        /// the value after performing a right shift operation.
        /// </returns>
        static int rightMove(int value, int pos)
        {
            if (pos != 0)
            {
                int mask = 0x7fffffff;
                value >>= 1;
                value &= mask;
                value >>= pos - 1;
            }
            return value;
        }
        public class RoiEncoder
        {
            static int HEADER_SIZE = 64;
            static int HEADER2_SIZE = 64;
            static int VERSION = 228; // v1.52t (roi groups, scale stroke width)
            private string path;
            private FileStream f;
            private int polygon = 0, rect = 1, oval = 2, line = 3, freeline = 4, polyline = 5, noRoi = 6, freehand = 7,
                traced = 8, angle = 9, point = 10;
            private byte[] data;
            private string roiName;
            private int roiNameSize;
            private string roiProps;
            private int roiPropsSize;
            private int countersSize;
            private int[] counters;
            private bool subres = true;

            /** Creates an RoiEncoder using the specified path. */
            public RoiEncoder(String path)
            {
                this.path = path;
            }

            /** Creates an RoiEncoder using the specified OutputStream. */
            public RoiEncoder(FileStream f)
            {
                this.f = f;
            }

            /// <summary>
            /// The save function saves a BioImage and ROI to a specified path using a RoiEncoder.
            /// </summary>
            /// <param name="BioImage">The BioImage parameter is an object that represents an image or a
            /// collection of images along with associated metadata. It may contain information such as
            /// pixel data, image dimensions, color space, and other properties specific to the image
            /// format being used.</param>
            /// <param name="ROI">The ROI parameter is an object that represents a region of interest in
            /// the BioImage. It contains information about the coordinates and dimensions of the
            /// region.</param>
            /// <param name="String">The "String" parameter in the save method represents the path where
            /// the ROI (Region of Interest) will be saved. It is the file path where the ROI data will
            /// be written to.</param>
            /// <returns>
            /// The method is returning a boolean value.
            /// </returns>
            public static bool save(BioImage bi, ROI roi, String path)
            {
                RoiEncoder re = new RoiEncoder(path);
                try
                {
                    re.write(bi, roi);
                }
                catch (IOException e)
                {
                    return false;
                }
                return true;
            }

            /// <summary>
            /// The write function writes a BioImage and ROI to a file, creating a new file if it
            /// doesn't exist, and closing the file afterwards.
            /// </summary>
            /// <param name="BioImage">The BioImage parameter represents an image or data structure that
            /// contains biological image data. It could be an object that holds information about the
            /// image, such as pixel values, dimensions, and metadata.</param>
            /// <param name="ROI">The ROI parameter represents a region of interest in the BioImage. It
            /// is used to specify a specific area or region within the image that needs to be
            /// written.</param>
            public void write(BioImage bi, ROI roi)
            {
                if (f != null) 
                {
                    write(bi, roi, f);
                } 
                else
                {
                    f = new FileStream(path,FileMode.Create);
                    write(bi, roi, f);
                    f.Close();
                }
            }
            
            /** Saves the specified ROI as a byte array. 
            public static byte[] saveAsByteArray(ROI roi)
            {
                if (roi == null) return null;
                byte[] bytes = null;
                try
                {
                    MemoryStream outs = new MemoryStream(4096);
                    RoiEncoder encoder = new RoiEncoder(path);
                    encoder.write(roi);
			        outs.close();
                    bytes = out.toByteArray();
                }
                catch (IOException e)
                {
                    return null;
                }
                return bytes;
            }
            */
            /// <summary>
            /// The function "write" saves the information of a region of interest (ROI) in a bioimage
            /// to a file stream.
            /// </summary>
            /// <param name="BioImage">The BioImage parameter represents an image object that contains
            /// the image data and metadata.</param>
            /// <param name="ROI">The "ROI" parameter represents a region of interest in an image. It
            /// contains information about the shape, position, and properties of the region.</param>
            /// <param name="FileStream">FileStream is a class that represents a stream of bytes to be
            /// written to a file. It is used to write the data of the BioImage and ROI objects to a
            /// file.</param>
            /// <returns>
            /// The code snippet does not have a return statement, so nothing is being returned.
            /// </returns>
            void write(BioImage bi, ROI roi, FileStream f)
            {
                RectangleD r = roi.Rect;
                //if (r.width > 60000 || r.height > 60000 || r.x > 60000 || r.y > 60000)
                //    roi.enableSubPixelResolution();
                //int roiType = GetImageJType(roi);
                int type = GetImageJType(roi);
                int options = 0;
                //if (roi.getScaleStrokeWidth())
                //    options |= RoiDecoder.SCALE_STROKE_WIDTH;
                roiName = roi.Text;
                if (roiName != null)
                    roiNameSize = roiName.Length * 2;
                else
                    roiNameSize = 0;

                roiProps = roi.properties;
                if (roiProps != null)
                    roiPropsSize = roiProps.Length * 2;
                else
                    roiPropsSize = 0;
                /*
                switch (roiType) {
                    case Roi.POLYGON: type = polygon; break;
                    case Roi.FREEROI: type = freehand; break;
                    case Roi.TRACED_ROI: type = traced; break;
                    case Roi.OVAL: type = oval; break;
                    case Roi.LINE: type = line; break;
                    case Roi.POLYLINE: type = polyline; break;
                    case Roi.FREELINE: type = freeline; break;
                    case Roi.ANGLE: type = angle; break;
                    case Roi.COMPOSITE: type = rect; break; // shape array size (36-39) will be >0 to indicate composite type
                    case Roi.POINT: type = point; break;
                    default: type = rect; break;
                }
                */
                /*
                if (roiType == Roi.COMPOSITE) {
                    saveShapeRoi(roi, type, f, options);
                    return;
                }
                */
                int n = 0;
                int[]
                x = null, y = null;
                float[]
                xf = null, yf = null;
                int floatSize = 0;
                //if (roi instanceof PolygonRoi) {
                //PolygonRoi proi = (PolygonRoi)roi;
                //Polygon p = proi.getNonSplineCoordinates();
                n = roi.PointsD.Count; //p.npoints;
                //x = p.xpoints;
                //y = p.ypoints;
                GetPointsXY(bi, roi, out x, out y);
                if (subres)
                {
                    /*
                    if (proi.isSplineFit())
                        fp = proi.getNonSplineFloatPolygon();
                    else
                        fp = roi.getFloatPolygon();
                    if (n == fp.npoints)
                    {
                        options |= RoiDecoder.SUB_PIXEL_RESOLUTION;
                        if (roi.getDrawOffset())
                            options |= RoiDecoder.DRAW_OFFSET;
                        xf = fp.xpoints;
                        yf = fp.ypoints;
                        floatSize = n * 8;
                    }
                    */
                }
                

                countersSize = 0;
                /*
                if (roi instanceof PointRoi) {
                    counters = ((PointRoi)roi).getCounters();
                    if (counters != null && counters.length >= n)
                        countersSize = n * 4;
                }
                */
                data = new byte[HEADER_SIZE + HEADER2_SIZE + n * 4 + floatSize + roiNameSize + roiPropsSize + countersSize];
                data[0] = 73; data[1] = 111; data[2] = 117; data[3] = 116; // "Iout"
                putShort(RoiDecoder.VERSION_OFFSET, VERSION);
                data[RoiDecoder.TYPE] = (byte)type;
                float px, py, pw, ph;
                GetXY(bi, roi, out px, out py);
                GetWH(bi, roi, out pw, out ph);
                putShort(RoiDecoder.TOP, (int)py);
                putShort(RoiDecoder.LEFT, (int)px);
                putShort(RoiDecoder.BOTTOM, (int)(py + ph));
                putShort(RoiDecoder.RIGHT, (int)(px + pw));
                if (subres && (type == rect || type == oval))
                {
                    //FloatPolygon p = null;
                    /*
                    if (roi instanceof OvalRoi)
				            p = ((OvalRoi)roi).getFloatPolygon4();
                        else
                    {
                        int d = roi.getCornerDiameter();
                        if (d > 0)
                        {
                            roi.setCornerDiameter(0);
                            p = roi.getFloatPolygon();
                            roi.setCornerDiameter(d);
                        }
                        else
                            p = roi.getFloatPolygon();
                    }
                        */
                    if (roi.PointsD.Count == 4)
                    {
                        PointD[] pts = roi.ImagePoints(bi.Resolutions[bi.Level]);
                        putFloat(RoiDecoder.XD, (float)pts[0].X);
                        putFloat(RoiDecoder.YD, (float)pts[0].Y);
                        //putFloat(RoiDecoder.WIDTHD, p.xpoints[1] - roi.PointsD[0]);
                        //putFloat(RoiDecoder.HEIGHTD, p.ypoints[2] - p.ypoints[1]);
                        putFloat(RoiDecoder.WIDTHD, (float)pts[1].X - (float)pts[0].X);
                        putFloat(RoiDecoder.HEIGHTD, (float)pts[2].Y - (float)pts[1].Y);
                        options |= RoiDecoder.SUB_PIXEL_RESOLUTION;
                        putShort(RoiDecoder.OPTIONS, options);
                    }
                }
                if (n > 65535 && type != point)
                {
                    if (type == polygon || type == freehand || type == traced)
                    {
                        //String name = roi.Text;
                        //roi = new ShapeRoi(roi);
                        //if (name != null) roi.setName(name);
                        saveShapeRoi(bi, roi, rect, f, options);
                        return;
                    }
                    //ij.IJ.beep();
                    //ij.IJ.log("Non-polygonal selections with more than 65k points cannot be saved.");
                    n = 65535;
                }
                if (type == point && n > 65535)
                    putInt(RoiDecoder.SIZE, n);
                else
                    putShort(RoiDecoder.N_COORDINATES, n);
                putInt(RoiDecoder.POSITION, roi.coord.Z);

                /*
                if (type == rect)
                {
                    int arcSize = roi.getCornerDiameter();
                    if (arcSize > 0)
                        putShort(RoiDecoder.ROUNDED_RECT_ARC_SIZE, arcSize);
                }
                */

                if(type == line) //(roi instanceof Line) 
                {
                    PointD[] pts = roi.ImagePoints(bi.Resolutions[bi.Level]);
                    //Line line = (Line)roi;
                    putFloat(RoiDecoder.X1, (float)pts[0].X);
                    putFloat(RoiDecoder.Y1, (float)pts[0].Y);
                    putFloat(RoiDecoder.X2, (float)pts[1].X);
                    putFloat(RoiDecoder.Y2, (float)pts[1].Y);
                    /*
                    if (roi instanceof Arrow) {
                        putShort(RoiDecoder.SUBTYPE, RoiDecoder.ARROW);
                        if (((Arrow)roi).getDoubleHeaded())
                            options |= RoiDecoder.DOUBLE_HEADED;
                        if (((Arrow)roi).getOutline())
                            options |= RoiDecoder.OUTLINE;
                        putShort(RoiDecoder.OPTIONS, options);
                        putByte(RoiDecoder.ARROW_STYLE, ((Arrow)roi).getStyle());
                        putByte(RoiDecoder.ARROW_HEAD_SIZE, (int)((Arrow)roi).getHeadSize());
                    } else
                    {
                        if (roi.getDrawOffset())
                            options |= RoiDecoder.SUB_PIXEL_RESOLUTION + RoiDecoder.DRAW_OFFSET;
                    }
                    */
                }

                if (type == point) {
                    //PointRoi point = (PointRoi)roi;
                    putByte(RoiDecoder.POINT_TYPE, 1);//point.getPointType());
                    putShort(RoiDecoder.STROKE_WIDTH, (int)roi.strokeWidth);
                    /*
                    if (point.getShowLabels())
                        options |= RoiDecoder.SHOW_LABELS;
                    if (point.promptBeforeDeleting())
                        options |= RoiDecoder.PROMPT_BEFORE_DELETING;
                    */
                }

                if (type == oval) 
                {
                    /*
                    double[] p = null;
                    if (roi instanceof RotatedRectRoi) {
                        putShort(RoiDecoder.SUBTYPE, RoiDecoder.ROTATED_RECT);
                        p = ((RotatedRectRoi)roi).getParams();
                    } else
                    {
                        */
                        putShort(RoiDecoder.SUBTYPE, RoiDecoder.ELLIPSE);
                    //p = ((EllipseRoi)roi).getParams();
                    //}
                    float fx, fy, fw, fh;
                    GetXY(bi, roi, out fx, out fy);
                    GetWH(bi, roi, out fw, out fh);
                    putFloat(RoiDecoder.X1, fx);
                    putFloat(RoiDecoder.Y1, fy);
                    putFloat(RoiDecoder.X2, fw);
                    putFloat(RoiDecoder.Y2, fh);
                    //putFloat(RoiDecoder.FLOAT_PARAM, (float)p[4]);
                }

                // save stroke width, stroke color and fill color (1.43i or later)
                if (VERSION >= 218)
                {
                    saveStrokeWidthAndColor(roi);
                    /*
                    if ((roi instanceof PolygonRoi) && ((PolygonRoi)roi).isSplineFit()) {
                        options |= RoiDecoder.SPLINE_FIT;
                        putShort(RoiDecoder.OPTIONS, options);
                    }
                    */
                }

                if (roi.type == ROI.Type.Label)//(n == 0 && roi instanceof TextRoi)
			            saveTextRoi(roi);
                /*
                else if (n == 0 && roi instanceof ImageRoi)
			        options = saveImageRoi((ImageRoi)roi, options);
                */
                //else
                putHeader2(roi, HEADER_SIZE + n * 4 + floatSize);

                if (n > 0)
                {
                    int base1 = 64;
                    int base2 = base1 + 2 * n;
                    for (int i = 0; i < n; i++)
                    {
                        putShort(base1 + i * 2, (int)(x[i] - px));
                        putShort(base2 + i * 2, (int)(y[i] - py));
                    }
                    if (xf != null)
                    {
                        base1 = 64 + 4 * n;
                        base2 = base1 + 4 * n;
                        for (int i = 0; i < n; i++)
                        {
                            putFloat(base1 + i * 4, xf[i]);
                            putFloat(base2 + i * 4, yf[i]);
                        }
                    }
                }

                //saveOverlayOptions(roi, options);
                f.Write(data);
            }

            /// <summary>
            /// The function saves the stroke width and color of a region of interest (ROI) in a
            /// specific format.
            /// </summary>
            /// <param name="ROI">The ROI parameter is an object that represents a region of interest.
            /// It contains information about the stroke width, stroke color, and fill color of the
            /// region.</param>
            void saveStrokeWidthAndColor(ROI roi)
            {
                //BasicStroke stroke = roi.getStroke();
                //if (stroke != null)
                    putShort(RoiDecoder.STROKE_WIDTH, (int)roi.strokeWidth);
                Color strokeColor = roi.strokeColor;
                int intColor = (strokeColor.R << 16) | (strokeColor.G << 8) | (strokeColor.B);
                putInt(RoiDecoder.STROKE_COLOR, 0);
                Color fillColor = roi.fillColor;
                int intFillColor = (fillColor.R << 16) | (fillColor.G << 8) | (fillColor.B);
                putInt(RoiDecoder.FILL_COLOR, 0);
            }

            /// <summary>
            /// The function `saveShapeRoi` saves the shape region of interest (ROI) in a BioImage to a
            /// file stream in a specific format.
            /// </summary>
            /// <param name="BioImage">The BioImage parameter is an object that represents an image in a
            /// biological context. It likely contains information about the image, such as pixel data,
            /// dimensions, and metadata.</param>
            /// <param name="ROI">The ROI parameter represents the region of interest in an image. It
            /// contains information about the position and size of the ROI.</param>
            /// <param name="type">The "type" parameter is an integer that represents the type of the
            /// ROI. It is used to specify the shape of the ROI, such as rectangle, ellipse, polygon,
            /// etc.</param>
            /// <param name="FileStream">FileStream is a class in C# that represents a stream of bytes
            /// to read from or write to a file. It is used to handle file operations such as reading,
            /// writing, and seeking within a file. In the given code, the FileStream object "f" is used
            /// to write the data to a</param>
            /// <param name="options">The "options" parameter is an integer that represents various
            /// options for saving the shape ROI. The specific options and their meanings are not
            /// provided in the code snippet, so you would need to refer to the documentation or the
            /// rest of the code to determine the possible values and their effects.</param>
            void saveShapeRoi(BioImage bi, ROI roi, int type, FileStream f, int options)
            {
                //float[] shapeArray = ((ShapeRoi)roi).getShapeAsArray();
                //if (shapeArray == null) return;
                //BufferedOutputStream bout = new BufferedOutputStream(f);

                data = new byte[HEADER_SIZE + HEADER2_SIZE + roiNameSize + roiPropsSize];//shapeArray.length * 4 + roiNameSize + roiPropsSize];
                data[0] = 73; data[1] = 111; data[2] = 117; data[3] = 116; // "Iout"

                putShort(RoiDecoder.VERSION_OFFSET, VERSION);
                data[RoiDecoder.TYPE] = (byte)type;

                float x, y, w, h;
                GetXY(bi ,roi, out x,out y);
                GetWH(bi ,roi, out w, out h);
                putShort(RoiDecoder.TOP, (int)y);
                putShort(RoiDecoder.LEFT, (int)x);
                putShort(RoiDecoder.BOTTOM, (int)(y + h));
                putShort(RoiDecoder.RIGHT, (int)(x + w));
                putInt(RoiDecoder.POSITION, roi.coord.Z);
                ///putShort(16, n);
                //putInt(36, shapeArray.Length); // non-zero segment count indicate composite type
                if (VERSION >= 218)
                    saveStrokeWidthAndColor(roi);
                //saveOverlayOptions(roi, options);

                // handle the actual data: data are stored segment-wise, i.e.,
                // the type of the segment followed by 0-6 control point coordinates.
                /*
                int bas = 64;
                for (int i = 0; i < shapeArray.Length; i++)
                {
                    putFloat(bas, shapeArray[i]);
                    bas += 4;
                }
                */
                int hdr2Offset = HEADER_SIZE;// + shapeArray.Length * 4;
                //ij.IJ.log("saveShapeRoi: "+HEADER_SIZE+"  "+shapeArray.length);
                putHeader2(roi, hdr2Offset);
                f.Write(data, 0, data.Length);
                f.Flush();
            }

            /*
            void saveOverlayOptions(ROI roi, int options)
            {
                Overlay proto = roi.getPrototypeOverlay();
                if (proto.getDrawLabels())
                    options |= RoiDecoder.OVERLAY_LABELS;
                if (proto.getDrawNames())
                    options |= RoiDecoder.OVERLAY_NAMES;
                if (proto.getDrawBackgrounds())
                    options |= RoiDecoder.OVERLAY_BACKGROUNDS;
                Font font = proto.getLabelFont();
                if (font != null && font.getStyle() == Font.BOLD)
                    options |= RoiDecoder.OVERLAY_BOLD;
                if (proto.scalableLabels())
                    options |= RoiDecoder.SCALE_LABELS;
                putShort(RoiDecoder.OPTIONS, options);
            }
            */
            /// <summary>
            /// The function `saveTextRoi` saves the properties of a text region of interest (ROI) into
            /// a byte array.
            /// </summary>
            /// <param name="ROI">The `ROI` parameter is an object that represents a region of interest.
            /// It contains information about the font, size, style, text, and other properties of the
            /// region of interest.</param>
            void saveTextRoi(ROI roi)
            {
                //Font font = roi.getCurrentFont();
                string fontName = roi.family;
                int size = (int)roi.fontSize;
                int drawStringMode = 0; //roi.getDrawStringMode() ? 1024 : 0;
                int style = 0;//font.getStyle() + roi.getJustification() * 256 + drawStringMode;
                string text = roi.roiName;
                float angle = 0;
                int angleLength = 4;
                int fontNameLength = fontName.Length;
                int textLength = text.Length;
                int textRoiDataLength = 16 + fontNameLength * 2 + textLength * 2 + angleLength;
                byte[] data2 = new byte[HEADER_SIZE + HEADER2_SIZE + textRoiDataLength + roiNameSize + roiPropsSize];
                Array.Copy(data, 0, data2, 0, HEADER_SIZE);
                data = data2;
                putShort(RoiDecoder.SUBTYPE, RoiDecoder.TEXT);
                putInt(HEADER_SIZE, size);
                putInt(HEADER_SIZE + 4, style);
                putInt(HEADER_SIZE + 8, fontNameLength);
                putInt(HEADER_SIZE + 12, textLength);
                for (int i = 0; i < fontNameLength; i++)
                    putShort(HEADER_SIZE + 16 + i * 2, fontName.ElementAt(i));
                for (int i = 0; i < textLength; i++)
                    putShort(HEADER_SIZE + 16 + fontNameLength * 2 + i * 2, text.ElementAt(i));
                int hdr2Offset = HEADER_SIZE + textRoiDataLength;
                //ij.IJ.log("saveTextRoi: "+HEADER_SIZE+"  "+textRoiDataLength+"  "+fontNameLength+"  "+textLength);
                putFloat(hdr2Offset - angleLength, angle);
                putHeader2(roi, hdr2Offset);
            }
            /*
            private int saveImageRoi(ROI roi, int options)
            {
                byte[] bytes = roi.getSerializedImage();
                int imageSize = bytes.length;
                byte[] data2 = new byte[HEADER_SIZE + HEADER2_SIZE + imageSize + roiNameSize + roiPropsSize];
                System.arraycopy(data, 0, data2, 0, HEADER_SIZE);
                data = data2;
                putShort(RoiDecoder.SUBTYPE, RoiDecoder.IMAGE);
                for (int i = 0; i < imageSize; i++)
                    putByte(HEADER_SIZE + i, bytes[i] & 255);
                int hdr2Offset = HEADER_SIZE + imageSize;
                double opacity = roi.getOpacity();
                putByte(hdr2Offset + RoiDecoder.IMAGE_OPACITY, (int)(opacity * 255.0));
                putInt(hdr2Offset + RoiDecoder.IMAGE_SIZE, imageSize);
                if (roi.getZeroTransparent())
                    options |= RoiDecoder.ZERO_TRANSPARENT;
                putHeader2(roi, hdr2Offset);
                return options;
            }
            */
            /// <summary>
            /// The function "putHeader2" is used to set various properties of a Region of Interest
            /// (ROI) object, such as its position, label color, font size, stroke width, and group.
            /// </summary>
            /// <param name="ROI">The ROI parameter is an object of type ROI, which represents a region
            /// of interest in an image. It contains information about the position and size of the ROI,
            /// as well as other properties such as the stroke color, stroke width, and font
            /// size.</param>
            /// <param name="hdr2Offset">The `hdr2Offset` parameter is an integer that represents the
            /// offset position in the header where the information for the second header should be
            /// stored.</param>
            void putHeader2(ROI roi, int hdr2Offset)
            {
                //ij.IJ.log("putHeader2: "+hdr2Offset+" "+roiNameSize+"  "+roiName);
                putInt(RoiDecoder.HEADER2_OFFSET, hdr2Offset);
                putInt(hdr2Offset + RoiDecoder.C_POSITION, roi.coord.C + 1);
                putInt(hdr2Offset + RoiDecoder.Z_POSITION, roi.coord.Z + 1);
                putInt(hdr2Offset + RoiDecoder.T_POSITION, roi.coord.T + 1);
                //Overlay proto = roi.getPrototypeOverlay();
                Color overlayLabelColor = roi.strokeColor; //proto.getLabelColor();
                int intColor = (overlayLabelColor.R << 16) | (overlayLabelColor.G << 8) | (overlayLabelColor.B);
                //if (overlayLabelColor != null)
                putInt(hdr2Offset + RoiDecoder.OVERLAY_LABEL_COLOR, 0);
                //Font font = proto.getLabelFont();
                //if (font != null)
                    putShort(hdr2Offset + RoiDecoder.OVERLAY_FONT_SIZE, (int)roi.fontSize);
                if (roiNameSize > 0)
                    putName(roi, hdr2Offset);
                double strokeWidth = roi.strokeWidth;
                //if (roi.getStroke() == null)
                //    strokeWidth = 0.0;
                putFloat(hdr2Offset + RoiDecoder.FLOAT_STROKE_WIDTH, (float)strokeWidth);
                if (roiPropsSize > 0)
                    putProps(roi, hdr2Offset);
                if (countersSize > 0)
                    putPointCounters(roi, hdr2Offset);
                putByte(hdr2Offset + RoiDecoder.GROUP, roi.serie);//roi.getGroup());
            }

            /// <summary>
            /// The function "putName" takes a ROI object and an offset value, and sets the name and
            /// length of the ROI in the header.
            /// </summary>
            /// <param name="ROI">The ROI parameter is of type ROI, which is likely a custom class
            /// representing a region of interest in an image. It could contain information such as the
            /// coordinates, size, and properties of the region.</param>
            /// <param name="hdr2Offset">The `hdr2Offset` parameter is an integer value representing the
            /// offset of the header 2 in a data structure or file. It is used to calculate the offset
            /// for storing the name of the ROI (Region of Interest) in the data structure or
            /// file.</param>
            void putName(ROI roi, int hdr2Offset)
            {
                int offset = hdr2Offset + HEADER2_SIZE;
                int nameLength = roiNameSize / 2;
                putInt(hdr2Offset + RoiDecoder.NAME_OFFSET, offset);
                putInt(hdr2Offset + RoiDecoder.NAME_LENGTH, nameLength);
                for (int i = 0; i < nameLength; i++)
                    putShort(offset + i * 2, roiName.ElementAt(i));
            }

            /// <summary>
            /// The function "putProps" takes a ROI object and an offset value, and sets the properties
            /// of the ROI object in memory.
            /// </summary>
            /// <param name="ROI">The ROI parameter is an object of type ROI. It is used to pass
            /// information about a region of interest.</param>
            /// <param name="hdr2Offset">The `hdr2Offset` parameter is an integer value representing the
            /// offset of the header2 in memory. It is used to calculate the offset for storing the ROI
            /// properties.</param>
            void putProps(ROI roi, int hdr2Offset)
            {
                int offset = hdr2Offset + HEADER2_SIZE + roiNameSize;
                int roiPropsLength = roiPropsSize / 2;
                putInt(hdr2Offset + RoiDecoder.ROI_PROPS_OFFSET, offset);
                putInt(hdr2Offset + RoiDecoder.ROI_PROPS_LENGTH, roiPropsLength);
                for (int i = 0; i < roiPropsLength; i++)
                    putShort(offset + i * 2, roiProps.ElementAt(i));
            }

            /// <summary>
            /// The function "putPointCounters" updates the counters in a region of interest (ROI) by
            /// copying the values from an array to a specific offset in memory.
            /// </summary>
            /// <param name="ROI">The ROI parameter is an object of type ROI, which likely represents a
            /// region of interest in an image. It may contain information such as the coordinates,
            /// size, and properties of the region.</param>
            /// <param name="hdr2Offset">The `hdr2Offset` parameter is the offset value for the second
            /// header in the data structure. It is used to calculate the offset for storing the point
            /// counters.</param>
            void putPointCounters(ROI roi, int hdr2Offset)
            {
                int offset = hdr2Offset + HEADER2_SIZE + roiNameSize + roiPropsSize;
                putInt(hdr2Offset + RoiDecoder.COUNTERS_OFFSET, offset);
                for (int i = 0; i < countersSize / 4; i++)
                    putInt(offset + i * 4, counters[i]);
                countersSize = 0;
            }

            /// <summary>
            /// The function "putByte" assigns a byte value to a specific index in an array.
            /// </summary>
            /// <param name="bas">The parameter "bas" is an integer that represents the base address or
            /// index of the array "data" where the byte value will be stored.</param>
            /// <param name="v">The parameter "v" is an integer value that represents the value to be
            /// stored in the byte array.</param>
            void putByte(int bas, int v)
            {
                data[bas] = (byte)v;
            }

            /// <summary>
            /// The function "putShort" takes two integer parameters, "bas" and "v", and stores the
            /// value of "v" in the "data" array at index "bas" and "bas + 1" after performing a right
            /// shift operation on "v" by 8 bits.
            /// </summary>
            /// <param name="bas">The parameter "bas" represents the base index in the "data" array
            /// where the short value will be stored.</param>
            /// <param name="v">The parameter "v" is an integer value that represents the value to be
            /// stored in the data array.</param>
            void putShort(int bas, int v)
            {
                //data[bas] = (byte)(v >>> 8);
                //data[bas] = (byte)UnsignedRightShift(v, 8);
                data[bas] = (byte)rightMove(v, 8);
                data[bas + 1] = (byte)v;
            }

            /// <summary>
            /// The function "putFloat" takes an integer and a float as input and converts the float
            /// into its binary representation, storing it in a byte array.
            /// </summary>
            /// <param name="bas">The parameter "bas" represents the base index in the "data" array
            /// where the float value will be stored.</param>
            /// <param name="v">The parameter "v" is a float value that needs to be converted and stored
            /// in the "data" array.</param>
            void putFloat(int bas, float v)
            {
                int tmp = BitConverter.SingleToInt32Bits(v);//Float.floatToIntBits(v);
                data[bas] = (byte)(tmp >> 24);
                data[bas + 1] = (byte)(tmp >> 16);
                data[bas + 2] = (byte)(tmp >> 8);
                data[bas + 3] = (byte)tmp;
            }

            /// <summary>
            /// The function "putInt" takes two integer parameters and stores the bytes of the second
            /// integer in a byte array starting at the specified index in big-endian order.
            /// </summary>
            /// <param name="bas">The parameter "bas" represents the base index in the "data" array
            /// where the integer value will be stored.</param>
            /// <param name="i">The parameter "i" is an integer value that needs to be stored in the
            /// "data" array.</param>
            void putInt(int bas, int i)
            {
                data[bas] = (byte)(i >> 24);
                data[bas + 1] = (byte)(i >> 16);
                data[bas + 2] = (byte)(i >> 8);
                data[bas + 3] = (byte)i;
            }
        }

    }
}
