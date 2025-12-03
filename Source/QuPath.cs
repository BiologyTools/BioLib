using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using AForge;
using System.Text.Json;
using Newtonsoft.Json.Serialization;

namespace BioLib
{
    public class QuPath
    {
        public class GeoJsonFeatureCollection
        {
            public string type { get; set; }
            public List<GeoJsonFeature> features { get; set; }
        }

        public class GeoJsonFeature
        {
            public string type { get; set; }
            public GeoJsonGeometry geometry { get; set; }
            public IDictionary<string, object> properties { get; set; }
        }

        public class GeoJsonGeometry
        {
            public class GeoJsonPlane
            {
                public int c { get; set; }
                public int z { get; set; }
                public int t { get; set; }
            }
            public string type { get; set; }
            public GeoJsonPlane plane { get; set; }
            public object coordinates { get; set; }

            public ZCT GetZCT()
            {
                if (plane.c < 0)
                    return new ZCT(plane.z, 0, plane.t);
                else
                    return new ZCT(plane.z, plane.c, plane.t);
            }
        }

        public class GeoJsonPoint
        {
            public class GeoJsonPlane
            {
                public int c { get; set; }
                public int z { get; set; }
                public int t { get; set; }
            }
            public string type { get; set; }
            public double[] coordinates { get; set; }
            public GeoJsonPlane plane { get; set; }
            public static GeoJsonPoint FromROI(BioLib.ROI roi, BioImage b)
            {
                GeoJsonPoint g = new GeoJsonPoint();
                PointD p = b.ToImageSpace(roi.Points[0]);
                g.coordinates = new double[2] { p.X, p.Y };
                g.type = "Point";
                g.plane = new GeoJsonPlane();
                g.plane.z = roi.coord.Z;
                g.plane.c = roi.coord.C;
                g.plane.t = roi.coord.T;
                return g;
            }
        }
        public static PointD[] GetPoints(GeoJsonGeometry p, BioImage b)
        {
            if (p.type == "Point")
            {
                List<PointD> points = new List<PointD>();
                double[] gs = JsonConvert.DeserializeObject<double[]>(p.coordinates.ToString());
                points.Add(new PointD(gs[0], gs[1]));
                for (int i = 0; i < points.Count; i++)
                {
                    points[i] = new PointD(b.StageSizeX + (points[i].X / b.SizeX) * b.Volume.Width, b.StageSizeY + (points[i].Y / b.SizeY) * b.Volume.Height);
                }
                return points.ToArray();
            }
            else if (p.type == "Polygon")
            {
                List<PointD> points = new List<PointD>();
                double[][][] gs = JsonConvert.DeserializeObject<double[][][]>(p.coordinates.ToString());
                foreach (double[] item in gs[0])
                {
                    points.Add(new PointD(item[0], item[1]));
                }
                for (int i = 0; i < points.Count; i++)
                {
                    points[i] = new PointD(b.StageSizeX + (points[i].X / b.SizeX) * b.Volume.Width, b.StageSizeY + (points[i].Y / b.SizeY) * b.Volume.Height);
                }
                return points.ToArray();
            }
            else
            {
                List<PointD> points = new List<PointD>();
                double[][] gs = JsonConvert.DeserializeObject<double[][]>(p.coordinates.ToString());
                foreach (double[] item in gs)
                {
                    points.Add(new PointD(item[0], item[1]));
                }
                for (int i = 0; i < points.Count; i++)
                {
                    points[i] = new PointD(b.StageSizeX + (points[i].X / b.SizeX) * b.Volume.Width, b.StageSizeY + (points[i].Y / b.SizeY) * b.Volume.Height);
                }
                return points.ToArray();
            }
        }
        public class GeoJsonLineString
        {
            public class GeoJsonPlane
            {
                public int c { get; set; }
                public int z { get; set; }
                public int t { get; set; }
            }
            public string type { get; set; }
            public double[][] coordinates { get; set; }
            public GeoJsonPlane plane { get; set; }
            public static GeoJsonLineString FromROI(ROI roi, BioImage b)
            {
                GeoJsonLineString g = new GeoJsonLineString();
                double[][] ds = new double[roi.Points.Count][];
                for (int i = 0; i < roi.Points.Count; i++)
                {
                    PointD po = b.ToImageSpace(roi.Points[i]);
                    ds[i] = new double[2] { po.X, po.Y };
                }
                g.coordinates = ds;
                g.type = "LineString";
                g.plane = new GeoJsonPlane();
                g.plane.z = roi.coord.Z;
                g.plane.c = roi.coord.C;
                g.plane.t = roi.coord.T;
                return g;
            }
        }

