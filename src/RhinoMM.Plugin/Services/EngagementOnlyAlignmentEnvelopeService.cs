using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using RhinoMM.Core.Domain;
using RhinoMM.Plugin.Geometry;

namespace RhinoMM.Plugin.Services;

internal sealed record EngagementOnlyAlignmentEnvelope(
    double EntryMinimum,
    double EntryMaximum,
    double ExitMinimum);

internal static class EngagementOnlyAlignmentEnvelopeService
{
    private const int AngularSamples = 64;

    public static bool TryGet(
        GeometryBase geometry,
        PlacementFrame placement,
        double guideRadius,
        double tolerance,
        out EngagementOnlyAlignmentEnvelope? envelope,
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
            message = "顶部对位孔只能从有效的封闭 Brep 或挤出实体计算。";
            return false;
        }
        if (!double.IsFinite(guideRadius) || guideRadius <= tolerance)
        {
            message = "顶部对位孔半径无效。";
            return false;
        }

        var frame = FastenerGeometryFactory.ToPlane(placement);
        var axis = frame.ZAxis;
        if (!axis.Unitize())
        {
            message = "螺丝轴向无效，无法计算顶部对位孔入口。";
            return false;
        }
        var projected = host.GetBoundingBox(true).GetCorners()
            .Select(point => Vector3d.Multiply(point - frame.Origin, axis))
            .ToArray();
        var padding = Math.Max(0.2, tolerance * 10);
        var zMinimum = projected.Min() - padding;
        var zMaximum = projected.Max() + padding;
        var entries = new List<double>(AngularSamples + 1);
        var exits = new List<double>(AngularSamples + 1);

        for (var index = -1; index < AngularSamples; index++)
        {
            var radius = index < 0 ? 0 : guideRadius;
            var angle = index < 0 ? 0 : Math.PI * 2 * index / AngularSamples;
            var start = frame.PointAt(
                radius * Math.Cos(angle),
                radius * Math.Sin(angle),
                zMinimum);
            var end = frame.PointAt(
                radius * Math.Cos(angle),
                radius * Math.Sin(angle),
                zMaximum);
            if (!Intersection.CurveBrep(
                    new LineCurve(start, end),
                    host,
                    tolerance,
                    out _,
                    out var points)
                || points.Length < 2)
            {
                message = "顶部对位孔圆周靠近宿主边缘，缺少形成完整孔口的材料。";
                return false;
            }
            var depths = points
                .Select(point => Vector3d.Multiply(point - frame.Origin, axis))
                .OrderBy(value => value)
                .Aggregate(new List<double>(), (values, value) =>
                {
                    if (values.Count == 0 || Math.Abs(values[^1] - value) > tolerance * 2)
                        values.Add(value);
                    return values;
                });
            if (depths.Count != 2)
            {
                message = "顶部对位孔入口存在多个轴向材料区间，无法唯一确定宿主。";
                return false;
            }
            if (depths[1] - depths[0] <= tolerance)
            {
                message = "顶部对位孔所在宿主厚度不足。";
                return false;
            }
            entries.Add(depths[0]);
            exits.Add(depths[1]);
        }

        envelope = new EngagementOnlyAlignmentEnvelope(
            entries.Min(),
            entries.Max(),
            exits.Min());
        return true;
    }
}
