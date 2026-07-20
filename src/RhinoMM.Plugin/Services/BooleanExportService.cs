using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Geometry;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Services;

public sealed record BooleanExportBody(
    Guid SourceObjectId,
    ObjectAttributes SourceAttributes,
    Brep Geometry);

public sealed record BooleanExportResult(
    IReadOnlyList<BooleanExportBody> Bodies,
    IReadOnlyList<string> Warnings)
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
    public static bool TryBuild(
        RhinoDoc doc,
        IReadOnlyList<RhinoObject> hosts,
        out BooleanExportResult result,
        out string message)
    {
        result = new BooleanExportResult([], []);
        if (hosts.Count == 0)
        {
            message = "没有选择需要布尔导出的宿主。";
            return false;
        }

        try
        {
            var components = ComponentRepository.ReadAllControlPoints(doc, out var ignoredComponentCount);
            var unresolvedComponents = components
                .Where(ComponentHostResolver.NeedsRelink)
                .ToArray();
            if (unresolvedComponents.Length > 0)
                throw new InvalidOperationException(
                    $"文档中有 {unresolvedComponents.Length} 个复制组件尚未绑定宿主；请选择对应宿主并运行“刷新 / 清理”后再导出。"
                    + " 控制点："
                    + string.Join(", ", unresolvedComponents.Select(component => component.ComponentId.ToString("N")[..8])));
            var componentsById = components.ToDictionary(component => component.ComponentId);
            ValidateSelectedHostLinks(doc, hosts, componentsById);
            var output = new List<BooleanExportBody>();
            var warnings = new List<string>();
            if (ignoredComponentCount > 0)
                warnings.Add($"文档中有 {ignoredComponentCount} 个无有效控制点的残留组件未参与导出；建议运行“刷新 / 清理”。");
            foreach (var host in hosts)
            {
                var current = ToBreps(host.Geometry);
                if (current.Count == 0)
                    throw new InvalidOperationException($"无法转换实体：{HostName(host)}");

                var cutters = new List<Brep>();
                var enabledBindingCount = 0;
                foreach (var component in components)
                {
                    var bindings = component.Bindings
                        .Where(binding => binding.TargetObjectId == host.Id && binding.IsBooleanEnabled)
                        .ToArray();
                    if (bindings.Length == 0)
                        continue;
                    var spec = RhinoMMPlugIn.Catalog.Get(component.Size);
                    var validation = FastenerComponentValidator.Validate(component, spec);
                    if (!validation.IsValid)
                    {
                        var errors = string.Join(" ", validation.Issues
                            .Where(issue => issue.IsError)
                            .Select(issue => issue.Message));
                        throw new InvalidOperationException(
                            $"组件 {component.Size} · {component.ComponentId.ToString("N")[..8]} 参数无效：{errors}");
                    }
                    foreach (var binding in bindings)
                    {
                        if (!CutterGeometryService.TryBuild(
                                doc, component, spec, binding, out var build, out var buildError))
                        {
                            throw new InvalidOperationException(
                                $"组件 {component.Size} · {component.ComponentId.ToString("N")[..8]} 切割体生成失败：{buildError}");
                        }
                        enabledBindingCount++;
                        cutters.Add(build!.Shaft);
                        if (build.Head is not null)
                            cutters.Add(build.Head);
                        warnings.AddRange(build.Warnings);
                    }
                }
                if (enabledBindingCount == 0)
                    warnings.Add($"{HostName(host)}：没有启用的补偿切割模块，已保留原几何。");

                if (cutters.Count > 0)
                {
                    foreach (var body in current)
                        FastenerGeometryFactory.EnsureOutward(body);
                    foreach (var cutter in cutters)
                        FastenerGeometryFactory.EnsureOutward(cutter);

                    // Shaft and head-seat cutters overlap by design. Union them first and
                    // subtract the complete set in one operation so a countersink cannot
                    // leave a coincident seam that breaks a later sequential boolean.
                    var unitedCutters = Brep.CreateBooleanUnion(
                        cutters,
                        doc.ModelAbsoluteTolerance,
                        false);
                    var booleanCutters = unitedCutters is { Length: > 0 }
                        ? unitedCutters
                        : cutters.ToArray();
                    var difference = Brep.CreateBooleanDifference(
                        current,
                        booleanCutters,
                        doc.ModelAbsoluteTolerance,
                        false);
                    if (difference is null
                        || difference.Length == 0
                        || difference.Any(body => !body.IsValid))
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
            result = new BooleanExportResult(output, distinctWarnings);
            message = $"已生成 {output.Count} 个布尔成果。"
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
                     obj.Attributes.GetUserString(ComponentRepository.RoleKey) is "Cutter" or "HeadCutter"
                     && Guid.TryParse(obj.Attributes.GetUserString(ComponentRepository.TargetIdKey), out var targetId)
                     && hostIds.Contains(targetId)))
        {
            if (!Guid.TryParse(obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey), out var componentId)
                || !components.ContainsKey(componentId))
                throw new InvalidOperationException("所选宿主存在缺少有效控制点的切割模块；请先运行“刷新 / 清理”。");
        }
    }

    private static string HostName(RhinoObject host) =>
        string.IsNullOrWhiteSpace(host.Attributes.Name)
            ? host.Id.ToString("N")[..8]
            : host.Attributes.Name;
}
