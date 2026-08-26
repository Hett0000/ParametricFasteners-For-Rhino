using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Geometry;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.UI;

namespace RhinoMM.Plugin.Services;

internal static class AssemblyInspectionSettingsService
{
    private const string Prefix = "AssemblyInspection.";
    private static AssemblyInspectionProfile _current = new();

    public static AssemblyInspectionProfile Current => _current;

    public static void Load(PersistentSettings settings) => _current = new AssemblyInspectionProfile(
        settings.GetDouble(Prefix + "MinimumWallThickness", 0.8),
        settings.GetDouble(Prefix + "MinimumHoleLigament", 0.8),
        settings.GetDouble(Prefix + "ToolRadialClearance", 2.0),
        settings.GetDouble(Prefix + "ToolAxialClearance", 10.0));

    public static bool Save(AssemblyInspectionProfile profile, out string message)
    {
        if (!profile.IsValid)
        {
            message = "检查阈值必须是有限的非负数。";
            return false;
        }
        _current = profile;
        var settings = RhinoMMPlugIn.Instance?.Settings;
        if (settings is null)
        {
            message = "检查阈值已用于当前会话。";
            return true;
        }
        settings.SetDouble(Prefix + "MinimumWallThickness", profile.MinimumWallThickness);
        settings.SetDouble(Prefix + "MinimumHoleLigament", profile.MinimumHoleLigament);
        settings.SetDouble(Prefix + "ToolRadialClearance", profile.ToolRadialClearance);
        settings.SetDouble(Prefix + "ToolAxialClearance", profile.ToolAxialClearance);
        message = "检查阈值已保存。";
        return true;
    }
}

internal static class AssemblyInspectionService
{
    public static AssemblyInspectionReport Inspect(
        RhinoDoc doc,
        IReadOnlyList<FastenerComponentData>? components = null,
        AssemblyInspectionProfile? profile = null,
        FastenerUiOperationContext? operation = null)
    {
        profile ??= AssemblyInspectionSettingsService.Current;
        components ??= ComponentRepository.ReadAllControlPoints(doc, out _);
        var issues = new List<AssemblyInspectionIssue>();
        var valid = new List<(FastenerComponentData Component, FastenerSizeSpec Spec)>();
        var distinct = components.DistinctBy(item => item.ComponentId).ToArray();
        var existingHostIds = distinct
            .SelectMany(item => item.Bindings)
            .Select(item => item.TargetObjectId)
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        var sharedHosts = SmartHostBindingService.CaptureHosts(doc, existingHostIds);
        try
        {
            for (var componentIndex = 0; componentIndex < distinct.Length; componentIndex++)
            {
                var component = distinct[componentIndex];
                if (operation is not null && !operation.Yield(
                        "装配检查", componentIndex, distinct.Length,
                        component.ComponentId.ToString("N")[..8]))
                    break;
                var spec = FastenerSpecResolver.Resolve(component, RhinoMMPlugIn.Catalog);
                var location = Location(component);
                var validation = FastenerComponentValidator.Validate(component, spec);
                foreach (var issue in validation.Issues.Where(item => item.IsError))
                    issues.Add(Issue(component, Guid.Empty, AssemblyInspectionSeverity.Error,
                        "PARAMETER_INVALID", issue.Message, "读取组件并修正参数。", location));
                var missing = component.Bindings.Where(binding => binding.TargetObjectId == Guid.Empty
                    || doc.Objects.FindId(binding.TargetObjectId) is null).ToArray();
                if (missing.Length > 0)
                {
                    issues.Add(Issue(component, Guid.Empty, AssemblyInspectionSeverity.Error,
                        "HOST_MISSING", $"{missing.Length} 个宿主绑定已丢失。", "打开维护中心并重新绑定。", location));
                    continue;
                }
                if (!validation.IsValid)
                    continue;
                var boolean = ComponentBooleanHealthService.Check(doc, component);
                if (!boolean.Success)
                    issues.Add(Issue(component, Guid.Empty, AssemblyInspectionSeverity.Error,
                        "BOOLEAN_FAILURE", boolean.Message, "检查宿主与孔参数后重新应用。", location));
                CheckAssembly(doc, component, spec, profile, issues, sharedHosts);
                valid.Add((component, spec));
            }
            if (operation?.IsCancellationRequested != true)
                CheckHoleSpacing(doc, valid, profile, issues, operation);
        }
        finally
        {
            foreach (var host in sharedHosts)
                if (host.Object.Geometry is not Brep)
                    host.Brep.Dispose();
        }
        return new AssemblyInspectionReport(issues, components.Count, DateTimeOffset.UtcNow);
    }

