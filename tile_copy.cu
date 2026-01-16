// StitchTileKernel.cu
// Windows-ready CUDA kernel for 32bpp RGBA tile stitching

#include <cuda_runtime.h>
#include <device_launch_parameters.h>
extern "C" __global__
void StitchViewport(
    const unsigned char** tilePtrs,
    const int* tileWidths,
    const int* tileHeights,
    const int* tileStartX,
    const int* tileStartY,
    size_t numTiles,

    unsigned char* viewport,
    size_t viewportWidth,
    size_t viewportHeight,
    size_t bytesPerPixel)
{
    int x = blockIdx.x * blockDim.x + threadIdx.x;
    int y = blockIdx.y * blockDim.y + threadIdx.y;

    if (x >= viewportWidth || y >= viewportHeight)
        return;

    size_t dstIndex = (y * viewportWidth + x) * bytesPerPixel;

    for (size_t i = 0; i < numTiles; ++i)
    {
        int tx = x - tileStartX[i];
        int ty = y - tileStartY[i];

        if (tx >= 0 && ty >= 0 &&
            tx < tileWidths[i] &&
            ty < tileHeights[i])
        {
            const unsigned char* src = tilePtrs[i];
            size_t srcIndex = (ty * tileWidths[i] + tx) * bytesPerPixel;

            // Copy RGBA (32bpp)
            viewport[dstIndex + 0] = src[srcIndex + 0];
            viewport[dstIndex + 1] = src[srcIndex + 1];
            viewport[dstIndex + 2] = src[srcIndex + 2];
            viewport[dstIndex + 3] = src[srcIndex + 3];

            return; // first tile wins
        }
    }
}

