using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Geometry;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Services;

internal enum ComponentOperationKind
{
    Create,
    Update,
    Clone,
    Transform,
    Relink,
    Repair,
    Delete,
    Export
}

internal enum ComponentOperationStage
{
    CaptureSnapshot,
    ResolveSpecification,
    ResolveHosts,
    BuildGeometry,
    Validate,
    Commit,
    Rollback
}

internal enum ComponentOperationSeverity
{
    Information,
    Warning,
    Error
}

internal sealed record ComponentOperationIssue(
    Guid ComponentId,
    Guid HostId,
    Guid BindingId,
    ComponentOperationStage Stage,
    ComponentOperationSeverity Severity,
    string Reason,
    string Recommendation)
{
    public string ShortComponentId => ComponentId == Guid.Empty
        ? "--------"
        : ComponentId.ToString("N")[..8];

    public override string ToString()
    {
        var host = HostId == Guid.Empty ? string.Empty : $" · 宿主 {HostId.ToString("N")[..8]}";
        return $"组件 {ShortComponentId}{host} · {Stage}：{Reason} 建议：{Recommendation}";
    }
}

internal sealed record ComponentHostSnapshot(
    Guid BindingId,
    Guid TargetObjectId,
    ShaftFitRole Role,
    bool IsAvailable,
    string GeometryFingerprint);

internal sealed record FastenerComponentSnapshot(
    uint DocumentSerial,
    long DocumentRevision,
    FastenerComponentData StoredData,
    FastenerComponentData EffectiveData,
    Guid ControlPointObjectId,
    Point3d ActualControlPoint,
    bool PlacementMismatch,
    FastenerSizeSpec ResolvedSpecification,
    IReadOnlyList<ComponentHostSnapshot> Hosts,
    string ParameterSignature);

internal sealed record ComponentOperationPlan(
    ComponentOperationKind Kind,
    IReadOnlyList<FastenerComponentSnapshot> Before,
    IReadOnlyList<PreparedFastenerGeometry> Prepared,
    IReadOnlyList<ComponentOperationIssue> Issues)
{
    public bool IsValid => Issues.All(issue => issue.Severity != ComponentOperationSeverity.Error);
}

