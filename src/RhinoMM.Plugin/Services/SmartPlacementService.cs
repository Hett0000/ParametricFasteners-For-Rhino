using Rhino;
using Rhino.ApplicationSettings;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Geometry;
using RhinoMM.Plugin.Persistence;
using RhinoViewport = Rhino.Display.RhinoViewport;

namespace RhinoMM.Plugin.Services;

internal sealed record SmartPlacementHost(
    Guid ObjectId,
    RhinoObject Object,
    Brep Brep,
    BoundingBox BoundingBox);

internal sealed record SmartPlacementPreview(
    bool IsValid,
    Point3d Anchor,
    string Message,
    string SnapLabel,
    SmartPlacementParameterSignature ParameterSignature,
    FastenerComponentData? Draft,
    IReadOnlyList<Brep> Proxies,
    IReadOnlyList<CutterGeometryBuild> Cutters,
    IReadOnlyDictionary<Guid, ShaftFitRole> HostRoles)
{
    public static SmartPlacementPreview Invalid(
        Point3d anchor,
        string message,
        SmartPlacementParameterSignature parameterSignature,
        string snapLabel = "自由面") =>
        new(
            false,
            anchor,
            message,
            snapLabel,
            parameterSignature,
            null,
            Array.Empty<Brep>(),
            Array.Empty<CutterGeometryBuild>(),
            new Dictionary<Guid, ShaftFitRole>());
}

internal readonly record struct SmartPlacementParameterSignature(
    FastenerKind Kind,
    string Size,
    double Length,
    double HeadEmbedDepth,
    double InsertOuterDiameter,
    double InsertDiameterCompensation,
    double InsertDepthCompensation,
    double PrinterCorrection,
    ClearanceFitClass ClearanceFit,
    double BiteReduction,
    DepthMode EngagementDepthMode,
    double EngagementBlindDepth,
    bool ClearancePreviewVisible,
    bool ClearanceBooleanEnabled,
    bool EngagementPreviewVisible,
    bool EngagementBooleanEnabled,
    bool HeatSetPreviewVisible,
    bool HeatSetBooleanEnabled);

internal sealed record SmartPlacementParameterSnapshot(
    SmartPlacementParameterSignature Signature,
    FastenerSizeSpec Spec,
    PlacementCutterPreset Preset,
    HeatSetInsertPreset HeatSetPreset,
    double FastenerOpacityPercent,
    double CutterOpacityPercent)
{
    public static SmartPlacementParameterSnapshot Capture(EditorState state)
    {
        var preset = PlacementPresetService.Current;
        var heatSetPreset = HeatSetInsertPresetService.Current;
        var signature = new SmartPlacementParameterSignature(
            state.Kind,
            state.Size,
            state.Length,
            state.HeadEmbedDepth,
            state.InsertOuterDiameter,
            state.InsertDiameterCompensation,
            state.InsertDepthCompensation,
            preset.PrinterCorrection,
            preset.ClearanceFit,
            preset.BiteReduction,
            preset.EngagementDepthMode,
            preset.EngagementBlindDepth,
            preset.ClearancePreviewVisible,
            preset.ClearanceBooleanEnabled,
            preset.EngagementPreviewVisible,
            preset.EngagementBooleanEnabled,
            heatSetPreset.PreviewVisible,
            heatSetPreset.BooleanEnabled);
        return new SmartPlacementParameterSnapshot(
            signature,
            RhinoMMPlugIn.Catalog.Get(signature.Size),
            preset,
            heatSetPreset,
            GlobalDisplaySettingsService.Current.FastenerOpacityPercent,
            GlobalDisplaySettingsService.Current.CutterOpacityPercent);
    }

    public FastenerComponentData CreateDraft(
        Plane plane,
        IReadOnlyList<HoleTargetBinding> bindings,
        SmartPlacementRecognitionMode? recognitionMode = null) => new()
    {
        ComponentId = Guid.NewGuid(),
        Kind = Signature.Kind,
        Size = Signature.Size,
        Length = Signature.Length,
        HeadEmbedDepth = FastenerKindTraits.SupportsEmbedDepth(Signature.Kind)
            ? Signature.HeadEmbedDepth
            : 0,
        InsertOuterDiameter = Signature.Kind == FastenerKind.HeatSetInsert
            ? Signature.InsertOuterDiameter
            : 0,
        InsertDiameterCompensation = Signature.Kind == FastenerKind.HeatSetInsert
            ? Signature.InsertDiameterCompensation
            : 0,
        InsertDepthCompensation = Signature.Kind == FastenerKind.HeatSetInsert
            ? Signature.InsertDepthCompensation
            : 0,
        Placement = FastenerGeometryFactory.FromPlane(plane),
        PrintProfile = new PrintProfileSnapshot(
            "当前 FDM 配置",
            Signature.PrinterCorrection),
        FastenerOpacityPercent = FastenerOpacityPercent,
        CutterOpacityPercent = CutterOpacityPercent,
        AutoRecognizeHosts = recognitionMode.HasValue
            && !FastenerKindTraits.IsNut(Signature.Kind),
        SmartRecognitionMode = recognitionMode ?? SmartPlacementRecognitionMode.Automatic,
        SmartBindingProfile = recognitionMode.HasValue
            ? Preset.ToSmartBindingProfile()
            : null,
        ProxyObjectId = Guid.Empty,
        ControlPointObjectId = Guid.Empty,
        Bindings = bindings,
        UpdatedAt = DateTimeOffset.UtcNow
    };
}

