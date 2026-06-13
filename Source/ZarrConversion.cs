using AForge;
using APixelFormat = AForge.PixelFormat;
using ZarrNET.Core.Helpers;
using ZarrNET.Core.OmeZarr.Metadata;
using ZarrNET.Core.Nodes;
using ZarrNET.Core.OmeZarr.Metadata;

namespace BioLib
{
    public static class ZarrConversion
    {
        public static async Task<BioImage> OpenAsBioImageAsync(
            string source,
            int multiscaleIndex = 0,
            int datasetIndex = 0,
            string? id = null,
            CancellationToken ct = default)
        {
            await using var reader = await ZarrNET.Core.OmeZarrReader.OpenAsync(source, ct).ConfigureAwait(false);
            var multiscale = await reader.AsMultiscaleImageAsync(ct).ConfigureAwait(false);
            return await multiscale.ToBioImageAsync(multiscaleIndex, datasetIndex, id ?? source, ct).ConfigureAwait(false);
        }

        public static async Task<BioImage> ToBioImageAsync(
            this MultiscaleNode multiscale,
            int multiscaleIndex = 0,
            int datasetIndex = 0,
            string? id = null,
            CancellationToken ct = default)
        {
            var level = await multiscale.OpenResolutionLevelAsync(multiscaleIndex, datasetIndex, ct).ConfigureAwait(false);
            return await level.ToBioImageAsync(id, ct).ConfigureAwait(false);
        }

        public static async Task<BioImage> ToBioImageAsync(
            this ResolutionLevelNode level,
            string? id = null,
            CancellationToken ct = default)
        {
            var axes = level.EffectiveAxes;
            var shape = level.Shape;
            string dataType = level.DataType.ToLowerInvariant();

            int sizeZ = GetAxisSize(axes, shape, "z", 1);
            int sizeT = GetAxisSize(axes, shape, "t", 1);
            int channelCount = GetAxisSize(axes, shape, "c", 1);
            bool isRgb = channelCount == 3 && (dataType == "uint8" || dataType == "uint16");
            int logicalChannels = isRgb ? 1 : Math.Max(1, channelCount);
            int bitsPerSample = GetBitsPerSample(dataType);
            APixelFormat pixelFormat = GetPixelFormat(dataType, isRgb);

            string imageId = string.IsNullOrWhiteSpace(id) ? "zarr.ome.zarr" : id;
            var bio = new BioImage(imageId);
            bio.ID = imageId;
            bio.Filename = imageId;
            bio.file = imageId;
            bio.littleEndian = BitConverter.IsLittleEndian;
            bio.Coordinate = new ZCT(0, 0, 0);

            var samplePlane = await level.ReadPlaneAsync(t: 0, c: 0, z: 0, ct: ct).ConfigureAwait(false);
            int sizeX = samplePlane.Width;
            int sizeY = samplePlane.Height;

            var pixelSizes = level.GetPixelSize();
            var (physX, physY, physZ) = MapGeometry(axes, pixelSizes);

            bio.Resolutions.Add(new Resolution(sizeX, sizeY, pixelFormat, physX, physY, physZ, 0, 0, 0));

            if (isRgb)
            {
                bio.Channels.Add(new Channel(0, bitsPerSample, 3));
            }
            else
            {
                for (int c = 0; c < logicalChannels; c++)
                {
                    bio.Channels.Add(new Channel(c, bitsPerSample, 1));
                }
            }

            if (isRgb)
            {
                for (int t = 0; t < sizeT; t++)
                {
                    for (int z = 0; z < sizeZ; z++)
                    {
                        PlaneResult red = (t == 0 && z == 0)
                            ? samplePlane
                            : await level.ReadPlaneAsync(t: t, c: 0, z: z, ct: ct).ConfigureAwait(false);
                        PlaneResult green = await level.ReadPlaneAsync(t: t, c: 1, z: z, ct: ct).ConfigureAwait(false);
                        PlaneResult blue = await level.ReadPlaneAsync(t: t, c: 2, z: z, ct: ct).ConfigureAwait(false);

                        byte[] bytes = InterleaveRgbPlanes(red, green, blue, dataType);
                        var bitmap = new Bitmap(
                            bio.ID,
                            red.Width,
                            red.Height,
                            pixelFormat,
                            bytes,
                            new ZCT(z, 0, t),
                            bio.Buffers.Count,
                            null,
                            bio.littleEndian,
                            true);

                        TrySeedStats(bitmap);
                        bio.Buffers.Add(bitmap);
                    }
                }
            }
            else
            {
                for (int t = 0; t < sizeT; t++)
                {
                    for (int z = 0; z < sizeZ; z++)
                    {
                        for (int c = 0; c < logicalChannels; c++)
                        {
                            int cIndex = channelCount > 1 ? c : 0;
                            PlaneResult plane = (t == 0 && z == 0 && cIndex == 0)
                                ? samplePlane
                                : await level.ReadPlaneAsync(t: t, c: cIndex, z: z, ct: ct).ConfigureAwait(false);

                            var bitmap = new Bitmap(
                                bio.ID,
                                plane.Width,
                                plane.Height,
                                pixelFormat,
                                plane.Data,
                                new ZCT(z, c, t),
                                bio.Buffers.Count,
                                null,
                                bio.littleEndian,
                                true);

                            TrySeedStats(bitmap);
                            bio.Buffers.Add(bitmap);
                        }
                    }
                }
            }

            bio.UpdateCoords(sizeZ, logicalChannels, sizeT);
            bio.StackOrder = BioImage.Order.ZCT;
            bio.Volume = new VolumeD(
                new Point3D(bio.StageSizeX, bio.StageSizeY, bio.StageSizeZ),
                new Point3D(bio.PhysicalSizeX * bio.SizeX, bio.PhysicalSizeY * bio.SizeY, bio.PhysicalSizeZ * bio.SizeZ));

            BioImage.AutoThreshold(bio, false);
            if (bio.bitsPerPixel > 8)
                bio.StackThreshold(true);
            else
                bio.StackThreshold(false);

            Images.AddImage(bio);
            return bio;
        }

