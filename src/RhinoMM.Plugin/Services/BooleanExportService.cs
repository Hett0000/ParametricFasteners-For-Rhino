using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Geometry;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.UI;

namespace RhinoMM.Plugin.Services;

public sealed record BooleanExportBody(
    Guid SourceObjectId,
    ObjectAttributes SourceAttributes,
    Brep Geometry);

public sealed record BooleanExportResult(
    IReadOnlyList<BooleanExportBody> Bodies,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<FastenerComponentData>? RelatedComponents = null)
{
    public BoundingBox BoundingBox
    {
        get
        {
            var box = BoundingBox.Empty;
            foreach (var body in Bodies)
                box.Union(body.Geometry.GetBoundingBox(true));
            return box;
        }
    }

    public Point3d BottomCenter
    {
        get
        {
            var box = BoundingBox;
            return new Point3d(
                (box.Min.X + box.Max.X) * 0.5,
                (box.Min.Y + box.Max.Y) * 0.5,
                box.Min.Z);
        }
    }
}

public static class BooleanExportService
{
    internal static bool TryBuild(
        RhinoDoc doc,
        IReadOnlyList<RhinoObject> hosts,
        out BooleanExportResult result,
        out string message,
        FastenerUiOperationContext? operation = null)
    {
        result = new BooleanExportResult([], []);
        if (hosts.Count == 0)
        {
            message = "没有选择需要布尔导出的宿主。";
            return false;
        }

        try
        {
            var selectedHostIds = hosts.Select(host => host.Id).ToHashSet();
            var storedComponents = ComponentRepository.ReadAllControlPoints(doc, out var ignoredComponentCount);
            var components = storedComponents
                .Where(component => component.Bindings.Any(binding =>
                    selectedHostIds.Contains(binding.TargetObjectId)))
                .DistinctBy(component => component.ComponentId)
                .ToArray();
            var componentsById = components.ToDictionary(component => component.ComponentId);
            ValidateSelectedHostLinks(doc, hosts, componentsById);

            var warnings = new List<string>();
            if (ignoredComponentCount > 0)
                warnings.Add($"已忽略文档中 {ignoredComponentCount} 个与当前范围无关的残留组件。");
            var unrelated = storedComponents.Count - components.Length;
            if (unrelated > 0)
                warnings.Add($"范围隔离导出已忽略 {unrelated} 个未关联所选宿主的组件。");

            var cuttersByBinding = new Dictionary<(Guid ComponentId, Guid BindingId), CutterGeometryBuild>();
            foreach (var component in components)
            {
                var spec = FastenerSpecResolver.Resolve(component, RhinoMMPlugIn.Catalog);
                var validation = FastenerComponentValidator.Validate(component, spec);
                var coreErrors = validation.Issues
                    .Where(issue => issue.IsError && !BindingScopedValidationCodes.Contains(issue.Code))
                    .Select(issue => issue.Message)
                    .ToArray();
                if (coreErrors.Length > 0)
                    throw new InvalidOperationException(
                        $"组件 {component.Size} · {component.ComponentId.ToString("N")[..8]} 核心参数无效："
                        + string.Join(" ", coreErrors));

                foreach (var binding in component.Bindings.Where(binding =>
                             binding.IsBooleanEnabled
                             && selectedHostIds.Contains(binding.TargetObjectId)))
                {
                    if (!CutterGeometryService.TryBuild(
                            doc,
                            component,
                            spec,
                            binding,
                            out var cutter,
                            out var cutterError))
                    {
                        throw new InvalidOperationException(
                            $"组件 {component.Size} · {component.ComponentId.ToString("N")[..8]} "
                            + $"在所选宿主上的切割体生成失败：{cutterError}");
                    }
                    cuttersByBinding[(component.ComponentId, binding.BindingId)] = cutter!;
                    warnings.AddRange(cutter!.Warnings);
                }

                var invalidUnselected = component.Bindings.Count(binding =>
                    !selectedHostIds.Contains(binding.TargetObjectId)
                    && (binding.TargetObjectId == Guid.Empty
                        || doc.Objects.FindId(binding.TargetObjectId) is null));
                if (invalidUnselected > 0)
                    warnings.Add($"组件 {component.ComponentId.ToString("N")[..8]} 有 {invalidUnselected} 个未选宿主绑定失效，已按范围忽略。");
            }

            var output = new List<BooleanExportBody>();
            for (var hostIndex = 0; hostIndex < hosts.Count; hostIndex++)
            {
                if (operation is not null && !operation.Yield(
                        "布尔宿主", hostIndex, hosts.Count,
                        string.IsNullOrWhiteSpace(hosts[hostIndex].Attributes.Name)
                            ? hosts[hostIndex].Id.ToString("N")[..8]
                            : hosts[hostIndex].Attributes.Name))
                {
                    message = "操作已取消；未写入输出成果。";
                    result = new BooleanExportResult([], warnings, components);
                    return false;
                }
                var host = hosts[hostIndex];
                var current = ToBreps(host.Geometry);
                if (current.Count == 0 || current.Any(body => !body.IsSolid || !body.IsValid))
                    throw new InvalidOperationException($"所选宿主不是可布尔的封闭实体：{HostName(host)}");

                var cutters = new List<Brep>();
                var enabledBindingCount = 0;
                foreach (var component in components)
                foreach (var binding in component.Bindings.Where(binding =>
                             binding.TargetObjectId == host.Id && binding.IsBooleanEnabled))
                {
                    var build = cuttersByBinding[(component.ComponentId, binding.BindingId)];
                    enabledBindingCount++;
                    cutters.AddRange(build.Shafts);
                    cutters.AddRange(build.Heads);
                }
                if (enabledBindingCount == 0)
                    warnings.Add($"{HostName(host)}：没有启用的补偿切割模块，已保留原几何。");

                if (cutters.Count > 0)
                {
                    foreach (var body in current)
                        FastenerGeometryFactory.EnsureOutward(body);
                    foreach (var cutter in cutters)
                        FastenerGeometryFactory.EnsureOutward(cutter);
                    var unitedCutters = Brep.CreateBooleanUnion(cutters, doc.ModelAbsoluteTolerance, false);
                    var booleanCutters = unitedCutters is { Length: > 0 } ? unitedCutters : cutters.ToArray();
                    var difference = Brep.CreateBooleanDifference(
                        current,
                        booleanCutters,
                        doc.ModelAbsoluteTolerance,
                        false);
                    if (difference is null || difference.Length == 0 || difference.Any(body => !body.IsValid))
                        throw new InvalidOperationException($"布尔失败。宿主：{HostName(host)}");
                    current = difference.ToList();
                }
                output.AddRange(current.Select(body => new BooleanExportBody(
                    host.Id,
                    host.Attributes.Duplicate(),
                    body)));
            }

            if (output.Count == 0)
                throw new InvalidOperationException("布尔运算没有生成可用实体。");
            var distinctWarnings = warnings.Distinct().ToArray();
            result = new BooleanExportResult(output, distinctWarnings, components);
            message = $"已处理 {hosts.Count} 个宿主、{components.Length} 个关联组件，生成 {output.Count} 个布尔成果。"
                + (distinctWarnings.Length == 0 ? string.Empty : $" 警告：{string.Join(" ", distinctWarnings)}");
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    public static List<Brep> ToBreps(GeometryBase geometry) => geometry switch
    {
        Brep brep => [brep.DuplicateBrep()],
        Extrusion extrusion => [extrusion.ToBrep()],
        _ => []
    };

    private static void ValidateSelectedHostLinks(
        RhinoDoc doc,
        IReadOnlyList<RhinoObject> hosts,
        IReadOnlyDictionary<Guid, FastenerComponentData> components)
    {
        var hostIds = hosts.Select(host => host.Id).ToHashSet();
        foreach (var obj in doc.Objects.Where(obj =>
                     obj.Attributes.GetUserString(ComponentRepository.RoleKey) is "Cutter" or "HeadCutter"))
        {
            if (!Guid.TryParse(
                    obj.Attributes.GetUserString(ComponentRepository.TargetIdKey),
                    out var targetId)
                || !hostIds.Contains(targetId))
                continue;
            if (!Guid.TryParse(obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey), out var componentId)
                || !components.TryGetValue(componentId, out var component)
                || !Guid.TryParse(obj.Attributes.GetUserString(ComponentRepository.BindingIdKey), out var bindingId)
                || !component.Bindings.Any(binding =>
                    binding.BindingId == bindingId
                    && binding.TargetObjectId == targetId))
                throw new InvalidOperationException("所选宿主存在缺少有效控制点的切割模块；请先运行“刷新 / 清理”。");
        }
    }

    private static string HostName(RhinoObject host) =>
        string.IsNullOrWhiteSpace(host.Attributes.Name)
            ? host.Id.ToString("N")[..8]
            : host.Attributes.Name;

    // These rules depend on the complete binding collection. A host-scoped export
    // validates the selected bindings by building their cutters instead, so a bad
    // binding on an unselected host cannot block an otherwise independent result.
    private static readonly HashSet<string> BindingScopedValidationCodes =
    [
        "engagement-only-binding", "nut-fastened-clearance", "nut-pocket-count",
        "nut-fastened-engagement", "head-seat-required", "nut-binding-count",
        "target", "blind-depth", "engagement-custom-depth", "nut-binding-role",
        "nut-depth-mode", "nut-embed-depth", "screw-installation-pocket",
        "nut-pocket-mode", "nut-pocket-depth", "nut-pocket-size", "clearance-depth",
        "bite-required", "diameter-positive", "clearance-range", "engagement-range",
        "head-seat", "confirmed-engagement-binding"
    ];
}
