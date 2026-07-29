using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;

namespace RhinoMM.Plugin.Geometry;

public static class FastenerGeometryFactory
{
    public static IReadOnlyList<Brep> CreateProxy(FastenerComponentData data, FastenerSizeSpec spec)
    {
        var local = new List<Brep>();
        var embed = FastenerKindTraits.SupportsEmbedDepth(data.Kind) ? data.HeadEmbedDepth : 0;
        var headHeight = HeadGeometryCalculator.GetHeadHeight(data.Kind, spec);
        var tolerance = RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0.001;
        if (FastenerKindTraits.HasShaftProxy(data.Kind))
        {
            // A merely coplanar head/shaft interface can make Rhino's boolean union
            // return only the head for countersunk and hex-head fasteners. Keep a
            // small internal overlap without changing the visible shaft end.
            var overlap = Math.Min(
                headHeight * 0.1,
                Math.Max(tolerance * 2, spec.NominalDiameter * 0.005));
            var shaft = CreateCylinder(
                spec.NominalDiameter / 2,
                data.Length + overlap,
                embed - overlap);
            local.Add(shaft);
        }

        switch (data.Kind)
        {
            case FastenerKind.SocketCap:
                local.Add(CreateCylinder(spec.Head.SocketDiameter / 2, headHeight, embed - headHeight));
                break;
            case FastenerKind.Countersunk:
                local.Add(CreateFrustum(
                    spec.Head.CountersunkDiameter / 2,
                    spec.NominalDiameter / 2,
                    headHeight,
                    embed - headHeight));
                break;
            case FastenerKind.HexBolt:
                local.Add(CreateHexPrism(spec.Head.HexAcrossFlats, headHeight, embed - headHeight));
                break;
            case FastenerKind.HexNut:
                var nutStart = embed - spec.Head.NutThickness;
                local.Add(CreateHollowProxy(
                    CreateHexPrism(spec.Head.NutAcrossFlats, spec.Head.NutThickness, nutStart),
                    spec.NominalDiameter / 2,
                    spec.Head.NutThickness,
                    nutStart,
                    tolerance));
                break;
            case FastenerKind.HeatSetInsert:
                local.Add(CreateHollowProxy(
                    CreateCylinder(data.InsertOuterDiameter / 2, data.Length, 0),
                    spec.NominalDiameter / 2,
                    data.Length,
                    0,
                    tolerance));
                break;
        }

        IReadOnlyList<Brep> result = local;
        if (local.Count > 1)
        {
            var union = Brep.CreateBooleanUnion(local, tolerance);
            var expectedMin = embed - headHeight;
            var expectedMax = embed + data.Length;
            if (union is { Length: 1 }
                && CoversExpectedAxialSpan(union, expectedMin, expectedMax, tolerance))
                result = union;
            // If union is incomplete, keep the original closed shaft and head.
            // Their Rhino group remains the stable component selection boundary.
        }

        var transform = Transform.PlaneToPlane(Plane.WorldXY, ToPlane(data.Placement));
        foreach (var brep in result)
            brep.Transform(transform);
        return result;
    }

