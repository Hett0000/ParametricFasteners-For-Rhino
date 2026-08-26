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

internal enum SmartPlacementGlyphKind
{
    CircularHole,
    HeadSeat,
    HexPocket,
    EntryChamfer,
    Bridge
}

internal sealed record SmartPlacementCutterGlyph(
    ShaftFitRole Role,
    SmartPlacementGlyphKind Kind,
    Plane Plane,
    double Radius,
    double Start,
    double End,
    string Label,
    double EndRadius = 0);

internal readonly record struct SmartPlacementAnchorSignature(
    SmartPlacementParameterSignature ParameterSignature,
    Guid PlacementHostId,
    int FaceIndex,
    Point3d Anchor,
    Vector3d Axis,
    long DocumentRevision);

/// <summary>
/// Cursor-time preview. It deliberately contains no host classification,
/// bindings, cutter geometry or prepared document geometry.
/// </summary>
internal sealed record SmartPlacementCursorPreview(
    bool IsValid,
    Point3d Anchor,
    string Message,
    string SnapLabel,
    SmartPlacementAnchorSignature AnchorSignature,
    IReadOnlyList<Brep> LocalProxies,
    Transform DisplayTransform)
{
    public static SmartPlacementCursorPreview Invalid(
        Point3d anchor,
        string message,
        SmartPlacementParameterSignature parameterSignature,
        string snapLabel = "自由面") => new(
            false,
            anchor,
            message,
            snapLabel,
            new SmartPlacementAnchorSignature(
                parameterSignature,
                Guid.Empty,
                -1,
                anchor,
                Vector3d.Unset,
                0),
            Array.Empty<Brep>(),
            Transform.Identity);
}

internal sealed record SmartPlacementPreview(
    bool IsValid,
    Point3d Anchor,
    string Message,
    string SnapLabel,
    SmartPlacementParameterSignature ParameterSignature,
    FastenerComponentData? Draft,
    IReadOnlyList<Brep> Proxies,
    IReadOnlyList<CutterGeometryBuild> Cutters,
    IReadOnlyDictionary<Guid, ShaftFitRole> HostRoles,
    IReadOnlyList<SmartPlacementCutterGlyph> Glyphs,
    PreparedFastenerGeometry? Prepared = null,
    long DocumentRevision = 0,
    SmartPlacementAnchorSignature AnchorSignature = default)
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
            new Dictionary<Guid, ShaftFitRole>(),
            Array.Empty<SmartPlacementCutterGlyph>());
}

internal readonly record struct SmartPlacementParameterSignature(
    FastenerKind Kind,
    HexNutStyle HexNutStyle,
    string Size,
    double Length,
    double HeadEmbedDepth,
    bool CounterboreBridgeEnabled,
    double CounterboreBridgeLayerHeight,
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
    bool HeatSetBooleanEnabled,
    ScrewAssemblyMode AssemblyMode,
    HexNutStyle PairedNutStyle,
    double NutTipProtrusion,
    double NutPocketCompensation,
    bool NutPocketPreviewVisible,
    bool NutPocketBooleanEnabled,
    bool EngagementOnly,
    bool EngagementEntryChamferEnabled,
    double EngagementEntryChamferSize,
    EngagementEntryChamferMode EngagementEntryChamferMode,
    double EngagementOnlyAlignmentDepth,
    double EngagementOnlyAlignmentDiameterCompensation);