        public class GeoJsonPolygon
        {
            public class GeoJsonPlane
            {
                public int c { get; set; }
                public int z { get; set; }
                public int t { get; set; }
            }
            public string type { get; set; }
            public double[][][] coordinates { get; set; }
            public GeoJsonPlane plane { get; set; }
            public static GeoJsonPolygon FromROI(ROI roi, BioImage b)
            {
                int pc = roi.Points.Count;
                if (roi.Points.Last() != roi.Points.First())
                    pc = roi.Points.Count + 1;
                GeoJsonPolygon g = new GeoJsonPolygon();
                double[][][] dds = new double[1][][];
                double[][] ds = new double[pc][];
                for (int i = 0; i < pc; i++)
                {
                    if (i >= roi.Points.Count)
                    {
                        PointD po = b.ToImageSpace(roi.Points[0]);
                        ds[i] = new double[2] { (int)po.X, (int)po.Y };
                    }
                    else
                    {
                        PointD po = b.ToImageSpace(roi.Points[i]);
                        ds[i] = new double[2] { (int)po.X, (int)po.Y };
                    }
                }

                dds[0] = ds;
                g.coordinates = dds;
                g.type = "Polygon";
                g.plane = new GeoJsonPlane();
                g.plane.z = roi.coord.Z;
                g.plane.c = roi.coord.C;
                g.plane.t = roi.coord.T;
                return g;
            }
        }

        public class Properties
        {
            public string ObjectType { get; set; }
            public bool IsLocked { get; set; }
        }

        public static void SaveROI(string file, BioImage b)
        {
            string j = "{ \"type\":\"FeatureCollection\",\"features\":[";
            int i = 0;
            foreach (ROI roi in b.Annotations)
            {
                if (i == 0)
                    j += "{\"type\":\"Feature\",\"geometry\":";
                else
                    j += ",{\"type\":\"Feature\",\"geometry\":";
                if (roi.type == ROI.Type.Point)
                {
                    GeoJsonPoint p = GeoJsonPoint.FromROI(roi, b);
                    j += JsonConvert.SerializeObject(p);
                }
                else if (roi.type == ROI.Type.Rectangle || (roi.type == ROI.Type.Polygon && roi.closed) || roi.type == ROI.Type.Freeform)
                {
                    GeoJsonPolygon p = GeoJsonPolygon.FromROI(roi, b);
                    j += JsonConvert.SerializeObject(p);
                }
                else if (roi.type == ROI.Type.Polyline || roi.type == ROI.Type.Line || roi.type == ROI.Type.Polygon)
                {
                    GeoJsonLineString p = GeoJsonLineString.FromROI(roi, b);
                    j += JsonConvert.SerializeObject(p);
                }

                j += ",\"properties\":{\"object_type\":\"annotation\",\"isLocked\":false}}";
                i++;
            }
            j += "]}";
            File.WriteAllText(file, j);
        }

        public static ROI[] ReadROI(string filePath, BioImage b)
        {
            List<ROI> rois = new List<ROI>();
            string st = File.ReadAllText(filePath);
            GeoJsonFeatureCollection gs = JsonConvert.DeserializeObject<GeoJsonFeatureCollection>(st);

            foreach (GeoJsonFeature f in gs.features)
            {
                ROI r = new ROI();
                if (f.geometry.type == "Polygon")
                {
                    r.type = ROI.Type.Polygon;
                    r.closed = true;
                    r.AddPoints(GetPoints(f.geometry, b));
                    if (f.geometry.plane != null)
                        r.coord = f.geometry.GetZCT();
                }
                else if (f.geometry.type == "LineString")
                {
                    r.type = ROI.Type.Line;
                    r.AddPoints(GetPoints(f.geometry, b));
                    if (r.Points.Count > 2)
                        r.type = ROI.Type.Polyline;
                    if (f.geometry.plane != null)
                        r.coord = f.geometry.GetZCT();
                }
                else
                {
                    r.type = ROI.Type.Point;
                    r.AddPoints(GetPoints(f.geometry, b));
                    if (f.geometry.plane != null)
                        r.coord = f.geometry.GetZCT();
                }
                rois.Add(r);
            }
            return rois.ToArray();
        }

