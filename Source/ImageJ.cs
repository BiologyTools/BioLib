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
        public static async void RunString(BioImage b,string con, string param, bool headless)
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
        public static void RunOnImage(BioImage bi,string con, int index, bool headless, bool onTab, bool bioformats, bool resultInNewTab, string outputStem = null)
        {
            if (OperatingSystem.IsMacOS())
                bioformats = false;
            Console.WriteLine($"[ImageJ.RunOnImage] input={bi?.ID} index={index} headless={headless} onTab={onTab} bioformats={bioformats} resultInNewTab={resultInNewTab} outputStem={outputStem}");
            string filename = "";
            string dir = Path.GetDirectoryName(bi.file);
            dir = dir.Replace("\\", "/");
            string inputPath = dir + "/" + bi.ID;
            bool inputIsZarr = bi.ID != null && bi.ID.EndsWith(".zarr", StringComparison.OrdinalIgnoreCase);
            if (bi.ID.EndsWith(".ome.tif"))
            {
                filename = Path.GetFileNameWithoutExtension(bi.ID);
                filename = filename.Remove(filename.Length - 4, 4);
            }
            else
                filename = Path.GetFileNameWithoutExtension(bi.ID);
            string file = inputIsZarr ? inputPath : dir + "/" + filename + ".ome.tif";
            if (!bioformats && !inputIsZarr)
                file = dir + "/" + filename + ".tif";
            string outputName = string.IsNullOrWhiteSpace(outputStem) ? filename : outputStem;
            string exportPath = bioformats
                ? dir + "/" + outputName + "-temp.ome.tif"
                : dir + "/" + outputName + ".tif";
            string donepath = Path.GetDirectoryName(Environment.ProcessPath);
            donepath = donepath.Replace("\\", "/");
            string st =
            "run(\"Bio-Formats Importer\", \"open=" + file + " autoscale color_mode=Default open_all_series display_rois rois_import=[ROI manager] view=Hyperstack stack_order=XYCZT\"); " + con +
            "run(\"Bio-Formats Exporter\", \"save=" + exportPath + " export compression=Uncompressed\"); " +
            "dir = \"" + donepath + "\"" +
            "File.saveString(\"done\", dir + \"/done.txt\");";
            if (!bioformats)
                st =
                "open(getArgument); " + con +
                "saveAs(\"Tiff\",\"" + exportPath + "\"); " +
                "dir = \"" + donepath + "\"" +
                "File.saveString(\"done\", dir + \"/done.txt\");";
            Console.WriteLine($"[ImageJ.RunOnImage] inputFile={file}");
            RunString(bi,st, dir + "/" + bi.ID, headless);
            if (!File.Exists(file))
            {
                Console.WriteLine($"[ImageJ.RunOnImage] input file missing after run: {file}");
                return;
            }

            string f = ResolveExportedImagePath(exportPath);
            if (f == null)
            {
                Console.WriteLine($"[ImageJ.RunOnImage] output file not found for exportPath={exportPath}");
                return;
            }

            Console.WriteLine($"[ImageJ.RunOnImage] resolvedOutput={f}");

            string fn = Path.GetFileName(f);
            //If not in images we add it to a new tab.
            if (Images.GetImage(fn) == null)
            {
                BioImage bm = BioImage.OpenFileAsync(f, index, false, false).Result;
                Console.WriteLine($"[ImageJ.RunOnImage] opened new image {bm?.ID} filename={bm?.Filename}");
                bm.Filename = fn;
                bm.ID = fn;
                bm.file = dir + "/" + fn;
                Images.AddImage(bm);
            }
            else
            {
                BioImage b = BioImage.OpenFileAsync(f, index, false, false).Result;
                Console.WriteLine($"[ImageJ.RunOnImage] updating existing image with {b?.ID} filename={b?.Filename}");
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

        private static string ResolveExportedImagePath(string exportPath)
        {
            if (File.Exists(exportPath))
                return exportPath.Replace("\\", "/");

            string dir = Path.GetDirectoryName(exportPath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                return null;

            string stem = Path.GetFileNameWithoutExtension(exportPath);
            string[] candidates = Directory.GetFiles(dir)
                .Where(f =>
                {
                    string name = Path.GetFileName(f);
                    return name.StartsWith(stem, StringComparison.OrdinalIgnoreCase) &&
                           (name.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) ||
                            name.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase));
                })
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToArray();

            return candidates.Length > 0 ? candidates[0].Replace("\\", "/") : null;
        }

        /// <summary>
        /// Flattens a pyramidal BioImage to a single resolution level so it can
        /// be processed by the existing ImageJ import/export path.
        /// </summary>
        private static async Task<BioImage> FlattenPyramidalImage(BioImage b, int level)
        {
            if (b == null)
                return null;
            if (!b.isPyramidal || b.Resolutions.Count == 0)
                return b;

            level = Math.Clamp(level, 0, b.Resolutions.Count - 1);
            Resolution res = b.Resolutions[level];
            int width = res.SizeX > 0 ? res.SizeX : b.SizeX;
            int height = res.SizeY > 0 ? res.SizeY : b.SizeY;
            if (width <= 0 || height <= 0)
                return null;

            BioImage flat = BioImage.CopyInfo(b, true, true);
            flat.Buffers.Clear();
            flat.Resolutions.Clear();
            flat.seriesCount = 1;
            flat.UpdateCoords(b.SizeZ, b.SizeC, b.SizeT);
            flat.Coordinate = new ZCT(0, 0, 0);
            flat.Resolutions.Add(new Resolution(
                width,
                height,
                res.PixelFormat,
                res.PhysicalSizeX,
                res.PhysicalSizeY,
                res.PhysicalSizeZ,
                res.StageSizeX,
                res.StageSizeY,
                res.StageSizeZ));

            for (int t = 0; t < b.SizeT; t++)
            {
                for (int c = 0; c < b.SizeC; c++)
                {
                    for (int z = 0; z < b.SizeZ; z++)
                    {
                        int index = b.GetFrameIndex(z, c, t);
                        Bitmap tile = await b.GetTile(index, level, 0, 0, width, height, new ZCT(z, c, t), true).ConfigureAwait(false);
                        if (tile == null)
                            return null;
                        tile.Stats = Statistics.FromBytes(tile);
                        flat.Buffers.Add(tile);
                    }
                }
            }

            flat.Volume = new VolumeD(
                new Point3D(b.Volume.Location.X, b.Volume.Location.Y, b.Volume.Location.Z),
                new Point3D(
                width * flat.Resolutions[0].PhysicalSizeX,
                height * flat.Resolutions[0].PhysicalSizeY,
                b.SizeZ * flat.Resolutions[0].PhysicalSizeZ));

            // Keep the metadata aligned with the actual raw buffers so OME
            // writing matches the tile bytes we assembled.
            int flatBits = 8;
            if (flat.Buffers.Count > 0)
            {
                PixelFormat pf = flat.Buffers[0].PixelFormat;
                if (pf == PixelFormat.Format16bppGrayScale || pf == PixelFormat.Format48bppRgb)
                    flatBits = 16;
            }

            if (flat.Channels == null)
                flat.Channels = new List<Channel>();
            if (flat.Channels.Count == 0)
            {
                int channelCount = Math.Max(1, flat.SizeC);
                for (int i = 0; i < channelCount; i++)
                    flat.Channels.Add(new Channel(i, flatBits, 1));
            }
            else
            {
                foreach (Channel ch in flat.Channels)
                    ch.BitsPerPixel = flatBits;
            }

            if (flat.Resolutions.Count > 0)
            {
                Resolution res0 = flat.Resolutions[0];
                res0.PixelFormat = flatBits > 8 ? PixelFormat.Format16bppGrayScale : PixelFormat.Format8bppIndexed;
                flat.Resolutions[0] = res0;
            }

            return flat;
        }

        /// <summary>
        /// Runs an ImageJ command on a pyramidal BioImage by flattening the
        /// selected pyramid level to a temporary OME-TIFF first.
        /// </summary>
        private static async Task<BioImage> RunOnPyramidalImageInternal(BioImage b, string con, int level, bool headless, bool onTab, bool bioformats, bool resultInNewTab, bool record)
        {
            if (b == null)
                return null;
            if (!b.isPyramidal)
            {
                Console.WriteLine($"[ImageJ.RunOnPyramidalImageInternal] non-pyramidal passthrough id={b.ID}");
                RunOnImage(b, con, headless, onTab, bioformats, resultInNewTab);
                return b;
            }

            Console.WriteLine($"[ImageJ.RunOnPyramidalImageInternal] start id={b.ID} level={level} bioformats={bioformats}");
            BioImage flat = await FlattenPyramidalImage(b, level).ConfigureAwait(false);
            if (flat == null)
            {
                Console.WriteLine("[ImageJ.RunOnPyramidalImageInternal] flatten returned null");
                return null;
            }

            string tempDir = Path.GetDirectoryName(Environment.ProcessPath);
            string tempPath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".ome.tif");
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            BioImage.SaveOME(flat, tempPath);
            flat.file = tempPath;
            flat.Filename = Path.GetFileName(tempPath);
            flat.ID = Path.GetFileName(tempPath);
            Console.WriteLine($"[ImageJ.RunOnPyramidalImageInternal] flatTemp={tempPath} flatId={flat.ID}");

            try
            {
                string outputStem = Path.GetFileNameWithoutExtension(tempPath) + "-result";
                Console.WriteLine($"[ImageJ.RunOnPyramidalImageInternal] outputStem={outputStem}");
                RunOnImage(flat, con, level ,headless, onTab, bioformats, resultInNewTab, outputStem);
                string fn = outputStem + (bioformats ? ".ome.tif" : ".tif");
                BioImage bm = Images.GetImage(fn);
                if (bm == null && bioformats)
                    bm = Images.GetImage(outputStem + ".tif");
                Console.WriteLine($"[ImageJ.RunOnPyramidalImageInternal] lookup fn={fn} result={(bm == null ? "null" : bm.ID)}");
                if (record)
                    Recorder.Record($"ImageJ.RunOnPyramidalImage({b}, \"{con}\", {level}, {headless.ToString().ToLower()}, {onTab.ToString().ToLower()}, {bioformats.ToString().ToLower()}, {resultInNewTab.ToString().ToLower()});");
                return bm;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Runs an ImageJ command on a pyramidal BioImage by flattening the
        /// selected pyramid level to a temporary OME-TIFF first.
        /// </summary>
        public static async Task<BioImage> RunOnPyramidalImage(BioImage b, string con, int level, bool headless, bool onTab, bool bioformats, bool resultInNewTab)
        {
            return await RunOnPyramidalImageInternal(b, con, level, headless, onTab, bioformats, resultInNewTab, true).ConfigureAwait(false);
        }

        /// <summary>
        /// Runs an ImageJ command on every pyramid level and returns a single
        /// pyramidal BioImage backed by a temporary OME-TIFF.
        /// </summary>
        public static async Task<BioImage> RunOnAllPyramidLevels(BioImage b, string con, bool headless, bool onTab, bool bioformats, bool resultInNewTab)
        {
            if (b == null)
                return null;

            if (!b.isPyramidal || b.Resolutions.Count == 0)
            {
                RunOnImage(b, con, headless, onTab, bioformats, resultInNewTab);
                Recorder.Record($"ImageJ.RunOnImage({b}, \"{con}\", {headless.ToString().ToLower()}, {onTab.ToString().ToLower()}, {bioformats.ToString().ToLower()}, {resultInNewTab.ToString().ToLower()});");
                return b;
            }

            List<BioImage> results = new List<BioImage>();
            for (int level = 0; level < b.Resolutions.Count; level++)
            {
                BioImage result = await RunOnPyramidalImageInternal(b, con, level, headless, onTab, bioformats, resultInNewTab, false).ConfigureAwait(false);
                if (result != null)
                    results.Add(result);
            }

            if (results.Count == 0)
                return null;

            string tempDir = Path.GetDirectoryName(Environment.ProcessPath);
            string tempFile = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + "-pyr.ome.tif");
            BioImage.SaveOMEPyramidal(results.ToArray(), tempFile, NetVips.Enums.ForeignTiffCompression.None, 0);

            BioImage pyramidal = await BioImage.OpenFileAsync(tempFile, 0, true, true).ConfigureAwait(false);
            Recorder.Record($"ImageJ.RunOnImage({b}, \"{con}\", {headless.ToString().ToLower()}, {onTab.ToString().ToLower()}, {bioformats.ToString().ToLower()}, {resultInNewTab.ToString().ToLower()});");
            return pyramidal;
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
            if (b != null && b.isPyramidal)
            {
                RunOnPyramidalImage(b, con, b.Level, headless, onTab, bioformats, resultInNewTab).Wait();
                return;
            }
            RunOnImage(b, con,0,headless,onTab,bioformats,resultInNewTab);
            Recorder.AddLine("ImageJ.RunOnImage(Images.GetImage(\"" + b.Filename + "\"),\"" + con + "\"," + 0 + "," + headless + "," + onTab + "," + bioformats + "," + resultInNewTab + ");",false);
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
        /// contains a list of points (Points) that define the boundary of the region.</param>
        /// <param name="xp">An output parameter that will contain the x-coordinates of the points in
        /// the ROI.</param>
        /// <param name="yp">The `yp` parameter is an output parameter of type `int[]`. It is used to
        /// store the Y-coordinates of the points in the `roi` object after converting them to image
        /// space using the `bi` object.</param>
        static void GetPointsXY(BioImage bi,ROI roi, out int[] xp, out int[] yp)
        {
            int[] x = new int[roi.Points.Count];
            int[] y = new int[roi.Points.Count];
            for (int i = 0; i < roi.Points.Count; i++)
            {
                PointD pd = bi.ToImageSpace(roi.Points[i]);
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