    private static void CheckAssembly(
        RhinoDoc doc,
        FastenerComponentData component,
        FastenerSizeSpec spec,
        AssemblyInspectionProfile profile,
        ICollection<AssemblyInspectionIssue> issues,
        IReadOnlyList<SmartPlacementHost> sharedHosts)
    {
        var location = Location(component);
        var intervals = component.Bindings
            .Select(binding => (Binding: binding, Host: doc.Objects.FindId(binding.TargetObjectId)))
            .Where(item => item.Host is not null)
            .Select(item =>
            {
                var ok = FastenerGeometryFactory.TryGetTargetInterval(item.Host!.Geometry, component.Placement,
                    doc.ModelAbsoluteTolerance, out var interval, out var fallback) && !fallback;
                return (item.Binding, Host: item.Host!, Ok: ok, Interval: interval);
            }).ToArray();
        if (component.Bindings.Any(binding => !binding.IsBooleanEnabled))
            issues.Add(Issue(component, Guid.Empty, AssemblyInspectionSeverity.Warning,
                "BOOLEAN_DISABLED", "一个或多个切割模块已关闭导出布尔。", "确认这是否符合交付意图。", location));
        foreach (var item in intervals.Where(item => !item.Ok))
            issues.Add(Issue(component, item.Host.Id, AssemblyInspectionSeverity.Error,
                "HOST_INTERVAL_UNKNOWN", "无法可靠计算宿主沿螺丝轴线的范围。", "检查宿主是否为有效封闭实体。", location));

        if (FastenerKindTraits.IsScrew(component.Kind))
        {
            var placementHostId = component.Bindings
                .FirstOrDefault(item => item.IncludeHeadSeat)?.TargetObjectId
                ?? component.Bindings.FirstOrDefault()?.TargetObjectId
                ?? Guid.Empty;
            var measurement = AssemblyMeasurementService.Measure(
                doc,
                component.Placement,
                placementHostId,
                sharedHosts);
            var assemblySuggestion = AssemblySuggestionCalculator.Create(component, spec, measurement);
            if (!assemblySuggestion.IsBlocked && assemblySuggestion.ChangesAssemblyMode)
            {
                issues.Add(Issue(component, Guid.Empty, AssemblyInspectionSeverity.Warning,
                    "ASSEMBLY_MODE_SUGGESTION",
                    $"当前装配方式不可行；可考虑“{FastenerLabels.AssemblyMode(assemblySuggestion.RecommendedMode)}”。",
                    $"测量依据：{assemblySuggestion.MeasurementBasis} 请在更新前明确确认装配方式。",
                    location,
                    suggestedMode: assemblySuggestion.RecommendedMode));
            }
            else if (!assemblySuggestion.IsBlocked
                && Math.Abs(assemblySuggestion.SuggestedLength - component.Length) > 1e-6)
            {
                var suggestion = new FastenerLengthSuggestion(
                    component.Length,
                    assemblySuggestion.SuggestedLength,
                    assemblySuggestion.SuggestedLength - component.Length,
                    assemblySuggestion.MeasurementBasis);
                var severity = component.AssemblyMode == ScrewAssemblyMode.EngagementOnly
                    && suggestion.SuggestedLength < component.Length
                        ? AssemblyInspectionSeverity.Error
                        : AssemblyInspectionSeverity.Info;
                issues.Add(Issue(component, Guid.Empty, severity, "LENGTH_SUGGESTION",
                    $"当前长度 {component.Length:0.##} mm，建议 {suggestion.SuggestedLength:0.##} mm。",
                    "采用建议长度，或保留当前自定义长度。", location, suggestion));
            }
        }

        foreach (var item in intervals.Where(item => item.Ok))
        {
            var radius = FinalRadius(component, spec, item.Binding);
            if (TryMinimumRadialWall(doc, component, item.Host, item.Interval, radius, out var edge)
                && edge < profile.MinimumWallThickness)
                issues.Add(Issue(component, item.Host.Id, AssemblyInspectionSeverity.Warning,
                    "THIN_WALL", $"孔到宿主边界的采样余量约 {Math.Max(0, edge):0.##} mm。",
                    $"建议至少保留 {profile.MinimumWallThickness:0.##} mm 壁厚，并在剖面中复核。", location));
        }

        if (FastenerKindTraits.SupportsHeadEmbed(component.Kind))
        {
            var headRadius = component.Kind switch
            {
                FastenerKind.SocketCap => spec.Head.SocketDiameter / 2,
                FastenerKind.Countersunk => spec.Head.CountersunkDiameter / 2,
                _ => spec.Head.HexAcrossFlats / Math.Sqrt(3)
            };
            var toolRadius = headRadius + profile.ToolRadialClearance;
            var toolEnd = component.PlacementPoint() - component.PlacementAxis() * profile.ToolAxialClearance;
            var toolBox = new BoundingBox(
                new Point3d(Math.Min(location.X, toolEnd.X), Math.Min(location.Y, toolEnd.Y), Math.Min(location.Z, toolEnd.Z))
                    - new Vector3d(toolRadius, toolRadius, toolRadius),
                new Point3d(Math.Max(location.X, toolEnd.X), Math.Max(location.Y, toolEnd.Y), Math.Max(location.Z, toolEnd.Z))
                    + new Vector3d(toolRadius, toolRadius, toolRadius));
            var bound = component.Bindings.Select(binding => binding.TargetObjectId).ToHashSet();
            if (doc.Objects.Any(obj => ComponentHostResolver.IsOrdinaryHost(obj)
                && !bound.Contains(obj.Id)
                && BoxesIntersect(obj.Geometry.GetBoundingBox(true), toolBox)))
                issues.Add(Issue(component, Guid.Empty, AssemblyInspectionSeverity.Warning,
                    "TOOL_CLEARANCE", "螺丝头上方可能存在工具操作空间遮挡。",
                    "检查扳手/批头的径向与轴向操作空间。", location));
        }

        CheckUnexpectedCollisions(doc, component, spec, issues, location);
    }