internal static class FastenerComponentSnapshotService
{
    public static bool TryCapture(
        RhinoDoc doc,
        Guid componentId,
        out FastenerComponentSnapshot? snapshot,
        out ComponentOperationIssue? issue)
    {
        snapshot = null;
        issue = null;
        var controls = ComponentRepository.FindControlPoints(doc, componentId).ToArray();
        if (controls.Length != 1)
        {
            issue = new ComponentOperationIssue(
                componentId,
                Guid.Empty,
                Guid.Empty,
                ComponentOperationStage.CaptureSnapshot,
                ComponentOperationSeverity.Error,
                controls.Length == 0 ? "缺少实际控制点。" : "存在重复控制点。",
                "打开维护中心定位损坏组件；不要从代理体参数猜测恢复。");
            return false;
        }
        var control = controls[0];
        if (!ComponentRepository.TryReadControlPoint(control, out var stored))
        {
            issue = new ComponentOperationIssue(
                componentId,
                Guid.Empty,
                Guid.Empty,
                ComponentOperationStage.CaptureSnapshot,
                ComponentOperationSeverity.Error,
                "控制点组件 JSON 损坏或无法迁移。",
                "从备份恢复组件，或删除控制点后重新放置。");
            return false;
        }
        if (stored.ComponentId != componentId)
        {
            issue = new ComponentOperationIssue(
                componentId,
                Guid.Empty,
                Guid.Empty,
                ComponentOperationStage.CaptureSnapshot,
                ComponentOperationSeverity.Error,
                "控制点对象 ID 与组件 JSON 中的组件 ID 不一致。",
                "运行文档健康检查并定位冲突组件。");
            return false;
        }

        var point = ((Point)control.Geometry).Location;
        var savedPlane = FastenerGeometryFactory.ToPlane(stored.Placement);
        var mismatch = point.DistanceTo(savedPlane.Origin) > Math.Max(doc.ModelAbsoluteTolerance, 1e-6);
        var effective = mismatch
            ? stored with
            {
                Placement = FastenerGeometryFactory.FromPlane(
                    new Plane(point, savedPlane.XAxis, savedPlane.YAxis)),
                ControlPointObjectId = control.Id
            }
            : stored with { ControlPointObjectId = control.Id };

        FastenerSizeSpec spec;
        try
        {
            spec = FastenerSpecResolver.Resolve(effective, RhinoMMPlugIn.Catalog);
        }
        catch (Exception ex)
        {
            issue = new ComponentOperationIssue(
                componentId,
                Guid.Empty,
                Guid.Empty,
                ComponentOperationStage.ResolveSpecification,
                ComponentOperationSeverity.Error,
                ex.Message,
                "检查规格或自定义标准件尺寸快照。");
            return false;
        }

        var hosts = effective.Bindings.Select(binding =>
        {
            var target = binding.TargetObjectId == Guid.Empty
                ? null
                : doc.Objects.FindId(binding.TargetObjectId);
            return new ComponentHostSnapshot(
                binding.BindingId,
                binding.TargetObjectId,
                binding.Role,
                target is not null && ComponentHostResolver.IsOrdinaryHost(target),
                target is null ? string.Empty : ComponentReliabilitySignatureService.GeometryFingerprint(target.Geometry));
        }).ToArray();
        var signature = ComponentReliabilitySignatureService.ParameterSignature(doc, effective, spec, hosts);
        snapshot = new FastenerComponentSnapshot(
            doc.RuntimeSerialNumber,
            FastenerDocumentIndexService.CurrentRevision(doc),
            stored,
            effective,
            control.Id,
            point,
            mismatch,
            spec,
            hosts,
            signature);
        return true;
    }
}

internal static class ComponentReliabilitySignatureService
{
    public static string ParameterSignature(
        RhinoDoc doc,
        FastenerComponentData data,
        FastenerSizeSpec spec,
        IReadOnlyList<ComponentHostSnapshot>? hosts = null)
    {
        var text = new StringBuilder(1024);
        Append(text, data.Kind);
        Append(text, data.HexNutStyle);
        Append(text, data.Size);
        Append(text, data.Length);
        Append(text, data.HeadEmbedDepth);
        Append(text, data.CounterboreBridgeEnabled);
        Append(text, data.CounterboreBridgeLayerHeight);
        Append(text, data.EngagementEntryChamferEnabled);
        Append(text, data.EngagementEntryChamferSize);
        Append(text, data.EngagementEntryChamferMode);
        Append(text, data.EngagementOnlyAlignmentDepth);
        Append(text, data.EngagementOnlyAlignmentDiameterCompensation);
        Append(text, data.InsertOuterDiameter);
        Append(text, data.InsertDiameterCompensation);
        Append(text, data.InsertDepthCompensation);
        Append(text, data.PrintProfile.HoleDiameterCorrection);
        Append(text, data.HoleDiameterFormula);
        Append(text, data.AssemblyMode);
        Append(text, data.PairedNutStyle);
        Append(text, data.NutTipProtrusion);
        Append(text, data.NutPocketCompensation);
        Append(text, data.AutoRecognizeHosts);
        Append(text, data.ConfirmedEngagementHostId);
        Append(text, data.SmartRecognitionMode);
        Append(text, data.EngagementOnly);
        if (data.SmartBindingProfile is { } profile)
        {
            Append(text, profile.ClearanceFit);
            Append(text, profile.BiteReduction);
            Append(text, profile.EngagementDepthMode);
            Append(text, profile.EngagementBlindDepth);
            Append(text, profile.ClearancePreviewVisible);
            Append(text, profile.ClearanceBooleanEnabled);
            Append(text, profile.EngagementPreviewVisible);
            Append(text, profile.EngagementBooleanEnabled);
            Append(text, profile.NutPocketPreviewVisible);
            Append(text, profile.NutPocketBooleanEnabled);
        }
        AppendPlacement(text, data.Placement);
        AppendSpecification(text, spec);
        Append(text, doc.ModelAbsoluteTolerance);
        Append(text, doc.ModelAngleToleranceRadians);
        Append(text, doc.ModelUnitSystem);
        var hostMap = hosts?.ToDictionary(host => host.BindingId) ?? [];
        foreach (var binding in data.Bindings.OrderBy(binding => binding.BindingId))
        {
            Append(text, binding.BindingId);
            Append(text, binding.TargetObjectId);
            Append(text, binding.Role);
            Append(text, binding.ClearanceFit);
            Append(text, binding.BiteReduction);
            Append(text, binding.BindingOverride);
            Append(text, binding.DepthMode);
            Append(text, binding.BlindDepth);
            Append(text, binding.IncludeHeadSeat);
            Append(text, binding.IsPreviewVisible);
            Append(text, binding.IsBooleanEnabled);
            if (hostMap.TryGetValue(binding.BindingId, out var host))
                Append(text, host.GeometryFingerprint);
            else if (doc.Objects.FindId(binding.TargetObjectId) is { } target)
                Append(text, GeometryFingerprint(target.Geometry));
        }
        return Hash(text.ToString());
    }

