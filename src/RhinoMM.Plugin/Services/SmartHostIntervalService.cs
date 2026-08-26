using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using RhinoMM.Core.Domain;
using RhinoMM.Plugin.Geometry;

namespace RhinoMM.Plugin.Services;

/// <summary>
/// Computes material intervals along a fastener axis from exact Brep tests.
/// Bounding boxes only bound the search; they are never returned as geometry.
/// </summary>
internal static class SmartHostIntervalService
{
    public static bool TryGet(
        GeometryBase geometry,
        PlacementFrame placement,
        double tolerance,
        out Interval interval,
        out string message)
    {
        interval = Interval.Unset;
        message = string.Empty;
        var brep = geometry switch
        {
            Brep value => value,
            Extrusion extrusion => extrusion.ToBrep(),
            _ => null
        };
        if (brep is null || !brep.IsSolid)
        {
            message = "宿主不是有效封闭实体。";
            return false;
        }

        var ownsBrep = geometry is Extrusion;
        try
        {
            var plane = FastenerGeometryFactory.ToPlane(placement);
            if (TryRay(brep, plane.Origin, plane.ZAxis, tolerance, out interval))
                return true;

            // A centre ray can coincide with a Brep seam. Probe a tolerance-sized
            // ring and accept only a clear majority with mutually agreeing ranges.
            var offset = Math.Max(tolerance * 4, 1e-5);
            var recovered = new List<Interval>();
            for (var index = 0; index < 8; index++)
            {
                var angle = index * Math.PI / 4;
                var origin = plane.Origin
                    + plane.XAxis * (Math.Cos(angle) * offset)
                    + plane.YAxis * (Math.Sin(angle) * offset);
                if (TryRay(brep, origin, plane.ZAxis, tolerance, out var candidate))
                    recovered.Add(candidate);
            }
            if (recovered.Count < 5)
            {
                message = "中心轴与宿主接缝相交，微偏移射线未得到一致的实体区间。";
                return false;
            }

            var starts = recovered.Select(value => value.Min).OrderBy(value => value).ToArray();
            var ends = recovered.Select(value => value.Max).OrderBy(value => value).ToArray();
            var median = new Interval(starts[starts.Length / 2], ends[ends.Length / 2]);
            var agreement = Math.Max(tolerance * 12, offset * 4);
            if (recovered.Count(value =>
                    Math.Abs(value.Min - median.Min) <= agreement
                    && Math.Abs(value.Max - median.Max) <= agreement) < 5)
            {
                message = "宿主微偏移射线得到多个不一致的轴向区间。";
                return false;
            }
            interval = median;
            return interval.Length > tolerance;
        }
        finally
        {
            if (ownsBrep)
                brep.Dispose();
        }
    }

    private static bool TryRay(
        Brep brep,
        Point3d origin,
        Vector3d direction,
        double tolerance,
        out Interval interval)
    {
        interval = Interval.Unset;
        if (!direction.Unitize())
            return false;
        var box = brep.GetBoundingBox(true);
        var projected = box.GetCorners()
            .Select(point => Vector3d.Multiply(point - origin, direction))
            .ToArray();
        var extension = Math.Max(box.Diagonal.Length * 0.05, Math.Max(tolerance * 20, 0.2));
        var minimum = projected.Min() - extension;
        var maximum = projected.Max() + extension;
        using var curve = new LineCurve(origin + direction * minimum, origin + direction * maximum);
        var parameters = new List<double> { minimum, maximum };
        if (Intersection.CurveBrep(curve, brep, tolerance, out _, out var points))
        {
            parameters.AddRange(points.Select(point =>
                Vector3d.Multiply(point - origin, direction)));
        }
        parameters.Sort();
        var clustered = new List<double>();
        foreach (var value in parameters)
        {
            if (clustered.Count == 0 || Math.Abs(clustered[^1] - value) > tolerance * 2)
                clustered.Add(value);
            else
                clustered[^1] = (clustered[^1] + value) * 0.5;
        }

        var material = new List<Interval>();
        for (var index = 0; index + 1 < clustered.Count; index++)
        {
            var from = clustered[index];
            var to = clustered[index + 1];
            if (to - from <= tolerance)
                continue;
            var midpoint = origin + direction * ((from + to) * 0.5);
            if (brep.IsPointInside(midpoint, tolerance, false))
                material.Add(new Interval(from, to));
        }
        if (material.Count == 0)
            return false;

        var merged = new List<Interval>();
        foreach (var candidate in material)
        {
            if (merged.Count > 0 && candidate.Min - merged[^1].Max <= tolerance * 2)
                merged[^1] = new Interval(merged[^1].Min, candidate.Max);
            else
                merged.Add(candidate);
        }
        if (merged.Count != 1)
            return false;
        interval = merged[0];
        return interval.Length > tolerance;
    }
}