    private static void CheckUnexpectedCollisions(
        RhinoDoc doc,
        FastenerComponentData component,
        FastenerSizeSpec spec,
        ICollection<AssemblyInspectionIssue> issues,
        Point3d location)
    {
        var proxies = FastenerGeometryFactory.CreateProxy(component, spec);
        var proxyBox = BoundingBox.Empty;
        foreach (var proxy in proxies) proxyBox.Union(proxy.GetBoundingBox(true));
        var expected = component.Bindings.Select(binding => binding.TargetObjectId).ToHashSet();
        foreach (var host in doc.Objects.Where(ComponentHostResolver.IsOrdinaryHost))
        {
            if (expected.Contains(host.Id) || !BoxesIntersect(proxyBox, host.Geometry.GetBoundingBox(true)))
                continue;
            var hostBreps = BooleanExportService.ToBreps(host.Geometry);
            var collision = proxies.Any(proxy => hostBreps.Any(body =>
            {
                var intersection = Brep.CreateBooleanIntersection([proxy], [body], doc.ModelAbsoluteTolerance, false);
                return intersection is { Length: > 0 } && intersection.Any(item => item.GetVolume() > Math.Pow(doc.ModelAbsoluteTolerance, 3));
            }));
            if (!collision) continue;
            issues.Add(Issue(component, host.Id, AssemblyInspectionSeverity.Error,
                "UNEXPECTED_COLLISION", "紧固件实体与未绑定宿主发生真实几何碰撞。",
                "调整位置、长度，或显式重新绑定正确宿主。", location));
            break;
        }
    }

