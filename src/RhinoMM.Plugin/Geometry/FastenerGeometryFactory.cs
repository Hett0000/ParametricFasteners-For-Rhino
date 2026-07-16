using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using RhinoMM.Core.Domain;

namespace RhinoMM.Plugin.Geometry;

public static class FastenerGeometryFactory
{
    public static IReadOnlyList<Brep> CreateProxy(FastenerComponentData data, FastenerSizeSpec spec)
    {
        var local = new List<Brep>();
        if (data.Kind != FastenerKind.HexNut)
        {
            var shaft = CreateCylinder(spec.NominalDiameter / 2, data.Length, 0);
            local.Add(shaft);
        }

        switch (data.Kind)
        {
            case FastenerKind.SocketCap:
                local.Add(CreateCylinder(spec.Head.SocketDiameter / 2, spec.Head.SocketHeight, -spec.Head.SocketHeight));
                break;
            case FastenerKind.Countersunk:
                local.Add(CreateCone(spec.Head.CountersunkDiameter / 2, spec.Head.CountersunkDiameter / 2, -spec.Head.CountersunkDiameter / 2));
                break;
            case FastenerKind.HexBolt:
                local.Add(CreateHexPrism(spec.Head.HexAcrossFlats, spec.Head.HexHeight, -spec.Head.HexHeight));
                break;
            case FastenerKind.HexNut:
                local.Add(CreateHexPrism(spec.Head.NutAcrossFlats, spec.Head.NutThickness, 0));
                break;
        }

        var tolerance = RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0.001;
        IReadOnlyList<Brep> result = local;
        if (local.Count > 1)
        {
            var union = Brep.CreateBooleanUnion(local, tolerance);
            if (union is { Length: 1 })
                result = union;
            else
            {
                var joined = Brep.JoinBreps(local, tolerance);
                if (joined is { Length: 1 })
                    result = joined;
            }
        }

        var transform = Transform.PlaneToPlane(Plane.WorldXY, ToPlane(data.Placement));
        foreach (var brep in result)
            brep.Transform(transform);
        return result;
    }

    public static Brep CreateShaftCutter(
        FastenerComponentData data,
        FastenerSizeSpec spec,
        HoleTargetBinding binding,
        double start,
        double end)
    {
        var diameter = Core.Services.HoleDiameterCalculator.Calculate(spec, binding, data.PrintProfile).FinalDiameter;
        if (end <= start)
            throw new InvalidOperationException("切割深度没有与宿主产生有效重叠。");
        var cutter = CreateCylinder(diameter / 2, end - start, start);
        cutter.Transform(Transform.PlaneToPlane(Plane.WorldXY, ToPlane(data.Placement)));
        return cutter;
    }

    public static bool TryGetTargetInterval(
        GeometryBase geometry,
        PlacementFrame placement,
        double tolerance,
        out Interval interval,
        out bool usedFallback)
    {
        var plane = ToPlane(placement);
        var axis = plane.ZAxis;
        axis.Unitize();
        var box = geometry.GetBoundingBox(true);
        var projected = box.GetCorners()
            .Select(point => Vector3d.Multiply(point - plane.Origin, axis))
            .ToArray();
        var boxMin = projected.Min();
        var boxMax = projected.Max();
        var extension = Math.Max(box.Diagonal.Length * 0.05, Math.Max(tolerance * 10, 0.2));
        var line = new LineCurve(
            plane.Origin + axis * (boxMin - extension),
            plane.Origin + axis * (boxMax + extension));

        var brep = geometry switch
        {
            Brep value => value,
            Extrusion extrusion => extrusion.ToBrep(),
            _ => null
        };
        if (brep is not null
            && Intersection.CurveBrep(line, brep, tolerance, out _, out var points)
            && points.Length >= 2)
        {
            var parameters = points
                .Select(point => Vector3d.Multiply(point - plane.Origin, axis))
                .OrderBy(value => value)
                .ToArray();
            interval = new Interval(parameters[0], parameters[^1]);
            usedFallback = false;
            return interval.Length > tolerance;
        }

        interval = new Interval(boxMin, boxMax);
        usedFallback = true;
        return interval.Length > tolerance;
    }

    public static double DepthLimit(FastenerComponentData data, FastenerSizeSpec spec, HoleTargetBinding binding) =>
        Core.Services.HoleDepthCalculator.GetLimit(data, spec, binding);

    public static Brep? CreateHeadSeatCutter(FastenerComponentData data, FastenerSizeSpec spec, double extra = 0.2)
    {
        Brep? cutter = data.Kind switch
        {
            FastenerKind.SocketCap => CreateCylinder(
                spec.Head.SocketDiameter / 2 + extra,
                spec.Head.SocketHeight + extra,
                -extra),
            FastenerKind.Countersunk => CreateCone(
                spec.Head.CountersunkDiameter / 2 + extra,
                spec.Head.CountersunkDiameter / 2 + extra,
                0),
            FastenerKind.HexBolt => CreateHexPrism(
                spec.Head.HexAcrossFlats + extra * 2,
                spec.Head.HexHeight + extra,
                -extra),
            _ => null
        };
        cutter?.Transform(Transform.PlaneToPlane(Plane.WorldXY, ToPlane(data.Placement)));
        return cutter;
    }

    public static Plane ToPlane(PlacementFrame frame) => new(
        new Point3d(frame.OriginX, frame.OriginY, frame.OriginZ),
        new Vector3d(frame.XAxisX, frame.XAxisY, frame.XAxisZ),
        new Vector3d(frame.YAxisX, frame.YAxisY, frame.YAxisZ));

    public static PlacementFrame FromPlane(Plane plane) => new(
        plane.OriginX, plane.OriginY, plane.OriginZ,
        plane.XAxis.X, plane.XAxis.Y, plane.XAxis.Z,
        plane.YAxis.X, plane.YAxis.Y, plane.YAxis.Z,
        plane.ZAxis.X, plane.ZAxis.Y, plane.ZAxis.Z);

    private static Brep CreateCylinder(double radius, double height, double startZ)
    {
        var circle = new Circle(new Plane(new Point3d(0, 0, startZ), Vector3d.ZAxis), radius);
        return new Cylinder(circle, height).ToBrep(true, true);
    }

    private static Brep CreateCone(double radius, double height, double startZ)
    {
        var plane = new Plane(new Point3d(0, 0, startZ), Vector3d.ZAxis);
        return new Cone(plane, height, radius).ToBrep(true);
    }

    private static Brep CreateHexPrism(double acrossFlats, double height, double startZ)
    {
        var radius = acrossFlats / Math.Sqrt(3);
        var points = Enumerable.Range(0, 6)
            .Select(i =>
            {
                var angle = Math.PI / 6 + i * Math.PI / 3;
                return new Point3d(radius * Math.Cos(angle), radius * Math.Sin(angle), startZ);
            })
            .ToList();
        points.Add(points[0]);
        var profile = new PolylineCurve(points);
        var faces = Brep.CreatePlanarBreps(profile, RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0.001);
        if (faces is null || faces.Length == 0)
            throw new InvalidOperationException("无法生成六角截面。");
        var surface = Surface.CreateExtrusion(profile, Vector3d.ZAxis * height)
            ?? throw new InvalidOperationException("无法拉伸六角截面。");
        var brep = surface.ToBrep();
        return brep.CapPlanarHoles(RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0.001) ?? brep;
    }
}
