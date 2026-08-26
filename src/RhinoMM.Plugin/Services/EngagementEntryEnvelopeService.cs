using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Geometry;

namespace RhinoMM.Plugin.Services;

internal sealed record EngagementEntryEnvelope(
    double EntryMinimum,
    double EntryMaximum,
    double ExitMinimum,
    double MouthMinimum,
    double MouthMaximumRadius,
    bool IsPlanar);

/// <summary>
/// Solves the physical entrance of an axis-aligned 45 degree chamfer.
/// Bounding boxes only limit the ray/root search; every reported depth is
/// obtained from an exact Brep intersection.
/// </summary>
internal static class EngagementEntryEnvelopeService
{
    private const int AngularSamples = 48;
    private static readonly double[] RingFactors = [0, 0.25, 0.5, 0.75, 1];

    public static bool TryGet(
        GeometryBase geometry,
        PlacementFrame placement,
        double shaftRadius,
        double chamferSize,
        double tolerance,
        out EngagementEntryEnvelope? envelope,
        out string message)
    {
        envelope = null;
        message = string.Empty;
        var host = geometry switch
        {
            Brep brep => brep.DuplicateBrep(),
            Extrusion extrusion => extrusion.ToBrep(),
            _ => null
        };
        if (host is null || !host.IsValid || !host.IsSolid)
        {
            message = "咬合孔入口只能从有效的封闭 Brep 或挤出实体计算。";
            return false;
        }
        if (!double.IsFinite(shaftRadius) || shaftRadius <= tolerance
            || !double.IsFinite(chamferSize) || chamferSize <= tolerance)
        {
            message = "咬合孔半径或倒角尺寸无效。";
            return false;
        }

        var frame = FastenerGeometryFactory.ToPlane(placement);
        var axis = frame.ZAxis;
        if (!axis.Unitize())
        {
            message = "螺丝轴向无效，无法计算咬合孔入口。";
            return false;
        }

        var box = host.GetBoundingBox(true);
        var localCorners = box.GetCorners()
            .Select(point => new Point3d(
                Vector3d.Multiply(point - frame.Origin, frame.XAxis),
                Vector3d.Multiply(point - frame.Origin, frame.YAxis),
                Vector3d.Multiply(point - frame.Origin, axis)))
            .ToArray();
        var padding = Math.Max(0.2, tolerance * 10);
        var zMinimum = localCorners.Min(point => point.Z) - padding;
        var zMaximum = localCorners.Max(point => point.Z) + padding;
        var radialMaximum = localCorners.Max(point => Math.Sqrt(point.X * point.X + point.Y * point.Y))
            + chamferSize + padding;

        var entries = new List<double>();
        var exits = new List<double>();
        AxialInterval? center = null;
        foreach (var ring in RingFactors)
        {
            var count = ring == 0 ? 1 : AngularSamples;
            for (var index = 0; index < count; index++)
            {
                var angle = 2 * Math.PI * index / count;
                var x = shaftRadius * ring * Math.Cos(angle);
                var y = shaftRadius * ring * Math.Sin(angle);
                if (!TryGetInterval(host, frame, x, y, zMinimum, zMaximum, tolerance, out var interval, out message))
                    return false;
                if (ring == 0)
                    center = interval;
                entries.Add(interval.Entry);
                exits.Add(interval.Exit);
            }
        }

        if (center is null)
        {
            message = "无法识别咬合孔中心轴的唯一入口。";
            return false;
        }

        var entryMinimum = entries.Min();
        var entryMaximum = entries.Max();
        var exitMinimum = exits.Min();
        var smallEnd = entryMaximum + chamferSize;

        if (TryGetEntryPlane(host, center.Value.EntryPoint, frame, tolerance,
                out var surfaceOffset, out var slopeX, out var slopeY))
        {
            if (!EngagementEntryChamferCalculator.TrySolvePlanarEntry(
                    shaftRadius,
                    chamferSize,
                    surfaceOffset,
                    slopeX,
                    slopeY,
                    tolerance,
                    out var planar,
                    out message)
                || planar is null)
                return false;

            // The analytic mouth must still lie on material. This rejects a
            // truncated contour close to an edge, while allowing adjacent
            // coplanar faces to participate in the same closed entrance.
            var depthTolerance = Math.Max(tolerance * 10, 0.01);
            for (var index = 0; index < AngularSamples; index++)
            {
                var angle = 2 * Math.PI * index / AngularSamples;
                var radius = planar.MouthRadiusAt(angle);
                if (!double.IsFinite(radius) || radius > radialMaximum
                    || !TryGetInterval(
                        host,
                        frame,
                        radius * Math.Cos(angle),
                        radius * Math.Sin(angle),
                        zMinimum,
                        zMaximum,
                        tolerance,
                        out var interval,
                        out _))
                {
                    message = "倒角锥口靠近宿主边缘，缺少形成完整闭合切口的材料。";
                    return false;
                }
                var expected = surfaceOffset
                    + slopeX * radius * Math.Cos(angle)
                    + slopeY * radius * Math.Sin(angle);
                if (Math.Abs(interval.Entry - expected) > depthTolerance)
                {
                    message = "倒角入口跨越非共面区域，无法形成唯一的闭合45°切口。";
                    return false;
                }
            }

            envelope = new EngagementEntryEnvelope(
                planar.EntryMinimum,
                planar.EntryMaximum,
                exitMinimum,
                planar.MouthMinimum,
                planar.MouthMaximumRadius,
                true);
            return true;
        }

        var mouthMinimum = double.PositiveInfinity;
        var mouthMaximumRadius = shaftRadius;
        for (var index = 0; index < AngularSamples; index++)
        {
            var angle = 2 * Math.PI * index / AngularSamples;
            if (!TrySolveCurvedMouth(
                    host,
                    frame,
                    shaftRadius,
                    smallEnd,
                    angle,
                    radialMaximum,
                    zMinimum,
                    zMaximum,
                    tolerance,
                    out var radius,
                    out var depth,
                    out message))
                return false;
            mouthMinimum = Math.Min(mouthMinimum, depth);
            mouthMaximumRadius = Math.Max(mouthMaximumRadius, radius);
        }

        envelope = new EngagementEntryEnvelope(
            entryMinimum,
            entryMaximum,
            exitMinimum,
            mouthMinimum,
            mouthMaximumRadius,
            false);
        return true;
    }