internal static class SmartPlacementDraftChangeService
{
    private static long _revision;

    public static long Revision => Interlocked.Read(ref _revision);

    public static void NotifyChanged() => Interlocked.Increment(ref _revision);
}

internal sealed class SmartPlacementService
{
    private readonly RhinoDoc _doc;
    private readonly EditorState _state;
    private readonly IReadOnlyList<SmartPlacementHost> _hosts;
    private readonly double _tolerance;
    private readonly double _snapTolerance;

    public SmartPlacementService(RhinoDoc doc, EditorState state)
    {
        _doc = doc;
        _state = state;
        _tolerance = Math.Max(doc.ModelAbsoluteTolerance, 1e-6);
        _snapTolerance = Math.Max(0.2, _tolerance * 10);
        _hosts = SmartHostBindingService.CaptureHosts(doc);
    }

    public SmartPlacementParameterSignature CurrentParameterSignature() =>
        SmartPlacementParameterSnapshot.Capture(_state).Signature;

    public SmartPlacementPreview Evaluate(
        RhinoViewport viewport,
        System.Drawing.Point windowPoint,
        Point3d getterPoint,
        OsnapModes osnapMode,
        Guid snapObjectId,
        SmartPlacementRecognitionMode recognitionMode,
        bool flipDirection)
    {
        SmartPlacementParameterSnapshot parameters;
        try
        {
            parameters = SmartPlacementParameterSnapshot.Capture(_state);
        }
        catch (Exception ex)
        {
            return SmartPlacementPreview.Invalid(
                getterPoint,
                $"当前紧固件参数无效：{ex.Message}",
                default);
        }

        var hasSnap = osnapMode != OsnapModes.None && getterPoint.IsValid;
        var snapLabel = hasSnap ? SnapLabel(osnapMode) : "自由面";
        if (!TryGetViewRay(viewport, windowPoint, out var ray))
        {
            return SmartPlacementPreview.Invalid(
                getterPoint,
                "无法计算当前视口的鼠标拾取射线。",
                parameters.Signature,
                snapLabel);
        }

        if (!TryFindPlacementFace(
                ray,
                getterPoint,
                hasSnap,
                snapObjectId,
                out var faceError,
                out var host,
                out var face,
                out var origin,
                out var u,
                out var v))
        {
            var anchor = getterPoint.IsValid ? getterPoint : ray.From;
            return SmartPlacementPreview.Invalid(
                anchor,
                !string.IsNullOrWhiteSpace(faceError)
                    ? faceError
                    : hasSnap
                    ? "捕捉点无法关联到可用的封闭实体面。"
                    : "鼠标下方没有可用的封闭实体面。",
                parameters.Signature,
                snapLabel);
        }

        if (!TryCreatePlacementPlane(face, origin, u, v, flipDirection, out var plane))
            return SmartPlacementPreview.Invalid(
                origin,
                "无法计算放置面的局部方向。",
                parameters.Signature,
                snapLabel);

        return FastenerKindTraits.UsesSingleHostPlacement(parameters.Signature.Kind)
            ? BuildInstallationPocketPreview(parameters, host, plane, snapLabel)
            : BuildScrewPreview(parameters, host, plane, snapLabel, recognitionMode);
    }

