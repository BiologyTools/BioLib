extern alias FijiNet;
using AForge;
using BioLib;
using CSScripting;
using FijiNet::ij;
using Gtk;
using java.util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ByteProcessor = FijiNet::ij.process.ByteProcessor;
using ColorProcessor = FijiNet::ij.process.ColorProcessor;
using ImageJRecorder = FijiNet::ij.plugin.frame.Recorder;
using IJ = FijiNet::ij.IJ;
using ij = FijiNet::ij;
using ImagePlus = FijiNet::ij.ImagePlus;
using ImageProcessor = FijiNet::ij.process.ImageProcessor;
using ImageStack = FijiNet::ij.ImageStack;
using IndexColorModel = java.awt.image.IndexColorModel;
using Random = System.Random;
using RectangleD = AForge.RectangleD;
using Roi = FijiNet::ij.gui.Roi;
using ShortProcessor = FijiNet::ij.process.ShortProcessor;
using WindowManager = FijiNet::ij.WindowManager;

namespace BioLib
{
    public static class Fiji
    {
        public static class Macro
        {
            public class Command
            {
                public string Name { get; set; }
                public string Arguments { get; set; }
                public string Description { get; set; }
                public Command(string name, string args, string description)
                {
                    Name = name;
                    Arguments = args;
                    Description = description;
                }
                public override string ToString()
                {
                    return Name.ToString();
                }
            }
            public class Function
            {
                public string Name { get; set; }
                public string Arguments { get; set; }
                public string Description { get; set; }
                public Function(string name, string args, string description)
                {
                    Name = name;
                    Arguments = args;
                    Description = description;
                }
                public override string ToString()
                {
                    return Name.ToString();
                }
            }
            public static Dictionary<string, Command> Commands = new Dictionary<string, Command>();
            public static Dictionary<string, List<Function>> Functions = new Dictionary<string, List<Function>>();
            internal static void Initialize()
            {
                Commands.Clear();
                string[] sts = File.ReadAllLines(System.IO.Path.GetDirectoryName(Environment.ProcessPath) + "/macro-functions.txt");
                foreach (string s in sts)
                {
                    string com = "";
                    string args = "";
                    string doc = "";
                    bool indoc = false, inargs = false;
                    if (!s.StartsWith('#'))
                    {
                        for (int i = 0; i < s.Length; i++)
                        {
                            if (!inargs)
                            {
                                if (s[i] != '(')
                                    com += s[i];
                                else
                                    inargs = true;
                                if (s[i] == ' ')
                                {
                                    inargs = true;
                                    indoc = true;
                                }
                            }
                            else
                            if (!indoc)
                                if (s[i] != ')')
                                    args += s[i];
                                else
                                    indoc = true;
                            else
                                doc += s[i];
                        }
                        if (!Functions.ContainsKey(com))
                            Functions.Add(com, new List<Function>() { new Function(com, args, doc) });
                        else
                            Functions[com].Add(new Function(com, args, doc));
                    }
                }
                if (OperatingSystem.IsMacOS())
                    LoadCommandTableFromCsv();
                else
                    EnsureCommandTableLoaded();
            }
        }
        static bool init = false;
        public static bool Initialized { get { return init; } private set { init = true; } }
        static string ijpath = "";
        public static string ImageJPath
        {
            get
            {
                return ijpath;
            }
            set
            {
                ijpath = value.Replace("\\","/");
            }
        }
        public static string ImageJMacroPath
        {
            get
            {
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    string p = System.IO.Path.GetDirectoryName(ImageJPath).Replace("\\", "/");
                    p = p.Replace("/Fiji.app/Contents/MacOS/", "/Fiji.app/");
                    return p;
                }
                else
                    return System.IO.Path.GetDirectoryName(ImageJPath) + "/macros/";
            }
        }

        public static bool UseFiji
        {
            get
            {
                if(ImageJPath.Contains("Fiji"))
                    return true;
                else
                    return false;
            }
        }
        static short[] ConvertByteArrayToShortArray(byte[] byteArray, bool littleEndian)
        {
            if (byteArray.Length % 2 != 0)
            {
                throw new ArgumentException("The length of the byte array must be even.");
            }

            // Each short is made from two bytes
            short[] shortArray = new short[byteArray.Length / 2];

            for (int i = 0; i < shortArray.Length; i++)
            {
                if (littleEndian)
                {
                    // If the system is little-endian, reverse the byte order
                    shortArray[i] = (short)((byteArray[i * 2 + 1] << 8) | byteArray[i * 2]);
                }
                else
                {
                    // If the system is big-endian, keep the byte order as is
                    shortArray[i] = (short)((byteArray[i * 2] << 8) | byteArray[i * 2 + 1]);
                }
            }
            return shortArray;
        }
        public static ImagePlus GetImagePlus(BioImage bm)
        {
            if (bm == null)
                throw new ArgumentNullException(nameof(bm));
            if (bm.Buffers == null || bm.Buffers.Count == 0 || bm.Buffers[0] == null)
            {
                if (bm.isPyramidal)
                    throw new InvalidOperationException("Pyramidal images do not populate BioImage.Buffers. Extract a pyramid level or tile region before converting it to ImagePlus.");
                throw new InvalidOperationException("The selected image does not contain any pixel buffers.");
            }

            int width = bm.Buffers[0].Width;
            int height = bm.Buffers[0].Height;
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException($"The selected image has invalid buffer dimensions ({width}x{height}).");

            ImageStack ims = new ImageStack(width, height);
            if (bm.Buffers[0].PixelFormat == AForge.PixelFormat.Format16bppGrayScale)
            {
                // Populate the stack with 16-bit images
                for (int t = 0; t < bm.SizeT; t++)
                {
                    for (int z = 0; z < bm.SizeZ; z++)
                    {
                        for (int c = 0; c < bm.SizeC; c++)
                        {
                            AForge.Bitmap buffer = GetImagePlusBuffer(bm, z, c, t, width, height);
                            short[] pixels = Copy16BitPixels(buffer, width, height);
                            ImageProcessor ip = new ShortProcessor(width, height, pixels, java.awt.image.ColorModel.getRGBdefault());
                            ims.addSlice("Z" + z + "C" + c + "T" + t, ip);
                        }
                    }
                }
            }
            else if (bm.Buffers[0].PixelFormat == AForge.PixelFormat.Format8bppIndexed)
            {
                // Create a grayscale IndexColorModel
                byte[] grayscale = new byte[256];
                for (int i = 0; i < 256; i++)
                {
                    grayscale[i] = (byte)i;
                }
                IndexColorModel colorModel = new IndexColorModel(8, 256, grayscale, grayscale, grayscale);
                for (int t = 0; t < bm.SizeT; t++)
                {
                    for (int z = 0; z < bm.SizeZ; z++)
                    {
                        for (int c = 0; c < bm.SizeC; c++)
                        {
                            AForge.Bitmap buffer = GetImagePlusBuffer(bm, z, c, t, width, height);
                            byte[] pixels = Copy8BitPixels(buffer, width, height);
                            ImageProcessor ip = new ByteProcessor(width, height, pixels, colorModel);
                            ims.addSlice("Z" + z + "C" + c + "T" + t, ip);
                        }
                    }
                }
            }
            else if (bm.Buffers[0].PixelFormat == AForge.PixelFormat.Format24bppRgb)
            {
                for (int t = 0; t < bm.SizeT; t++)
                {
                    for (int z = 0; z < bm.SizeZ; z++)
                    {
                        for (int c = 0; c < bm.SizeC; c++)
                        {
                            AForge.Bitmap buffer = GetImagePlusBuffer(bm, z, c, t, width, height);
                            byte[] bts = buffer.Bytes;
                            // Convert the byte array to an int array for the RGB processor
                            int[] rgbPixels = new int[width * height];
                            for (int y = 0; y < height; y++)
                            {
                                int sourceRow = y * buffer.Stride;
                                int targetRow = y * width;
                                for (int x = 0; x < width; x++)
                                {
                                    int source = sourceRow + (x * 3);
                                    // AForge RGB formats use the native Windows
                                    // packed layout: B, G, R in memory.
                                    int b = bts[source] & 0xFF;
                                    int g = bts[source + 1] & 0xFF;
                                    int r = bts[source + 2] & 0xFF;
                                    rgbPixels[targetRow + x] = (r << 16) | (g << 8) | b;
                                }
                            }
                            // Create a ColorProcessor
                            ImageProcessor ip = new ColorProcessor(width, height, rgbPixels);
                            ims.addSlice("Z" + z + "C" + c + "T" + t, ip);
                        }
                    }
                }
            }
            else if (bm.Buffers[0].PixelFormat == AForge.PixelFormat.Format32bppArgb || bm.Buffers[0].PixelFormat == AForge.PixelFormat.Format32bppRgb)
            {
                for (int t = 0; t < bm.SizeT; t++)
                {
                    for (int z = 0; z < bm.SizeZ; z++)
                    {
                        for (int c = 0; c < bm.SizeC; c++)
                        {
                            AForge.Bitmap buffer = GetImagePlusBuffer(bm, z, c, t, width, height);
                            byte[] bts = buffer.Bytes;
                            // Convert the byte array to an int array for the ColorProcessor
                            int[] rgbaPixels = new int[width * height];
                            for (int y = 0; y < height; y++)
                            {
                                int sourceRow = y * buffer.Stride;
                                int targetRow = y * width;
                                for (int x = 0; x < width; x++)
                                {
                                    int source = sourceRow + (x * 4);
                                    int b = bts[source] & 0xFF;
                                    int g = bts[source + 1] & 0xFF;
                                    int r = bts[source + 2] & 0xFF;
                                    int a = bts[source + 3] & 0xFF;
                                    rgbaPixels[targetRow + x] = (a << 24) | (r << 16) | (g << 8) | b;
                                }
                            }
                            // Create a ColorProcessor with the ARGB int array
                            ImageProcessor ip = new ColorProcessor(width, height, rgbaPixels);
                            ims.addSlice("Z" + z + "C" + c + "T" + t, ip);
                        }
                    }
                }
            }
            else if (bm.Buffers[0].PixelFormat == AForge.PixelFormat.Format48bppRgb)
            {
                bm.To16Bit();
                // Populate the stack with 16-bit images
                for (int t = 0; t < bm.SizeT; t++)
                {
                    for (int z = 0; z < bm.SizeZ; z++)
                    {
                        for (int c = 0; c < bm.SizeC; c++)
                        {
                            AForge.Bitmap buffer = GetImagePlusBuffer(bm, z, c, t, width, height);
                            short[] pixels = Copy16BitPixels(buffer, width, height);
                            ImageProcessor ip = new ShortProcessor(width, height, pixels, java.awt.image.ColorModel.getRGBdefault());
                            ims.addSlice("Z" + z + "C" + c + "T" + t, ip);
                        }
                    }
                }
            }
            ImagePlus imp = new ImagePlus(bm.ID, ims);
            imp.setDimensions(bm.SizeC, bm.SizeZ, bm.SizeT);
            return imp;
        }

