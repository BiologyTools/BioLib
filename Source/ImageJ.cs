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
        private static string imjpath;
        public static string ImageJPath
        {
            get
            {
                return imjpath;
            }
            set
            {
                imjpath = value.Replace("\\", "/");
            }
        }
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
            string file = dir + "/" + filename + ".ome.tif";
            if(!bioformats)
                file = dir + "/" + filename + ".tif";
            string filet = dir + "/" + filename + "-" + Images.GetImageCountByName(filename);
            string donepath = Path.GetDirectoryName(Environment.ProcessPath);
            donepath = donepath.Replace("\\", "/");
            string st =
            "run(\"Bio-Formats Importer\", \"open=" + file + " autoscale color_mode=Default open_all_series display_rois rois_import=[ROI manager] view=Hyperstack stack_order=XYCZT\"); " + con +
            "run(\"Bio-Formats Exporter\", \"save=" + filet + " export compression=Uncompressed\"); " +
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
            SetImageJPath(path);
        }
        /// This function creates a file chooser dialog that allows the user to select the location of
        /// the ImageJ executable
        /// 
        /// @return A boolean value.
        public static bool SetImageJPath(string? path)
        {
            if (path == null || path == "")
            {
                string st = Settings.GetSettings("ImageJPath");
                if (st != "")
                {
                    ImageJPath = Settings.GetSettings("ImageJPath");
                    return true;
                }
                return false;
            }
            else
            {
                if (Settings.GetSettings("ImageJPath") == "")
                {
                    Settings.AddSettings("ImageJPath", path);
                    Settings.Save();
                }
                ImageJPath = path;
                return true;
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
    }
}