    private SmartPlacementPreview BuildScrewPreview(
        SmartPlacementParameterSnapshot parameters,
        SmartPlacementHost placementHost,
        Plane plane,
        string snapLabel,
        SmartPlacementRecognitionMode recognitionMode)
    {
        var reach = Math.Max(
            _tolerance,
            parameters.Signature.HeadEmbedDepth + parameters.Signature.Length);
        var intervals = FindAxisHosts(plane, reach);
        var classification = SmartHostClassifier.Classify(intervals, recognitionMode, _tolerance);
        if (!classification.IsValid)
            return SmartPlacementPreview.Invalid(
                plane.Origin,
                classification.Message,
                parameters.Signature,
                snapLabel);

        if (classification.Assignments.All(item => item.ObjectId != placementHost.ObjectId))
        {
            return SmartPlacementPreview.Invalid(
                plane.Origin,
                "放置面所属实体未与螺杆轴线形成有效交集。",
                parameters.Signature,
                snapLabel);
        }

        var bindings = classification.Assignments
            .Select(item => item.Role == ShaftFitRole.Clearance
                ? parameters.Preset.CreateClearanceBinding(
                    item.ObjectId,
                    item.ObjectId == placementHost.ObjectId)
                : parameters.Preset.CreateEngagementBinding(
                    item.ObjectId,
                    item.ObjectId == placementHost.ObjectId))
            .ToArray();
        var draft = parameters.CreateDraft(plane, bindings, recognitionMode);
        var clearanceCount = bindings.Count(item => item.Role == ShaftFitRole.Clearance);
        var engagementCount = bindings.Count(item => item.Role == ShaftFitRole.ThreadEngagement);
        var message =
            $"{draft.Size}×{draft.Length:0.##} · 通{clearanceCount} / 咬{engagementCount} · {DepthLabel(parameters.Preset.EngagementDepthMode, parameters.Preset.EngagementBlindDepth)}";
        return BuildGeometryPreview(parameters, draft, message, snapLabel);
    }

    private SmartPlacementPreview BuildInstallationPocketPreview(
        SmartPlacementParameterSnapshot parameters,
        SmartPlacementHost placementHost,
        Plane plane,
        string snapLabel)
    {
        var depth = parameters.Signature.Kind == FastenerKind.HexNut
            ? parameters.Signature.HeadEmbedDepth
            : parameters.Signature.Length + parameters.Signature.InsertDepthCompensation;
        var preview = parameters.Signature.Kind == FastenerKind.HeatSetInsert
            ? parameters.HeatSetPreset.PreviewVisible
            : parameters.Preset.ClearancePreviewVisible;
        var booleanEnabled = parameters.Signature.Kind == FastenerKind.HeatSetInsert
            ? parameters.HeatSetPreset.BooleanEnabled
            : parameters.Preset.ClearanceBooleanEnabled;
        var binding = new HoleTargetBinding
        {
            TargetObjectId = placementHost.ObjectId,
            Role = ShaftFitRole.InstallationPocket,
            DepthMode = DepthMode.Blind,
            BlindDepth = depth,
            IsPreviewVisible = preview,
            IsBooleanEnabled = booleanEnabled
        };
        var draft = parameters.CreateDraft(plane, [binding]);
        var pocketName = parameters.Signature.Kind == FastenerKind.HexNut ? "六角槽" : "热熔孔";
        return BuildGeometryPreview(
            parameters,
            draft,
            $"{draft.Size} · {pocketName} · 深{depth:0.##}",
            snapLabel);
    }

    private SmartPlacementPreview BuildGeometryPreview(
        SmartPlacementParameterSnapshot parameters,
        FastenerComponentData draft,
        string message,
        string snapLabel)
    {
        var validation = FastenerComponentValidator.Validate(draft, parameters.Spec);
        if (!validation.IsValid)
        {
            return SmartPlacementPreview.Invalid(
                FastenerGeometryFactory.ToPlane(draft.Placement).Origin,
                validation.Issues.First(item => item.IsError).Message,
                parameters.Signature,
                snapLabel);
        }

        try
        {
            var proxies = FastenerGeometryFactory.CreateProxy(draft, parameters.Spec);
            var cutters = new List<CutterGeometryBuild>();
            foreach (var binding in draft.Bindings)
            {
                if (!CutterGeometryService.TryBuild(
                        _doc,
                        draft,
                        parameters.Spec,
                        binding,
                        out var cutter,
                        out var cutterError))
                {
                    return SmartPlacementPreview.Invalid(
                        FastenerGeometryFactory.ToPlane(draft.Placement).Origin,
                        cutterError,
                        parameters.Signature,
                        snapLabel);
                }
                cutters.Add(cutter!);
            }

            var roles = draft.Bindings
                .GroupBy(item => item.TargetObjectId)
                .ToDictionary(group => group.Key, group => group.First().Role);
            return new SmartPlacementPreview(
                true,
                FastenerGeometryFactory.ToPlane(draft.Placement).Origin,
                message,
                snapLabel,
                parameters.Signature,
                draft,
                proxies,
                cutters,
                roles);
        }
        catch (Exception ex)
        {
            return SmartPlacementPreview.Invalid(
                FastenerGeometryFactory.ToPlane(draft.Placement).Origin,
                ex.Message,
                parameters.Signature,
                snapLabel);
        }
    }