internal sealed record SmartPlacementParameterSnapshot(
    SmartPlacementParameterSignature Signature,
    FastenerSizeSpec Spec,
    PlacementCutterPreset Preset,
    HeatSetInsertPreset HeatSetPreset,
    double FastenerOpacityPercent,
    double CutterOpacityPercent,
    Guid? CustomDefinitionId = null,
    string CustomDefinitionName = "",
    FastenerDefinitionSnapshot? CustomDefinitionSnapshot = null)
{
    public static SmartPlacementParameterSnapshot Capture(EditorState state)
    {
        var preset = PlacementPresetService.Current;
        var heatSetPreset = HeatSetInsertPresetService.Current;
        var signature = new SmartPlacementParameterSignature(
            state.Kind,
            state.HexNutStyle,
            state.Size,
            state.Length,
            state.HeadEmbedDepth,
            state.Kind == FastenerKind.SocketCap
                && state.HeadEmbedDepth > 0
                && preset.CounterboreBridgeEnabled,
            preset.CounterboreBridgeLayerHeight,
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
            heatSetPreset.BooleanEnabled,
            preset.AssemblyMode,
            preset.PairedNutStyle,
            preset.NutTipProtrusion,
            preset.NutPocketCompensation,
            preset.NutPocketPreviewVisible,
            preset.NutPocketBooleanEnabled,
            preset.AssemblyMode == ScrewAssemblyMode.EngagementOnly,
            preset.AssemblyMode == ScrewAssemblyMode.ThreadEngagement
                && preset.EngagementEntryChamferEnabled,
            preset.EngagementEntryChamferSize,
            preset.EngagementEntryChamferMode,
            preset.EngagementOnlyAlignmentDepth,
            preset.EngagementOnlyAlignmentDiameterCompensation);
        return new SmartPlacementParameterSnapshot(
            signature,
            state.CustomDefinitionSnapshot?.SizeSpec ?? RhinoMMPlugIn.Catalog.Get(signature.Size),
            preset,
            heatSetPreset,
            GlobalDisplaySettingsService.Current.FastenerOpacityPercent,
            GlobalDisplaySettingsService.Current.CutterOpacityPercent,
            state.CustomDefinitionId,
            state.CustomDefinitionName,
            state.CustomDefinitionSnapshot);
    }

    public static SmartPlacementParameterSnapshot Capture(FastenerTemplateData source)
    {
        var data = source.Normalize();
        var clearancePreview = data.ClearancePreviewVisible ?? true;
        var clearanceBoolean = data.ClearanceBooleanEnabled ?? true;
        var engagementPreview = data.EngagementPreviewVisible ?? true;
        var engagementBoolean = data.EngagementBooleanEnabled ?? true;
        var installationPreview = data.InstallationPreviewVisible ?? true;
        var installationBoolean = data.InstallationBooleanEnabled ?? true;
        var nutPreview = data.NutPocketPreviewVisible ?? true;
        var nutBoolean = data.NutPocketBooleanEnabled ?? true;
        var preset = new PlacementCutterPreset(
            data.PrinterCorrection,
            ClearanceFitClass.Normal,
            data.BiteReduction,
            data.CounterboreBridgeEnabled,
            data.CounterboreBridgeLayerHeight,
            data.EngagementDepthMode,
            data.EngagementBlindDepth,
            clearancePreview,
            clearanceBoolean,
            engagementPreview,
            engagementBoolean,
            data.AssemblyMode == ScrewAssemblyMode.EngagementOnly,
            data.AssemblyMode,
            data.PairedNutStyle,
            data.NutTipProtrusion,
            data.NutPocketCompensation,
            nutPreview,
            nutBoolean,
            data.EngagementEntryChamferEnabled,
            data.EngagementEntryChamferSize,
            data.EngagementEntryChamferMode,
            data.EngagementOnlyAlignmentDepth,
            data.EngagementOnlyAlignmentDiameterCompensation);
        var heatSet = new HeatSetInsertPreset(
            data.Length,
            data.InsertOuterDiameter,
            data.InsertDiameterCompensation,
            data.InsertDepthCompensation,
            installationPreview,
            installationBoolean);
        var signature = new SmartPlacementParameterSignature(
            data.Kind,
            data.NutStyle,
            data.Size,
            data.Length,
            data.HeadEmbedDepth,
            data.CounterboreBridgeEnabled,
            data.CounterboreBridgeLayerHeight,
            data.InsertOuterDiameter,
            data.InsertDiameterCompensation,
            data.InsertDepthCompensation,
            data.PrinterCorrection,
            ClearanceFitClass.Normal,
            data.BiteReduction,
            data.EngagementDepthMode,
            data.EngagementBlindDepth,
            clearancePreview,
            clearanceBoolean,
            engagementPreview,
            engagementBoolean,
            installationPreview,
            installationBoolean,
            data.AssemblyMode,
            data.PairedNutStyle,
            data.NutTipProtrusion,
            data.NutPocketCompensation,
            nutPreview,
            nutBoolean,
            data.AssemblyMode == ScrewAssemblyMode.EngagementOnly,
            data.AssemblyMode == ScrewAssemblyMode.ThreadEngagement
                && data.EngagementEntryChamferEnabled,
            data.EngagementEntryChamferSize,
            data.EngagementEntryChamferMode,
            data.EngagementOnlyAlignmentDepth,
            data.EngagementOnlyAlignmentDiameterCompensation);
        return new SmartPlacementParameterSnapshot(
            signature,
            FastenerSpecResolver.Resolve(data, RhinoMMPlugIn.Catalog),
            preset,
            heatSet,
            GlobalDisplaySettingsService.Current.FastenerOpacityPercent,
            GlobalDisplaySettingsService.Current.CutterOpacityPercent,
            data.CustomDefinitionId,
            data.CustomDefinitionName,
            data.CustomDefinitionSnapshot);
    }

    public FastenerComponentData CreateDraft(
        Plane plane,
        IReadOnlyList<HoleTargetBinding> bindings,
        SmartPlacementRecognitionMode? recognitionMode = null) => new()
    {
        ComponentId = Guid.NewGuid(),
        Kind = Signature.Kind,
        HexNutStyle = Signature.Kind == FastenerKind.HexNut
            ? Signature.HexNutStyle
            : HexNutStyle.Standard,
        Size = Signature.Size,
        Length = Signature.Length,
        HeadEmbedDepth = FastenerKindTraits.SupportsEmbedDepth(Signature.Kind)
            ? Signature.HeadEmbedDepth
            : 0,
        CounterboreBridgeEnabled = Signature.Kind == FastenerKind.SocketCap
            && Signature.HeadEmbedDepth > 0
            && Signature.CounterboreBridgeEnabled,
        CounterboreBridgeLayerHeight = Signature.CounterboreBridgeLayerHeight,
        EngagementEntryChamferEnabled = FastenerKindTraits.IsScrew(Signature.Kind)
            && Signature.AssemblyMode == ScrewAssemblyMode.ThreadEngagement
            && Signature.EngagementEntryChamferEnabled,
        EngagementEntryChamferSize = Signature.EngagementEntryChamferSize,
        EngagementEntryChamferMode = Signature.EngagementEntryChamferMode,
        EngagementOnlyAlignmentDepth = Signature.EngagementOnlyAlignmentDepth,
        EngagementOnlyAlignmentDiameterCompensation =
            Signature.EngagementOnlyAlignmentDiameterCompensation,
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
        AssemblyMode = FastenerKindTraits.IsScrew(Signature.Kind)
            ? Signature.AssemblyMode
            : ScrewAssemblyMode.ThreadEngagement,
        PairedNutStyle = Signature.PairedNutStyle,
        NutTipProtrusion = Signature.NutTipProtrusion,
        NutPocketCompensation = Signature.NutPocketCompensation,
        EngagementOnly = FastenerKindTraits.IsScrew(Signature.Kind)
            && Signature.AssemblyMode == ScrewAssemblyMode.EngagementOnly,
        AutoRecognizeHosts = recognitionMode.HasValue
            && !FastenerKindTraits.IsNut(Signature.Kind),
        SmartRecognitionMode = recognitionMode ?? SmartPlacementRecognitionMode.Automatic,
        SmartBindingProfile = recognitionMode.HasValue
            ? Preset.ToSmartBindingProfile()
            : null,
        ProxyObjectId = Guid.Empty,
        ControlPointObjectId = Guid.Empty,
        Bindings = bindings,
        CustomDefinitionId = CustomDefinitionId,
        CustomDefinitionName = CustomDefinitionName,
        CustomDefinitionSnapshot = CustomDefinitionSnapshot,
        UpdatedAt = DateTimeOffset.UtcNow
    };
}

internal static class SmartPlacementDraftChangeService
{
    private static long _revision;

    public static long Revision => Interlocked.Read(ref _revision);

    public static void NotifyChanged() => Interlocked.Increment(ref _revision);
}

internal sealed class SmartPlacementService : IDisposable
{
    private readonly RhinoDoc _doc;
    private readonly Func<SmartPlacementParameterSnapshot> _snapshotProvider;
    private readonly SmartPlacementHostIndex _hostIndex;
    private readonly double _tolerance;
    private readonly double _snapTolerance;
    private readonly double _pointSurfaceTolerance;

    public SmartPlacementService(RhinoDoc doc, EditorState state)
        : this(doc, () => SmartPlacementParameterSnapshot.Capture(state))
    {
    }

