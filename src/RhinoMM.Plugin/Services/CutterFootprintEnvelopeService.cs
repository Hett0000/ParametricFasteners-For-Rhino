using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Plugin.Geometry;

namespace RhinoMM.Plugin.Services;

internal static class CutterFootprintEnvelopeService
{
    public static bool TryGet(
        GeometryBase geometry,
        PlacementFrame placement,
        double diameter,
        double tolerance,
        out Interval interval,
        out string message)
    {
        interval = Interval.Unset;
        message = string.Empty;
        if (!double.IsFinite(diameter) || diameter <= tolerance)
        {
            message = "最终孔径无效，无法计算贯穿范围。";
            return false;
        }

        var host = geometry switch
        {
            Brep brep => brep.DuplicateBrep(),
            Extrusion extrusion => extrusion.ToBrep(),
            _ => null
        };
        if (host is null || !host.IsValid || !host.IsSolid)
        {
            message = "贯穿范围只能从有效的封闭 Brep 或挤出实体计算。";
            return false;
        }

        var plane = FastenerGeometryFactory.ToPlane(placement);
        var axis = plane.ZAxis;
        if (!axis.Unitize())
        {
            message = "螺丝轴向无效，无法计算贯穿范围。";
            return false;
        }

        var projected = host.GetBoundingBox(true).GetCorners()
            .Select(point => Vector3d.Multiply(point - plane.Origin, axis))
            .ToArray();
        if (projected.Length == 0)
        {
            message = "宿主包围范围无效。";
            return false;
        }
        var padding = Math.Max(0.2, tolerance * 10);
        var start = projected.Min() - padding;
        var end = projected.Max() + padding;
        if (end - start <= tolerance)
        {
            message = "宿主沿螺丝轴向没有有效厚度。";
            return false;
        }

        var circlePlane = new Plane(plane.Origin + axis * start, plane.XAxis, plane.YAxis);
        var probe = new Cylinder(
            new Circle(circlePlane, diameter * 0.5 + tolerance),
            end - start).ToBrep(true, true);
        if (probe is null || !probe.IsValid || !probe.IsSolid)
        {
            message = "无法生成贯穿范围探测圆柱。";
            return false;
        }

        var overlap = Brep.CreateBooleanIntersection(
            [host],
            [probe],
            tolerance,
            false);
        if (overlap is not { Length: > 0 }
            || overlap.Any(item => !item.IsValid))
        {
            message = "无法计算孔圆柱在宿主内的完整覆盖范围；已停止以避免倾斜背面残留。";
            return false;
        }

        var worldToLocal = Transform.PlaneToPlane(plane, Plane.WorldXY);
        var minimum = double.PositiveInfinity;
        var maximum = double.NegativeInfinity;
        foreach (var body in overlap)
        {
            var local = body.DuplicateBrep();
            if (!local.Transform(worldToLocal))
                continue;
            var box = local.GetBoundingBox(true);
            if (!box.IsValid)
                continue;
            minimum = Math.Min(minimum, box.Min.Z);
            maximum = Math.Max(maximum, box.Max.Z);
        }

        if (!double.IsFinite(minimum)
            || !double.IsFinite(maximum)
            || maximum - minimum <= tolerance)
        {
            message = "孔圆柱与宿主没有可用的轴向重叠范围。";
            return false;
        }

        interval = new Interval(minimum, maximum);
        return true;
    }
}