    private IReadOnlyList<SmartHostInterval> FindAxisHosts(Plane plane, double reach)
        => SmartHostBindingService.FindIntervals(
            _hosts,
            FastenerGeometryFactory.FromPlane(plane),
            reach,
            _tolerance);

    private bool TryFindPlacementFace(
        Line ray,
        Point3d getterPoint,
        bool hasSnap,
        Guid snapObjectId,
        out string error,
        out SmartPlacementHost host,
        out BrepFace face,
        out Point3d origin,
        out double u,
        out double v)
    {
        error = string.Empty;
        host = null!;
        face = null!;
        origin = Point3d.Unset;
        u = 0;
        v = 0;

        var rayHit = FindRayFace(ray, out error);
        if (!hasSnap)
        {
            if (rayHit is null)
                return false;
            host = rayHit.Value.Host;
            face = rayHit.Value.Face;
            origin = rayHit.Value.Point;
            u = rayHit.Value.U;
            v = rayHit.Value.V;
            return true;
        }

        if (rayHit is not null
            && rayHit.Value.Face.ClosestPoint(getterPoint, out var rayU, out var rayV))
        {
            var projected = rayHit.Value.Face.PointAt(rayU, rayV);
            if (projected.DistanceTo(getterPoint) <= _snapTolerance)
            {
                host = rayHit.Value.Host;
                face = rayHit.Value.Face;
                origin = projected;
                u = rayU;
                v = rayV;
                return true;
            }
        }

        // Object snaps may move the getter point away from the literal cursor ray.
        // Only search other faces on the front-most visible host; otherwise a snap
        // can incorrectly associate with an occluded object behind it.
        if (rayHit is null)
            return false;

        var bestDistance = double.PositiveInfinity;
        var candidate = rayHit.Value.Host;
        for (var faceIndex = 0; faceIndex < candidate.Brep.Faces.Count; faceIndex++)
        {
            var candidateFace = candidate.Brep.Faces[faceIndex];
            if (!candidateFace.ClosestPoint(getterPoint, out var candidateU, out var candidateV))
                continue;
            var projected = candidateFace.PointAt(candidateU, candidateV);
            var distance = projected.DistanceTo(getterPoint);
            if (distance > _snapTolerance || distance >= bestDistance)
                continue;
            bestDistance = distance;
            host = candidate;
            face = candidateFace;
            origin = projected;
            u = candidateU;
            v = candidateV;
        }
        return face is not null;
    }

