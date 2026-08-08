using Rhino;
using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Geometry;

namespace RhinoMM.Plugin.Services;

internal sealed record PreparedFastenerGeometry(
    FastenerComponentData Draft,
    IReadOnlyList<Brep> Proxies,
    IReadOnlyList<CutterGeometryBuild> Cutters,
    IReadOnlyList<string> Warnings);

internal static class FastenerGeometryPreparationService
{
    public static bool TryPrepare(
        RhinoDoc doc,
        FastenerComponentData draft,
        out PreparedFastenerGeometry? prepared,
        out string message)
    {
        prepared = null;
        message = string.Empty;
        try
        {
            if (!SmartHostBindingService.TryReconcile(
                    doc,
                    draft,
                    out var effectiveDraft,
                    out var bindingChanges,
                    out var bindingError))
            {
                message = $"宿主重识别失败：{bindingError}";
                return false;
            }

            var spec = RhinoMMPlugIn.Catalog.Get(effectiveDraft.Size);
            var validation = FastenerComponentValidator.Validate(effectiveDraft, spec);
            if (!validation.IsValid)
            {
                message = string.Join(
                    Environment.NewLine,
                    validation.Issues.Where(item => item.IsError).Select(item => item.Message));
                return false;
            }

            var proxies = FastenerGeometryFactory.CreateProxy(effectiveDraft, spec);
            var cutters = new List<CutterGeometryBuild>();
            var warnings = new List<string>();
            if (bindingChanges.HasChanges)
                warnings.Add(bindingChanges.ToString());
            foreach (var binding in effectiveDraft.Bindings)
            {
                if (!CutterGeometryService.TryBuild(
                        doc,
                        effectiveDraft,
                        spec,
                        binding,
                        out var cutter,
                        out var cutterError))
                    throw new InvalidOperationException(cutterError);
                cutters.Add(cutter!);
                warnings.AddRange(cutter!.Warnings);
            }
            prepared = new PreparedFastenerGeometry(effectiveDraft, proxies, cutters, warnings);
            return true;
        }
        catch (Exception ex)
        {
            message = $"几何预检失败：{ex.Message}";
            return false;
        }
    }
}
