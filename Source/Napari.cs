using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AForge;

namespace BioLib
{
    /// <summary>
    /// Parser for Napari CSV format ROI files.
    /// Handles both Points.csv and Shapes.csv formats.
    /// </summary>
    public static class Napari
    {
        #region Data Structures
        public static bool Initialized => PhysicalX > 0 && PhysicalY > 0;
        public static bool HasTime = false;
        public static void Initialize(double physicalX, double physicalY, bool hasTime)
        {
            PhysicalX = physicalX;
            PhysicalY = physicalY;
            HasTime = hasTime;
        }
        public static double PhysicalX;
        public static double PhysicalY;
        /// <summary>
        /// Represents a parsed CSV row with coordinate data.
        /// Supports both 4-axis (ZC) and 5-axis (ZCT) formats.
        /// </summary>
        private class CsvRow
        {
            public double Index { get; set; }
            public string ShapeType { get; set; }
            public double VertexIndex { get; set; }
            public double Axis0 { get; set; }  // T coordinate (Time)
            public double Axis1 { get; set; }  // Z coordinate
            public double Axis2 { get; set; }  // C coordinate (Channel)
            public double Axis3 { get; set; }  // X coordinate (if 5-axis) OR Y coordinate (if 4-axis)
            public double Axis4 { get; set; }  // Y coordinate (if 5-axis only)
            public bool HasTimeAxis { get; set; }  // True if 5-axis format (ZCT)
        }

        /// <summary>
        /// Groups shape data by index for multi-vertex shapes
        /// </summary>
        private class ShapeGroup
        {
            public double Index { get; set; }
            public string ShapeType { get; set; }
            public List<CsvRow> Vertices { get; set; } = new List<CsvRow>();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Parse a Points CSV file and return ROIs
        /// </summary>
        public static List<ROI> ParsePointsFile(string filePath)
        {
            List<ROI> rois = new List<ROI>();

            // Stage: Read and validate file
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Points file not found: {filePath}");

            string[] lines = File.ReadAllLines(filePath);
            if (lines.Length < 2)
                return rois; // Empty or header-only file

            // Stage: Parse each point row
            for (int i = 1; i < lines.Length; i++) // Skip header
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line))
                    continue;

                CsvRow row = ParsePointRow(line);
                if (row != null)
                {
                    ROI roi = CreatePointRoi(row);
                    rois.Add(roi);
                }
            }

