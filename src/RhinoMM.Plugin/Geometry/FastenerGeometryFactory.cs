using Rhino;
using Rhino.Geometry;
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
        double targetSpan)
    {
        var diameter = Core.Services.HoleDiameterCalculator.Calculate(spec, binding, data.PrintProfile).FinalDiameter;
        var depth = binding.DepthMode == DepthMode.Blind ? binding.BlindDepth : Math.Max(targetSpan * 2, data.Length + 20);
        var start = binding.DepthMode == DepthMode.Blind ? 0 : -depth * 0.25;
        var cutter = CreateCylinder(diameter / 2, depth, start);
        cutter.Transform(Transform.PlaneToPlane(Plane.WorldXY, ToPlane(data.Placement)));
        return cutter;
    }

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