    public static string GeometryFingerprint(GeometryBase geometry)
    {
        var box = geometry.GetBoundingBox(true);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{geometry.ObjectType}:{geometry.DataCRC(0):X8}:{box.Min.X:R},{box.Min.Y:R},{box.Min.Z:R}:{box.Max.X:R},{box.Max.Y:R},{box.Max.Z:R}");
    }

    public static string GeometrySignature(GeometryBase geometry) => Hash(GeometryFingerprint(geometry));

    public static string PreparationSignature(
        string parameterSignature,
        IReadOnlyList<Brep> proxies,
        IReadOnlyList<CutterGeometryBuild> cutters)
    {
        var text = new StringBuilder(parameterSignature);
        foreach (var geometry in proxies)
            Append(text, GeometrySignature(geometry));
        foreach (var cutter in cutters.OrderBy(item => item.Binding.BindingId))
        {
            Append(text, cutter.Binding.BindingId);
            foreach (var geometry in cutter.Shafts)
                Append(text, GeometrySignature(geometry));
            foreach (var geometry in cutter.Heads)
                Append(text, GeometrySignature(geometry));
        }
        return Hash(text.ToString());
    }

    private static void AppendPlacement(StringBuilder text, PlacementFrame placement)
    {
        Append(text, placement.OriginX); Append(text, placement.OriginY); Append(text, placement.OriginZ);
        Append(text, placement.XAxisX); Append(text, placement.XAxisY); Append(text, placement.XAxisZ);
        Append(text, placement.YAxisX); Append(text, placement.YAxisY); Append(text, placement.YAxisZ);
        Append(text, placement.ZAxisX); Append(text, placement.ZAxisY); Append(text, placement.ZAxisZ);
    }

    private static void AppendSpecification(StringBuilder text, FastenerSizeSpec spec)
    {
        Append(text, spec.Designation);
        Append(text, spec.NominalDiameter);
        Append(text, spec.CoarsePitch);
        Append(text, spec.Head.SocketDiameter); Append(text, spec.Head.SocketHeight);
        Append(text, spec.Head.CountersunkDiameter); Append(text, spec.Head.CountersunkAngle);
        Append(text, spec.Head.HexAcrossFlats); Append(text, spec.Head.HexHeight);
        Append(text, spec.Head.NutAcrossFlats); Append(text, spec.Head.NutThickness);
    }

    private static void Append(StringBuilder text, object? value)
    {
        if (value is IFormattable formattable)
            text.Append(formattable.ToString(null, CultureInfo.InvariantCulture));
        else
            text.Append(value?.ToString() ?? "<null>");
        text.Append('|');
    }

    private static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
