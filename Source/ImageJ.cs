using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using CSScripting;
using Gtk;
using AForge;
using ZarrNET.Core;
using ZarrNET.Core.OmeZarr.Metadata;
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
        private static readonly object extractionCacheLock = new object();
        private static readonly Dictionary<string, BioImage> extractionCache = new Dictionary<string, BioImage>();
        private static readonly LinkedList<string> extractionCacheOrder = new LinkedList<string>();
        private const int MaxExtractionCacheEntries = 6;
        private const int MaxRemoteExtractionParallelism = 6;
        private const int MaxLocalExtractionParallelism = 3;

        private sealed class PyramidExtractionRequest
        {
            public int Level;
            public int X;
            public int Y;
            public int Width;
            public int Height;
            public double StageX;
            public double StageY;
            public bool IsCurrentViewOnly;
            public string Description;
        }

        private static int GetExtractionParallelism(BioImage image)
        {
            int cap = IsRemotePyramidalSource(image) ? MaxRemoteExtractionParallelism : MaxLocalExtractionParallelism;
            return Math.Max(1, Math.Min(cap, Environment.ProcessorCount));
        }
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
            string sourcePath = bioformats ? file : inputPath;
            if (!File.Exists(sourcePath))
            {
                Console.WriteLine($"[ImageJ.RunOnImage] input file missing after run: {sourcePath}");
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
                lastRunOutput = bm;
            }
            else
            {
                BioImage b = BioImage.OpenFileAsync(f, index, false, false).Result;
                Console.WriteLine($"[ImageJ.RunOnImage] updating existing image with {b?.ID} filename={b?.Filename}");
                b.ID = bi.ID;
                b.Filename = bi.ID;
                b.file = dir + "/" + fn;
                Images.UpdateImage(b);
                lastRunOutput = b;
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

        private static BioImage lastRunOutput;
        private static bool TryGetPyramidLevelGeometry(BioImage b, int level, out Resolution res, out int width, out int height, out double unitsPerPixel)
        {
            res = default;
            width = 0;
            height = 0;
            unitsPerPixel = 0;

            if (b == null || b.Resolutions.Count == 0)
                return false;

            level = Math.Clamp(level, 0, b.Resolutions.Count - 1);
            res = b.Resolutions[level];
            int schemaWidth = 0;
            int schemaHeight = 0;
            unitsPerPixel = res.PhysicalSizeX;

            if (b.OpenSlideBase?.Schema?.Resolutions != null && b.OpenSlideBase.Schema.Resolutions.Count > level)
            {
                var schemaRes = b.OpenSlideBase.Schema.Resolutions[level];
                schemaWidth = GetIntProperty(schemaRes, "Width", "SizeX");
                schemaHeight = GetIntProperty(schemaRes, "Height", "SizeY");
                unitsPerPixel = schemaRes.UnitsPerPixel;
            }
            else if (b.SlideBase?.Schema?.Resolutions != null && b.SlideBase.Schema.Resolutions.Count > level)
            {
                var schemaRes = b.SlideBase.Schema.Resolutions[level];
                schemaWidth = GetIntProperty(schemaRes, "Width", "SizeX");
                schemaHeight = GetIntProperty(schemaRes, "Height", "SizeY");
                unitsPerPixel = schemaRes.UnitsPerPixel;
            }

            width = schemaWidth > 0 ? schemaWidth : (res.SizeX > 0 ? res.SizeX : b.SizeX);
            height = schemaHeight > 0 ? schemaHeight : (res.SizeY > 0 ? res.SizeY : b.SizeY);
            return width > 0 && height > 0;
        }
        private static bool IsRemotePyramidalSource(BioImage b)
        {
            if (b == null || !b.IsZarrSource)
                return false;

            string source = b.SourceFile;
            return !string.IsNullOrWhiteSpace(source) &&
                (source.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                 source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 source.StartsWith("s3://", StringComparison.OrdinalIgnoreCase));
        }
        private static PyramidExtractionRequest BuildExtractionRequest(BioImage b, int level, bool currentViewOnly)
        {
            if (!TryGetPyramidLevelGeometry(b, level, out Resolution res, out int levelWidth, out int levelHeight, out double schemaUnitsPerPixel))
                return null;

            double physX = res.PhysicalSizeX > 0 ? res.PhysicalSizeX : Math.Max(schemaUnitsPerPixel, 1.0);
            double physY = res.PhysicalSizeY > 0 ? res.PhysicalSizeY : physX;

            var request = new PyramidExtractionRequest
            {
                Level = Math.Clamp(level, 0, b.Resolutions.Count - 1),
                X = 0,
                Y = 0,
                Width = levelWidth,
                Height = levelHeight,
                StageX = res.StageSizeX,
                StageY = res.StageSizeY,
                IsCurrentViewOnly = false,
                Description = $"pyramid level {level + 1}/{b.Resolutions.Count}"
            };

            if (!currentViewOnly)
                return request;

            if (b.PyramidalSize.Width <= 1 || b.PyramidalSize.Height <= 1)
                return request;

            double viewUnitsPerPixel = b.Resolution > 0 ? b.Resolution : physX;
            double viewWorldX = b.PyramidalOrigin.X;
            double viewWorldY = b.PyramidalOrigin.Y;
            double viewWorldW = Math.Max(1, b.PyramidalSize.Width) * viewUnitsPerPixel;
            double viewWorldH = Math.Max(1, b.PyramidalSize.Height) * (b.Resolution > 0 ? b.Resolution : physY);

            int x = (int)Math.Floor((viewWorldX - res.StageSizeX) / physX);
            int y = (int)Math.Floor((viewWorldY - res.StageSizeY) / physY);
            int w = (int)Math.Ceiling(viewWorldW / physX);
            int h = (int)Math.Ceiling(viewWorldH / physY);

            x = Math.Max(0, x);
            y = Math.Max(0, y);
            w = Math.Min(levelWidth - x, Math.Max(1, w));
            h = Math.Min(levelHeight - y, Math.Max(1, h));

            if (w <= 0 || h <= 0)
                return request;

            long fullArea = Math.Max(1L, (long)levelWidth * levelHeight);
            long regionArea = Math.Max(1L, (long)w * h);
            if (regionArea * 10 >= fullArea * 9)
                return request;

            request.X = x;
            request.Y = y;
            request.Width = w;
            request.Height = h;
            request.StageX = res.StageSizeX + (x * physX);
            request.StageY = res.StageSizeY + (y * physY);
            request.IsCurrentViewOnly = true;
            request.Description = $"visible region of level {level + 1}/{b.Resolutions.Count}";
            return request;
        }
        private static string GetExtractionCacheKey(BioImage source, PyramidExtractionRequest request)
        {
            string raw = string.Join("|",
                source?.SourceFile ?? source?.ID ?? string.Empty,
                request.Level,
                request.X,
                request.Y,
                request.Width,
                request.Height,
                source?.SizeZ ?? 0,
                source?.SizeC ?? 0,
                source?.SizeT ?? 0);

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            StringBuilder sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
        private static BioImage TryGetCachedExtraction(string cacheKey)
        {
            if (string.IsNullOrWhiteSpace(cacheKey))
                return null;

            lock (extractionCacheLock)
            {
                if (!extractionCache.TryGetValue(cacheKey, out BioImage cached) || cached == null)
                    return null;

                var node = extractionCacheOrder.Find(cacheKey);
                if (node != null)
                {
                    extractionCacheOrder.Remove(node);
                    extractionCacheOrder.AddLast(node);
                }
                return cached;
            }
        }
        private static void StoreCachedExtraction(string cacheKey, BioImage image)
        {
            if (string.IsNullOrWhiteSpace(cacheKey) || image == null)
                return;

            lock (extractionCacheLock)
            {
                if (extractionCache.ContainsKey(cacheKey))
                    return;

                extractionCache[cacheKey] = image;
                extractionCacheOrder.AddLast(cacheKey);
                while (extractionCacheOrder.Count > MaxExtractionCacheEntries)
                {
                    string evictedKey = extractionCacheOrder.First.Value;
                    extractionCacheOrder.RemoveFirst();
                    if (extractionCache.TryGetValue(evictedKey, out BioImage evicted) && evicted != null)
                    {
                        try
                        {
                            evicted.Dispose();
                        }
                        catch
                        {
                        }
                    }
                    extractionCache.Remove(evictedKey);
                }
            }
        }
        /// <summary>
        /// Extracts either a full pyramid level or the currently visible region
        /// from a pyramidal BioImage and returns it as a single-resolution image.
        /// </summary>
        private static async Task<BioImage> PreparePyramidalLevelImage(BioImage b, int level, bool currentViewOnly)
        {
            if (b == null)
                return null;
            if (!b.isPyramidal || b.Resolutions.Count == 0)
                return b;

            level = Math.Clamp(level, 0, b.Resolutions.Count - 1);
            PyramidExtractionRequest request = BuildExtractionRequest(b, level, currentViewOnly);
            if (request == null)
                return null;

            string cacheKey = IsRemotePyramidalSource(b) ? GetExtractionCacheKey(b, request) : null;
            if (!string.IsNullOrWhiteSpace(cacheKey))
            {
                BioImage cached = TryGetCachedExtraction(cacheKey);
                if (cached != null)
                {
                    ReportProgress(100, $"Using cached {request.Description}");
                    return cached;
                }
            }

            Console.WriteLine($"[ImageJ.PreparePyramidalLevelImage] extracting {request.Description} from {b.ID}");
            BioImage.Status = $"Extracting {request.Description}";

            if (!TryGetPyramidLevelGeometry(b, level, out Resolution res, out _, out _, out double schemaUnitsPerPixel))
                return null;

            BioImage pyrlevel = BioImage.CopyInfo(b, true, true);
            Resolution fixedRes = pyrlevel.Resolutions[level];
            fixedRes.SizeX = request.Width;
            fixedRes.SizeY = request.Height;
            fixedRes.PhysicalSizeX = res.PhysicalSizeX;
            fixedRes.PhysicalSizeY = res.PhysicalSizeY;
            fixedRes.PhysicalSizeZ = res.PhysicalSizeZ;
            fixedRes.StageSizeX = request.StageX;
            fixedRes.StageSizeY = request.StageY;
            fixedRes.StageSizeZ = res.StageSizeZ;
            pyrlevel.Resolutions.Clear();
            pyrlevel.Resolutions.Add(fixedRes);

            pyrlevel.PyramidalOrigin = new PointD(0, 0);
            pyrlevel.PyramidalSize = new AForge.Size(request.Width, request.Height);
            pyrlevel.Resolution = schemaUnitsPerPixel > 0 ? schemaUnitsPerPixel : fixedRes.PhysicalSizeX;
            pyrlevel.Level = 0;
            pyrlevel.StackOrder = BioImage.Order.ZCT;
            pyrlevel.UpdateCoords(b.SizeZ, b.SizeC, b.SizeT, BioImage.Order.ZCT);
            pyrlevel.Volume = new VolumeD(
                new Point3D(request.StageX, request.StageY, fixedRes.StageSizeZ),
                new Point3D(request.Width * fixedRes.PhysicalSizeX, request.Height * fixedRes.PhysicalSizeY, Math.Max(1, b.SizeZ) * fixedRes.PhysicalSizeZ));

            pyrlevel.Buffers.Clear();
            var planes = new List<(int Sequence, int Index, int Z, int C, int T)>(Math.Max(1, b.SizeT * b.SizeC * b.SizeZ));
            for (int t = 0; t < b.SizeT; t++)
            {
                for (int c = 0; c < b.SizeC; c++)
                {
                    for (int z = 0; z < b.SizeZ; z++)
                    {
                        int index = b.GetFrameIndex(z, c, t);
                        if (index < 0)
                            continue;

                        planes.Add((planes.Count, index, z, c, t));
                    }
                }
            }

            int totalPlanes = planes.Count;
            if (totalPlanes == 0)
                return null;

            Bitmap[] extractedPlanes = new Bitmap[totalPlanes];
            int loadedPlanes = 0;
            int parallelism = GetExtractionParallelism(b);
            using (SemaphoreSlim gate = new SemaphoreSlim(parallelism, parallelism))
            {
                Task[] extractionTasks = planes.Select(async plane =>
                {
                    await gate.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        extractedPlanes[plane.Sequence] = await b.GetTile(
                            plane.Index, level,
                            request.X, request.Y, request.Width, request.Height,
                            new AForge.ZCT(plane.Z, plane.C, plane.T),
                            true).ConfigureAwait(false);

                        int completedPlanes = Interlocked.Increment(ref loadedPlanes);
                        ReportProgress(
                            (completedPlanes * 100f) / totalPlanes,
                            $"Extracting {request.Description} plane T{plane.T + 1}/{b.SizeT} Z{plane.Z + 1}/{b.SizeZ} C{plane.C + 1}/{b.SizeC}");
                    }
                    finally
                    {
                        gate.Release();
                    }
                }).ToArray();

                await Task.WhenAll(extractionTasks).ConfigureAwait(false);
            }

            foreach (Bitmap tile in extractedPlanes)
            {
                if (tile != null)
                    pyrlevel.Buffers.Add(tile);
            }

            if (pyrlevel.Buffers.Count == 0)
                return null;

            if (!string.IsNullOrWhiteSpace(cacheKey))
                StoreCachedExtraction(cacheKey, pyrlevel);
            return pyrlevel;
        }

        private static int GetIntProperty(object obj, params string[] names)
        {
            if (obj == null)
                return 0;

            foreach (string name in names)
            {
                var prop = obj.GetType().GetProperty(name);
                if (prop == null)
                    continue;

                object value = prop.GetValue(obj);
                if (value == null)
                    continue;

                if (value is int i)
                    return i;

                if (int.TryParse(value.ToString(), out int parsed))
                    return parsed;
            }

            return 0;
        }

        private static void ReportProgress(float progress, string status = null)
        {
            BioImage.Progress = Math.Max(0f, Math.Min(100f, progress));
            if (!string.IsNullOrWhiteSpace(status))
                BioImage.Status = status;
        }
        private static BioImage ApplyProcessedResultName(BioImage source, BioImage result)
        {
            if (source == null || result == null)
                return result;

            string sourceName = string.IsNullOrWhiteSpace(source.Filename) ? source.ID : source.Filename;
            if (string.IsNullOrWhiteSpace(sourceName))
                return result;

            string friendlyName = Images.GetImageName(Path.GetFileName(sourceName));
            result.Filename = friendlyName;
            result.ID = friendlyName;
            return result;
        }

        private static int GetCommandCaptureLevel(BioImage b)
        {
            if (b == null || b.Resolutions.Count == 0)
                return 0;

            int level = b.Resolutions.Count - 1;
            if (b.MacroResolution.HasValue)
                level = Math.Min(level, Math.Max(0, b.MacroResolution.Value - 1));
            return Math.Max(0, level);
        }

        private static async Task<string> TryParameterizePyramidalCommand(BioImage b, string con, bool headless, bool resultInNewTab)
        {
            if (b == null || headless || !Fiji.CanParameterizeInteractiveRunCommand(con))
                return con;

            int captureLevel = GetCommandCaptureLevel(b);
            ReportProgress(0, $"Configuring Fiji command from pyramid level {captureLevel + 1}/{b.Resolutions.Count}");
            BioImage previewImage = await PreparePyramidalLevelImage(b, captureLevel, false).ConfigureAwait(false);
            if (previewImage == null)
                return con;

            if (Fiji.TryParameterizeInteractiveRunCommand(previewImage, con, headless, resultInNewTab, out string parameterizedCommand))
                return parameterizedCommand;
            return con;
        }

        /// <summary>
        /// Runs an ImageJ command on a pyramidal BioImage by extracting the
        /// requested level, persisting that level as a temporary Zarr dataset,
        /// then processing it through Fiji.
        /// </summary>
        private static async Task<BioImage> RunOnPyramidalImageInternal(BioImage b, string con, int level, bool headless, bool onTab, bool bioformats, bool resultInNewTab, bool record, bool currentViewOnly)
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
            ReportProgress(0, $"Preparing pyramid level {level + 1}/{b.Resolutions.Count}");
            BioImage levelImage = await PreparePyramidalLevelImage(b, level, currentViewOnly).ConfigureAwait(false);
            if (levelImage == null)
            {
                Console.WriteLine("[ImageJ.RunOnPyramidalImageInternal] level preparation returned null");
                return null;
            }

            bool useTiledFallback = ShouldUseTiledFallback(levelImage);
            if (useTiledFallback)
            {
                Console.WriteLine("[ImageJ.RunOnPyramidalImageInternal] using tiled fallback for large pyramid level");
                try
                {
                    BioImage tiled = await RunOnPyramidalLevelTiled(levelImage, b, con, level, headless, resultInNewTab).ConfigureAwait(false);
                    if (record)
                        Recorder.Record($"ImageJ.RunOnPyramidalImage({b}, \"{con}\", {level}, {headless.ToString().ToLower()}, {onTab.ToString().ToLower()}, {bioformats.ToString().ToLower()}, {resultInNewTab.ToString().ToLower()}, {currentViewOnly.ToString().ToLower()});");
                    return tiled;
                }
                finally
                {
                }
            }

            try
            {
                ReportProgress(0, $"Running Fiji on pyramid level {level + 1}/{b.Resolutions.Count}");
                Console.WriteLine($"[ImageJ.RunOnPyramidalImageInternal] dispatching level image through IKVM/ImagePlus runner");
                BioImage bm = Fiji.RunOnImageInProcess(levelImage, con, headless, true, resultInNewTab, false);
                if (bm != null && bm.Resolutions.Count > 0 && levelImage.Resolutions.Count > 0)
                {
                    Resolution srcRes = levelImage.Resolutions[0];
                    if (bm.Buffers.Count > 0 && bm.Buffers[0].PixelFormat != srcRes.PixelFormat)
                    {
                        Console.WriteLine($"[ImageJ.RunOnPyramidalImageInternal] normalizing {bm.Buffers[0].PixelFormat} -> {srcRes.PixelFormat}");
                        if (srcRes.PixelFormat == AForge.PixelFormat.Format8bppIndexed)
                            bm.To8Bit();
                        else if (srcRes.PixelFormat == AForge.PixelFormat.Format16bppGrayScale)
                            bm.To16Bit();
                        else if (srcRes.PixelFormat == AForge.PixelFormat.Format24bppRgb)
                            bm.To24Bit();
                        else if (srcRes.PixelFormat == AForge.PixelFormat.Format48bppRgb)
                            bm.To48Bit();
                    }
                    Resolution outRes = bm.Resolutions[0];
                    outRes.PixelFormat = srcRes.PixelFormat;
                    outRes.PhysicalSizeX = srcRes.PhysicalSizeX;
                    outRes.PhysicalSizeY = srcRes.PhysicalSizeY;
                    outRes.PhysicalSizeZ = srcRes.PhysicalSizeZ;
                    outRes.StageSizeX = srcRes.StageSizeX;
                    outRes.StageSizeY = srcRes.StageSizeY;
                    outRes.StageSizeZ = srcRes.StageSizeZ;
                    bm.Resolutions[0] = outRes;
                    bm.Volume = levelImage.Volume;
                }
                ReportProgress(100, $"Completed pyramid level {level + 1}/{b.Resolutions.Count}");
                bm = ApplyProcessedResultName(b, bm);
                Console.WriteLine($"[ImageJ.RunOnPyramidalImageInternal] result={(bm == null ? "null" : bm.ID)}");
                if (record)
                    Recorder.Record($"ImageJ.RunOnPyramidalImage({b}, \"{con}\", {level}, {headless.ToString().ToLower()}, {onTab.ToString().ToLower()}, {bioformats.ToString().ToLower()}, {resultInNewTab.ToString().ToLower()}, {currentViewOnly.ToString().ToLower()});");
                return bm;
            }
            finally
            {
            }
        }

        private static bool ShouldUseTiledFallback(BioImage levelImage)
        {
            if (levelImage == null || levelImage.Buffers == null || levelImage.Buffers.Count == 0)
                return false;

            var fmt = levelImage.Buffers[0].PixelFormat;
            if (fmt != PixelFormat.Format8bppIndexed && fmt != PixelFormat.Format16bppGrayScale)
                return false;

            long estimatedBytes = (long)Math.Max(1, levelImage.SizeX) * Math.Max(1, levelImage.SizeY)
                * Math.Max(1, levelImage.SizeZ) * Math.Max(1, levelImage.SizeC) * Math.Max(1, levelImage.SizeT)
                * (fmt == PixelFormat.Format16bppGrayScale ? 2L : 1L);
            return estimatedBytes > 256L * 1024L * 1024L;
        }

        private static async Task<BioImage> RunOnPyramidalLevelTiled(BioImage levelImage, BioImage source, string con, int level, bool headless, bool resultInNewTab)
        {
            if (levelImage == null || levelImage.Buffers.Count == 0)
                return null;

            int width = levelImage.SizeX;
            int height = levelImage.SizeY;
            if (width <= 0 || height <= 0)
                return null;

            string tempDir = Path.GetDirectoryName(Environment.ProcessPath);
            string tempLevelZarr = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + "-level.zarr");
            if (Directory.Exists(tempLevelZarr))
                Directory.Delete(tempLevelZarr, true);
            if (File.Exists(tempLevelZarr))
                File.Delete(tempLevelZarr);

            int bytesPerSample = levelImage.Buffers[0].PixelFormat == PixelFormat.Format16bppGrayScale ? 2 : 1;
            int planeCount = Math.Max(1, levelImage.SizeZ * levelImage.SizeC * levelImage.SizeT);
            (int tileW, int tileH) = GetLargestSafeTileSize(width, height, bytesPerSample, planeCount);
            var coord = new ZarrNET.Core.ZCT(levelImage.SizeZ, levelImage.SizeC, levelImage.SizeT);
            var baseDescriptor = new BioImageDescriptor(width, height, coord)
            {
                Name = Path.GetFileNameWithoutExtension(levelImage.Filename),
                DataType = bytesPerSample == 2 ? "uint16" : "uint8",
                PhysicalSizeX = levelImage.PhysicalSizeX,
                PhysicalSizeY = levelImage.PhysicalSizeY,
                PhysicalSizeZ = levelImage.PhysicalSizeZ,
                ChunkX = Math.Max(1, tileW),
                ChunkY = Math.Max(1, tileH),
            };
            var levelDescriptors = new List<ResolutionLevelDescriptor> { new ResolutionLevelDescriptor(width, height, 1.0) };
            var writer = await OmeZarrWriter.CreateAsync(tempLevelZarr, baseDescriptor, levelDescriptors).ConfigureAwait(false);
            try
            {
                long totalTiles = Math.Max(1L, ((width + tileW - 1) / tileW) * ((height + tileH - 1) / tileH) * Math.Max(1, levelImage.SizeZ) * Math.Max(1, levelImage.SizeC) * Math.Max(1, levelImage.SizeT));
                long writtenTiles = 0;

                for (int tileY = 0; tileY < height; tileY += tileH)
                {
                    for (int tileX = 0; tileX < width; tileX += tileW)
                    {
                        int actualW = Math.Min(tileW, width - tileX);
                        int actualH = Math.Min(tileH, height - tileY);

                        BioImage tileInput = BioImage.CopyInfo(source, true, true);
                        tileInput.Buffers.Clear();
                        tileInput.UpdateCoords(source.SizeZ, source.SizeC, source.SizeT);
                        tileInput.Resolutions.Clear();
                        tileInput.Resolutions.Add(new Resolution(actualW, actualH, source.Buffers[0].PixelFormat, source.PhysicalSizeX, source.PhysicalSizeY, source.PhysicalSizeZ, source.StageSizeX, source.StageSizeY, source.StageSizeZ));

                        for (int t = 0; t < source.SizeT; t++)
                        {
                            for (int c = 0; c < source.SizeC; c++)
                            {
                                for (int z = 0; z < source.SizeZ; z++)
                                {
                                    int index = source.GetFrameIndex(z, c, t);
                                    if (index < 0)
                                        continue;

                                    Bitmap tile = await source.GetTile(
                                        index, level,
                                        tileX, tileY, actualW, actualH,
                                        new AForge.ZCT(z, c, t),
                                        true).ConfigureAwait(false);
                                    if (tile != null)
                                        tileInput.Buffers.Add(tile);
                                }
                            }
                        }

                        if (tileInput.Buffers.Count == 0)
                            continue;

                        BioImage processed = Fiji.RunOnImageInProcess(tileInput, con, headless, true, resultInNewTab, false);
                        if (processed == null || processed.Buffers.Count == 0)
                            continue;

                        int outIndex = 0;
                        for (int t = 0; t < processed.SizeT; t++)
                        {
                            for (int c = 0; c < processed.SizeC; c++)
                            {
                                for (int z = 0; z < processed.SizeZ; z++)
                                {
                                    if (outIndex >= processed.Buffers.Count)
                                        continue;

                                    await Zarr.WriteTileFromBuffer(
                                        writer,
                                        processed.Buffers[outIndex],
                                        levelDescriptors[0],
                                        t, c, z,
                                        tileX, tileY,
                                        processed.Buffers[outIndex].PixelFormat == PixelFormat.Format16bppGrayScale ? 2 : 1,
                                        levelIndex: 0).ConfigureAwait(false);
                                    outIndex++;
                                    writtenTiles++;
                                    BioImage.Progress = (float)(writtenTiles * 100.0 / totalTiles);
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                try
                {
                    await writer.DisposeAsync().ConfigureAwait(false);
                }
                catch
                {
                }
            }

            BioImage result = await BioImage.OpenFileAsync(tempLevelZarr, 0, false, false).ConfigureAwait(false);
            return ApplyProcessedResultName(source, result);
        }

        private static (int tileW, int tileH) GetLargestSafeTileSize(int width, int height, int bytesPerSample, int planeCount)
        {
            const long maxTileBytes = 128L * 1024L * 1024L;
            if (width <= 0 || height <= 0)
                return (1, 1);

            long bytesPerPixelAllPlanes = Math.Max(1L, (long)bytesPerSample * planeCount);

            (int w, int h) best = (1, 1);
            long bestArea = 1;

            void Consider(int candidateW, int candidateH)
            {
                candidateW = Math.Max(1, Math.Min(width, candidateW));
                candidateH = Math.Max(1, Math.Min(height, candidateH));
                long bytes = (long)candidateW * candidateH * bytesPerPixelAllPlanes;
                if (bytes > maxTileBytes)
                    return;

                long area = (long)candidateW * candidateH;
                if (area > bestArea)
                {
                    best = (candidateW, candidateH);
                    bestArea = area;
                }
            }

            long maxHeightForFullWidth = maxTileBytes / Math.Max(1L, (long)width * bytesPerPixelAllPlanes);
            if (maxHeightForFullWidth > 0)
                Consider(width, (int)Math.Min(height, maxHeightForFullWidth));

            long maxWidthForFullHeight = maxTileBytes / Math.Max(1L, (long)height * bytesPerPixelAllPlanes);
            if (maxWidthForFullHeight > 0)
                Consider((int)Math.Min(width, maxWidthForFullHeight), height);

            long pixelsBudget = Math.Max(1L, maxTileBytes / bytesPerPixelAllPlanes);
            int square = Math.Max(1, (int)Math.Min(Math.Min(width, height), Math.Sqrt(pixelsBudget)));
            for (int delta = 0; delta < 8; delta++)
            {
                int candidateW = Math.Max(1, Math.Min(width, square + delta));
                int candidateH = Math.Max(1, (int)Math.Min(height, pixelsBudget / candidateW));
                Consider(candidateW, candidateH);
            }

            if (bestArea > 1)
                return best;

            return (1, Math.Max(1, (int)Math.Min(height, pixelsBudget)));
        }

        /// <summary>
        /// Runs an ImageJ command on a pyramidal BioImage by processing the
        /// requested level as a single-resolution image for Fiji.
        /// </summary>
        public static async Task<BioImage> RunOnPyramidalImage(BioImage b, string con, int level, bool headless, bool onTab, bool bioformats, bool resultInNewTab, bool currentViewOnly = false)
        {
            return await RunOnPyramidalImageInternal(b, con, level, headless, onTab, bioformats, resultInNewTab, true, currentViewOnly).ConfigureAwait(false);
        }

        /// <summary>
        /// Runs an ImageJ command on every pyramid level and returns a single
        /// pyramidal BioImage assembled from the processed resolution levels.
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

            string effectiveCommand = await TryParameterizePyramidalCommand(b, con, headless, resultInNewTab).ConfigureAwait(false);
            List<BioImage> results = new List<BioImage>();
            for (int level = 0; level < b.Resolutions.Count; level++)
            {
                ReportProgress(0, $"Processing pyramid level {level + 1}/{b.Resolutions.Count}");
                BioImage result = await RunOnPyramidalImageInternal(b, effectiveCommand, level, headless, onTab, bioformats, resultInNewTab, false, false).ConfigureAwait(false);
                if (result != null)
                    results.Add(result);
            }

            if (results.Count == 0)
                return null;

            string tempDir = Path.GetDirectoryName(Environment.ProcessPath);
            string tempFile = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + "-pyr.zarr");
            ReportProgress(0, $"Saving processed pyramid ({results.Count}/{b.Resolutions.Count} levels)");
            await BioImage.SaveOMEZarr(results.ToArray(), tempFile).ConfigureAwait(false);

            ReportProgress(0, "Opening processed pyramid");
            BioImage pyramidal = await BioImage.OpenFileAsync(tempFile, 0, false, false).ConfigureAwait(false);
            pyramidal = ApplyProcessedResultName(b, pyramidal);
            ReportProgress(100, "Completed pyramid command");
            Recorder.Record($"ImageJ.RunOnAllPyramidLevels({b}, \"{con}\", {headless.ToString().ToLower()}, {onTab.ToString().ToLower()}, {bioformats.ToString().ToLower()}, {resultInNewTab.ToString().ToLower()});");
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
                RunOnAllPyramidLevels(b, con, headless, onTab, bioformats, resultInNewTab).Wait();
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