    public SmartPlacementService(RhinoDoc doc, FastenerTemplateData template)
        : this(doc, () => SmartPlacementParameterSnapshot.Capture(template))
    {
    }

    private SmartPlacementService(
        RhinoDoc doc,
        Func<SmartPlacementParameterSnapshot> snapshotProvider)
    {
        _doc = doc;
        _snapshotProvider = snapshotProvider;
        _tolerance = Math.Max(doc.ModelAbsoluteTolerance, 1e-6);
        _snapTolerance = Math.Max(0.2, _tolerance * 10);
        _pointSurfaceTolerance = Math.Max(_tolerance * 5, 1e-5);
        _hostIndex = new SmartPlacementHostIndex(doc);
    }

    public SmartPlacementParameterSignature CurrentParameterSignature() =>
        _snapshotProvider().Signature;

    public SmartPlacementPreview EvaluateAtPoint(
        Point3d point,
        RhinoViewport? viewport,
        SmartPlacementRecognitionMode recognitionMode = SmartPlacementRecognitionMode.Automatic)
    {
        _hostIndex.EnsureCurrent();
        SmartPlacementParameterSnapshot parameters;
        try
        {
            parameters = _snapshotProvider();
        }
        catch (Exception ex)
        {
            return SmartPlacementPreview.Invalid(
                point,
                $"当前紧固件参数无效：{ex.Message}",
                default,
                "点集");
        }

        return EvaluateAtPointCore(point, viewport, recognitionMode, parameters);
    }

    public SmartPlacementPreview EvaluateAtPointWithLength(
        Point3d point,
        RhinoViewport? viewport,
        SmartPlacementRecognitionMode recognitionMode,
        double length)
    {
        SmartPlacementParameterSnapshot parameters;
        try
        {
            parameters = _snapshotProvider();
        }
        catch (Exception ex)
        {
            return SmartPlacementPreview.Invalid(
                point,
                $"当前紧固件参数无效：{ex.Message}",
                default,
                "点集");
        }
        if (!double.IsFinite(length) || length <= 0 || length > 1000)
            return SmartPlacementPreview.Invalid(
                point,
                "采用长度必须是 0–1000 mm 范围内的大于 0 数值。",
                parameters.Signature,
                "点集");
        parameters = parameters with
        {
            Signature = parameters.Signature with { Length = length }
        };
        return EvaluateAtPointCore(point, viewport, recognitionMode, parameters);
    }

    public SmartPlacementPreview EvaluateSeed(
        AssemblyPlacementSeed seed,
        RhinoViewport? viewport,
        SmartPlacementRecognitionMode recognitionMode,
        double? length = null)
    {
        SmartPlacementParameterSnapshot parameters;
        try
        {
            parameters = _snapshotProvider();
        }
        catch (Exception ex)
        {
            return SmartPlacementPreview.Invalid(seed.Point, $"当前紧固件参数无效：{ex.Message}", default, seed.SourceLabel);
        }
        if (length.HasValue)
        {
            if (!double.IsFinite(length.Value) || length.Value <= 0 || length.Value > 1000)
                return SmartPlacementPreview.Invalid(seed.Point, "采用长度必须是 0–1000 mm 范围内的大于 0 数值。", parameters.Signature, seed.SourceLabel);
            parameters = parameters with { Signature = parameters.Signature with { Length = length.Value } };
        }
        if (seed.PlacementHostId == Guid.Empty)
            return EvaluateAtPointCore(seed.Point, viewport, recognitionMode, parameters);
        if (!TryResolveSeedPlane(seed, out var host, out var plane, out var error))
            return SmartPlacementPreview.Invalid(seed.Point, error, parameters.Signature, seed.SourceLabel);
        return FastenerKindTraits.UsesSingleHostPlacement(parameters.Signature.Kind)
            ? BuildInstallationPocketPreview(parameters, host, plane, seed.SourceLabel, exact: true)
            : BuildScrewPreview(parameters, host, plane, seed.SourceLabel, recognitionMode, exact: true);
    }

    public AssemblySuggestion CreateSuggestion(SmartPlacementMeasurement measurement)
    {
        var parameters = _snapshotProvider();
        var draft = parameters.CreateDraft(
            measurement.Plane,
            [],
            SmartPlacementRecognitionMode.Automatic);
        return AssemblySuggestionCalculator.Create(draft, parameters.Spec, measurement.Snapshot);
    }

    private SmartPlacementPreview EvaluateAtPointCore(
        Point3d point,
        RhinoViewport? viewport,
        SmartPlacementRecognitionMode recognitionMode,
        SmartPlacementParameterSnapshot parameters)
    {
        if (!TryFindFaceAtPoint(
                point,
                viewport,
                out var error,
                out var host,
                out var face,
                out var origin,
                out var u,
                out var v))
        {
            return SmartPlacementPreview.Invalid(
                point,
                error,
                parameters.Signature,
                "点集");
        }

        if (!TryCreatePlacementPlane(face, origin, u, v, out var plane))
        {
            return SmartPlacementPreview.Invalid(
                origin,
                "无法计算点位所在面的局部方向。",
                parameters.Signature,
                "点集");
        }

        return FastenerKindTraits.UsesSingleHostPlacement(parameters.Signature.Kind)
            ? BuildInstallationPocketPreview(parameters, host, plane, "点集", exact: true)
            : BuildScrewPreview(parameters, host, plane, "点集", recognitionMode, exact: true);
    }

    public bool TryMeasureAtPoint(
        Point3d point,
        RhinoViewport? viewport,
        out SmartPlacementMeasurement measurement,
        out string error)
    {
        measurement = null!;
        if (!TryFindFaceAtPoint(
                point,
                viewport,
                out error,
                out var host,
                out var face,
                out var origin,
                out var u,
                out var v))
            return false;
        if (!TryCreatePlacementPlane(face, origin, u, v, out var plane))
        {
            error = "无法计算点位所在面的局部方向。";
            return false;
        }
        var snapshot = AssemblyMeasurementService.Measure(
            _doc,
            FastenerGeometryFactory.FromPlane(plane),
            host.ObjectId,
            _hostIndex.All);
        measurement = new SmartPlacementMeasurement(plane, host.ObjectId, snapshot);
        error = snapshot.IsReliable ? string.Empty : snapshot.Message;
        return snapshot.Hosts.Count > 0;
    }