        public static Project OpenProject(string file)
        {
            try
            {
                return JsonConvert.DeserializeObject<QuPath.Project>(File.ReadAllText("D:\\QuPath\\project.qpproj"));
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return null;
        }

        public class Project
        {
            public string Version { get; set; }
            public long CreateTimestamp { get; set; }
            public long ModifyTimestamp { get; set; }
            public string Uri { get; set; }
            public int LastID { get; set; }
            public List<Image> Images { get; set; }
            public static Project FromImages(List<BioImage[]> bms, string QuPathProjectFile)
            {
                QuPath.Project proj = new QuPath.Project();
                proj.Version = "0.6.0";
                proj.Uri = "file:/" + QuPathProjectFile.Replace("\\","/");
                DateTime dateTime = DateTime.Now;
                long timestampMillis = new DateTimeOffset(dateTime).ToUnixTimeMilliseconds();
                proj.CreateTimestamp = timestampMillis;
                proj.ModifyTimestamp = timestampMillis;
                proj.Images = new List<Image>();
                int id = 0;
                foreach (var item in bms)
                {
                    foreach (var b in item)
                    {
                        List<Image> im = Image.FromBioImages(item.ToList(), id);
                        proj.Images.AddRange(im);
                        id++;
                    }
                }
                proj.LastID = id;
                return proj;
            }
            public static void SaveProject(string file, List<BioImage[]> bms)
            {
                Project pr = Project.FromImages(bms, file);
                Formatting form = new Formatting();
                var settings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(), // Enable camelCase
                    Formatting = Formatting.Indented // Optional: pretty print
                };
                string json = JsonConvert.SerializeObject(pr, settings);
                File.WriteAllText(file, json);
            }
        }

        public class Image
        {
            public ServerBuilder ServerBuilder { get; set; }
            public int EntryID { get; set; }
            public string RandomizedName { get; set; }
            public string ImageName { get; set; }
            public Dictionary<string, object> Metadata { get; set; }
            public static Image FromBioImage(BioImage b, int id)
            {
                QuPath.Image im = new QuPath.Image();
                im.EntryID = id;
                im.RandomizedName = Guid.NewGuid().ToString();
                im.ImageName = b.Filename;
                im.ServerBuilder = new QuPath.ServerBuilder();
                im.ServerBuilder.Uri = "file:/" + b.ID.Replace("\\","/");
                im.ServerBuilder.ProviderClassName = "qupath.lib.images.servers.bioformats.BioFormatsServerBuilder";
                im.ServerBuilder.Args = new List<string>();
                im.ServerBuilder.Args.Add("--series");
                im.ServerBuilder.Args.Add(id.ToString());
                im.ServerBuilder.Metadata = new QuPath.Metadata();
                im.ServerBuilder.Metadata.Name = b.Filename;
                im.ServerBuilder.Metadata.Width = b.SizeX;
                im.ServerBuilder.Metadata.Height = b.SizeY;
                im.ServerBuilder.Metadata.Magnification = b.Magnification;
                im.ServerBuilder.Metadata.IsRGB = b.isRGB;
                im.ServerBuilder.Metadata.ChannelType = "DEFAULT";
                im.ServerBuilder.Metadata.PixelCalibration = new PixelCalibration();
                im.ServerBuilder.Metadata.PixelCalibration.PixelWidth = new PixelDimension();
                im.ServerBuilder.Metadata.PixelCalibration.PixelWidth.Value = b.PhysicalSizeX;
                im.ServerBuilder.Metadata.PixelCalibration.PixelWidth.Unit = "µm";
                im.ServerBuilder.Metadata.PixelCalibration.PixelHeight = new PixelDimension();
                im.ServerBuilder.Metadata.PixelCalibration.PixelHeight.Value = b.PhysicalSizeY;
                im.ServerBuilder.Metadata.PixelCalibration.PixelHeight.Unit = "µm";
                im.ServerBuilder.Metadata.PixelCalibration.ZSpacing = new ZSpacing();
                if (b.Resolutions[0].PixelFormat == PixelFormat.Format16bppGrayScale || b.Resolutions[0].PixelFormat == PixelFormat.Format48bppRgb)
                    im.ServerBuilder.Metadata.PixelType = "UINT16";
                else if(b.Resolutions[0].PixelFormat == PixelFormat.Format8bppIndexed || b.Resolutions[0].PixelFormat == PixelFormat.Format24bppRgb)
                    im.ServerBuilder.Metadata.PixelType = "UINT8";

                if (b.PhysicalSizeZ != 0 || b.PhysicalSizeZ < 0)
                {
                    im.ServerBuilder.Metadata.PixelCalibration.ZSpacing.Value = b.PhysicalSizeZ;
                    im.ServerBuilder.Metadata.PixelCalibration.ZSpacing.Unit = "µm";
                }
                else
                {
                    im.ServerBuilder.Metadata.PixelCalibration.ZSpacing.Value = 1;
                    im.ServerBuilder.Metadata.PixelCalibration.ZSpacing.Unit = "z-slice";
                }
                im.ServerBuilder.Metadata.PixelCalibration.TimeUnit = "SECONDS";
                im.ServerBuilder.Metadata.PixelCalibration.Timepoints = new List<object>();
                if(b.SizeT > 1)
                for (int i = 0; i < b.SizeT; i++)
                {
                    im.ServerBuilder.Metadata.PixelCalibration.Timepoints.Add(0.0);
                }
                im.ServerBuilder.Metadata.PreferredTileHeight = b.SizeX;
                im.ServerBuilder.Metadata.PreferredTileWidth = b.SizeY;
                im.ServerBuilder.Metadata.SizeT = b.SizeT;
                im.ServerBuilder.Metadata.SizeZ = b.SizeZ;
                im.Metadata = new Dictionary<string, object>();
                double max = Double.MinValue;
                foreach (Bitmap item in b.Buffers)
                {
                    foreach (var s in item.Stats)
                    {
                        if(max < s.Max)
                            max = s.Max;
                    }
                }
                im.ServerBuilder.Metadata.MaxValue = (int)max;
                im.ServerBuilder.Metadata.Levels = new List<Level>();
                int li = 0;
                foreach (var r in b.Resolutions)
                {
                    Level l = new Level();
                    l.Width = r.SizeX;
                    l.Height = r.SizeY;
                    if (b.Type != BioImage.ImageType.stack)
                        l.Downsample = 1;
                    else if (b.Type != BioImage.ImageType.pyramidal)
                        l.Downsample = b.GetLevelDownsample(li);
                    else
                        l.Downsample = 1;
                    im.ServerBuilder.Metadata.Levels.Add(l);
                    li++;
                }
                im.ServerBuilder.Metadata.Channels = new List<Channel>();
                foreach (AForge.Channel c in b.Channels)
                {
                    if(b.isRGB)
                    {
                        QuPath.Channel rr = new QuPath.Channel();
                        QuPath.Channel gg = new QuPath.Channel();
                        QuPath.Channel bb = new QuPath.Channel();
                        rr.Name = c.Name;
                        gg.Name = c.Name;
                        bb.Name = c.Name;
                        int alpha = 255;
                        rr.Color = -65536;
                        gg.Color = -16711936;
                        bb.Color = -16776961;
                        im.ServerBuilder.Metadata.Channels.Add(rr);
                        im.ServerBuilder.Metadata.Channels.Add(gg);
                        im.ServerBuilder.Metadata.Channels.Add(bb);
                    }
                    else
                    {
                        QuPath.Channel rr = new QuPath.Channel();
                        rr.Color = (255 << 24) | (c.EmissionColor.R << 16) | (c.EmissionColor.G << 8) | c.EmissionColor.B;
                        im.ServerBuilder.Metadata.Channels.Add(rr);
                    }
                    
                }
                im.ServerBuilder.BuilderType = "uri";
                return im;
            }
            public static List<Image> FromBioImages(List<BioImage> b, int id)
            {
                List<Image> images = new List<Image>();
                foreach (var image in b)
                {
                    images.Add(FromBioImage(image,id));
                    id++;
                }
                return images;
            }
        }

