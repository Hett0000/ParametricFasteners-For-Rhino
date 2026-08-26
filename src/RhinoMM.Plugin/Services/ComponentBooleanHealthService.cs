using Rhino;
using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;

namespace RhinoMM.Plugin.Services;

internal sealed record ComponentBooleanHealthResult(
    Guid ComponentId,
    bool Success,
    string Message);

internal static class ComponentBooleanHealthService
{
    public static ComponentBooleanHealthResult Check(
        RhinoDoc doc,
        FastenerComponentData component)
    {
        try
        {
            if (!SmartHostBindingService.TryReconcile(
                    doc,
                    component,
                    out var effective,
                    out _,
                    out var reconcileError))
                return Failed(component, $"宿主复核失败：{reconcileError}");

            var spec = FastenerSpecResolver.Resolve(effective, RhinoMMPlugIn.Catalog);
            var validation = FastenerComponentValidator.Validate(effective, spec);
            if (!validation.IsValid)
                return Failed(component, string.Join(" ", validation.Issues
                    .Where(issue => issue.IsError)
                    .Select(issue => issue.Message)));

            foreach (var group in effective.Bindings
                         .Where(binding => binding.IsBooleanEnabled)
                         .GroupBy(binding => binding.TargetObjectId))
            {
                var host = doc.Objects.FindId(group.Key);
                if (host is null)
                    return Failed(component, $"宿主 {group.Key.ToString("N")[..8]} 已丢失。");
                var bodies = BooleanExportService.ToBreps(host.Geometry);
                if (bodies.Count == 0)
                    return Failed(component, "宿主无法转换为可布尔的 Brep。");
                var cutters = new List<Brep>();
                foreach (var binding in group)
                {
                    if (!CutterGeometryService.TryBuild(
                            doc,
                            effective,
                            spec,
                            binding,
                            out var build,
                            out var buildError))
                        return Failed(component, $"切割体生成失败：{buildError}");
                    cutters.AddRange(build!.Shafts);
                    cutters.AddRange(build.Heads);
                }
                if (cutters.Count == 0)
                    continue;
                foreach (var body in bodies)
                    Geometry.FastenerGeometryFactory.EnsureOutward(body);
                foreach (var cutter in cutters)
                    Geometry.FastenerGeometryFactory.EnsureOutward(cutter);
                var united = Brep.CreateBooleanUnion(cutters, doc.ModelAbsoluteTolerance, false);
                var difference = Brep.CreateBooleanDifference(
                    bodies,
                    united is { Length: > 0 } ? united : cutters,
                    doc.ModelAbsoluteTolerance,
                    false);
                if (difference is null || difference.Length == 0 || difference.Any(item => !item.IsValid))
                    return Failed(component, $"宿主 {HostName(host)} 实际布尔失败。");
            }
            return new ComponentBooleanHealthResult(component.ComponentId, true, "布尔检查通过");
        }
        catch (Exception ex)
        {
            return Failed(component, ex.Message);
        }
    }

    private static ComponentBooleanHealthResult Failed(
        FastenerComponentData component,
        string message) => new(component.ComponentId, false, message);

    private static string HostName(Rhino.DocObjects.RhinoObject host) =>
        string.IsNullOrWhiteSpace(host.Attributes.Name)
            ? host.Id.ToString("N")[..8]
            : host.Attributes.Name;
}