    public bool TryMeasureSeed(
        AssemblyPlacementSeed seed,
        RhinoViewport? viewport,
        out SmartPlacementMeasurement measurement,
        out string error)
    {
        if (seed.PlacementHostId == Guid.Empty)
            return TryMeasureAtPoint(seed.Point, viewport, out measurement, out error);
        measurement = null!;
        if (!TryResolveSeedPlane(seed, out var host, out var plane, out error))
            return false;
        var snapshot = AssemblyMeasurementService.Measure(
            _doc,
            FastenerGeometryFactory.FromPlane(plane),
            host.ObjectId,
            _hostIndex.All);
        measurement = new SmartPlacementMeasurement(plane, host.ObjectId, snapshot);
        error = snapshot.IsReliable ? string.Empty : snapshot.Message;
        return snapshot.Hosts.Count > 0;
    }

    public SmartPlacementCursorPreview EvaluateCursor(
        RhinoViewport viewport,
        System.Drawing.Point windowPoint,
        Point3d getterPoint,
        OsnapModes osnapMode,
        Guid snapObjectId,
        SmartPlacementRecognitionMode recognitionMode)
    {
        _hostIndex.EnsureCurrent();
        SmartPlacementParameterSnapshot parameters;
        try
        {
            parameters = _snapshotProvider();
        }
        catch (Exception ex)
        {
            return SmartPlacementCursorPreview.Invalid(
                getterPoint,
                $"当前紧固件参数无效：{ex.Message}",
                default);
        }

        var hasSnap = osnapMode != OsnapModes.None && getterPoint.IsValid;
        var snapLabel = hasSnap ? SnapLabel(osnapMode) : "自由面";
        if (!ViewportPickRayService.TryCreate(viewport, windowPoint, out var ray, out var rayError))
            return SmartPlacementCursorPreview.Invalid(getterPoint, rayError, parameters.Signature, snapLabel);

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
            return SmartPlacementCursorPreview.Invalid(
                getterPoint.IsValid ? getterPoint : ray.From,
                !string.IsNullOrWhiteSpace(faceError)
                    ? faceError
                    : hasSnap
                        ? "捕捉点无法关联到可用的封闭实体面。"
                        : "鼠标下方没有可用的封闭实体面。",
                parameters.Signature,
                snapLabel);
        }

        if (!TryCreatePlacementPlane(face, origin, u, v, out var plane))
            return SmartPlacementCursorPreview.Invalid(
                origin,
                "无法计算放置面的局部方向。",
                parameters.Signature,
                snapLabel);