    internal static bool CoversExpectedAxialSpan(
        IEnumerable<Brep> breps,
        double expectedMin,
        double expectedMax,
        double tolerance)
    {
        var boxes = breps.Select(brep => brep.GetBoundingBox(true)).ToArray();
        if (boxes.Length == 0 || boxes.Any(box => !box.IsValid))
            return false;
        var actualMin = boxes.Min(box => box.Min.Z);
        var actualMax = boxes.Max(box => box.Max.Z);
        return actualMin <= expectedMin + tolerance
            && actualMax >= expectedMax - tolerance
            && breps.All(brep => brep.IsSolid);
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

    public static Brep CreateHexNutPocketCutter(
        FastenerComponentData data,
        FastenerSizeSpec spec,
        HoleTargetBinding binding,
        double padding)
    {
        var acrossFlats = InstallationPocketCalculator.HexNutAcrossFlats(data, spec, binding);
        if (acrossFlats <= 0)
            throw new InvalidOperationException("六角螺母槽最终对边尺寸必须大于 0。");
        var depth = InstallationPocketCalculator.HexNutEmbedDepth(data);
        if (depth <= 0)
            throw new InvalidOperationException("六角螺母嵌入深度为 0 时不应生成安装槽。");
        var cutter = CreateHexPrism(
            acrossFlats,
            depth + padding,
            -padding);
        cutter.Transform(Transform.PlaneToPlane(Plane.WorldXY, ToPlane(data.Placement)));
        return cutter;
    }

    public static (Brep Shaft, Brep LeadIn) CreateHeatSetPocketCutters(
        FastenerComponentData data,
        FastenerSizeSpec spec,
        double padding,
        double cuttingDepth)
    {
        var finalDiameter = InstallationPocketCalculator.HeatSetFinalDiameter(data);
        if (finalDiameter <= spec.NominalDiameter)
            throw new InvalidOperationException(
                $"热熔安装孔最终直径 {finalDiameter:0.###} mm 必须大于螺纹公称直径 {spec.NominalDiameter:0.###} mm。");
        var radius = finalDiameter / 2;
        var chamferDepth = InstallationPocketCalculator.HeatSetChamferDepth(data);
        var mouthRadius = radius + chamferDepth;
        // Extend the 45-degree cone outside the host while preserving the exact
        // mouth radius at z=0. The shaft ends at the compensated cutting depth.
        var outsideRadius = mouthRadius + padding;
        if (cuttingDepth <= 0)
            throw new InvalidOperationException("热熔安装孔切割深度必须大于 0。");
        var shaft = CreateCylinder(radius, cuttingDepth, 0);
        var leadIn = CreateFrustum(
            outsideRadius,
            radius,
            padding + chamferDepth,
            -padding);
        var transform = Transform.PlaneToPlane(Plane.WorldXY, ToPlane(data.Placement));
        shaft.Transform(transform);
        leadIn.Transform(transform);
        return (shaft, leadIn);
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

    public static IReadOnlyList<Brep> CreateHeadSeatCutters(
        FastenerComponentData data,
        FastenerSizeSpec spec,
        double extra = 0.2)
    {
        if (data.HeadEmbedDepth <= 0 || !FastenerKindTraits.SupportsHeadEmbed(data.Kind))
            return [];

        var headHeight = HeadGeometryCalculator.GetHeadHeight(data.Kind, spec);
        var envelope = HeadGeometryCalculator.GetHeadSeatAxialEnvelope(
            data.HeadEmbedDepth,
            headHeight,
            extra);
        var countersunk = data.Kind == FastenerKind.Countersunk
            ? HeadGeometryCalculator.GetCountersunkSeatProfile(spec, extra, extra)
            : default;
        var cutters = new List<Brep>();
        switch (data.Kind)
        {
            case FastenerKind.SocketCap:
                cutters.Add(CreateCylinder(
                    spec.Head.SocketDiameter / 2 + extra,
                    envelope.End - envelope.CombinedStart,
                    envelope.CombinedStart));
                break;
            case FastenerKind.Countersunk:
                cutters.Add(CreateFrustum(
                    countersunk.LargeRadius,
                    countersunk.SmallRadius,
                    countersunk.Height,
                    envelope.CavityStart));
                if (envelope.RequiresAccess)
                {
                    var tolerance = RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0.001;
                    var overlap = Math.Min(
                        extra,
                        Math.Max(tolerance * 2, spec.NominalDiameter * 0.005));
                    var accessEnd = Math.Min(envelope.End, envelope.CavityStart + overlap);
                    cutters.Add(CreateCylinder(
                        countersunk.LargeRadius,
                        accessEnd - envelope.EntryStart,
                        envelope.EntryStart));
                }
                break;
            case FastenerKind.HexBolt:
                cutters.Add(CreateHexPrism(
                    spec.Head.HexAcrossFlats + extra * 2,
                    envelope.End - envelope.CombinedStart,
                    envelope.CombinedStart));
                break;
        }

        var transform = Transform.PlaneToPlane(Plane.WorldXY, ToPlane(data.Placement));
        foreach (var cutter in cutters)
            cutter.Transform(transform);
        return cutters;
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
        return EnsureOutward(new Cylinder(circle, height).ToBrep(true, true));
    }

    private static Brep CreateFrustum(double topRadius, double bottomRadius, double height, double startZ)
    {
        if (height <= 0 || topRadius <= 0 || bottomRadius <= 0)
            throw new InvalidOperationException("沉头截锥尺寸必须大于 0。");
        var top = new Circle(
            new Plane(new Point3d(0, 0, startZ), Vector3d.ZAxis),
            topRadius).ToNurbsCurve();
        var bottom = new Circle(
            new Plane(new Point3d(0, 0, startZ + height), Vector3d.ZAxis),
            bottomRadius).ToNurbsCurve();
        var loft = Brep.CreateFromLoft(
            [top, bottom],
            Point3d.Unset,
            Point3d.Unset,
            LoftType.Straight,
            false);
        if (loft is null || loft.Length != 1)
            throw new InvalidOperationException("无法生成沉头截锥。");
        var capped = loft[0].CapPlanarHoles(RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0.001)
            ?? throw new InvalidOperationException("无法封闭沉头截锥。");
        return EnsureOutward(capped);
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
        return EnsureOutward(
            brep.CapPlanarHoles(RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0.001) ?? brep);
    }

    private static Brep CreateHollowProxy(
        Brep outer,
        double boreRadius,
        double height,
        double startZ,
        double tolerance)
    {
        if (boreRadius <= 0 || height <= 0)
            return outer;
        try
        {
            var padding = Math.Max(tolerance * 2, 0.01);
            var bore = CreateCylinder(boreRadius, height + padding * 2, startZ - padding);
            var difference = Brep.CreateBooleanDifference([outer], [bore], tolerance);
            if (difference is { Length: 1 } && difference[0].IsSolid)
                return EnsureOutward(difference[0]);
        }
        catch
        {
            // The proxy is display-only; the cutter is generated independently.
        }
        return outer;
    }

    internal static Brep EnsureOutward(Brep brep)
    {
        if (brep.IsSolid && brep.SolidOrientation == BrepSolidOrientation.Inward)
            brep.Flip();
        return brep;
    }
}
