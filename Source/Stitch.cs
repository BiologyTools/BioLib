using ManagedCuda;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BruTile;
using System.Runtime.InteropServices;
using ManagedCuda.VectorTypes;
using ome.xml.model;
using AForge;
using ManagedCuda.BasicTypes;
using java.awt;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using Gtk;
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
        private const int maxTiles = 100;
        private static CudaContext context = new CudaContext();
        public static List<Tuple<Extent, CudaDeviceVariable<byte>>> gpuTiles = new List<Tuple<Extent, CudaDeviceVariable<byte>>>();
        private static CudaKernel kernel;
        private static bool initialized = false;
        public static bool HasTile(Extent ex)
        {
            foreach (var item in gpuTiles)
            {
                if (item.Item1 == ex)
                    return true;
            }
            return false;
        }
        public static byte[] ConvertBGRToRGB(byte[] bgrData)
        {
            // Ensure the array length is a multiple of 3 (since each pixel is 3 bytes)
            if (bgrData.Length % 3 != 0)
            {
                throw new ArgumentException("The length of the byte array must be a multiple of 3.");
            }

            byte[] rgbData = new byte[bgrData.Length];

            for (int i = 0; i < bgrData.Length; i += 3)
            {
                rgbData[i] = bgrData[i + 2];     // R
                rgbData[i + 1] = bgrData[i + 1]; // G
                rgbData[i + 2] = bgrData[i];     // B
            }

            return rgbData;
        }
        public void AddTile(Tuple<Extent, byte[]> tile)
        {
            foreach (var t in gpuTiles)
            {
                if (t.Item1 == tile.Item1)
                    return;
            }
            byte[] tileData = ConvertBGRToRGB(tile.Item2);
            if(gpuTiles.Count > maxTiles)
            {
                var ti = gpuTiles.First();
                ti.Item2.Dispose();
                gpuTiles.Remove(gpuTiles.First());
            }
            CudaDeviceVariable<byte> devTile = new CudaDeviceVariable<byte>(tileData.Length);
            devTile.CopyToDevice(tileData);
            gpuTiles.Add(new Tuple<Extent, CudaDeviceVariable<byte>>(tile.Item1, devTile));
        }
        public static void Initialize()
        {
            try
            {
                // Load the CUDA kernel
                kernel = context.LoadKernelPTX("tile_copy.ptx", "copyTileToCanvas");
                initialized = true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
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
        public static byte[] StitchImages(int pxwidth, int pxheight, double x, double y)
        {
            try
            {
                if (!initialized)
                {
                    Initialize();
                }
                // Calculate canvas size based on extents
                double maxX = gpuTiles.Max(t => t.Item1.MaxX);
                double maxY = gpuTiles.Max(t => t.Item1.MaxY);
                double minX = gpuTiles.Min(t => t.Item1.MinX);
                double minY = gpuTiles.Min(t => t.Item1.MinY);

                // Calculate canvas width and height
                int canvasWidth = (int)(maxX - minX);
                int canvasHeight = (int)(maxY - minY);

                // Allocate memory for the output stitched image
                using (CudaDeviceVariable<byte> devCanvas = new CudaDeviceVariable<byte>(canvasWidth * canvasHeight * 3)) // Assuming 3 channels
                {
                    // Set the block and grid sizes
                    dim3 blockSize = new dim3(16, 16, 1);
                    dim3 gridSize = new dim3((uint)((canvasWidth + blockSize.x - 1) / blockSize.x), (uint)((canvasHeight + blockSize.y - 1) / blockSize.y), 1);
                    /*
                    // Copy the tile information to the GPU
                    CudaDeviceVariable<TileData> devTiles = new CudaDeviceVariable<TileData>(deviceTiles.Count);
                    devTiles.CopyToDevice(deviceTiles.ToArray());
                    */
                    // Stitch tiles using the CUDA kernel
                    foreach (var tile in gpuTiles)
                    {
                        Extent extent = tile.Item1;
                        CudaDeviceVariable<byte> devTile = tile.Item2;

                        int startX = (int)Math.Round(extent.MinX - minX);
                        int startY = (int)Math.Round(extent.MinY - minY);
                        int tileWidth = (int)Math.Round(extent.MaxX - extent.MinX);
                        int tileHeight = (int)Math.Round(extent.MaxY - extent.MinY);

                        kernel.BlockDimensions = blockSize;
                        kernel.GridDimensions = gridSize;
                        kernel.Run(devCanvas.DevicePointer, canvasWidth, canvasHeight, devTile.DevicePointer, tileWidth, tileHeight, startX, startY);
                    }

                    // Download the result back to host
                    byte[] stitchedImageData = new byte[canvasWidth * canvasHeight * 3];
                    devCanvas.CopyToHost(stitchedImageData);

                    // Ensure (x, y) is within bounds of the full image
                    int clippedX = Math.Max(0, (int)(x - minX));
                    int clippedY = Math.Max(0, (int)(y - minY));

                    // Ensure viewport does not exceed the boundaries of the canvas
                    int viewportWidth = pxwidth;
                    int viewportHeight = pxheight;

                    // Extract the viewport region directly
                    byte[] viewportImageData = new byte[viewportWidth * viewportHeight * 3];
                    System.Threading.Tasks.Parallel.For(0, viewportHeight, row =>
                    {
                        int srcOffset = (clippedY + row) * canvasWidth * 3 + clippedX * 3;
                        int dstOffset = row * viewportWidth * 3;
                        Array.Copy(stitchedImageData, srcOffset, viewportImageData, dstOffset, viewportWidth * 3);
                    });
                    return viewportImageData;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return null;
            }
        }

    }
}
