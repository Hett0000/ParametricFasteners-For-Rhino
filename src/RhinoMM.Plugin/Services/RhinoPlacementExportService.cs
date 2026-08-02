using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Geometry;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Services;

public sealed record RhinoPlacementExportOptions(bool IncludeFastenerSolids);

public enum RhinoExportMaterialKind
{
    Steel,
    Brass
}

public sealed record RhinoExportFastenerBody(
    Guid ComponentId,
    FastenerKind Kind,
    string Name,
    RhinoExportMaterialKind MaterialKind,
    Brep Geometry);

public sealed record RhinoPlacementExportResult(
    BooleanExportResult BooleanResult,
    IReadOnlyList<RhinoExportFastenerBody> FastenerBodies,
    IReadOnlyList<string> Warnings)
{
    // Keep placement stable when the option is toggled. Protruding fasteners
    // must not change the user's host-based placement anchor.
    public Point3d BottomCenter => BooleanResult.BottomCenter;
}

public static class RhinoPlacementExportService
{
    public static bool TryBuild(
        RhinoDoc doc,
        IReadOnlyList<RhinoObject> hosts,
        RhinoPlacementExportOptions options,
        out RhinoPlacementExportResult result,
        out string message)
    {
        result = new RhinoPlacementExportResult(
            new BooleanExportResult([], []),
            [],
            []);
        if (!BooleanExportService.TryBuild(doc, hosts, out var booleanResult, out message))
            return false;

        var warnings = booleanResult.Warnings.ToList();
        if (!options.IncludeFastenerSolids)
        {
            result = new RhinoPlacementExportResult(booleanResult, [], warnings);
            return true;
        }

        try
        {
            var hostIds = hosts.Select(host => host.Id).ToHashSet();
            var relatedComponents = ComponentRepository.ReadAllControlPoints(doc, out _)
                .Where(component => component.Bindings.Any(binding =>
                    binding.TargetObjectId != Guid.Empty
                    && hostIds.Contains(binding.TargetObjectId)))
                .GroupBy(component => component.ComponentId)
                .Select(group => group.First())
                .ToArray();

            if (relatedComponents.Length == 0)
            {
                warnings.Add("所选宿主没有关联的有效紧固件，已仅生成布尔宿主。");
                result = new RhinoPlacementExportResult(booleanResult, [], warnings.Distinct().ToArray());
                return true;
            }

            var fastenerBodies = new List<RhinoExportFastenerBody>();
            foreach (var component in relatedComponents)
            {
                if (ComponentHostResolver.NeedsRelink(component))
                    throw new InvalidOperationException(
                        $"组件 {component.Size} · {component.ComponentId.ToString("N")[..8]} 尚未绑定宿主；请运行“刷新 / 清理”后重试。");

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

                var proxies = FastenerGeometryFactory.CreateProxy(component, spec);
                if (proxies.Count == 0
                    || proxies.Any(proxy => !proxy.IsValid || !proxy.IsSolid))
                    throw new InvalidOperationException(
                        $"组件 {component.Size} · {component.ComponentId.ToString("N")[..8]} 无法生成有效的紧固件实体。");

                var materialKind = component.Kind == FastenerKind.HeatSetInsert
                    ? RhinoExportMaterialKind.Brass
                    : RhinoExportMaterialKind.Steel;
                var name = $"{component.Size} · {FastenerLabels.Kind(component)} · 渲染实体";
                fastenerBodies.AddRange(proxies.Select(proxy => new RhinoExportFastenerBody(
                    component.ComponentId,
                    component.Kind,
                    name,
                    materialKind,
                    proxy)));
            }

            result = new RhinoPlacementExportResult(
                booleanResult,
                fastenerBodies,
                warnings.Distinct().ToArray());
            message = $"已生成 {booleanResult.Bodies.Count} 个布尔成果和 {fastenerBodies.Count} 个紧固件实体。";
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }
}
