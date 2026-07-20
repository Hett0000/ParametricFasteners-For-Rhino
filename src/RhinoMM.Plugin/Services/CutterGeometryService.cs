using Rhino;
using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Geometry;

namespace RhinoMM.Plugin.Services;

internal sealed record CutterGeometryBuild(
    HoleTargetBinding Binding,
    Brep Shaft,
    Brep? Head,
    IReadOnlyList<string> Warnings);

internal static class CutterGeometryService
{
    public static bool TryBuild(
        RhinoDoc doc,
        FastenerComponentData component,
        FastenerSizeSpec spec,
        HoleTargetBinding binding,
        out CutterGeometryBuild? result,
        out string message)
    {
        result = null;
        message = string.Empty;
        try
        {
            var target = doc.Objects.FindId(binding.TargetObjectId)
                ?? throw new InvalidOperationException(
                    $"被切割宿主已丢失：{binding.TargetObjectId}。请运行“刷新 / 清理”重新绑定。");
            if (!FastenerGeometryFactory.TryGetTargetInterval(
                    target.Geometry,
                    component.Placement,
                    doc.ModelAbsoluteTolerance,
                    out var interval,
                    out var usedFallback))
                throw new InvalidOperationException($"无法计算宿主“{TargetName(target)}”的轴向切割范围。");

            if (binding.IncludeHeadSeat && component.HeadEmbedDepth > 0)
            {
                if (interval.Min > doc.ModelAbsoluteTolerance)
                    throw new InvalidOperationException($"头部宿主“{TargetName(target)}”不包含放置点。");
                if (component.HeadEmbedDepth > interval.Max + doc.ModelAbsoluteTolerance)
                    throw new InvalidOperationException(
                        $"螺丝头嵌入深度 {component.HeadEmbedDepth:0.###} mm 超过宿主“{TargetName(target)}”的有效厚度 {interval.Max:0.###} mm。");
            }

            var warnings = new List<string>();
            if (usedFallback)
                warnings.Add($"{TargetName(target)} 的切割范围使用了包围盒估算。");
            var padding = Math.Max(0.2, doc.ModelAbsoluteTolerance * 10);

            if (FastenerKindTraits.IsNut(component.Kind))
            {
                if (binding.Role != ShaftFitRole.InstallationPocket)
                    throw new InvalidOperationException("螺母组件缺少有效的安装槽/孔绑定。");
                if (interval.Min > doc.ModelAbsoluteTolerance)
                    throw new InvalidOperationException($"宿主“{TargetName(target)}”不包含螺母放置点。");
                var requiredDepth = InstallationPocketCalculator.RequiredDepth(component, spec);
                if (interval.Max < requiredDepth - doc.ModelAbsoluteTolerance)
                    throw new InvalidOperationException(
                        $"宿主“{TargetName(target)}”有效厚度 {interval.Max:0.###} mm 小于所需盲槽/孔深度 {requiredDepth:0.###} mm。");

                if (component.Kind == FastenerKind.HexNut)
                {
                    result = new CutterGeometryBuild(
                        binding,
                        FastenerGeometryFactory.CreateHexNutPocketCutter(component, spec, binding, padding),
                        null,
                        warnings);
                    return true;
                }

                var heatSet = FastenerGeometryFactory.CreateHeatSetPocketCutters(
                    component,
                    spec,
                    padding);
                result = new CutterGeometryBuild(
                    binding,
                    heatSet.Shaft,
                    heatSet.LeadIn,
                    warnings);
                return true;
            }

            var start = interval.Min - padding;
            var end = interval.Max + padding;
            var limit = FastenerGeometryFactory.DepthLimit(component, spec, binding);
            if (!double.IsPositiveInfinity(limit))
            {
                if (limit < interval.Min - doc.ModelAbsoluteTolerance)
                    throw new InvalidOperationException(
                        $"螺杆深度 {limit:0.###} mm 无法到达咬合宿主“{TargetName(target)}”。");
                start = Math.Max(start, -padding);
                var reachesExit = limit >= interval.Max - doc.ModelAbsoluteTolerance;
                end = reachesExit ? interval.Max + padding : limit;
                if (reachesExit)
                    warnings.Add($"{TargetName(target)}：计算深度超过宿主厚度，将贯穿。");
            }

            result = new CutterGeometryBuild(
                binding,
                FastenerGeometryFactory.CreateShaftCutter(component, spec, binding, start, end),
                binding.IncludeHeadSeat
                    ? FastenerGeometryFactory.CreateHeadSeatCutter(component, spec)
                    : null,
                warnings);
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    private static string TargetName(Rhino.DocObjects.RhinoObject target) =>
        string.IsNullOrWhiteSpace(target.Attributes.Name)
            ? $"实体 {target.Id.ToString("N")[..8]}"
            : target.Attributes.Name;
}