        public class ServerBuilder
        {
            public string BuilderType { get; set; }
            public string ProviderClassName { get; set; }
            public string Uri { get; set; }
            public List<string> Args { get; set; }
            public Metadata Metadata { get; set; }
        }

        public class Metadata
        {
            public string Name { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public int SizeZ { get; set; }
            public int SizeT { get; set; }
            public string ChannelType { get; set; }
            public bool IsRGB { get; set; }
            public string PixelType { get; set; }
            public List<Level> Levels { get; set; }
            public List<Channel> Channels { get; set; }
            public int MaxValue { get; set; }
            public PixelCalibration PixelCalibration { get; set; }
            public double Magnification { get; set; }
            public int PreferredTileWidth { get; set; }
            public int PreferredTileHeight { get; set; }
        }

        public class Level
        {
            public double Downsample { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }

        public class Channel
        {
            public string Name { get; set; }
            public int Color { get; set; }
        }

        public class PixelCalibration
        {
            public PixelDimension PixelWidth { get; set; }
            public PixelDimension PixelHeight { get; set; }
            public ZSpacing ZSpacing { get; set; }
            public string TimeUnit { get; set; }
            public List<object> Timepoints { get; set; }
        }

        public class PixelDimension
        {
            public double Value { get; set; }
            public string Unit { get; set; }
        }

        public class ZSpacing
        {
            public double Value { get; set; }
            public string Unit { get; set; }
        }
    }
}