    private static void CheckHoleSpacing(
        RhinoDoc doc,
        IReadOnlyList<(FastenerComponentData Component, FastenerSizeSpec Spec)> values,
        AssemblyInspectionProfile profile,
        ICollection<AssemblyInspectionIssue> issues,
        FastenerUiOperationContext? operation)
    {
        var items = values.SelectMany(value => value.Component.Bindings.Select(binding => new
        {
            value.Component,
            value.Spec,
            Binding = binding,
            Location = Location(value.Component),
            Radius = FinalRadius(value.Component, value.Spec, binding)
        })).Where(item => item.Binding.TargetObjectId != Guid.Empty).ToArray();
        var groups = items.GroupBy(item => item.Binding.TargetObjectId).ToArray();
        for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            if (operation is not null && !operation.Yield(
                    "检查孔间距", groupIndex, groups.Length,
                    groups[groupIndex].Key.ToString("N")[..8]))
                return;
            var group = groups[groupIndex];
            var array = group.ToArray();
            for (var i = 0; i < array.Length; i++)
            for (var j = i + 1; j < array.Length; j++)
            {
                if (array[i].Component.ComponentId == array[j].Component.ComponentId)
                    continue;
                var ligament = array[i].Location.DistanceTo(array[j].Location) - array[i].Radius - array[j].Radius;
                if (ligament >= profile.MinimumHoleLigament)
                    continue;
                issues.Add(Issue(array[i].Component, group.Key, AssemblyInspectionSeverity.Warning,
                    "HOLE_SPACING", $"与相邻孔的保守实体间隔约 {Math.Max(0, ligament):0.##} mm。",
                    $"建议至少保留 {profile.MinimumHoleLigament:0.##} mm 孔间实体。", array[i].Location));
            }
        }
    }

    private static double FinalRadius(FastenerComponentData component, FastenerSizeSpec spec, HoleTargetBinding binding)
    {
        try { return HoleDiameterCalculator.Calculate(component, spec, binding).FinalDiameter / 2; }
        catch { return spec.NominalDiameter / 2; }
    }

    private static bool TryMinimumRadialWall(
        RhinoDoc doc,
        FastenerComponentData component,
        RhinoObject host,
        Interval interval,
        double holeRadius,
        out double wall)
    {
        wall = double.PositiveInfinity;
        var axis = component.PlacementAxis();
        var xAxis = new Vector3d(component.Placement.XAxisX, component.Placement.XAxisY, component.Placement.XAxisZ);
        if (!axis.Unitize() || !xAxis.Unitize())
            return false;
        var yAxis = Vector3d.CrossProduct(axis, xAxis);
        if (!yAxis.Unitize())
            return false;
        xAxis = Vector3d.CrossProduct(yAxis, axis);
        xAxis.Unitize();
        var center = component.PlacementPoint() + axis * ((interval.Min + interval.Max) * 0.5);
        var extent = Math.Max(host.Geometry.GetBoundingBox(true).Diagonal.Length * 1.5, holeRadius + 1.0);
        var breps = BooleanExportService.ToBreps(host.Geometry);
        if (breps.Count == 0)
            return false;
        var successfulRays = 0;
        const int samples = 24;
        for (var index = 0; index < samples; index++)
        {
            var angle = Math.PI * 2 * index / samples;
            var direction = xAxis * Math.Cos(angle) + yAxis * Math.Sin(angle);
            var curve = new LineCurve(center, center + direction * extent);
            var nearest = double.PositiveInfinity;
            foreach (var brep in breps)
            {
                if (!Rhino.Geometry.Intersect.Intersection.CurveBrep(
                        curve, brep, doc.ModelAbsoluteTolerance, out _, out var points))
                    continue;
                foreach (var point in points)
                {
                    var distance = center.DistanceTo(point);
                    if (distance > doc.ModelAbsoluteTolerance && distance < nearest)
                        nearest = distance;
                }
            }
            if (!double.IsFinite(nearest))
                continue;
            successfulRays++;
            wall = Math.Min(wall, nearest - holeRadius);
        }
        return successfulRays >= samples * 3 / 4 && double.IsFinite(wall);
    }

    private static AssemblyInspectionIssue Issue(
        FastenerComponentData component,
        Guid hostId,
        AssemblyInspectionSeverity severity,
        string code,
        string message,
        string action,
        Point3d location,
        FastenerLengthSuggestion? suggestion = null,
        ScrewAssemblyMode? suggestedMode = null) => new(
        component.ComponentId, hostId, severity, code, message, action,
        location.X, location.Y, location.Z, suggestion, suggestedMode);

    private static Point3d Location(FastenerComponentData component) => new(
        component.Placement.OriginX, component.Placement.OriginY, component.Placement.OriginZ);

    private static Point3d PlacementPoint(this FastenerComponentData component) => Location(component);
    private static Vector3d PlacementAxis(this FastenerComponentData component) => new(
        component.Placement.ZAxisX, component.Placement.ZAxisY, component.Placement.ZAxisZ);

    private static bool BoxesIntersect(BoundingBox a, BoundingBox b) =>
        a.Min.X <= b.Max.X && a.Max.X >= b.Min.X
        && a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y
        && a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z;
}