    private (SmartPlacementHost Host, BrepFace Face, Point3d Point, double U, double V)? FindRayFace(
        Line ray,
        out string error)
    {
        error = string.Empty;
        var direction = ray.Direction;
        var rayLength = direction.Length;
        if (rayLength <= RhinoMath.ZeroTolerance || !direction.Unitize())
            return null;

        var lineCurve = new LineCurve(ray);
        var hits = new List<(SmartPlacementHost Host, BrepFace Face, Point3d Point, double U, double V, double Depth)>();
        foreach (var host in _hosts)
        {
            if (!Intersection.LineBox(ray, host.BoundingBox, _tolerance, out _))
                continue;
            for (var faceIndex = 0; faceIndex < host.Brep.Faces.Count; faceIndex++)
            {
                var candidateFace = host.Brep.Faces[faceIndex];
                if (!Intersection.CurveBrepFace(
                        lineCurve,
                        candidateFace,
                        _tolerance,
                        out _,
                        out var points))
                    continue;
                foreach (var point in points)
                {
                    var depth = Vector3d.Multiply(point - ray.From, direction);
                    if (depth < -_tolerance || depth > rayLength + _tolerance)
                        continue;
                    if (!candidateFace.ClosestPoint(point, out var u, out var v))
                        continue;
                    var closest = candidateFace.PointAt(u, v);
                    if (closest.DistanceTo(point) > _snapTolerance)
                        continue;
                    hits.Add((host, candidateFace, closest, u, v, Math.Max(0, depth)));
                }
            }
        }

        if (hits.Count == 0)
            return null;

        var ordered = hits
            .OrderBy(item => item.Depth)
            .ThenBy(item => item.Host.ObjectId)
            .ThenBy(item => item.Face.FaceIndex)
            .ToArray();
        var nearest = ordered[0];
        var overlapTolerance = Math.Max(_tolerance, RhinoMath.ZeroTolerance);
        if (ordered.Any(item =>
                item.Host.ObjectId != nearest.Host.ObjectId
                && Math.Abs(item.Depth - nearest.Depth) <= overlapTolerance))
        {
            error = "鼠标下方存在深度重合的多个实体面，请改用经典放置。";
            return null;
        }

        return (nearest.Host, nearest.Face, nearest.Point, nearest.U, nearest.V);
    }

    private static bool TryGetViewRay(
        RhinoViewport viewport,
        System.Drawing.Point windowPoint,
        out Line ray)
    {
        if (!viewport.GetFrustumLine(windowPoint.X, windowPoint.Y, out ray)
            || !ray.IsValid
            || ray.Length <= RhinoMath.ZeroTolerance)
            return false;

        var cameraDirection = viewport.CameraDirection;
        if (cameraDirection.Unitize()
            && Vector3d.Multiply(ray.Direction, cameraDirection) < 0)
        {
            ray = new Line(ray.To, ray.From);
        }
        return true;
    }

    private static bool TryCreatePlacementPlane(
        BrepFace face,
        Point3d origin,
        double u,
        double v,
        bool flipDirection,
        out Plane plane)
    {
        if (!face.FrameAt(u, v, out var frame))
            frame = new Plane(origin, face.NormalAt(u, v));

        var inward = face.NormalAt(u, v);
        if (face.OrientationIsReversed)
            inward.Reverse();
        inward.Reverse();
        if (flipDirection)
            inward.Reverse();
        if (!inward.Unitize())
        {
            plane = Plane.Unset;
            return false;
        }

        var xAxis = frame.XAxis;
        xAxis -= inward * Vector3d.Multiply(xAxis, inward);
        if (!xAxis.Unitize())
        {
            xAxis = Math.Abs(Vector3d.Multiply(inward, Vector3d.XAxis)) < 0.9
                ? Vector3d.CrossProduct(Vector3d.XAxis, inward)
                : Vector3d.CrossProduct(Vector3d.YAxis, inward);
            if (!xAxis.Unitize())
            {
                plane = Plane.Unset;
                return false;
            }
        }
        var yAxis = Vector3d.CrossProduct(inward, xAxis);
        if (!yAxis.Unitize())
        {
            plane = Plane.Unset;
            return false;
        }
        plane = new Plane(origin, xAxis, yAxis);
        return plane.IsValid;
    }

    public SmartPlacementHost? FindHost(Guid objectId) =>
        _hosts.FirstOrDefault(item => item.ObjectId == objectId);

    private static string SnapLabel(OsnapModes mode) => mode switch
    {
        OsnapModes.End => "端点",
        OsnapModes.Midpoint => "中点",
        OsnapModes.Center => "圆心",
        OsnapModes.Intersection => "交点",
        OsnapModes.Vertex => "顶点",
        OsnapModes.Knot => "节点",
        OsnapModes.Point => "点",
        OsnapModes.Quadrant => "象限点",
        OsnapModes.Perpendicular => "垂足",
        OsnapModes.Tangent => "切点",
        OsnapModes.Near => "最近点",
        _ => "对象捕捉"
    };

    private static string DepthLabel(DepthMode mode, double customDepth) => mode switch
    {
        DepthMode.ThroughTarget => "贯穿",
        DepthMode.FastenerLengthPlusOneDiameter => "L+1D",
        DepthMode.FastenerLengthPlusCustom => $"L+{customDepth:0.##}",
        DepthMode.FastenerLengthPlusTwoDiameters => "L+2D",
        DepthMode.Blind => "自定义",
        _ => mode.ToString()
    };
}