        private static AForge.Bitmap GetImagePlusBuffer(BioImage image, int z, int c, int t, int width, int height)
        {
            int index = image.GetFrameIndex(z, c, t);
            if (index < 0 || index >= image.Buffers.Count)
                throw new InvalidOperationException($"Image plane Z={z}, C={c}, T={t} maps to buffer {index}, but only {image.Buffers.Count} buffers are available.");

            AForge.Bitmap buffer = image.Buffers[index];
            if (buffer == null)
                throw new InvalidOperationException($"Image buffer {index} (Z={z}, C={c}, T={t}) is empty.");
            if (buffer.Width != width || buffer.Height != height)
                throw new InvalidOperationException($"Image buffer {index} is {buffer.Width}x{buffer.Height}; expected {width}x{height}.");

            int requiredLength = checked(buffer.Stride * height);
            if (buffer.Bytes == null || buffer.Bytes.Length < requiredLength)
                throw new InvalidOperationException($"Image buffer {index} contains {buffer.Bytes?.Length ?? 0} bytes; {requiredLength} are required for {width}x{height} {buffer.PixelFormat} pixels.");
            return buffer;
        }

        private static byte[] Copy8BitPixels(AForge.Bitmap buffer, int width, int height)
        {
            byte[] pixels = new byte[checked(width * height)];
            for (int y = 0; y < height; y++)
                Buffer.BlockCopy(buffer.Bytes, y * buffer.Stride, pixels, y * width, width);
            return pixels;
        }

        private static short[] Copy16BitPixels(AForge.Bitmap buffer, int width, int height)
        {
            short[] pixels = new short[checked(width * height)];
            bool sourceLittleEndian = buffer.LittleEndian;
            for (int y = 0; y < height; y++)
            {
                int sourceRow = y * buffer.Stride;
                int targetRow = y * width;
                for (int x = 0; x < width; x++)
                {
                    int source = sourceRow + (x * 2);
                    ushort value = sourceLittleEndian
                        ? (ushort)(buffer.Bytes[source] | (buffer.Bytes[source + 1] << 8))
                        : (ushort)((buffer.Bytes[source] << 8) | buffer.Bytes[source + 1]);
                    pixels[targetRow + x] = unchecked((short)value);
                }
            }
            return pixels;
        }
        private static int GetHyperStackSliceIndex(ImagePlus pl, int z, int c, int t)
        {
            return pl.getStackIndex(c + 1, z + 1, t + 1) - 1;
        }
        public static bool isRGB(ImagePlus image)
        {
            return image.getType() == ImagePlus.COLOR_RGB;
        }
        public static int[] getRGBPixelsFromSlice(ImageStack stack, int sliceIndex)
        {
            // Get the ImageProcessor for the specified slice
            ImageProcessor ip = stack.getProcessor(sliceIndex + 1); // ImageStack uses 1-based indexing
            // Cast to ColorProcessor to access RGB data
            ColorProcessor colorProcessor = (ColorProcessor)ip;
            // Get the RGB pixel data
            return (int[])colorProcessor.getPixels();
        }
        /// <summary>
        /// Converts ImagePlus to BioImage.
        /// </summary>
        /// <param name="pl"></param>
        /// <param name="maxz"></param>
        /// <param name="maxc"></param>
        /// <param name="maxt"></param>
        /// <param name="location"></param>
        /// <returns>The BioImage represented by the ImagePlus object.</returns>
        public static BioImage GetBioImage(ImagePlus pl, VolumeD vol, double PhysicalX, double PhysicalY, double PhysicalZ)
        {
            if (pl == null)
                throw new ArgumentNullException(nameof(pl));

            BioImage bm = new BioImage(pl.getTitle());
            ImageStack st = pl.getImageStack();
            if (st == null || st.getSize() <= 0)
                throw new InvalidOperationException("The ImageJ command did not produce an image stack.");

            int b = pl.getBitDepth();
            int stackSize = st.getSize();
            int slices = Math.Max(1, pl.getNSlices());
            int chs = Math.Max(1, pl.getNChannels());
            int frs = Math.Max(1, pl.getNFrames());
            NormalizeImageJDimensions(stackSize, ref slices, ref chs, ref frs);
            int rgb = (int)(pl.getBytesPerPixel() * 8);
            bool isrgb = isRGB(pl);
            bm.StackOrder = BioImage.Order.ZCT;
            bm.UpdateCoords(slices, chs, frs, BioImage.Order.ZCT);
            AForge.Bitmap[] outputBuffers = new AForge.Bitmap[Math.Max(1, slices * chs * frs)];
            for (int t = 0; t < frs; t++)
            {
                for (int z = 0; z < slices; z++)
                {
                    for (int c = 0; c < chs; c++)
                    {
                        AForge.Bitmap bmp;
                        if (!isrgb)
                        {
                            if (rgb > 8)
                            {
                                bmp = new AForge.Bitmap(st.getWidth(), st.getHeight(), AForge.PixelFormat.Format16bppGrayScale);
                            }
                            else
                            {
                                bmp = new AForge.Bitmap(st.getWidth(), st.getHeight(), AForge.PixelFormat.Format8bppIndexed);
                            }
                        }
                        else
                        {
                            bmp = new AForge.Bitmap(st.getWidth(), st.getHeight(), AForge.PixelFormat.Format24bppRgb);
                        }
                        int ind = bm.GetFrameIndex(z, c, t);
                        // ImageJ stores hyperstacks with channels varying fastest.
                        // Calculate against the normalized dimensions instead of
                        // trusting a stale ImagePlus dimension table.
                        int stackIndex = c + (chs * (z + (slices * t)));
                        if (stackIndex < 0 || stackIndex >= stackSize)
                            throw new InvalidOperationException($"ImageJ stack index {stackIndex} is outside the {stackSize}-plane result stack.");
                        outputBuffers[ind] = bmp;
                        if (!isrgb)
                        {
                            for (int y = 0; y < pl.getHeight(); y++)
                            {
                                for (int x = 0; x < pl.getWidth(); x++)
                                {
                                    double d = st.getVoxel(x, y, stackIndex);
                                    if (bmp.PixelFormat == PixelFormat.Format16bppGrayScale)
                                        bmp.SetValue(x, y, (ushort)d);
                                    else if (bmp.PixelFormat == PixelFormat.Format8bppIndexed)
                                        bmp.SetValue(x, y, (byte)d);
                                }
                            }
                        }
                        else
                        {
                            ImageProcessor ip = st.getProcessor(stackIndex + 1); // ImageStack uses 1-based indexing
                            // Cast to ColorProcessor to access RGB data
                            ColorProcessor colorProcessor = (ColorProcessor)ip;
                            for (int y = 0; y < pl.getHeight(); y++)
                            {
                                for (int x = 0; x < pl.getWidth(); x++)
                                {
                                    int v = colorProcessor.getPixel(x, y);
                                    int rv = (v >> 16) & 0xFF; // Red
                                    int gv = (v >> 8) & 0xFF;  // Green
                                    int bv = v & 0xFF;         // Blue
                                    // Keep the AForge buffer in packed BGR order.
                                    bmp.SetValue(x, y, 0, (byte)bv);
                                    bmp.SetValue(x, y, 1, (byte)gv);
                                    bmp.SetValue(x, y, 2, (byte)rv);
                                }
                            }

                        }
                    }
                }
            }
            bm.Buffers.AddRange(outputBuffers.Where(buf => buf != null));
            for (int t = 0; t < chs; t++)
            {
                if (isrgb)
                    bm.Channels.Add(new AForge.Channel(t, rgb, 3));
                else
                    bm.Channels.Add(new AForge.Channel(t, rgb, 1));
                if (t == 0)
                {
                    bm.rgbChannels[0] = 0;
                }
                else
                if (t == 1)
                {
                    bm.rgbChannels[1] = 1;
                }
                else
                if (t == 2)
                {
                    bm.rgbChannels[2] = 2;
                }
            }
            bm.Resolutions.Add(new Resolution(pl.getWidth(), pl.getHeight(), bm.Buffers[0].PixelFormat, PhysicalX, PhysicalY, PhysicalZ, vol.Location.X, vol.Location.Y, vol.Location.X));
            bm.Volume = vol;
            bm.littleEndian = BitConverter.IsLittleEndian;
            bm.seriesCount = 1;
            BioImage.AutoThreshold(bm, true);
            if (rgb > 8)
                bm.StackThreshold(true);
            else
                bm.StackThreshold(false);

            if (bm.RGBChannelCount == 4)
            {
                bm.Channels.Last().SamplesPerPixel = 4;
            }
            return bm;
        }

