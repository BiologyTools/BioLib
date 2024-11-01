using ManagedCuda;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BruTile;
using System.Runtime.InteropServices;
using ManagedCuda.VectorTypes;
using AForge;
using ManagedCuda.BasicTypes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using Gtk;
using OpenSlideGTK;
namespace BioLib
{
    public class Stitch
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct TileData
        {
            public Extent Extent;        // Struct representing the Extent
            public CUdeviceptr DevTilePtr; // CUDA device pointer to the tile data

            public TileData(Extent extent, CUdeviceptr devTilePtr)
            {
                this.Extent = extent;
                this.DevTilePtr = devTilePtr;
            }
        }
        // Initialize CUDA context
        private const int maxTiles = 250;
        private CudaContext context;
        public List<Tuple<TileInfo, CudaDeviceVariable<byte>>> gpuTiles = new List<Tuple<TileInfo, CudaDeviceVariable<byte>>>();
        private CudaKernel kernel;
        private bool initialized = false;
        public Stitch()
        {
            Initialize();
        }
        public bool HasTile(Extent ex)
        {
            foreach (var item in gpuTiles)
            {
                if (item.Item1.Extent == ex)
                    return true;
            }
            return false;
        }
        public bool HasTile(TileInfo t)
        {
            foreach (var item in gpuTiles)
            {
                if (item.Item1.Index == t.Index)
                    return true;
            }
            return false;
        }

        public static byte[] ConvertRgbToBgr(byte[] rgbBytes)
        {
            // Ensure input is in RGB format with 3 bytes per pixel
            if (rgbBytes.Length % 3 != 0)
            {
                throw new ArgumentException("Input byte array length must be a multiple of 3.", nameof(rgbBytes));
            }

            // Create a new array for the BGR output
            byte[] bgrBytes = new byte[rgbBytes.Length];

            // Process each pixel (3 bytes per pixel)
            for (int i = 0; i < rgbBytes.Length; i += 3)
            {
                bgrBytes[i] = rgbBytes[i + 2];     // B
                bgrBytes[i + 1] = rgbBytes[i + 1]; // G
                bgrBytes[i + 2] = rgbBytes[i];     // R
            }

            return bgrBytes;
        }