    private static bool TrySolveCurvedMouth(
        Brep host,
        Plane frame,
        double shaftRadius,
        double smallEnd,
        double angle,
        double maximumRadius,
        double zMinimum,
        double zMaximum,
        double tolerance,
        out double radius,
        out double depth,
        out string message)
    {
        radius = 0;
        depth = 0;
        message = string.Empty;
        var cosine = Math.Cos(angle);
        var sine = Math.Sin(angle);
        if (!TryGetInterval(host, frame, shaftRadius * cosine, shaftRadius * sine,
                zMinimum, zMaximum, tolerance, out var lowerInterval, out message))
            return false;

        var lower = shaftRadius;
        var lowerValue = lower + lowerInterval.Entry - shaftRadius - smallEnd;
        if (lowerValue >= 0)
        {
            message = "曲面入口无法建立倒角根区间。";
            return false;
        }

        var upper = Math.Max(shaftRadius + 0.05, shaftRadius * 1.1);
        var upperInterval = default(AxialInterval);
        var bracketed = false;
        while (upper <= maximumRadius + tolerance)
        {
            if (!TryGetInterval(host, frame, upper * cosine, upper * sine,
                    zMinimum, zMaximum, tolerance, out upperInterval, out _))
            {
                message = "曲面倒角锥口在宿主边缘缺少材料，无法形成闭合切口。";
                return false;
            }
            var value = upper + upperInterval.Entry - shaftRadius - smallEnd;
            if (value >= 0)
            {
                bracketed = true;
                break;
            }
            lower = upper;
            lowerInterval = upperInterval;
            lowerValue = value;
            upper = Math.Min(maximumRadius, upper * 1.35 + 0.05);
            if (maximumRadius - lower <= tolerance)
                break;
        }
        if (!bracketed)
        {
            message = "曲面入口的45°锥体没有有限闭合交线；请减小倒角或调整螺丝方向。";
            return false;
        }

        for (var iteration = 0; iteration < 48; iteration++)
        {
            var middle = (lower + upper) * 0.5;
            if (!TryGetInterval(host, frame, middle * cosine, middle * sine,
                    zMinimum, zMaximum, tolerance, out var interval, out message))
                return false;
            var value = middle + interval.Entry - shaftRadius - smallEnd;
            if (Math.Abs(value) <= tolerance || upper - lower <= tolerance)
            {
                radius = middle;
                depth = interval.Entry;
                return true;
            }
            if (value < 0)
            {
                lower = middle;
                lowerInterval = interval;
                lowerValue = value;
            }
            else
            {
                upper = middle;
                upperInterval = interval;
            }
        }

        radius = (lower + upper) * 0.5;
        if (!TryGetInterval(host, frame, radius * cosine, radius * sine,
                zMinimum, zMaximum, tolerance, out var finalInterval, out message))
            return false;
        depth = finalInterval.Entry;
        return true;
    }