            return rois;
        }

        /// <summary>
        /// Parse a Shapes CSV file and return ROIs
        /// </summary>
        public static List<ROI> ParseShapesFile(string filePath)
        {
            List<ROI> rois = new List<ROI>();

            // Stage: Read and validate file
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Shapes file not found: {filePath}");

            string[] lines = File.ReadAllLines(filePath);
            if (lines.Length < 2)
                return rois; // Empty or header-only file

            // Stage: Parse all rows
            List<CsvRow> allRows = new List<CsvRow>();
            for (int i = 1; i < lines.Length; i++) // Skip header
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line))
                    continue;

                CsvRow row = ParseShapeRow(line);
                if (row != null)
                    allRows.Add(row);
            }

            // Stage: Group vertices by shape index
            Dictionary<double, ShapeGroup> shapeGroups = GroupShapesByIndex(allRows);

            // Stage: Create ROIs from grouped shapes
            foreach (ShapeGroup group in shapeGroups.Values.OrderBy(g => g.Index))
            {
                ROI roi = CreateShapeRoi(group);
                if (roi != null)
                    rois.Add(roi);
            }

            return rois;
        }

        /// <summary>
        /// Parse both Points and Shapes files and combine results
        /// </summary>
        public static List<ROI> ParseNapariFiles(string pointsPath, string shapesPath)
        {
            List<ROI> allRois = new List<ROI>();

            // Stage: Parse points file if it exists
            if (File.Exists(pointsPath))
            {
                List<ROI> pointRois = ParsePointsFile(pointsPath);
                allRois.AddRange(pointRois);
            }

            // Stage: Parse shapes file if it exists
            if (File.Exists(shapesPath))
            {
                List<ROI> shapeRois = ParseShapesFile(shapesPath);
                allRois.AddRange(shapeRois);
            }

            return allRois;
        }

        #endregion

        #region CSV Writing Public API

        /// <summary>
        /// Write Point ROIs to a Napari Points.csv file
        /// </summary>
        public static void WritePointsFile(string filePath, List<ROI> rois)
        {
            // Stage: Filter to only Point type ROIs
            List<ROI> pointRois = rois.Where(r => r.type == ROI.Type.Point).ToList();

            if (pointRois.Count == 0)
                return; // No points to write

            // Stage: Determine format (4-axis or 5-axis)
            bool hasTimeData = HasTime;
            // Stage: Build CSV content
            List<string> lines = new List<string>();

            // Header
            if (hasTimeData)
                lines.Add("index,axis-0,axis-1,axis-2,axis-3,axis-4");
            else
                lines.Add("index,axis-0,axis-1,axis-2,axis-3");

            // Data rows
            for (int i = 0; i < pointRois.Count; i++)
            {
                string line = FormatPointRow(pointRois[i], i, hasTimeData);
                lines.Add(line);
            }

            // Stage: Write to file
            File.WriteAllLines(filePath, lines);
        }

        /// <summary>
        /// Write Shape ROIs to a Napari Shapes.csv file
        /// </summary>
        public static void WriteShapesFile(string filePath, List<ROI> rois)
        {
            // Stage: Filter to only shape type ROIs (exclude Point and Label)
            List<ROI> shapeRois = rois.Where(r =>
                r.type != ROI.Type.Point &&
                r.type != ROI.Type.Label &&
                r.type != ROI.Type.Mask).ToList();

            if (shapeRois.Count == 0)
                return; // No shapes to write

            // Stage: Determine format (4-axis or 5-axis)
            bool hasTimeData = HasTime;

            // Stage: Build CSV content
            List<string> lines = new List<string>();

            // Header
            if (hasTimeData)
                lines.Add("index,shape-type,vertex-index,axis-0,axis-1,axis-2,axis-3,axis-4");
            else
                lines.Add("index,shape-type,vertex-index,axis-0,axis-1,axis-2,axis-3");

            // Data rows
            for (int i = 0; i < shapeRois.Count; i++)
            {
                List<string> shapeLines = FormatShapeRows(shapeRois[i], i, hasTimeData);
                lines.AddRange(shapeLines);
            }

            // Stage: Write to file
            File.WriteAllLines(filePath, lines);
        }

        /// <summary>
        /// Write both Points and Shapes files from a list of ROIs
        /// </summary>
        public static void WriteNapariFiles(string pointsPath, string shapesPath, List<ROI> rois)
        {
            // Stage: Write points if any exist
            List<ROI> pointRois = rois.Where(r => r.type == ROI.Type.Point).ToList();
            if (pointRois.Count > 0)
            {
                WritePointsFile(pointsPath, rois);
            }

            // Stage: Write shapes if any exist
            List<ROI> shapeRois = rois.Where(r =>
                r.type != ROI.Type.Point &&
                r.type != ROI.Type.Label &&
                r.type != ROI.Type.Mask).ToList();
            if (shapeRois.Count > 0)
            {
                WriteShapesFile(shapesPath, rois);
            }
        }

        #endregion

        #region Points File Parsing Helpers

        private static CsvRow ParsePointRow(string line)
        {
            try
            {
                string[] parts = line.Split(',');

                // Determine format: 4-axis (ZC) or 5-axis (ZCT)
                // Format 4: index,axis-0,axis-1,axis-2,axis-3
                // Format 5: index,axis-0,axis-1,axis-2,axis-3,axis-4
                bool hasTimeAxis = parts.Length >= 6;

                if (parts.Length < 5)
                    return null;

                CsvRow row = new CsvRow
                {
                    Index = ParseDouble(parts[0]),
                    Axis0 = ParseDouble(parts[1]),
                    Axis1 = ParseDouble(parts[2]),
                    Axis2 = ParseDouble(parts[3]),
                    Axis3 = ParseDouble(parts[4]),
                    HasTimeAxis = hasTimeAxis
                };

                if (hasTimeAxis && parts.Length >= 6)
                {
                    row.Axis4 = ParseDouble(parts[5]);
                }

                return row;
            }
            catch
            {
                return null; // Skip malformed rows
            }
        }

        private static ROI CreatePointRoi(CsvRow row)
        {
            // Stage: Extract coordinates based on format
            int z, c, t;
            double x, y;

            if (row.HasTimeAxis)
            {
                // 5-axis format: axis-0=Z, axis-1=C, axis-2=T, axis-3=X, axis-4=Y
                z = (int)Math.Round(row.Axis0);
                c = (int)Math.Round(row.Axis1);
                t = (int)Math.Round(row.Axis2);
                x = row.Axis3;
                y = row.Axis4;
            }
            else
            {
                // 4-axis format: axis-0=Z, axis-1=C, axis-2=X, axis-3=Y
                z = (int)Math.Round(row.Axis0);
                c = (int)Math.Round(row.Axis1);
                t = 0;
                x = row.Axis2;
                y = row.Axis3;
            }

            // Stage: Create ROI structure
            ROI roi = new ROI
            {
                type = ROI.Type.Point,
                coord = new ZCT(z, c, t)
            };

            // Stage: Set point location
            PointD point = new PointD(x, y);
            roi.AddPoint(point);

            // Stage: Set identification
            roi.id = $"point_{(int)row.Index}";
            roi.roiName = $"Point {(int)row.Index}";

            return roi;
        }

        #endregion

        #region Shapes File Parsing Helpers

        private static CsvRow ParseShapeRow(string line)
        {
            try
            {
                string[] parts = line.Split(',');

                // Determine format: 4-axis (ZC) or 5-axis (ZCT)
                // Format 4: index,shape-type,vertex-index,axis-0,axis-1,axis-2,axis-3
                // Format 5: index,shape-type,vertex-index,axis-0,axis-1,axis-2,axis-3,axis-4
                bool hasTimeAxis = parts.Length >= 8;

                if (parts.Length < 7)
                    return null;

                CsvRow row = new CsvRow
                {
                    Index = ParseDouble(parts[0]),
                    ShapeType = parts[1].Trim().ToLower(),
                    VertexIndex = ParseDouble(parts[2]),
                    Axis0 = ParseDouble(parts[3]),
                    Axis1 = ParseDouble(parts[4]),
                    Axis2 = ParseDouble(parts[5]),
                    Axis3 = ParseDouble(parts[6]),
                    HasTimeAxis = hasTimeAxis
                };

                if (hasTimeAxis && parts.Length >= 8)
                {
                    row.Axis4 = ParseDouble(parts[7]);
                }

                return row;
            }
            catch
            {
                return null; // Skip malformed rows
            }
        }

        private static Dictionary<double, ShapeGroup> GroupShapesByIndex(List<CsvRow> rows)
        {
            Dictionary<double, ShapeGroup> groups = new Dictionary<double, ShapeGroup>();

            foreach (CsvRow row in rows)
            {
                if (!groups.ContainsKey(row.Index))
                {
                    groups[row.Index] = new ShapeGroup
                    {
                        Index = row.Index,
                        ShapeType = row.ShapeType
                    };
                }

                groups[row.Index].Vertices.Add(row);
            }

            // Stage: Sort vertices within each group
            foreach (ShapeGroup group in groups.Values)
            {
                group.Vertices = group.Vertices.OrderBy(v => v.VertexIndex).ToList();
            }

            return groups;
        }

        private static ROI CreateShapeRoi(ShapeGroup group)
        {
            if (group.Vertices.Count == 0)
                return null;

            // Stage: Extract coordinates from first vertex based on format
            CsvRow firstVertex = group.Vertices[0];
            int z, c, t;

            if (firstVertex.HasTimeAxis)
            {
                // 5-axis format: axis-0=Z, axis-1=C, axis-2=T
                t = (int)Math.Round(firstVertex.Axis0);
                z = (int)Math.Round(firstVertex.Axis1);
                c = (int)Math.Round(firstVertex.Axis2);
            }
            else
            {
                // 4-axis format: axis-0=Z, axis-1=C, T defaults to 0
                z = (int)Math.Round(firstVertex.Axis0);
                c = (int)Math.Round(firstVertex.Axis1);
                t = 0;
            }

            // Stage: Create ROI based on shape type
            ROI roi = new ROI
            {
                coord = new ZCT(z, c, t),
                roiID = "Z_" + z + "C_" + c + "T_" + t,
                roiName = group.ToString(),
                id = group.ToString() + "Z_" + z + "C_" + c + "T_" + t,
            };

            switch (group.ShapeType)
            {
                case "rectangle":
                    ConfigureRectangleRoi(roi, group);
                    break;

                case "ellipse":
                    ConfigureEllipseRoi(roi, group);
                    break;

                case "polygon":
                    ConfigurePolygonRoi(roi, group);
                    break;

                case "line":
                    ConfigureLineRoi(roi, group);
                    break;

                case "path":
                    ConfigurePathRoi(roi, group);
                    break;

                default:
                    return null; // Unknown shape type
            }

            // Stage: Set identification
            roi.id = $"{group.ShapeType}_{(int)group.Index}";
            roi.roiName = $"{CapitalizeFirst(group.ShapeType)} {(int)group.Index}";
            roi.Text = roi.id;
            return roi;
        }

        #endregion

        #region Shape Configuration Methods

        private static void ConfigureRectangleRoi(ROI roi, ShapeGroup group)
        {
            roi.type = ROI.Type.Rectangle;

            // Stage: Extract rectangle bounds from 4 corner vertices
            List<PointD> points = ExtractPoints(group);
            if (points.Count < 4)
                return;

            // Stage: Calculate bounding box from corners
            double minX = points.Min(p => p.X);
            double maxX = points.Max(p => p.X);
            double minY = points.Min(p => p.Y);
            double maxY = points.Max(p => p.Y);

            roi.BoundingBox = new RectangleD(minX, minY, maxX - minX, maxY - minY);
            roi.Validate(); // This will populate Points from BoundingBox
        }

        private static void ConfigureEllipseRoi(ROI roi, ShapeGroup group)
        {
            roi.type = ROI.Type.Ellipse;

            // Stage: Extract ellipse bounds from 4 corner vertices
            List<PointD> points = ExtractPoints(group);
            if (points.Count < 4)
                return;

            // Stage: Calculate bounding box from corners
            double minX = points.Min(p => p.X);
            double maxX = points.Max(p => p.X);
            double minY = points.Min(p => p.Y);
            double maxY = points.Max(p => p.Y);

            roi.BoundingBox = new RectangleD(minX, minY, maxX - minX, maxY - minY);
            roi.Validate(); // This will populate Points from BoundingBox
        }

        private static void ConfigurePolygonRoi(ROI roi, ShapeGroup group)
        {
            roi.type = ROI.Type.Polygon;
            roi.closed = true;

            // Stage: Add all polygon vertices
            List<PointD> points = ExtractPoints(group);
            roi.AddPoints(points.ToArray());
        }

        private static void ConfigureLineRoi(ROI roi, ShapeGroup group)
        {
            roi.type = ROI.Type.Line;
            roi.closed = false;
            // Stage: Add line endpoints
            List<PointD> points = ExtractPoints(group);
            roi.AddPoints(points.ToArray());
        }

        private static void ConfigurePathRoi(ROI roi, ShapeGroup group)
        {
            // Treat path as polyline (open polygon)
            roi.type = ROI.Type.Polyline;
            roi.closed = false;

            // Stage: Add all path vertices
            List<PointD> points = ExtractPoints(group);
            roi.AddPoints(points.ToArray());
        }

        private static List<PointD> ExtractPoints(ShapeGroup group)
        {
            List<PointD> points = new List<PointD>();

            foreach (CsvRow vertex in group.Vertices)
            {
                double x, y;

                if (vertex.HasTimeAxis)
                {
                    // 5-axis format: axis-3=X, axis-4=Y
                    x = vertex.Axis3;
                    y = vertex.Axis4;
                }
                else
                {
                    // 4-axis format: axis-2=X, axis-3=Y
                    x = vertex.Axis2;
                    y = vertex.Axis3;
                }

                points.Add(new PointD(x * PhysicalX, y * PhysicalY));
            }

            return points;
        }

        #endregion

        #region CSV Writing Helpers

        private static string FormatPointRow(ROI roi, int index, bool hasTimeData)
        {
            // Stage: Extract coordinates
            int z = roi.coord.Z;
            int c = roi.coord.C;
            int t = roi.coord.T;

            // Stage: Get point location
            PointD point = roi.GetPoint(0);
            double x = point.X;
            double y = point.Y;

            // Stage: Format row based on format type
            if (hasTimeData)
            {
                // 5-axis format: index,axis-0,axis-1,axis-2,axis-3,axis-4
                return FormatCsvLine(
                    index.ToString(CultureInfo.InvariantCulture),
                    z.ToString(CultureInfo.InvariantCulture),
                    c.ToString(CultureInfo.InvariantCulture),
                    t.ToString(CultureInfo.InvariantCulture),
                    x.ToString(CultureInfo.InvariantCulture),
                    y.ToString(CultureInfo.InvariantCulture)
                );
            }
            else
            {
                // 4-axis format: index,axis-0,axis-1,axis-2,axis-3
                return FormatCsvLine(
                    index.ToString(CultureInfo.InvariantCulture),
                    z.ToString(CultureInfo.InvariantCulture),
                    c.ToString(CultureInfo.InvariantCulture),
                    x.ToString(CultureInfo.InvariantCulture),
                    y.ToString(CultureInfo.InvariantCulture)
                );
            }
        }

        private static List<string> FormatShapeRows(ROI roi, int index, bool hasTimeData)
        {
            List<string> rows = new List<string>();

            // Stage: Get shape type name
            string shapeType = GetNapariShapeType(roi.type);
            if (shapeType == null)
                return rows; // Unsupported shape type

            // Stage: Extract vertices based on ROI type
            List<PointD> vertices = ExtractShapeVertices(roi);
            if (vertices.Count == 0)
                return rows; // No vertices to export

            // Stage: Get common coordinates
            int z = roi.coord.Z;
            int c = roi.coord.C;
            int t = roi.coord.T;

            // Stage: Format each vertex as a row
            for (int vertexIndex = 0; vertexIndex < vertices.Count; vertexIndex++)
            {
                PointD vertex = vertices[vertexIndex];
                string row = FormatShapeVertexRow(
                    index, shapeType, vertexIndex,
                    t,z,c,
                    vertex.X / PhysicalX, vertex.Y / PhysicalY,
                    hasTimeData
                );
                rows.Add(row);
            }

            return rows;
        }

        private static string FormatShapeVertexRow(
            int index, string shapeType, int vertexIndex,
            int z, int c, int t,
            double x, double y,
            bool hasTimeData)
        {
            // Stage: Format row based on format type
            if (hasTimeData)
            {
                // 5-axis format: index,shape-type,vertex-index,axis-0,axis-1,axis-2,axis-3,axis-4
                return FormatCsvLine(
                    index.ToString(CultureInfo.InvariantCulture),
                    shapeType,
                    vertexIndex.ToString(CultureInfo.InvariantCulture),
                    t.ToString(CultureInfo.InvariantCulture),
                    z.ToString(CultureInfo.InvariantCulture),
                    c.ToString(CultureInfo.InvariantCulture),
                    x.ToString(CultureInfo.InvariantCulture),
                    y.ToString(CultureInfo.InvariantCulture)
                );
            }
            else
            {
                // 4-axis format: index,shape-type,vertex-index,axis-0,axis-1,axis-2,axis-3
                return FormatCsvLine(
                    index.ToString(CultureInfo.InvariantCulture),
                    shapeType,
                    vertexIndex.ToString(CultureInfo.InvariantCulture),
                    z.ToString(CultureInfo.InvariantCulture),
                    c.ToString(CultureInfo.InvariantCulture),
                    x.ToString(CultureInfo.InvariantCulture),
                    y.ToString(CultureInfo.InvariantCulture)
                );
            }
        }

        private static string GetNapariShapeType(ROI.Type roiType)
        {
            switch (roiType)
            {
                case ROI.Type.Rectangle:
                    return "rectangle";
                case ROI.Type.Ellipse:
                    return "ellipse";
                case ROI.Type.Polygon:
                    return "polygon";
                case ROI.Type.Line:
                    return "line";
                case ROI.Type.Polyline:
                case ROI.Type.Freeform:
                    return "path";
                default:
                    return null; // Unsupported type
            }
        }

        private static List<PointD> ExtractShapeVertices(ROI roi)
        {
            List<PointD> vertices = new List<PointD>();

            switch (roi.type)
            {
                case ROI.Type.Rectangle:
                case ROI.Type.Ellipse:
                    // Extract 4 corner points from bounding box
                    vertices.Add(new PointD(roi.BoundingBox.X, roi.BoundingBox.Y));
                    vertices.Add(new PointD(roi.BoundingBox.X, roi.BoundingBox.Y + roi.BoundingBox.H));
                    vertices.Add(new PointD(roi.BoundingBox.X + roi.BoundingBox.W, roi.BoundingBox.Y + roi.BoundingBox.H));
                    vertices.Add(new PointD(roi.BoundingBox.X + roi.BoundingBox.W, roi.BoundingBox.Y));
                    break;

                case ROI.Type.Polygon:
                case ROI.Type.Line:
                case ROI.Type.Polyline:
                case ROI.Type.Freeform:
                    // Use existing points
                    vertices.AddRange(roi.GetPoints());
                    break;

                default:
                    // Unsupported type
                    break;
            }

            return vertices;
        }

        private static string FormatCsvLine(params string[] values)
        {
            return string.Join(",", values);
        }

        #endregion

        #region Utility Methods

        private static double ParseDouble(string value)
        {
            // Use InvariantCulture to handle decimal points correctly
            return double.Parse(value.Trim(), CultureInfo.InvariantCulture);
        }

        private static string CapitalizeFirst(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return char.ToUpper(text[0]) + text.Substring(1);
        }

        #endregion
    }
}