        public void AddTile(Tuple<TileInfo, byte[]> tile)
        {
            if (HasTile(tile.Item1))
                return;
            byte[] tileData = ConvertRgbToBgr(tile.Item2);
            if (gpuTiles.Count > maxTiles)
            {
                var ti = gpuTiles.First();
                ti.Item2.Dispose();
                gpuTiles.Remove(gpuTiles.First());
            }
            try
            {
                CudaDeviceVariable<byte> devTile = new CudaDeviceVariable<byte>(tileData.Length);
                devTile.CopyToDevice(tileData);
                gpuTiles.Add(new Tuple<TileInfo, CudaDeviceVariable<byte>>(tile.Item1, devTile));
            }
            catch (Exception e)
            {
                Initialize();
                Console.WriteLine(e.Message);
            }

        }
        public void Initialize()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                OpenSlideBase.useGPU = false;
                SlideSourceBase.useGPU = false;
                return;
            }
            try
            {
                context = new CudaContext();
                // Load the CUDA kernel
                kernel = context.LoadKernelPTX(System.IO.Path.GetDirectoryName(Environment.ProcessPath) + "/tile_copy.ptx", "copyTileToCanvas");
                initialized = true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                OpenSlideBase.useGPU = false;
                SlideSourceBase.useGPU = false;
                SlideBase.UseVips = true;
                OpenSlideBase.UseVips = true;
            }

        }
        public static Bitmap ConvertCudaDeviceVariableToBitmap(CudaDeviceVariable<byte> deviceVar, int width, int height, PixelFormat pixelFormat)
        {
            // Step 1: Allocate a byte array on the CPU (host)
            byte[] hostArray = new byte[deviceVar.Size];

            // Step 2: Copy the data from the GPU (device) to the CPU (host)
            deviceVar.CopyToHost(hostArray);

            // Step 3: Create a Bitmap object from the byte array
            Bitmap bitmap = new Bitmap(width, height, pixelFormat);

            // Step 4: Lock the bitmap's bits for writing
            BitmapData bmpData = bitmap.LockBits(new AForge.Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, pixelFormat);

            // Step 5: Copy the byte array to the bitmap's pixel buffer
            System.Runtime.InteropServices.Marshal.Copy(hostArray, 0, bmpData.Scan0, hostArray.Length);

            // Step 6: Unlock the bitmap's bits
            bitmap.UnlockBits(bmpData);

            // Return the Bitmap object
            return bitmap;
        }
        public byte[] StitchImages(List<TileInfo> tiles, int pxwidth, int pxheight, double x, double y, double resolution)
        {
            try
            {
                // Convert world coordinates of tile extents to pixel space based on resolution
                foreach (var item in tiles)
                {
                    item.Extent = item.Extent.WorldToPixelInvertedY(resolution);
                }

                if (!initialized)
                {
                    Initialize();
                }

                // Calculate the bounding box (min/max extents) of the stitched image
                double maxX = tiles.Max(t => t.Extent.MaxX);
                double maxY = tiles.Max(t => t.Extent.MaxY);
                double minX = tiles.Min(t => t.Extent.MinX);
                double minY = tiles.Min(t => t.Extent.MinY);

                // Calculate canvas size in pixels
                int canvasWidth = (int)(maxX - minX);
                int canvasHeight = (int)(maxY - minY);

                // Allocate memory for the output stitched image on the GPU
                using (CudaDeviceVariable<byte> devCanvas = new CudaDeviceVariable<byte>(canvasWidth * canvasHeight * 3)) // 3 channels for RGB
                {
                    // Set block and grid sizes for kernel launch
                    dim3 blockSize = new dim3(16, 16, 1);
                    dim3 gridSize = new dim3((uint)((canvasWidth + blockSize.x - 1) / blockSize.x), (uint)((canvasHeight + blockSize.y - 1) / blockSize.y), 1);

                    // Iterate through each tile and copy it to the GPU canvas
                    foreach (var tile in tiles)
                    {
                        Extent extent = tile.Extent;
                        // Find the corresponding GPU tile (already loaded into GPU memory)
                        CudaDeviceVariable<byte> devTile = null;
                        foreach (var t in gpuTiles)
                        {
                            if (t.Item1.Index == tile.Index)
                            {
                                devTile = t.Item2;
                                break;
                            }
                        }
                        if (devTile != null)
                        {
                            // Calculate the start position on the canvas and the dimensions of the tile
                            int startX = (int)Math.Ceiling(extent.MinX - minX);
                            int startY = (int)Math.Ceiling(extent.MinY - minY);
                           
                            int tileWidth = (int)Math.Ceiling(extent.MaxX - extent.MinX);
                            int tileHeight = (int)Math.Ceiling(extent.MaxY - extent.MinY);

                            // canvasTileWidth and canvasTileHeight handle the scaling of the tile to the canvas
                            int canvasTileWidth = tileWidth;
                            int canvasTileHeight = tileHeight;

                            // Run the CUDA kernel to copy the tile to the canvas
                            kernel.BlockDimensions = blockSize;
                            kernel.GridDimensions = gridSize;

                            // Run the kernel, including scaling factors
                            kernel.Run(devCanvas.DevicePointer, canvasWidth, canvasHeight, devTile.DevicePointer, 256, 256, startX, startY, canvasTileWidth, canvasTileHeight);
                        }
                    }

                    // Download the stitched image from the GPU to the host (CPU)
                    byte[] stitchedImageData = new byte[canvasWidth * canvasHeight * 3]; // Assuming 3 channels (RGB)
                    devCanvas.CopyToHost(stitchedImageData);

                    // Clip (x, y) to the canvas bounds
                    int clippedX = Math.Max(0, (int)(x - minX));
                    int clippedY = Math.Max(0, (int)(y - minY));

                    // Make sure the viewport fits within the canvas bounds
                    int viewportWidth = pxwidth;
                    int viewportHeight = pxheight;

                    // Extract the viewport region from the stitched image
                    byte[] viewportImageData = new byte[viewportWidth * viewportHeight * 3]; // Assuming 3 channels
                    System.Threading.Tasks.Parallel.For(0, viewportHeight, row =>
                    {
                        try
                        {
                            int srcOffset = (clippedY + row) * canvasWidth * 3 + clippedX * 3;
                            int dstOffset = row * viewportWidth * 3;
                            Array.Copy(stitchedImageData, srcOffset, viewportImageData, dstOffset, viewportWidth * 3);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("An error occurred while extracting the viewport: " + ex.Message);
                        }
                    });

                    return viewportImageData; // Return the extracted viewport
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                Initialize(); // Reinitialize in case of errors
                return null;
            }
        }

    }

}