        private static int GetAxisSize(
            AxisMetadata[] axes,
            long[] shape,
            string axisName,
            int fallback)
        {
            for (int i = 0; i < axes.Length; i++)
            {
                if (axes[i].Name.Equals(axisName, StringComparison.OrdinalIgnoreCase))
                    return (int)shape[i];
            }

            return fallback;
        }

        private static int GetBitsPerSample(string dataType)
        {
            return dataType switch
            {
                "uint8" => 8,
                "uint16" => 16,
                "uint32" => 32,
                "int32" => 32,
                "float32" => 32,
                _ => throw new NotSupportedException($"Unsupported Zarr data type '{dataType}'.")
            };
        }

        private static APixelFormat GetPixelFormat(string dataType, bool isRgb)
        {
            return dataType switch
            {
                "uint8" => isRgb ? APixelFormat.Format24bppRgb : APixelFormat.Format8bppIndexed,
                "uint16" => isRgb ? APixelFormat.Format48bppRgb : APixelFormat.Format16bppGrayScale,
                "uint32" => APixelFormat.UInt,
                "int32" => APixelFormat.Int,
                "float32" => APixelFormat.Float,
                _ => throw new NotSupportedException($"Unsupported Zarr data type '{dataType}'.")
            };
        }

        private static (double PhysicalX, double PhysicalY, double PhysicalZ) MapGeometry(
            AxisMetadata[] axes,
            double[] pixelSizes)
        {
            double physicalX = 1.0;
            double physicalY = 1.0;
            double physicalZ = 1.0;

            for (int i = 0; i < axes.Length; i++)
            {
                string axis = axes[i].Name.ToLowerInvariant();
                double scale = i < pixelSizes.Length ? pixelSizes[i] : 1.0;

                switch (axis)
                {
                    case "x":
                        physicalX = scale;
                        break;
                    case "y":
                        physicalY = scale;
                        break;
                    case "z":
                        physicalZ = scale;
                        break;
                }
            }

            return (physicalX, physicalY, physicalZ);
        }

        private static byte[] InterleaveRgbPlanes(
            PlaneResult red,
            PlaneResult green,
            PlaneResult blue,
            string dataType)
        {
            int bytesPerSample = dataType == "uint16" ? 2 : 1;
            int pixelCount = Math.Min(red.Data.Length, Math.Min(green.Data.Length, blue.Data.Length)) / bytesPerSample;
            byte[] output = new byte[pixelCount * bytesPerSample * 3];

            if (bytesPerSample == 1)
            {
                for (int i = 0; i < pixelCount; i++)
                {
                    int d = i * 3;
                    output[d + 0] = blue.Data[i];
                    output[d + 1] = green.Data[i];
                    output[d + 2] = red.Data[i];
                }
            }
            else
            {
                for (int i = 0; i < pixelCount; i++)
                {
                    int s = i * 2;
                    int d = i * 6;
                    output[d + 0] = blue.Data[s + 0];
                    output[d + 1] = blue.Data[s + 1];
                    output[d + 2] = green.Data[s + 0];
                    output[d + 3] = green.Data[s + 1];
                    output[d + 4] = red.Data[s + 0];
                    output[d + 5] = red.Data[s + 1];
                }
            }

            return output;
        }

        private static void TrySeedStats(Bitmap bitmap)
        {
            try
            {
                bitmap.Stats = Statistics.FromBytes(bitmap);
            }
            catch
            {
            }
        }
    }
}