    private static bool TryGetInterval(
        Brep host,
        Plane frame,
        double x,
        double y,
        double zMinimum,
        double zMaximum,
        double tolerance,
        out AxialInterval interval,
        out string message)
    {
        interval = default;
        message = string.Empty;
        var start = frame.PointAt(x, y, zMinimum);
        var end = frame.PointAt(x, y, zMaximum);
        var ray = new LineCurve(start, end);
        if (!Intersection.CurveBrep(ray, host, tolerance, out _, out var points)
            || points.Length < 2)
        {
            message = "咬合孔入口圆周未能完整命中宿主，已停止以避免残缺倒角。";
            return false;
        }

        var depths = points
            .Select(point => Vector3d.Multiply(point - frame.Origin, frame.ZAxis))
            .OrderBy(value => value)
            .Aggregate(new List<double>(), (values, value) =>
            {
                if (values.Count == 0 || Math.Abs(values[^1] - value) > tolerance * 2)
                    values.Add(value);
                return values;
            });
        if (depths.Count != 2)
        {
            message = "咬合孔入口存在多个轴向区间，无法唯一确定倒角宿主。";
            return false;
        }

        interval = new AxialInterval(depths[0], depths[1], points.OrderBy(point =>
            Vector3d.Multiply(point - frame.Origin, frame.ZAxis)).First());
        return interval.Exit - interval.Entry > tolerance;
    }

    private static bool TryGetEntryPlane(
        Brep host,
        Point3d entryPoint,
        Plane frame,
        double tolerance,
        out double surfaceOffset,
        out double slopeX,
        out double slopeY)
    {
        surfaceOffset = slopeX = slopeY = 0;
        if (!host.ClosestPoint(
                entryPoint,
                out _,
                out var component,
                out _,
                out _,
                Math.Max(tolerance * 10, 0.01),
                out _)
            || component.ComponentIndexType != ComponentIndexType.BrepFace
            || component.Index < 0
            || component.Index >= host.Faces.Count
            || !host.Faces[component.Index].TryGetPlane(out var plane, tolerance * 10))
            return false;

        var normal = plane.Normal;
        if (!normal.Unitize())
            return false;
        var normalZ = Vector3d.Multiply(normal, frame.ZAxis);
        if (Math.Abs(normalZ) <= 1e-10)
            return false;
        var normalX = Vector3d.Multiply(normal, frame.XAxis);
        var normalY = Vector3d.Multiply(normal, frame.YAxis);
        surfaceOffset = Vector3d.Multiply(plane.Origin - frame.Origin, normal) / normalZ;
        slopeX = -normalX / normalZ;
        slopeY = -normalY / normalZ;
        return double.IsFinite(surfaceOffset)
            && double.IsFinite(slopeX)
            && double.IsFinite(slopeY);
    }

    private readonly record struct AxialInterval(double Entry, double Exit, Point3d EntryPoint);
}