        private static void NormalizeImageJDimensions(int stackSize, ref int slices, ref int channels, ref int frames)
        {
            long declaredSize = (long)slices * channels * frames;
            if (declaredSize == stackSize)
                return;

            // Some ImageJ commands replace or reshape the current stack without
            // updating all three hyperstack dimensions. Preserve every dimension
            // that still divides the actual stack before falling back to Z planes.
            long channelsAndFrames = (long)channels * frames;
            if (channelsAndFrames > 0 && stackSize % channelsAndFrames == 0)
            {
                slices = stackSize / (int)channelsAndFrames;
                return;
            }

            long slicesAndFrames = (long)slices * frames;
            if (slicesAndFrames > 0 && stackSize % slicesAndFrames == 0)
            {
                channels = stackSize / (int)slicesAndFrames;
                return;
            }

            long slicesAndChannels = (long)slices * channels;
            if (slicesAndChannels > 0 && stackSize % slicesAndChannels == 0)
            {
                frames = stackSize / (int)slicesAndChannels;
                return;
            }

            slices = stackSize;
            channels = 1;
            frames = 1;
        }

        public static List<Process> processes = new List<Process>();
        public static List<Macro.Command> Macros = new List<Macro.Command>();
        private static Random rng = new Random();
        public static BioImage RunImageJ(BioImage b,string file, string param)
        {
            if (!init)
                RunMacroFiji(b, file, param);
            Console.WriteLine("RunImageJ(" + file + "," + param);
            ImagePlus ip = null;
            try
            {
                ip = GetImagePlus(b);
                Console.WriteLine("Fiji.GetImagePlus(" + b.ToString() + ")");
                WindowManager.setTempCurrentImage(ip);
                Console.WriteLine("WindowManager.setTempCurrentImage(" + ip.ToString() + ")");
                IJ.runMacroFile(file, param);
                BioImage bm = GetBioImage(ip, b.Volume, b.PhysicalSizeX, b.PhysicalSizeY, b.PhysicalSizeZ);
                int i = Images.GetImageCountByName(bm.Filename);
                if (bm.Filename.EndsWith("-" + i))
                    bm.Filename.Replace("-" + i, "-" + (i + 1).ToString());
                return bm;
            }
            finally
            {
                ClearCurrentImageAndRelease(ip, null);
            }
        }
        /// It runs a macro in ImageJ
        /// 
        /// @param file the path to the macro file
        /// @param param The parameter to pass to the macro.
        /// 
        /// @return The macro is being returned.
        public static void RunMacroFiji(BioImage b, string macrofile, string param)
        {
            if (UseFiji)
            {
                Process pr = new Process();
                pr.StartInfo.FileName = ImageJPath;
                pr.StartInfo.Arguments = "-macro " + macrofile + " " + param;
                pr.Start();
                processes.Add(pr);
            }
            else
            {
                RunImageJ(b, b.file, param);
            }
            BioLib.Recorder.Record($"Fiji.RunMacroFiji({b}, \"{macrofile}\", \"{param}\");");
        }
        /// It runs a macro in ImageJ
        /// 
        /// @param con The macro code
        /// @param param The parameters to pass to the macro.
        /// @param headless Whether or not to run ImageJ in headless mode.
        /// 
        /// @return Nothing.
        public async static Task<BioImage> RunStringFiji(BioImage b,string con, string param, bool headless, string outputPath = null)
        {
            if (b == null)
                throw new ArgumentNullException(nameof(b));
            if (string.IsNullOrWhiteSpace(ImageJPath) || !File.Exists(ImageJPath))
                throw new FileNotFoundException("The Fiji/ImageJ executable is not configured or does not exist.", ImageJPath);

            string runDirectory = Path.Combine(Path.GetTempPath(), "BioGTK-Fiji", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(runDirectory);
            string macroPath = Path.Combine(runDirectory, "command.ijm");
            string donePath = Path.Combine(runDirectory, "done.txt");
            string escapedDonePath = donePath.Replace("\\", "/").Replace("\"", "\\\"");
            File.WriteAllText(macroPath,
                con + Environment.NewLine +
                "File.saveString(\"done\", \"" + escapedDonePath + "\");");

            Process pr = new Process();
            pr.StartInfo.FileName = ImageJPath;
            pr.StartInfo.UseShellExecute = false;
            pr.StartInfo.RedirectStandardOutput = true;
            pr.StartInfo.RedirectStandardError = true;
            string imageJDirectory = Path.GetDirectoryName(ImageJPath);
            if (!string.IsNullOrWhiteSpace(imageJDirectory))
                pr.StartInfo.WorkingDirectory = imageJDirectory;
            if (headless)
                pr.StartInfo.ArgumentList.Add("--headless");
            pr.StartInfo.ArgumentList.Add("-macro");
            pr.StartInfo.ArgumentList.Add(macroPath);
            if (!string.IsNullOrWhiteSpace(param))
                pr.StartInfo.ArgumentList.Add(param);

            Console.WriteLine($"[Fiji.RunStringFiji] Starting '{ImageJPath}' with macro '{macroPath}'.");
            if (!pr.Start())
                throw new InvalidOperationException("Fiji/ImageJ could not be started.");

            processes.Add(pr);
            Task<string> standardOutput = pr.StandardOutput.ReadToEndAsync();
            Task<string> standardError = pr.StandardError.ReadToEndAsync();
            try
            {
                while (!File.Exists(donePath) && !pr.HasExited)
                    await Task.Delay(100).ConfigureAwait(false);

                // Some macOS launchers exit just before their child process flushes
                // the marker/output. Give that handoff a short grace period.
                if (!File.Exists(donePath) && pr.HasExited)
                {
                    for (int attempt = 0; attempt < 50 && !File.Exists(donePath); attempt++)
                        await Task.Delay(100).ConfigureAwait(false);
                }

                if (File.Exists(donePath) && !pr.HasExited)
                {
                    pr.Kill(true);
                    await pr.WaitForExitAsync().ConfigureAwait(false);
                }

                string f = outputPath;
                if (string.IsNullOrWhiteSpace(f))
                {
                    f = Path.Combine(
                        Path.GetDirectoryName(b.file) ?? string.Empty,
                        Path.GetFileNameWithoutExtension(b.file) + "-temp.ome.tif");
                }

                for (int attempt = 0; attempt < 50 && !File.Exists(f); attempt++)
                    await Task.Delay(100).ConfigureAwait(false);

                if (!File.Exists(f))
                {
                    string error = (await standardError.ConfigureAwait(false)).Trim();
                    string output = (await standardOutput.ConfigureAwait(false)).Trim();
                    throw new InvalidOperationException(
                        "Fiji/ImageJ did not create the expected result file '" + f + "'." +
                        (string.IsNullOrWhiteSpace(error) ? string.Empty : Environment.NewLine + error) +
                        (string.IsNullOrWhiteSpace(output) ? string.Empty : Environment.NewLine + output));
                }

                Console.WriteLine("[Fiji.RunStringFiji] Opening result: " + f);
                BioImage bm = await BioImage.OpenFile(f, 0, false, true, false, 0, 0).ConfigureAwait(false);
                if (bm == null)
                    throw new InvalidDataException("The Fiji/ImageJ result could not be opened: " + f);

                BioLib.Recorder.Record($"Fiji.RunStringFiji({b}, \"{con}\", \"{param}\", {headless.ToString().ToLower()});");
                return bm;
            }
            finally
            {
                processes.Remove(pr);
                if (!pr.HasExited)
                {
                    try
                    {
                        pr.Kill(true);
                    }
                    catch
                    {
                    }
                }
                pr.Dispose();
                try
                {
                    Directory.Delete(runDirectory, true);
                }
                catch
                {
                }
            }
        }
        /// It runs a macro on the current image, saves the result as a new image, and then opens the
        /// new image in a new tab
        /// 
        /// @param con The ImageJ macro to run on the image.
        /// @param headless Whether to run ImageJ in headless mode.
        /// @param onTab If true, the imacro will be run on tab. If false, the macro will be run on selected image.
        /// @param bioformats If true, the image is opened using the bioformats plugin. If false, the
        /// image is opened using the default imagej open command.
        /// 
        /// @return The image is being returned as a new tab.
        public async static Task<BioImage> RunOnImageFiji(BioImage b, string con, int index, bool headless, bool onTab, bool bioformats, bool resultInNewTab)
        {
            // BioGTK hosts ImageJ through IKVM on macOS. Keep this legacy API
            // compatible without ever spawning the external Fiji application.
            if (OperatingSystem.IsMacOS())
            {
                BioLib.Recorder.Record($"Fiji.RunOnImageFiji({b}, \"{con}\", {index}, {headless.ToString().ToLower()}, {onTab.ToString().ToLower()}, {bioformats.ToString().ToLower()}, {resultInNewTab.ToString().ToLower()});");
                return RunOnImageInProcess(b, con, headless, bioformats, resultInNewTab);
            }

            if (UseFiji)
            {
                string filename = "";
                string dir = Path.GetDirectoryName(b.file);
                dir = dir.Replace("\\", "/");
                if (b.ID.EndsWith(".ome.tif"))
                {
                    filename = Path.GetFileNameWithoutExtension(b.ID);
                    filename = filename.Remove(filename.Length - 4, 4);
                }
                else
                    filename = Path.GetFileNameWithoutExtension(b.ID);
                string outputStem = GetSequentialOutputStem(dir, filename);
                string file = dir + "/" + filename + ".ome.tif";
                string filet = dir + "/" + outputStem + ".ome.tif";
                if (!bioformats)
                    file = dir + "/" + filename + ".tif";
                string outputFile = bioformats ? filet : dir + "/" + outputStem + ".tif";
                string roiZip = ExportRoiZipForFiji(b, filename);
                string op = b.ID.Replace("\\", "/");
                string ex = Path.GetExtension(b.ID);
                op = op.Replace(ex, ".ome.tif");
                //if (File.Exists(file))
                //    File.Delete(file);
                BioImage cleanInput = BioImage.Copy(b, false);
                BioImage.SaveOME(cleanInput, file);
                string st =
                "run(\"Bio-Formats Importer\", \"open=" + file + " autoscale color_mode=Default open_all_series display_rois rois_import=[ROI manager] view=Hyperstack stack_order=XYCZT\"); " + con +
                "roiManager(\"Show None\"); run(\"Select None\"); " +
                "run(\"Bio-Formats Exporter\", \"save=" + filet + " export compression=Uncompressed\");";
                if (!bioformats)
                    st =
                    "open(getArgument); " +
                    (!string.IsNullOrWhiteSpace(roiZip)
                        ? "roiManager(\"Reset\"); roiManager(\"Open\", \"" + roiZip.Replace("\\", "/") + "\"); roiManager(\"Show All\"); "
                        : string.Empty) +
                    con +
                    "roiManager(\"Show None\"); run(\"Select None\"); " +
                    "saveAs(\"Tiff\",\"" + outputFile + "\");";

                BioImage bm = await Fiji.RunStringFiji(b, st, bioformats ? "" : file, headless, outputFile);
                if (File.Exists(file) && bioformats)
                    File.Delete(file);
                BioLib.Recorder.Record($"Fiji.RunOnImageFiji({b}, \"{con}\", {index}, {headless.ToString().ToLower()}, {onTab.ToString().ToLower()}, {bioformats.ToString().ToLower()}, {resultInNewTab.ToString().ToLower()});");
                return bm;
            }
            else
            {
                cons = con;
                indexs = index;
                Fiji.headless = headless;
                onTabs = onTab;
                Fiji.bioformats = bioformats;
                resultInNewTabs = resultInNewTab;
                bioImage = b;
                Task<BioImage> t = new Task<BioImage>(() => RunImageJ());
                t.Start();
                t.Wait();
                BioLib.Recorder.Record($"Fiji.RunOnImageFiji({b}, \"{con}\", {index}, {headless.ToString().ToLower()}, {onTab.ToString().ToLower()}, {bioformats.ToString().ToLower()}, {resultInNewTab.ToString().ToLower()});");
                return t.Result;
            }
        }

        /// <summary>
        /// Returns a stable, collision-free name for a processed Fiji result.
        /// Reprocessing an earlier result advances the numeric suffix instead of
        /// repeatedly appending "-temp" (for example: image-temp, image-temp-2).
        /// </summary>
        private static string GetSequentialOutputStem(string directory, string inputStem)
        {
            const string generatedSuffixPattern = @"^(?<root>.+?)(?:-temp)+(?:-\d+)?$";
            Match generatedName = Regex.Match(
                inputStem ?? string.Empty,
                generatedSuffixPattern,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            string root = generatedName.Success
                ? generatedName.Groups["root"].Value
                : inputStem;
            if (string.IsNullOrWhiteSpace(root))
                root = "image";

            int sequence = 1;
            while (true)
            {
                string candidate = sequence == 1
                    ? root + "-temp"
                    : root + "-temp-" + sequence.ToString();

                bool fileExists = File.Exists(Path.Combine(directory, candidate + ".tif")) ||
                    File.Exists(Path.Combine(directory, candidate + ".ome.tif"));
                bool imageExists = Images.images.Any(image =>
                {
                    string imageName = image?.Filename;
                    if (string.IsNullOrWhiteSpace(imageName))
                        imageName = image?.ID;
                    return string.Equals(
                        Images.RemoveExtensions(Path.GetFileName(imageName ?? string.Empty)),
                        candidate,
                        StringComparison.OrdinalIgnoreCase);
                });

                if (!fileExists && !imageExists)
                    return candidate;

                sequence++;
            }
        }

        private static string ExportRoiZipForFiji(BioImage image, string stem)
        {
            if (image == null || image.Annotations == null || image.Annotations.Count == 0)
                return string.Empty;

            try
            {
                string tempRoot = Path.Combine(Path.GetTempPath(), "BioLib-Rois");
                Directory.CreateDirectory(tempRoot);

                string tempDir = Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);

                int index = 0;
                foreach (ROI roi in image.Annotations)
                {
                    if (roi == null)
                        continue;

                    string safeName = string.IsNullOrWhiteSpace(roi.roiName)
                        ? $"{stem}_{index}"
                        : $"{stem}_{index}_{roi.roiName}";
                    foreach (char bad in Path.GetInvalidFileNameChars())
                        safeName = safeName.Replace(bad, '_');

                    string roiPath = Path.Combine(tempDir, safeName + ".roi");
                    RoiEncoder.save(image, roi, roiPath);
                    index++;
                }

                if (index == 0)
                    return string.Empty;

                string zipPath = Path.Combine(tempRoot, stem + "-" + Guid.NewGuid().ToString("N") + ".zip");
                if (File.Exists(zipPath))
                    File.Delete(zipPath);

                using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    foreach (string file in Directory.GetFiles(tempDir, "*.roi"))
                        zip.CreateEntryFromFile(file, Path.GetFileName(file), CompressionLevel.Fastest);
                }

                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch
                {
                }

                return zipPath.Replace("\\", "/");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Fiji.ExportRoiZipForFiji] {ex.Message}");
                return string.Empty;
            }
        }

        static string cons;
        static int indexs;
        static bool headless;
        static bool onTabs;
        static bool bioformats;
        static bool resultInNewTabs;
        static BioImage bioImage;
        private static readonly object imageJRunLock = new object();
        private static readonly object embeddedRuntimeConfigurationLock = new object();
        // ImageJ clears macro options when the string is empty.
        private const string DefaultMacroOptions = " ";
        private static volatile bool embeddedRuntimeConfigured;
        private static readonly bool useHeadlessMacRuntime = OperatingSystem.IsMacOS() &&
            !(AppContext.TryGetSwitch("BioLib.EnableMacOSImageJDialogs", out bool enableDialogs) && enableDialogs);

        [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "setenv", SetLastError = true)]
        private static extern int SetNativeEnvironment(string name, string value, int overwrite);

        /// <summary>
        /// Prepares the embedded IKVM runtime before ImageJ initializes any
        /// Java graphics classes. GTK provides the AppKit event loop when the
        /// bundled IKVM runtime supports native dialogs.
        /// </summary>
        private static void ConfigureEmbeddedImageJRuntime()
        {
            if (embeddedRuntimeConfigured)
                return;

            lock (embeddedRuntimeConfigurationLock)
            {
                if (embeddedRuntimeConfigured)
                    return;

                if (useHeadlessMacRuntime)
                {
                    java.lang.System.setProperty("java.awt.headless", "true");
                    java.lang.System.setProperty("apple.awt.UIElement", "true");
                }
                else if (OperatingSystem.IsMacOS())
                {
                    // OpenJDK uses this flag to use the host's existing AppKit event loop.
                    if (SetNativeEnvironment($"JAVA_STARTED_ON_FIRST_THREAD_{Environment.ProcessId}", "1", 1) != 0)
                        throw new InvalidOperationException("Could not configure the macOS ImageJ dialog runtime.");

                    java.lang.System.setProperty("java.awt.headless", "false");
                    if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                    {
                        // The IKVM Java 8 runtime image ships the Aqua library only for Intel.
                        const string metalLookAndFeel = "javax.swing.plaf.metal.MetalLookAndFeel";
                        java.lang.System.setProperty("swing.defaultlaf", metalLookAndFeel);
                        java.lang.System.setProperty("swing.systemlaf", metalLookAndFeel);
                    }
                }

                embeddedRuntimeConfigured = true;
            }
        }

        public static BioImage RunOnImageInProcess(BioImage b, string con, bool headless, bool bioformats, bool resultInNewTab, bool registerWithImages = true)
        {
            ConfigureEmbeddedImageJRuntime();
            lock (imageJRunLock)
            {
                cons = PrepareEmbeddedMacCommand(con, headless);
                indexs = 0;
                Fiji.headless = headless;
                Fiji.bioformats = bioformats;
                resultInNewTabs = resultInNewTab;
                bioImage = b;
                return RunImageJ(registerWithImages);
            }
        }

        public static bool CanParameterizeInteractiveRunCommand(string con)
        {
            return TryExtractBareRunCommand(con, out _);
        }

        public static bool TryParameterizeInteractiveRunCommand(BioImage previewImage, string con, bool headless, bool resultInNewTab, out string parameterizedCommand)
        {
            parameterizedCommand = con;
            if (previewImage == null || headless || !TryExtractBareRunCommand(con, out string commandName))
                return false;

            ConfigureEmbeddedImageJRuntime();

            if (useHeadlessMacRuntime)
            {
                parameterizedCommand = BuildRunCommand(commandName, DefaultMacroOptions);
                return true;
            }

            lock (imageJRunLock)
            {
                ImageJRecorder recorder = ImageJRecorder.getInstance();
                bool createdRecorder = false;
                bool previousRecordInMacros = ImageJRecorder.recordInMacros;
                try
                {
                    if (recorder == null)
                    {
                        recorder = new ImageJRecorder(false);
                        createdRecorder = recorder != null;
                    }

                    if (recorder == null)
                        return false;

                    ImageJRecorder.recordInMacros = true;
                    ImageJRecorder.resetCommandOptions();
                    string previousText = recorder.getText() ?? string.Empty;

                    cons = con;
                    indexs = 0;
                    Fiji.headless = headless;
                    resultInNewTabs = resultInNewTab;
                    bioImage = previewImage;
                    RunImageJ(false);

                    string recordedText = recorder.getText() ?? string.Empty;
                    if (TryExtractRecordedRunCommand(recordedText, previousText, commandName, out parameterizedCommand))
                        return true;

                    string options = ImageJRecorder.getCommandOptions() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(options))
                        return false;

                    parameterizedCommand = BuildRunCommand(commandName, options);
                    return true;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[Fiji.TryParameterizeInteractiveRunCommand] {e.Message}");
                    parameterizedCommand = con;
                    return false;
                }
                finally
                {
                    ImageJRecorder.resetCommandOptions();
                    ImageJRecorder.recordInMacros = previousRecordInMacros;
                    if (createdRecorder && recorder != null)
                    {
                        try
                        {
                            recorder.close();
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        // ImageJ uses an invisible AWT owner when embedded in GTK. Its modal
        // dialogs can otherwise open behind the host's windows and wait unseen.
        private sealed class ImageJDialogFocusScope : java.awt.@event.AWTEventListener, IDisposable
        {
            private readonly java.awt.Toolkit toolkit = java.awt.Toolkit.getDefaultToolkit();
            private readonly object sync = new object();
            private readonly List<(java.awt.Dialog Dialog, bool AlwaysOnTop)> dialogs = new();
            private bool disposed;

            public ImageJDialogFocusScope()
            {
                toolkit.addAWTEventListener(this, java.awt.AWTEvent.WINDOW_EVENT_MASK);
            }

            public void eventDispatched(java.awt.AWTEvent ev)
            {
                if (ev.getID() != java.awt.@event.WindowEvent.WINDOW_OPENED ||
                    ev.getSource() is not java.awt.Dialog dialog || !dialog.isModal())
                    return;

                lock (sync)
                {
                    if (disposed)
                        return;
                    dialogs.Add((dialog, dialog.isAlwaysOnTop()));
                    BioImage.Progress = 0;
                    BioImage.Status = $"Waiting for ImageJ dialog: {dialog.getTitle()}";
                    dialog.setAlwaysOnTop(true);
                    dialog.toFront();
                    dialog.requestFocus();
                }
            }

            public void Dispose()
            {
                toolkit.removeAWTEventListener(this);
                lock (sync)
                {
                    disposed = true;
                    foreach (var entry in dialogs)
                    {
                        if (entry.Dialog.isDisplayable())
                            entry.Dialog.setAlwaysOnTop(entry.AlwaysOnTop);
                    }
                    dialogs.Clear();
                }
            }
        }

        private static BioImage RunImageJ(bool registerWithImages = true)
        {
            ConfigureEmbeddedImageJRuntime();
            using var dialogFocus = OperatingSystem.IsWindows() && !java.awt.GraphicsEnvironment.isHeadless()
                ? new ImageJDialogFocusScope()
                : null;
            ImagePlus ip = null;
            ImagePlus result = null;
            try
            {
                BioImage.Progress = 0;
                BioImage.Status = "Preparing ImageJ command";
                ip = GetImagePlus(bioImage);
                WindowManager.setTempCurrentImage(ip);
                BioImage.Status = "Running ImageJ command";
                string macroResult = IJ.runMacro(cons);
                BioImage.Status = "Converting ImageJ result";
                result = WindowManager.getCurrentImage() ?? ip;
                if (useHeadlessMacRuntime && macroResult == "[aborted]")
                    throw new InvalidOperationException("ImageJ aborted while running the macro in headless mode.");
                BioImage b = GetBioImage(result, bioImage.Volume, bioImage.PhysicalSizeX, bioImage.PhysicalSizeY, bioImage.PhysicalSizeZ);
                string s = registerWithImages ? Images.GetImageName(b.Filename) : b.Filename;
                if (registerWithImages)
                    Images.AddImage(b);
                b.Filename = s;
                b.ID = s;
                return b;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("[Fiji.RunImageJ] " + e);
                throw new InvalidOperationException("ImageJ command failed while converting its image data: " + e.Message, e);
            }
            finally
            {
                ClearCurrentImageAndRelease(ip, result);
            }
        }

        /// <summary>
        /// Releases the ImageJ images used by an embedded command. ImageJ keeps the
        /// temporary current image in a static, per-thread table; leaving it there
        /// roots the ImagePlus, its ImageStack and every managed pixel array under IKVM.
        /// </summary>
        private static void ClearCurrentImageAndRelease(ImagePlus input, ImagePlus result)
        {
            try
            {
                WindowManager.setTempCurrentImage(null);
            }
            catch
            {
            }

            ReleaseImagePlus(result);
            if (input != null && !ReferenceEquals(input, result))
                ReleaseImagePlus(input);
        }

        private static void ReleaseImagePlus(ImagePlus image)
        {
            if (image == null)
                return;

            try
            {
                image.close();
            }
            catch
            {
            }

            try
            {
                image.flush();
            }
            catch
            {
            }
        }

        private static bool TryExtractBareRunCommand(string con, out string commandName)
        {
            commandName = null;
            if (string.IsNullOrWhiteSpace(con))
                return false;

            string trimmed = con.Trim();
            const string prefix = "run(\"";
            const string suffix = "\");";
            if (!trimmed.StartsWith(prefix, StringComparison.Ordinal) || !trimmed.EndsWith(suffix, StringComparison.Ordinal))
                return false;

            string body = trimmed.Substring(prefix.Length, trimmed.Length - prefix.Length - suffix.Length);
            if (body.Contains("\",", StringComparison.Ordinal))
                return false;

            commandName = body;
            return !string.IsNullOrWhiteSpace(commandName);
        }

        private static string BuildRunCommand(string commandName, string options)
        {
            string safeCommand = EscapeMacroString(commandName);
            string safeOptions = EscapeMacroString(options);
            return $"run(\"{safeCommand}\", \"{safeOptions}\");";
        }

        private static string EscapeMacroString(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
        }

        private static string PrepareEmbeddedMacCommand(string command, bool headless)
        {
            if (!OperatingSystem.IsMacOS() || (!headless && !useHeadlessMacRuntime))
                return command;

            if (TryExtractBareRunCommand(command, out string commandName))
                return BuildRunCommand(commandName, DefaultMacroOptions);

            return command;
        }

        private static bool TryExtractRecordedRunCommand(string recorderText, string previousText, string commandName, out string parameterizedCommand)
        {
            parameterizedCommand = null;
            string safeCommand = ImageJRecorder.fixString(commandName ?? string.Empty);
            if (string.IsNullOrWhiteSpace(safeCommand) || string.IsNullOrWhiteSpace(recorderText))
                return false;

            string delta = recorderText;
            if (!string.IsNullOrEmpty(previousText) && recorderText.StartsWith(previousText, StringComparison.Ordinal))
                delta = recorderText.Substring(previousText.Length);

            string candidatePrefix = $"run(\"{safeCommand}\"";
            string[] lines = delta.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i].Trim();
                if (!line.StartsWith(candidatePrefix, StringComparison.Ordinal) || !line.EndsWith(");", StringComparison.Ordinal))
                    continue;

                if (line.Equals($"run(\"{safeCommand}\");", StringComparison.Ordinal))
                    return false;

                if (line.Equals($"run(\"{safeCommand}\", \"\");", StringComparison.Ordinal))
                    return false;

                parameterizedCommand = line;
                return true;
            }

            return false;
        }

        public async static Task<BioImage> RunOnImage(BioImage b, string con, bool headless, bool onTab, bool useBioformats,bool resultInNewTab)
        {
            if (b != null && b.isPyramidal)
                return await ImageJ.RunOnAllPyramidLevels(b, con, headless, onTab, useBioformats, resultInNewTab).ConfigureAwait(false);
            BioImage bm;
            if (UseFiji && !OperatingSystem.IsMacOS())
                bm = await RunOnImageFiji(b, con, 0, headless, onTab, useBioformats, resultInNewTab);
            else
            {
                Recorder.Record($"Fiji.RunOnImage({b}, \"{con}\", {headless.ToString().ToLower()}, {onTab.ToString().ToLower()}, {useBioformats.ToString().ToLower()}, {resultInNewTab.ToString().ToLower()});");
                return await Task.Run(() => RunOnImageInProcess(
                    b, con, headless, useBioformats, resultInNewTab)).ConfigureAwait(false);
            }
            return bm;
        }
        /*
        public static void RunOnImage(BioImage b, string con, bool headless, bool onTab, bool bioformats, bool resultInNewTab)
        {
            if (UseFiji)
            {
                if (b != null && b.isPyramidal)
                    ImageJ.RunOnAllPyramidLevels(b, con, headless, onTab, bioformats, resultInNewTab).Wait();
                else
                    RunOnImageFiji(b, con, 0, headless, onTab, bioformats, resultInNewTab).Wait();
            }
            else
                RunOnImage(b, con, headless, onTab, bioformats, resultInNewTab);
        }
        */
        /// This function is used to initialize the path of the ImageJ.exe file
        /// 
        /// @param path The path to the ImageJ executable.
        public static bool Initialize(string path)
        {
            ConfigureEmbeddedImageJRuntime();
            InitializeCommandMetadata();
            ImageJPath = path;
            return true;
        }

        /// <summary>
        /// Loads the ImageJ macro function and command metadata without starting
        /// ImageJ. On macOS the command table comes from the bundled CSV because
        /// the in-process ImageJ UI is not initialized there.
        /// </summary>
        public static void InitializeCommandMetadata()
        {
            ConfigureEmbeddedImageJRuntime();
            Macro.Initialize();
        }
        private static void EnsureCommandTableLoaded()
        {
            if (ij.IJ.getInstance() == null)
            {
                new ij.ImageJ(ij.ImageJ.NO_SHOW);
            }

            Hashtable commands = ij.Menus.getCommands();
            Enumeration keys = commands.keys();

            while (keys.hasMoreElements())
            {
                string name = (string)keys.nextElement();
                string className = (string)commands.get(name);
                Macro.Commands[name] = new Macro.Command(name, className ?? "", className ?? "");
            }
        }
        private static void LoadCommandTableFromCsv()
        {
            string csvPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Environment.ProcessPath), "macro-commands.csv");
            if (!File.Exists(csvPath))
                return;

            foreach (string s in File.ReadAllLines(csvPath))
            {
                if (string.IsNullOrWhiteSpace(s))
                    continue;

                int separator = s.IndexOf(',');
                if (separator <= 0 || separator == s.Length - 1)
                    continue;

                string name = s.Substring(0, separator).Trim();
                string command = s.Substring(separator + 1).Trim();
                Macro.Commands[name] = new Macro.Command(name, command, "");
            }
        }
        static class IJCommandDump
        {
            public static List<string> FijiCommands = new List<string>();

            public static List<string> DumpCommands()
            {
                List<string> AllCommands = new List<string>();
                // Ensure ImageJ is initialized without showing the UI.
                if (ij.IJ.getInstance() == null)
                {
                    new ij.ImageJ(ij.ImageJ.NO_SHOW);
                }

                // Get the command table
                Hashtable commands = ij.Menus.getCommands();

                Console.WriteLine("=== ImageJ Commands ===");
                Console.WriteLine($"Total: {commands.size()}");

                // Iterate keys
                Enumeration keys = commands.keys();

                while (keys.hasMoreElements())
                {
                    string name = (string)keys.nextElement();
                    string className = (string)commands.get(name);

                    Console.WriteLine($"{name} -> {className}");
                    AllCommands.Add(name);
                }
                return AllCommands;
            }
            public static List<string> DumpFijiLikeCommands()
            {
                List<string> FijiCommands = new List<string>();
                var commands = ij.Menus.getCommands();
                var keys = commands.keys();

                while (keys.hasMoreElements())
                {
                    string name = (string)keys.nextElement();
                    string cls = (string)commands.get(name);

                    if (cls.Contains("fiji") || cls.Contains("trackmate") || cls.Contains("bdv"))
                    {
                        Console.WriteLine($"{name} -> {cls}");
                        FijiCommands.Add(name);
                    }
                }
                return FijiCommands;
            }
            public static List<string> DumpBioFormatsCommands()
            {
                List<string> BioFormatsCommands = new List<string>();
                if (ij.IJ.getInstance() == null)
                {
                    new ij.ImageJ(ij.ImageJ.NO_SHOW);
                }

                Hashtable commands = ij.Menus.getCommands();
                Enumeration keys = commands.keys();

                Console.WriteLine("=== Bio-Formats related commands ===");

                while (keys.hasMoreElements())
                {
                    string name = (string)keys.nextElement();
                    string value = (string)commands.get(name);

                    string lowerName = name.ToLowerInvariant();
                    string lowerValue = value == null ? "" : value.ToLowerInvariant();

                    if (lowerName.Contains("bio-formats") ||
                        lowerName.Contains("loci") ||
                        lowerValue.Contains("loci") ||
                        lowerValue.Contains("bioformats"))
                    {
                        Console.WriteLine($"{name} -> {value}");
                        BioFormatsCommands.Add(name);
                    }
                }
                return BioFormatsCommands;
            }
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

                /** Constructs an RoiDecoder using a byte array. */
                public RoiDecoder(byte[] bytes, string name)
                {
                    ins = new MemoryStream(bytes);
                    this.name = name;
                    this.size = bytes.Length;
                }

                /** Opens the ROI at the specified path. Returns null if there is an error. */
                public static ROI open(string path, double physX, double physY, double volX, double volY)
                {
                    ROI roi = null;
                    RoiDecoder rd = new RoiDecoder(path);
                    roi = rd.getRoi(physX,physY,volX,volY);
                    return roi;
                }

                /** Returns the ROI. */
                public ROI getRoi(double physX, double physY, double volX, double volY)
                {
                    ROI roi = new ROI();
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
                    if (subPixelRect)
                    {
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
                                roi = ROI.CreateRectangle(new AForge.ZCT(slice - 1, channel - 1, frame - 1), xd, yd, widthd, heightd);
                            else
                                roi = ROI.CreateRectangle(new AForge.ZCT(slice - 1, channel - 1, frame - 1), left, top, width, height);
                            int arcSize = getShort(ROUNDED_RECT_ARC_SIZE);
                            if (arcSize > 0)
                                throw new NotSupportedException("Type rounded rectangle not supported.");
                            break;
                        case 2: //Ellipse
                            if (subPixelRect)
                                roi = ROI.CreateEllipse(new AForge.ZCT(slice - 1, channel - 1, frame - 1), xd, yd, widthd, heightd);
                            else
                                roi = ROI.CreateEllipse(new AForge.ZCT(slice - 1, channel - 1, frame - 1), left, top, width, height);
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
                        for (int i = 0; i < roi.Points.Count; i++)
                        {
                            AForge.PointD pd = BioImage.ToStageSpace(roi.Points[i], physX,physY,volX,volY);
                            roi.Points[i] = pd;
                            roi.UpdateBoundingBox();
                        }
                    }
                    if (roi.type == ROI.Type.Polygon || roi.type == ROI.Type.Freeform)
                        roi.closed = true;
                    return roi;
                }
                /*
                void decodeOverlayOptions(ROI roi, int version, int options, int color, int fontSize)
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
                void getStrokeWidthAndColor(ROI roi, int hdr2Offset, bool scaleStrokeWidth)
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
                public ROI getShapeRoi()
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

                    ROI roi = new ROI();
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
                void getTextRoi(ROI roi, int version)
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


                int getByte(int bas)
                {
                    return data[bas] & 255;
                }

                int getShort(int bas)
                {
                    int b0 = data[bas] & 255;
                    int b1 = data[bas + 1] & 255;
                    int n = (short)((b0 << 8) + b1);
                    if (n < -5000)
                        n = (b0 << 8) + b1; // assume n>32767 and unsigned
                    return n;
                }

                int getUnsignedShort(int bas)
                {
                    int b0 = data[bas] & 255;
                    int b1 = data[bas + 1] & 255;
                    return (b0 << 8) + b1;
                }

                int getInt(int bas)
                {
                    int b0 = data[bas] & 255;
                    int b1 = data[bas + 1] & 255;
                    int b2 = data[bas + 2] & 255;
                    int b3 = data[bas + 3] & 255;
                    return ((b0 << 24) + (b1 << 16) + (b2 << 8) + b3);
                }

                float getFloat(int bas)
                {
                    return BitConverter.Int32BitsToSingle(getInt(bas));
                }

                /** Opens an ROI from a byte array. */
                public static ROI openFromByteArray(byte[] bytes, double physX, double physY, double volX, double volY)
                {
                    ROI roi = null;
                    if (bytes == null || bytes.Length == 0)
                        return roi;
                    try
                    {
                        RoiDecoder decoder = new RoiDecoder(bytes, null);
                        roi = decoder.getRoi(physX,physY,volX,volY);
                    }
                    catch (IOException e)
                    {
                        return null;
                    }
                    return roi;
                }

            }

        /// The function `GetImageJType` takes in a `ROI` object and returns an integer representing the
        /// type of the ROI in ImageJ.
        /// 
        /// @param ROI The ROI parameter is an object of type ROI, which represents a region of interest
        /// in an image. It has a property called "type" which indicates the type of the ROI.
        /// 
        /// @return The method is returning an integer value that represents the ImageJ type of the
        /// given ROI.
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

        /// The function "GetPointsXY" takes a ROI object and returns the X and Y coordinates of its
        /// points as arrays.
        /// 
        /// @param ROI The ROI parameter is an object that represents a region of interest. It contains
        /// a collection of points (Points) that define the boundary of the region.
        /// @param xp An array of integers representing the x-coordinates of the points in the ROI.
        /// @param yp The `yp` parameter is an output parameter of type `int[]`. It is used to return
        /// the y-coordinates of the points in the `ROI` object.
        static void GetPointsXY(BioImage b, ROI roi, out int[] xp, out int[] yp)
        {
            int[] x = new int[roi.Points.Count];
            int[] y = new int[roi.Points.Count];
            PointD[] pd = b.ToImageSpace(roi.Points.ToList());
            for (int i = 0; i < roi.Points.Count; i++)
            {
                
                x[i] = (int)pd[i].X;
                y[i] = (int)pd[i].Y;
            }
            xp = x;
            yp = y;

        }

        /// The function "GetXY" takes a region of interest (ROI) and returns the corresponding X and Y
        /// coordinates in image space.
        /// 
        /// @param ROI The ROI parameter is of type ROI, which is likely a custom class representing a
        /// region of interest. It contains information about the position and size of the region.
        /// @param x An output parameter that will store the X coordinate of the ROI in image space.
        /// @param y The "y" parameter is an output parameter that will hold the y-coordinate of the ROI
        /// (Region of Interest) after the method is called.
        static void GetXY(BioImage b, ROI roi, out float x, out float y)
        {
            PointD pd = b.ToImageSpace(new PointD(roi.X, roi.Y));
            x = (float)pd.X;
            y = (float)pd.Y;
        }
        /// The function "GetWH" takes a ROI (region of interest) and returns the width and height of
        /// the ROI in terms of image size.
        /// 
        /// @param ROI The ROI parameter is an object that represents a region of interest. It likely
        /// contains information such as the position (x, y) and size (width, height) of the region.
        /// @param w The width of the ROI (Region of Interest) in the selected image.
        /// @param h The "h" parameter is an output parameter of type float. It is used to store the
        /// height value calculated in the method.
        static void GetWH(BioImage b, ROI roi, out float w, out float h)
        {
            w = (float)b.ToImageSizeX(roi.W);
            h = (float)b.ToImageSizeY(roi.H);
        }
        /// The function rightMove takes an integer value and a position as input, and returns the value
        /// after performing a right shift operation by the specified position.
        /// 
        /// @param value The value is an integer that represents the number you want to perform a right
        /// move on.
        /// @param pos The "pos" parameter represents the number of positions to move the bits to the
        /// right.
        /// 
        /// @return the value after performing a right shift operation.
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

            /** Saves the specified ROI as a file, returning 'true' if successful. */
            public static bool save(BioImage b, ROI roi, String path)
            {
                RoiEncoder re = new RoiEncoder(path);
                try
                {
                    re.write(b,roi);
                }
                catch (IOException e)
                {
                    return false;
                }
                return true;
            }

            /** Save the Roi to the file of stream. */
            public void write(BioImage b, ROI roi)
            {
                if (f != null)
                {
                    write(b,roi);
                }
                else
                {
                    f = new FileStream(path, FileMode.Create);
                    write(b,roi, f);
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
            /// The function "write" saves the properties and coordinates of a region of interest (ROI)
            /// to a file stream.
            /// 
            /// @param ROI The ROI (Region of Interest) is an object that represents a selected area or
            /// shape in an image. It contains information about the type of ROI (e.g., rectangle,
            /// polygon, line), its position and size, and any additional properties or data associated
            /// with it.
            /// @param FileStream FileStream is a class that represents a stream of bytes that can be
            /// written to a file. It is used to write the data of the ROI (Region of Interest) to a
            /// file.
            /// 
            /// @return The code snippet does not have a return statement, so it does not return
            /// anything.
            void write(BioImage b, ROI roi, FileStream f)
            {
                RectangleD r = roi.BoundingBox;
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
                bool usePointData = type != rect && type != oval;
                if (usePointData)
                {
                    //if (roi instanceof PolygonRoi) {
                    //PolygonRoi proi = (PolygonRoi)roi;
                    //Polygon p = proi.getNonSplineCoordinates();
                    n = roi.Points.Count; //p.npoints;
                    //x = p.xpoints;
                    //y = p.ypoints;
                    GetPointsXY(b, roi, out x, out y);
                }
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
                GetXY(b,roi, out px, out py);
                GetWH(b,roi, out pw, out ph);
                putShort(RoiDecoder.TOP, (int)py);
                putShort(RoiDecoder.LEFT, (int)px);
                putShort(RoiDecoder.BOTTOM, (int)(py + ph));
                putShort(RoiDecoder.RIGHT, (int)(px + pw));
                PointD[] pds = roi.GetPoints();
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
                    if (roi.Points.Count == 4)
                    {

                        putFloat(RoiDecoder.XD, (float)pds[0].X);
                        putFloat(RoiDecoder.YD, (float)pds[0].Y);
                        //putFloat(RoiDecoder.WIDTHD, p.xpoints[1] - roi.Points[0]);
                        //putFloat(RoiDecoder.HEIGHTD, p.ypoints[2] - p.ypoints[1]);
                        putFloat(RoiDecoder.WIDTHD, (float)pds[1].X - (float)pds[0].X);
                        putFloat(RoiDecoder.HEIGHTD, (float)pds[2].Y - (float)pds[1].Y);
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
                        saveShapeRoi(b,roi, rect, f, options);
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

                if (type == line) //(roi instanceof Line) 
                {
                    //Line line = (Line)roi;
                    putFloat(RoiDecoder.X1, (float)pds[0].X);
                    putFloat(RoiDecoder.Y1, (float)pds[0].Y);
                    putFloat(RoiDecoder.X2, (float)pds[1].X);
                    putFloat(RoiDecoder.Y2, (float)pds[1].Y);
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

                if (type == point)
                {
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
                    GetXY(b, roi, out fx, out fy);
                    GetWH(b, roi, out fw, out fh);
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

            /// The function saves the stroke width and color of a region of interest (ROI) in a
            /// specific format.
            /// 
            /// @param ROI The ROI parameter is an object that represents a region of interest. It
            /// contains information about the stroke width, stroke color, and fill color of the region.
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

            /// The function `saveShapeRoi` saves a shape region of interest (ROI) to a file stream in a
            /// specific format.
            /// 
            /// @param ROI The `ROI` parameter is an object that represents a region of interest. It
            /// contains information about the shape and position of the region.
            /// @param type The "type" parameter is an integer that represents the type of the ROI. It
            /// is used to determine how the ROI should be saved and interpreted.
            /// @param FileStream FileStream is a class in C# that represents a stream of bytes to read
            /// from or write to a file. It is used to handle file input/output operations. In the given
            /// code, it is used to write the data to a file.
            /// @param options The "options" parameter is an integer that represents various options for
            /// saving the shape ROI. It is used to specify additional information or settings related
            /// to the saving process. The specific meaning and usage of the options parameter would
            /// depend on the context and the implementation of the saveShapeRoi() method.
            void saveShapeRoi(BioImage b, ROI roi, int type, FileStream f, int options)
            {
                //float[] shapeArray = ((ShapeRoi)roi).getShapeAsArray();
                //if (shapeArray == null) return;
                //BufferedOutputStream bout = new BufferedOutputStream(f);

                data = new byte[HEADER_SIZE + HEADER2_SIZE + roiNameSize + roiPropsSize];//shapeArray.length * 4 + roiNameSize + roiPropsSize];
                data[0] = 73; data[1] = 111; data[2] = 117; data[3] = 116; // "Iout"

                putShort(RoiDecoder.VERSION_OFFSET, VERSION);
                data[RoiDecoder.TYPE] = (byte)type;

                float x, y, w, h;
                GetXY(b, roi, out x, out y);
                GetWH(b, roi, out w, out h);
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
            /// The function `saveTextRoi` saves the properties of a text region of interest (ROI) into
            /// a byte array.
            /// 
            /// @param ROI The `ROI` parameter is an object that represents a region of interest. It
            /// contains information about the font, size, style, text, and other properties of the
            /// region of interest.
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
            /// The function "putHeader2" is used to set various properties of a Region of Interest
            /// (ROI) object, such as its position, label color, font size, stroke width, and group.
            /// 
            /// @param ROI The ROI parameter is an object of type ROI, which represents a region of
            /// interest in an image. It contains information about the position and size of the ROI, as
            /// well as other properties such as the stroke color, stroke width, and font size.
            /// @param hdr2Offset The `hdr2Offset` parameter is an integer that represents the offset
            /// position in the header where the information for the second header should be stored.
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

            /// The function "putName" takes a ROI object and an offset value, and sets the name and
            /// length of the ROI in the header.
            /// 
            /// @param ROI The ROI parameter is an object of type ROI. It is used to access the
            /// properties and methods of the ROI object within the putName method.
            /// @param hdr2Offset The `hdr2Offset` parameter is an integer value representing the offset
            /// of the header2 in a data structure or file. It is used to calculate the offset for
            /// storing the name of the ROI (Region of Interest) in the data structure or file.
            void putName(ROI roi, int hdr2Offset)
            {
                int offset = hdr2Offset + HEADER2_SIZE;
                int nameLength = roiNameSize / 2;
                putInt(hdr2Offset + RoiDecoder.NAME_OFFSET, offset);
                putInt(hdr2Offset + RoiDecoder.NAME_LENGTH, nameLength);
                for (int i = 0; i < nameLength; i++)
                    putShort(offset + i * 2, roiName.ElementAt(i));
            }

            /// The function "putProps" takes a ROI object and an offset value, and updates the ROI
            /// properties in the header based on the given offset and ROI object.
            /// 
            /// @param ROI The ROI parameter is an object of type ROI. It is used to pass information
            /// about a region of interest.
            /// @param hdr2Offset The `hdr2Offset` parameter is an integer value representing the offset
            /// of the header2 in memory. It is used to calculate the offset for storing the ROI
            /// properties.
            void putProps(ROI roi, int hdr2Offset)
            {
                int offset = hdr2Offset + HEADER2_SIZE + roiNameSize;
                int roiPropsLength = roiPropsSize / 2;
                putInt(hdr2Offset + RoiDecoder.ROI_PROPS_OFFSET, offset);
                putInt(hdr2Offset + RoiDecoder.ROI_PROPS_LENGTH, roiPropsLength);
                for (int i = 0; i < roiPropsLength; i++)
                    putShort(offset + i * 2, roiProps.ElementAt(i));
            }

            /// The function "putPointCounters" updates the counters in a region of interest (ROI) by
            /// copying the values from an array to a specific offset in memory.
            /// 
            /// @param ROI The ROI parameter is an object of type ROI, which likely represents a region
            /// of interest in an image. It may contain information such as the coordinates, size, and
            /// properties of the region.
            /// @param hdr2Offset The `hdr2Offset` parameter is the offset value for the second header
            /// in the data structure. It is used to calculate the position where the point counters
            /// will be stored.
            void putPointCounters(ROI roi, int hdr2Offset)
            {
                int offset = hdr2Offset + HEADER2_SIZE + roiNameSize + roiPropsSize;
                putInt(hdr2Offset + RoiDecoder.COUNTERS_OFFSET, offset);
                for (int i = 0; i < countersSize / 4; i++)
                    putInt(offset + i * 4, counters[i]);
                countersSize = 0;
            }

            /// The function "putByte" assigns a byte value to a specific index in an array.
            /// 
            /// @param bas The parameter "bas" is an integer that represents the base address or index
            /// of the array "data" where the byte value will be stored.
            /// @param v The parameter "v" is an integer value that represents the value to be stored in
            /// the byte array.
            void putByte(int bas, int v)
            {
                data[bas] = (byte)v;
            }

            /// The function "putShort" takes two integer parameters, "bas" and "v", and stores the
            /// value of "v" in the "data" array at index "bas" and "bas + 1" after performing a right
            /// shift operation on "v" by 8 bits.
            /// 
            /// @param bas The parameter "bas" represents the base index in the "data" array where the
            /// short value will be stored.
            /// @param v The parameter "v" is an integer value that represents the value to be stored in
            /// the data array.
            void putShort(int bas, int v)
            {
                //data[bas] = (byte)(v >>> 8);
                //data[bas] = (byte)UnsignedRightShift(v, 8);
                data[bas] = (byte)rightMove(v, 8);
                data[bas + 1] = (byte)v;
            }

            /// The function "putFloat" takes an integer and a float as input and converts the float
            /// into its binary representation, storing it in a byte array.
            /// 
            /// @param bas The parameter "bas" represents the base index in the "data" array where the
            /// float value will be stored.
            /// @param v The parameter "v" is a float value that needs to be converted and stored in the
            /// "data" array.
            void putFloat(int bas, float v)
            {
                int tmp = BitConverter.SingleToInt32Bits(v);//Float.floatToIntBits(v);
                data[bas] = (byte)(tmp >> 24);
                data[bas + 1] = (byte)(tmp >> 16);
                data[bas + 2] = (byte)(tmp >> 8);
                data[bas + 3] = (byte)tmp;
            }

            /// The function "putInt" takes two integer parameters and stores the bytes of the second
            /// integer in a byte array starting at the specified index in big-endian order.
            /// 
            /// @param bas The parameter "bas" represents the base index in the "data" array where the
            /// integer value will be stored.
            /// @param i The parameter "i" is an integer value that needs to be stored in the "data"
            /// array.
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

