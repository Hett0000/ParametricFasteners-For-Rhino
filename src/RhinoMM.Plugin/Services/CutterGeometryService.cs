using Rhino;
using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Geometry;

namespace RhinoMM.Plugin.Services;

internal sealed record CutterGeometryBuild(
    HoleTargetBinding Binding,
    IReadOnlyList<Brep> Shafts,
    IReadOnlyList<Brep> Heads,
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

            if (binding.IncludeHeadSeat
                && component.HeadEmbedDepth > 0
                && component.Kind == FastenerKind.SocketCap
                && component.CounterboreBridgeEnabled)
            {
                if (component.CounterboreBridgeLayerHeight
                    <= doc.ModelAbsoluteTolerance)
                {
                    throw new InvalidOperationException(
                        $"架桥层高 {component.CounterboreBridgeLayerHeight:0.###} mm "
                        + $"必须大于文档绝对公差 {doc.ModelAbsoluteTolerance:0.###} mm。");
                }
                var bridgeEnd = component.HeadEmbedDepth
                    + component.CounterboreBridgeLayerHeight * 2;
                if (interval.Max < bridgeEnd - doc.ModelAbsoluteTolerance)
                {
                    throw new InvalidOperationException(
                        $"宿主“{TargetName(target)}”在沉孔底面后没有足够空间容纳双层架桥；"
                        + $"至少需要到达 {bridgeEnd:0.###} mm，当前有效厚度为 {interval.Max:0.###} mm。");
                }
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
                var requiredHostDepth = InstallationPocketCalculator.RequiredHostDepth(component, spec);
                if (component.Kind == FastenerKind.HexNut
                    && interval.Max < requiredHostDepth - doc.ModelAbsoluteTolerance)
                    throw new InvalidOperationException(
                        $"宿主“{TargetName(target)}”有效厚度 {interval.Max:0.###} mm 小于紧固件本体所需深度 {requiredHostDepth:0.###} mm。");

                if (component.Kind == FastenerKind.HexNut)
                {
                    var depth = InstallationPocketCalculator.HexNutEmbedDepth(component);
                    result = new CutterGeometryBuild(
                        binding,
                        depth <= 0
                            ? []
                            : [FastenerGeometryFactory.CreateHexNutPocketCutter(
                                component,
                                spec,
                                binding,
                                padding)],
                        [],
                        warnings);
                    return true;
                }

                var requestedCuttingDepth = InstallationPocketCalculator.CuttingDepth(component, spec);
                var mouthDiameter = InstallationPocketCalculator.HeatSetMouthDiameter(component);
                if (!CutterFootprintEnvelopeService.TryGet(
                        target.Geometry,
                        component.Placement,
                        mouthDiameter,
                        doc.ModelAbsoluteTolerance,
                        out var heatSetFootprint,
                        out var heatSetFootprintError))
                    throw new InvalidOperationException(heatSetFootprintError);

                var cuttingDepth = requestedCuttingDepth;
                if (requestedCuttingDepth >= heatSetFootprint.Max - doc.ModelAbsoluteTolerance)
                {
                    warnings.Add(component.Length >= heatSetFootprint.Max - doc.ModelAbsoluteTolerance
                        ? $"{TargetName(target)}：热熔螺母长度超过宿主厚度，安装孔将贯穿。"
                        : $"{TargetName(target)}：深度补偿超过宿主厚度，安装孔将贯穿。");
                    cuttingDepth = Math.Max(requestedCuttingDepth, heatSetFootprint.Max + padding);
                }
                var heatSet = FastenerGeometryFactory.CreateHeatSetPocketCutters(
                    component,
                    spec,
                    padding,
                    cuttingDepth);
                result = new CutterGeometryBuild(
                    binding,
                    [heatSet.Shaft],
                    [heatSet.LeadIn],
                    warnings);
                return true;
            }

            if (binding.Role == ShaftFitRole.NutPocket)
            {
                if (component.AssemblyMode != ScrewAssemblyMode.NutFastened)
                    throw new InvalidOperationException("配套螺母槽只能用于螺母固定模式。");
                var nutRange = PairedNutAssemblyCalculator.AxialRange(component, spec);
                var acrossFlats = PairedNutAssemblyCalculator.PocketAcrossFlats(
                    component,
                    spec,
                    binding);
                var cornerDiameter = 2 * acrossFlats / Math.Sqrt(3);
                if (!CutterFootprintEnvelopeService.TryGet(
                        target.Geometry,
                        component.Placement,
                        cornerDiameter,
                        doc.ModelAbsoluteTolerance,
                        out var nutFootprint,
                        out var nutFootprintError))
                    throw new InvalidOperationException(nutFootprintError);
                if (nutRange.OuterFace < nutFootprint.Min - doc.ModelAbsoluteTolerance
                    || nutRange.InnerFace > nutFootprint.Max + doc.ModelAbsoluteTolerance)
                    throw new InvalidOperationException(
                        $"配套螺母位置未与宿主“{TargetName(target)}”相交；请调整螺杆长度或末端露出量。");
                var pocketStart = nutRange.InnerFace
                    - Math.Max(doc.ModelAbsoluteTolerance * 2, 0.01);
                var pocketEnd = nutFootprint.Max + padding;
                result = new CutterGeometryBuild(
                    binding,
                    [FastenerGeometryFactory.CreatePairedNutPocketCutter(
                        component,
                        spec,
                        binding,
                        pocketStart,
                        pocketEnd)],
                    [],
                    warnings);
                return true;
            }

            var finalDiameter = HoleDiameterCalculator
                .Calculate(component, spec, binding)
                .FinalDiameter;
            if (!CutterFootprintEnvelopeService.TryGet(
                    target.Geometry,
                    component.Placement,
                    finalDiameter,
                    doc.ModelAbsoluteTolerance,
                    out var footprint,
                    out var footprintError))
                throw new InvalidOperationException(footprintError);

            // The center-axis interval is insufficient when the host surface is
            // oblique to the screw. Always begin before the earliest point touched
            // by the complete hole circle so the entrance cannot retain a wedge.
            var start = footprint.Min - padding;
            var end = footprint.Max + padding;
            var limit = FastenerGeometryFactory.DepthLimit(component, spec, binding);
            var isThrough = binding.Role == ShaftFitRole.Clearance
                || binding.DepthMode == DepthMode.ThroughTarget;
            if (!isThrough && !double.IsPositiveInfinity(limit))
            {
                if (limit < footprint.Min - doc.ModelAbsoluteTolerance
                    && binding.Role == ShaftFitRole.Clearance)
                    throw new InvalidOperationException(
                        $"螺杆有效长度 {limit:0.###} mm 无法到达穿过宿主“{TargetName(target)}”。");
                if (limit < footprint.Min - doc.ModelAbsoluteTolerance
                    && binding.Role != ShaftFitRole.Clearance)
                    throw new InvalidOperationException(
                        $"螺杆深度 {limit:0.###} mm 无法到达咬合宿主“{TargetName(target)}”。");
                var reachesExit = limit >= footprint.Max - doc.ModelAbsoluteTolerance;
                end = reachesExit ? footprint.Max + padding : limit;
                if (reachesExit && binding.Role != ShaftFitRole.Clearance)
                    warnings.Add($"{TargetName(target)}：计算深度超过宿主厚度，将贯穿。");
            }

            result = new CutterGeometryBuild(
                binding,
                [FastenerGeometryFactory.CreateShaftCutter(component, spec, binding, start, end)],
                binding.IncludeHeadSeat
                    ? FastenerGeometryFactory.CreateHeadSeatCutters(
                        component,
                        spec,
                        binding,
                        padding)
                    : [],
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