        try
        {
            var draft = parameters.CreateDraft(plane, [], recognitionMode);
            var localProxies = FastenerLightweightProxyCache.GetLocalOrCreate(
                _doc,
                draft,
                parameters.Spec);
            var documentRevision = FastenerDocumentIndexService.CurrentRevision(_doc);
            return new SmartPlacementCursorPreview(
                true,
                origin,
                CursorSummary(draft),
                snapLabel,
                new SmartPlacementAnchorSignature(
                    parameters.Signature,
                    host.ObjectId,
                    face.FaceIndex,
                    origin,
                    plane.ZAxis,
                    documentRevision),
                localProxies,
                Transform.PlaneToPlane(Plane.WorldXY, plane));
        }
        catch (Exception ex)
        {
            return SmartPlacementCursorPreview.Invalid(
                origin,
                $"无法建立紧固件本体预览：{ex.Message}",
                parameters.Signature,
                snapLabel);
        }
    }

    public SmartPlacementPreview EvaluateLightweight(
        RhinoViewport viewport,
        System.Drawing.Point windowPoint,
        Point3d getterPoint,
        OsnapModes osnapMode,
        Guid snapObjectId,
        SmartPlacementRecognitionMode recognitionMode) => EvaluateCore(
            viewport,
            windowPoint,
            getterPoint,
            osnapMode,
            snapObjectId,
            recognitionMode,
            exact: false);

    public SmartPlacementPreview EvaluateExact(
        RhinoViewport viewport,
        System.Drawing.Point windowPoint,
        Point3d getterPoint,
        OsnapModes osnapMode,
        Guid snapObjectId,
        SmartPlacementRecognitionMode recognitionMode) => EvaluateCore(
            viewport,
            windowPoint,
            getterPoint,
            osnapMode,
            snapObjectId,
            recognitionMode,
            exact: true,
            confirmedEngagementHostId: Guid.Empty);

    public SmartPlacementPreview EvaluateExactWithConfirmedEngagement(
        RhinoViewport viewport,
        System.Drawing.Point windowPoint,
        Point3d getterPoint,
        OsnapModes osnapMode,
        Guid snapObjectId,
        SmartPlacementRecognitionMode recognitionMode,
        Guid confirmedEngagementHostId) => EvaluateCore(
            viewport,
            windowPoint,
            getterPoint,
            osnapMode,
            snapObjectId,
            recognitionMode,
            exact: true,
            confirmedEngagementHostId);

    private SmartPlacementPreview EvaluateCore(
        RhinoViewport viewport,
        System.Drawing.Point windowPoint,
        Point3d getterPoint,
        OsnapModes osnapMode,
        Guid snapObjectId,
        SmartPlacementRecognitionMode recognitionMode,
        bool exact,
        Guid confirmedEngagementHostId = default)
    {
        _hostIndex.EnsureCurrent();
        SmartPlacementParameterSnapshot parameters;
        try
        {
            parameters = _snapshotProvider();
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
        if (!ViewportPickRayService.TryCreate(viewport, windowPoint, out var ray, out var rayError))
        {
            return SmartPlacementPreview.Invalid(
                getterPoint,
                rayError,
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

        if (!TryCreatePlacementPlane(face, origin, u, v, out var plane))
            return SmartPlacementPreview.Invalid(
                origin,
                "无法计算放置面的局部方向。",
                parameters.Signature,
                snapLabel);

        var result = FastenerKindTraits.UsesSingleHostPlacement(parameters.Signature.Kind)
            ? BuildInstallationPocketPreview(parameters, host, plane, snapLabel, exact)
            : BuildScrewPreview(
                parameters,
                host,
                plane,
                snapLabel,
                recognitionMode,
                exact,
                confirmedEngagementHostId);
        return result with
        {
            AnchorSignature = new SmartPlacementAnchorSignature(
                parameters.Signature,
                host.ObjectId,
                face.FaceIndex,
                origin,
                plane.ZAxis,
                FastenerDocumentIndexService.CurrentRevision(_doc))
        };
    }

    private SmartPlacementPreview BuildScrewPreview(
        SmartPlacementParameterSnapshot parameters,
        SmartPlacementHost placementHost,
        Plane plane,
        string snapLabel,
        SmartPlacementRecognitionMode recognitionMode,
        bool exact,
        Guid confirmedEngagementHostId = default)
    {
        var reach = Math.Max(
            _tolerance,
            parameters.Signature.HeadEmbedDepth + parameters.Signature.Length);
        var intervals = FindAxisHosts(
            plane,
            reach,
            parameters.Signature.AssemblyMode is ScrewAssemblyMode.EngagementOnly
                or ScrewAssemblyMode.NutFastened
                || recognitionMode == SmartPlacementRecognitionMode.Automatic);
        if (confirmedEngagementHostId != Guid.Empty
            && intervals.All(item => item.ObjectId != confirmedEngagementHostId)
            && _hostIndex.Find(confirmedEngagementHostId) is { } confirmedHost
            && SmartHostIntervalService.TryGet(
                confirmedHost.Brep,
                FastenerGeometryFactory.FromPlane(plane),
                _tolerance,
                out var confirmedInterval,
                out _)
            && confirmedInterval.Max > _tolerance)
        {
            intervals = intervals
                .Append(new SmartHostInterval(
                    confirmedEngagementHostId,
                    Math.Max(0, confirmedInterval.Min),
                    confirmedInterval.Max))
                .ToArray();
        }
        if (parameters.Signature.AssemblyMode == ScrewAssemblyMode.NutFastened)
        {
            var provisional = parameters.CreateDraft(plane, [], recognitionMode) with
            {
                AssemblyMode = ScrewAssemblyMode.NutFastened,
                EngagementOnly = false
            };
            var nutResolution = NutFastenedHostResolver.Resolve(
                intervals,
                placementHost.ObjectId,
                provisional,
                parameters.Spec,
                _tolerance);
            if (!nutResolution.IsValid)
                return SmartPlacementPreview.Invalid(
                    plane.Origin,
                    nutResolution.Message,
                    parameters.Signature,
                    snapLabel);
            var nutBindings = nutResolution.ClearanceAssignments
                .Select(item => parameters.Preset.CreateClearanceBinding(
                    item.ObjectId,
                    item.ObjectId == placementHost.ObjectId))
                .Append(parameters.Preset.CreateNutPocketBinding(
                    nutResolution.NutPocketTargetId))
                .ToArray();
            var nutDraft = parameters.CreateDraft(plane, nutBindings, recognitionMode) with
            {
                AssemblyMode = ScrewAssemblyMode.NutFastened,
                EngagementOnly = false
            };
            return BuildGeometryPreview(
                parameters,
                nutDraft,
                $"{nutDraft.Size}×{nutDraft.Length:0.##} · 螺母固定 · 通{nutResolution.ClearanceAssignments.Count} / 槽1 · 露出{nutDraft.NutTipProtrusion:0.##}",
                snapLabel,
                exact);
        }
        if (parameters.Signature.AssemblyMode == ScrewAssemblyMode.EngagementOnly)
        {
            var strict = EngagementOnlyHostValidator.Validate(
                placementHost.ObjectId,
                intervals,
                parameters.Signature.HeadEmbedDepth,
                parameters.Signature.Length,
                _tolerance);
            if (!strict.IsValid || strict.Assignment is null)
            {
                return SmartPlacementPreview.Invalid(
                    plane.Origin,
                    strict.Message,
                    parameters.Signature,
                    snapLabel);
            }

            var binding = parameters.Preset.CreateEngagementBinding(
                placementHost.ObjectId,
                includeHeadSeat: true);
            var strictDraft = parameters.CreateDraft(
                plane,
                [binding],
                recognitionMode) with
            {
                EngagementOnly = true
            };
            return BuildGeometryPreview(
                parameters,
                strictDraft,
                $"{strictDraft.Size}×{strictDraft.Length:0.##} · 只咬合 · {DepthLabel(parameters.Preset.EngagementDepthMode, parameters.Preset.EngagementBlindDepth)}",
                snapLabel,
                exact);
        }

        var classification = confirmedEngagementHostId != Guid.Empty
            ? SmartHostBindingService.ClassifyConfirmed(
                intervals,
                placementHost.ObjectId,
                confirmedEngagementHostId,
                _tolerance,
                parameters.Signature.HeadEmbedDepth,
                parameters.Signature.Length)
            : SmartHostClassifier.Classify(
                intervals,
                recognitionMode,
                _tolerance,
                placementHost.ObjectId,
                parameters.Signature.HeadEmbedDepth,
                parameters.Signature.Length);
        if (!classification.IsValid)
        {
            var classificationMessage = classification.Message;
            if (exact
                && confirmedEngagementHostId == Guid.Empty
                && recognitionMode == SmartPlacementRecognitionMode.Automatic
                && classificationMessage.Contains("未检测到后方咬合宿主", StringComparison.Ordinal))
            {
                var diagnosticReach = Math.Max(
                    reach + 1,
                    _hostIndex.All.Select(host => host.BoundingBox.Diagonal.Length).DefaultIfEmpty(0).Sum());
                var diagnostic = SmartHostBindingService.FindIntervals(
                    _hostIndex.All,
                    FastenerGeometryFactory.FromPlane(plane),
                    diagnosticReach,
                    _tolerance,
                    preserveFullExit: true)
                    .OrderBy(item => item.Entry)
                    .ToArray();
                var rear = diagnostic.FirstOrDefault(item =>
                    item.ObjectId != placementHost.ObjectId
                    && item.Entry > _tolerance);
                if (rear is not null)
                {
                    var minimum = Math.Max(0, rear.Entry - parameters.Signature.HeadEmbedDepth);
                    classificationMessage =
                        $"检测到最近后方宿主，但当前长度不足；至少需要 {minimum:0.###} mm。"
                        + " 可修改长度后重试，或点击该实体确认咬合体。";
                }
            }
            return SmartPlacementPreview.Invalid(
                plane.Origin,
                classificationMessage,
                parameters.Signature,
                snapLabel);
        }

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
        var draft = parameters.CreateDraft(plane, bindings, recognitionMode) with
        {
            ConfirmedEngagementHostId = confirmedEngagementHostId
        };
        var clearanceCount = bindings.Count(item => item.Role == ShaftFitRole.Clearance);
        var engagementCount = bindings.Count(item => item.Role == ShaftFitRole.ThreadEngagement);
        var message =
            $"{draft.Size}×{draft.Length:0.##} · 通{clearanceCount} / 咬{engagementCount} · {DepthLabel(parameters.Preset.EngagementDepthMode, parameters.Preset.EngagementBlindDepth)}";
        return BuildGeometryPreview(parameters, draft, message, snapLabel, exact);
    }

    private SmartPlacementPreview BuildInstallationPocketPreview(
        SmartPlacementParameterSnapshot parameters,
        SmartPlacementHost placementHost,
        Plane plane,
        string snapLabel,
        bool exact)
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
            snapLabel,
            exact);
    }

    private SmartPlacementPreview BuildGeometryPreview(
        SmartPlacementParameterSnapshot parameters,
        FastenerComponentData draft,
        string message,
        string snapLabel,
        bool exact)
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
            if (!exact)
            {
                if (draft.CounterboreBridgeEnabled)
                    message += " · 架桥";
                if (draft.EngagementEntryChamferEnabled)
                    message += draft.EngagementEntryChamferMode == EngagementEntryChamferMode.SurfaceEqualDistance
                        ? " · 等距倒角"
                        : " · 45°倒角";
                if (draft.AssemblyMode == ScrewAssemblyMode.EngagementOnly
                    && draft.EngagementOnlyAlignmentDepth > 0)
                    message += $" · 对位{draft.EngagementOnlyAlignmentDepth:0.##}";
                var lightweightProxies = FastenerLightweightProxyCache.GetOrCreate(
                    _doc,
                    draft,
                    parameters.Spec);
                var lightweightRoles = BuildHostRoles(draft.Bindings);
                return new SmartPlacementPreview(
                    true,
                    FastenerGeometryFactory.ToPlane(draft.Placement).Origin,
                    message,
                    snapLabel,
                    parameters.Signature,
                    draft,
                    lightweightProxies,
                    Array.Empty<CutterGeometryBuild>(),
                    lightweightRoles,
                    CreateLightweightGlyphs(draft, parameters.Spec),
                    null,
                    FastenerDocumentIndexService.CurrentRevision(_doc));
            }

            if (!FastenerGeometryPreparationService.TryPrepare(
                    _doc,
                    draft,
                    out var prepared,
                    out var preparationError))
            {
                return SmartPlacementPreview.Invalid(
                    FastenerGeometryFactory.ToPlane(draft.Placement).Origin,
                    preparationError,
                    parameters.Signature,
                    snapLabel);
            }
            draft = prepared!.Draft;
            var proxies = prepared.Proxies;
            var cutters = prepared.Cutters;

            var roles = BuildHostRoles(draft.Bindings);
            return new SmartPlacementPreview(
                true,
                FastenerGeometryFactory.ToPlane(draft.Placement).Origin,
                message,
                snapLabel,
                parameters.Signature,
                draft,
                proxies,
                cutters,
                roles,
                Array.Empty<SmartPlacementCutterGlyph>(),
                prepared,
                FastenerDocumentIndexService.CurrentRevision(_doc));
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

    private IReadOnlyList<SmartPlacementCutterGlyph> CreateLightweightGlyphs(
        FastenerComponentData draft,
        FastenerSizeSpec spec)
    {
        var plane = FastenerGeometryFactory.ToPlane(draft.Placement);
        var reach = Math.Max(_tolerance, draft.HeadEmbedDepth + draft.Length);
        var glyphs = new List<SmartPlacementCutterGlyph>();
        foreach (var binding in draft.Bindings)
        {
            // The lightweight path deliberately avoids a second exact host
            // interval calculation. Role classification already performed the
            // only exact axis scan; the glyph is an analytic visual descriptor.
            var start = 0.0;
            var end = reach;
            var kind = SmartPlacementGlyphKind.CircularHole;
            var radius = spec.NominalDiameter / 2;
            var label = binding.Role switch
            {
                ShaftFitRole.Clearance => "通孔",
                ShaftFitRole.ThreadEngagement => "咬合孔",
                ShaftFitRole.NutPocket => "螺母槽",
                _ => "安装槽"
            };

            if (binding.Role is ShaftFitRole.Clearance or ShaftFitRole.ThreadEngagement)
            {
                radius = HoleDiameterCalculator.Calculate(draft, spec, binding).FinalDiameter / 2;
                if (binding.Role == ShaftFitRole.ThreadEngagement
                    && binding.DepthMode != DepthMode.ThroughTarget)
                {
                    var requested = binding.DepthMode switch
                    {
                        DepthMode.FastenerLengthPlusOneDiameter =>
                            draft.HeadEmbedDepth + draft.Length + spec.NominalDiameter,
                        DepthMode.FastenerLengthPlusCustom =>
                            draft.HeadEmbedDepth + draft.Length + binding.BlindDepth,
                        DepthMode.FastenerLengthPlusTwoDiameters =>
                            draft.HeadEmbedDepth + draft.Length + spec.NominalDiameter * 2,
                        DepthMode.Blind => binding.BlindDepth,
                        _ => end
                    };
                    end = Math.Max(start + _tolerance, requested);
                }
            }
            else if (binding.Role == ShaftFitRole.NutPocket)
            {
                kind = SmartPlacementGlyphKind.HexPocket;
                radius = PairedNutAssemblyCalculator.PocketAcrossFlats(draft, spec, binding)
                    / Math.Sqrt(3);
                var range = PairedNutAssemblyCalculator.AxialRange(draft, spec);
                start = range.InnerFace;
            }
            else if (draft.Kind == FastenerKind.HexNut)
            {
                kind = SmartPlacementGlyphKind.HexPocket;
                radius = InstallationPocketCalculator.HexNutAcrossFlats(draft, spec, binding)
                    / Math.Sqrt(3);
                start = 0;
                end = Math.Max(_tolerance, draft.HeadEmbedDepth);
            }
            else
            {
                radius = InstallationPocketCalculator.HeatSetMouthDiameter(draft) / 2;
                start = 0;
                end = Math.Max(_tolerance, draft.Length + draft.InsertDepthCompensation);
            }

            glyphs.Add(new SmartPlacementCutterGlyph(
                binding.Role,
                kind,
                plane,
                Math.Max(radius, _tolerance),
                start,
                end,
                label));

            if (binding.Role == ShaftFitRole.ThreadEngagement
                && draft.EngagementEntryChamferEnabled)
            {
                glyphs.Add(new SmartPlacementCutterGlyph(
                    binding.Role,
                    SmartPlacementGlyphKind.EntryChamfer,
                    plane,
                    radius + draft.EngagementEntryChamferSize,
                    start,
                    start + draft.EngagementEntryChamferSize,
                    draft.EngagementEntryChamferMode == EngagementEntryChamferMode.SurfaceEqualDistance
                        ? "等距倒角"
                        : "45°倒角"));
            }
            if (binding.Role == ShaftFitRole.ThreadEngagement
                && draft.AssemblyMode == ScrewAssemblyMode.EngagementOnly
                && draft.EngagementOnlyAlignmentDepth > 0
                && EngagementOnlyAlignmentCalculator.TryCreate(
                    draft,
                    spec,
                    binding,
                    out var alignment,
                    out _)
                && alignment is not null)
            {
                var guideStart = Math.Max(0, draft.HeadEmbedDepth);
                var guideEnd = guideStart + alignment.GuideDepth;
                glyphs.Add(new SmartPlacementCutterGlyph(
                    binding.Role,
                    SmartPlacementGlyphKind.CircularHole,
                    plane,
                    alignment.GuideDiameter / 2,
                    guideStart,
                    guideEnd,
                    "顶部对位"));
                glyphs.Add(new SmartPlacementCutterGlyph(
                    binding.Role,
                    SmartPlacementGlyphKind.EntryChamfer,
                    plane,
                    alignment.GuideDiameter / 2,
                    guideEnd,
                    guideEnd + alignment.TransitionLength,
                    "60°过渡",
                    alignment.EngagementDiameter / 2));
            }
        }

        if (draft.Bindings.Any(item => item.IncludeHeadSeat) && draft.HeadEmbedDepth > 0)
        {
            var radius = draft.Kind switch
            {
                FastenerKind.SocketCap => spec.Head.SocketDiameter / 2,
                FastenerKind.Countersunk => spec.Head.CountersunkDiameter / 2,
                FastenerKind.HexBolt => spec.Head.HexAcrossFlats / Math.Sqrt(3),
                _ => 0
            };
            if (radius > _tolerance)
            {
                glyphs.Add(new SmartPlacementCutterGlyph(
                    ShaftFitRole.Clearance,
                    SmartPlacementGlyphKind.HeadSeat,
                    plane,
                    radius,
                    0,
                    draft.HeadEmbedDepth,
                    draft.CounterboreBridgeEnabled ? "沉孔 · 架桥" : "头部承座"));
            }
        }
        return glyphs;
    }

    private static IReadOnlyDictionary<Guid, ShaftFitRole> BuildHostRoles(
        IEnumerable<HoleTargetBinding> bindings) =>
        bindings
            .GroupBy(item => item.TargetObjectId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.Role switch
                    {
                        ShaftFitRole.NutPocket => 4,
                        ShaftFitRole.InstallationPocket => 3,
                        ShaftFitRole.ThreadEngagement => 2,
                        _ => 1
                    })
                    .First()
                    .Role);

    private IReadOnlyList<SmartHostInterval> FindAxisHosts(
        Plane plane,
        double reach,
        bool preserveFullExit = false)
    {
        _hostIndex.EnsureCurrent();
        return SmartHostBindingService.FindIntervals(
            _hostIndex.Query(new Line(
                plane.Origin - plane.ZAxis * Math.Max(_tolerance * 10, 0.2),
                plane.Origin + plane.ZAxis * (reach + Math.Max(_tolerance * 10, 0.2))),
                Math.Max(_tolerance * 10, 0.2)),
            FastenerGeometryFactory.FromPlane(plane),
            reach,
            _tolerance,
            preserveFullExit);
    }

    private bool TryResolveSeedPlane(
        AssemblyPlacementSeed seed,
        out SmartPlacementHost host,
        out Plane plane,
        out string error)
    {
        host = _hostIndex.All.FirstOrDefault(item => item.ObjectId == seed.PlacementHostId)!;
        plane = Plane.Unset;
        if (host is null)
        {
            error = "所选圆形特征所属宿主已失效。";
            return false;
        }
        var faceIndex = seed.SourceComponent.ComponentIndexType == ComponentIndexType.BrepFace
            ? seed.SourceComponent.Index
            : -1;
        if (faceIndex < 0 || faceIndex >= host.Brep.Faces.Count)
        {
            error = "无法从所选圆形特征确定唯一入口平面。";
            return false;
        }
        var face = host.Brep.Faces[faceIndex];
        if (!face.TryGetPlane(out _)
            || !face.ClosestPoint(seed.Point, out var u, out var v)
            || !TryCreatePlacementPlane(face, seed.Point, u, v, out plane))
        {
            error = "所选圆形特征不属于可用的连续平面入口。";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private bool TryFindFaceAtPoint(
        Point3d point,
        RhinoViewport? viewport,
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
        var search = new BoundingBox(
            point - new Vector3d(_pointSurfaceTolerance, _pointSurfaceTolerance, _pointSurfaceTolerance),
            point + new Vector3d(_pointSurfaceTolerance, _pointSurfaceTolerance, _pointSurfaceTolerance));
        var candidates = new List<(SmartPlacementHost Host, BrepFace Face, Point3d Point, double U, double V, double Distance, double Facing)>();
        var camera = viewport?.CameraDirection ?? Vector3d.Unset;
        camera.Unitize();
        foreach (var candidate in _hostIndex.Query(search))
        {
            for (var faceIndex = 0; faceIndex < candidate.Brep.Faces.Count; faceIndex++)
            {
                var candidateFace = candidate.Brep.Faces[faceIndex];
                if (!candidateFace.ClosestPoint(point, out var candidateU, out var candidateV))
                    continue;
                var projected = candidateFace.PointAt(candidateU, candidateV);
                var distance = projected.DistanceTo(point);
                if (distance > _pointSurfaceTolerance)
                    continue;
                var normal = candidateFace.NormalAt(candidateU, candidateV);
                if (candidateFace.OrientationIsReversed)
                    normal.Reverse();
                var facing = camera.IsValid ? -Vector3d.Multiply(normal, camera) : 0;
                candidates.Add((candidate, candidateFace, projected, candidateU, candidateV, distance, facing));
            }
        }

        if (candidates.Count == 0)
        {
            error = $"点位未落在有效封闭实体表面容差内（允许 {_pointSurfaceTolerance:0.###}）。";
            return false;
        }

        var bestByHost = candidates
            .GroupBy(item => item.Host.ObjectId)
            .Select(group => group
                .OrderBy(item => item.Distance)
                .ThenByDescending(item => item.Facing)
                .First())
            .OrderBy(item => item.Distance)
            .ThenByDescending(item => item.Facing)
            .ToArray();
        if (bestByHost.Length > 1
            && Math.Abs(bestByHost[0].Distance - bestByHost[1].Distance) <= _tolerance)
        {
            error = "点位同时落在多个宿主表面，无法唯一确定放置宿主。";
            return false;
        }

        var best = bestByHost[0];
        host = best.Host;
        face = best.Face;
        origin = best.Point;
        u = best.U;
        v = best.V;
        return true;
    }

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
        var candidates = _hostIndex.Query(ray, _tolerance)
            .Where(host => Intersection.LineBox(ray, host.BoundingBox, _tolerance, out _))
            .Select(host => new
            {
                Host = host,
                Entry = host.BoundingBox.GetCorners()
                    .Select(point => Vector3d.Multiply(point - ray.From, direction))
                    .Min()
            })
            .OrderBy(item => item.Entry)
            .ToArray();
        var nearestExactDepth = double.PositiveInfinity;
        foreach (var candidate in candidates)
        {
            if (candidate.Entry > nearestExactDepth + _tolerance)
                break;
            var host = candidate.Host;
            if (!Intersection.CurveBrep(
                    lineCurve,
                    host.Brep,
                    _tolerance,
                    out _,
                    out var hostPoints))
                continue;
            foreach (var point in hostPoints)
            {
                var depth = Vector3d.Multiply(point - ray.From, direction);
                if (depth < -_tolerance || depth > rayLength + _tolerance)
                    continue;
                if (TryResolveHitFace(host.Brep, point, direction, out var bestFace, out var bestPoint, out var bestU, out var bestV))
                {
                    hits.Add((host, bestFace, bestPoint, bestU, bestV, Math.Max(0, depth)));
                    nearestExactDepth = Math.Min(nearestExactDepth, Math.Max(0, depth));
                }
            }
        }

        if (hits.Count == 0)
        {
            error = _hostIndex.Count == 0
                ? "当前文档没有捕获到可见、未锁定的封闭 Brep/Extrusion 宿主。"
                : candidates.Length == 0
                    ? "鼠标下方未命中可放置的封闭实体。"
                    : $"鼠标射线与 {candidates.Length} 个候选包围盒重叠，但未能与实体精确相交。";
            return null;
        }

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

    private bool TryResolveHitFace(
        Brep brep,
        Point3d hit,
        Vector3d rayDirection,
        out BrepFace face,
        out Point3d point,
        out double u,
        out double v)
    {
        face = null!;
        point = Point3d.Unset;
        u = 0;
        v = 0;
        if (!brep.ClosestPoint(
                hit,
                out var closest,
                out var component,
                out var s,
                out var t,
                _snapTolerance,
                out _))
            return false;

        if (component.ComponentIndexType == ComponentIndexType.BrepFace
            && component.Index >= 0
            && component.Index < brep.Faces.Count)
        {
            face = brep.Faces[component.Index];
            point = closest;
            u = s;
            v = t;
            return true;
        }

        if (component.ComponentIndexType != ComponentIndexType.BrepEdge
            || component.Index < 0
            || component.Index >= brep.Edges.Count)
            return false;

        var adjacent = brep.Edges[component.Index].AdjacentFaces();
        var bestFacing = double.NegativeInfinity;
        foreach (var faceIndex in adjacent)
        {
            var candidate = brep.Faces[faceIndex];
            if (!candidate.ClosestPoint(hit, out var candidateU, out var candidateV))
                continue;
            var candidatePoint = candidate.PointAt(candidateU, candidateV);
            if (candidatePoint.DistanceTo(hit) > _snapTolerance)
                continue;
            var normal = candidate.NormalAt(candidateU, candidateV);
            if (candidate.OrientationIsReversed)
                normal.Reverse();
            var facing = -Vector3d.Multiply(normal, rayDirection);
            if (facing <= bestFacing)
                continue;
            bestFacing = facing;
            face = candidate;
            point = candidatePoint;
            u = candidateU;
            v = candidateV;
        }
        return face is not null;
    }

    private static bool TryCreatePlacementPlane(
        BrepFace face,
        Point3d origin,
        double u,
        double v,
        out Plane plane)
    {
        if (!face.FrameAt(u, v, out var frame))
            frame = new Plane(origin, face.NormalAt(u, v));

        var inward = face.NormalAt(u, v);
        if (face.OrientationIsReversed)
            inward.Reverse();
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

    public SmartPlacementHost? FindHost(Guid objectId)
    {
        _hostIndex.EnsureCurrent();
        return _hostIndex.Find(objectId);
    }

    public void Dispose() => _hostIndex.Dispose();

    private static bool BoxesIntersect(BoundingBox left, BoundingBox right) =>
        left.IsValid
        && right.IsValid
        && left.Min.X <= right.Max.X && left.Max.X >= right.Min.X
        && left.Min.Y <= right.Max.Y && left.Max.Y >= right.Min.Y
        && left.Min.Z <= right.Max.Z && left.Max.Z >= right.Min.Z;

    private static string CursorSummary(FastenerComponentData draft)
    {
        var kind = draft.Kind == FastenerKind.HexNut
            ? draft.HexNutStyle == HexNutStyle.NylonInsertLocking ? "防松螺母" : "六角螺母"
            : FastenerLabels.ShortKind(draft.Kind);
        if (draft.Kind == FastenerKind.HexNut)
            return $"{kind} {draft.Size}";
        if (draft.Kind == FastenerKind.HeatSetInsert)
            return $"{kind} {draft.Size}×{draft.Length:0.##}";
        var assembly = draft.AssemblyMode switch
        {
            ScrewAssemblyMode.EngagementOnly => "只咬合",
            ScrewAssemblyMode.NutFastened => "螺母固定",
            _ => "螺纹咬合"
        };
        return $"{kind} {draft.Size}×{draft.Length:0.##} · {assembly}";
    }

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
