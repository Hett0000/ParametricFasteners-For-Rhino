using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class FastenerComponentValidator
{
    public static ValidationResult Validate(FastenerComponentData component, FastenerSizeSpec spec)
    {
        var result = new ValidationResult();

        if (FastenerKindTraits.UsesLengthInStatistics(component.Kind) && component.Length <= 0)
            result.Issues.Add(new("length", "紧固件长度必须大于 0。"));

        if (component.HeadEmbedDepth < 0)
            result.Issues.Add(new("head-embed-negative", "螺丝头嵌入深度不能小于 0。"));

        if (!FastenerKindTraits.SupportsHeadEmbed(component.Kind) && component.HeadEmbedDepth != 0)
            result.Issues.Add(new("nut-head-embed", "螺母不支持螺丝头嵌入深度。"));

        if (FastenerKindTraits.SupportsHeadEmbed(component.Kind)
            && component.HeadEmbedDepth > 0
            && component.Bindings.All(binding => !binding.IncludeHeadSeat))
            result.Issues.Add(new("head-seat-required", "螺丝头嵌入时必须指定一个头部切割宿主。"));

        if (component.FastenerOpacityPercent is < 0 or > 100)
            result.Issues.Add(new("fastener-opacity", "紧固件不透明度必须在 0–100% 之间。"));

        if (component.CutterOpacityPercent is < 0 or > 100)
            result.Issues.Add(new("cutter-opacity", "切割模块不透明度必须在 0–100% 之间。"));

        if (FastenerKindTraits.IsNut(component.Kind) && component.Bindings.Count != 1)
            result.Issues.Add(new("nut-binding-count", "螺母必须绑定且只能绑定一个安装宿主。"));

        if (component.Kind == FastenerKind.HeatSetInsert)
        {
            if (component.InsertOuterDiameter <= 0)
                result.Issues.Add(new("insert-outer-diameter", "热熔螺母外径必须大于 0。"));
            if (component.InsertDepthCompensation is < 0 or > 1000)
                result.Issues.Add(new("insert-depth-compensation", "热熔螺母深度补偿必须在 0–1000 mm 之间。"));
            var finalDiameter = InstallationPocketCalculator.HeatSetFinalDiameter(component);
            if (finalDiameter <= spec.NominalDiameter)
                result.Issues.Add(new(
                    "insert-final-diameter",
                    $"热熔安装孔最终直径 {finalDiameter:0.###} mm 必须大于螺纹公称直径 {spec.NominalDiameter:0.###} mm。"));
        }

        if (component.Kind == FastenerKind.HexNut)
        {
            var binding = component.Bindings.FirstOrDefault() ?? new HoleTargetBinding();
            var finalAcrossFlats = InstallationPocketCalculator.HexNutAcrossFlats(component, spec, binding);
            if (finalAcrossFlats <= 0)
                result.Issues.Add(new("nut-pocket-size", "六角螺母槽最终对边尺寸必须大于 0。"));
        }

        if (component.Bindings.Count(x => x.IncludeHeadSeat) > 1)
            result.Issues.Add(new("head-seat", "一个组件只能在一个被切割体上生成头部沉孔。"));

        foreach (var binding in component.Bindings)
        {
            if (binding.TargetObjectId == Guid.Empty)
                result.Issues.Add(new("target", "被切割体引用不能为空。"));

            if (binding.DepthMode == DepthMode.Blind && binding.BlindDepth <= 0)
                result.Issues.Add(new("blind-depth", "盲孔深度必须大于 0。"));

            if (FastenerKindTraits.IsNut(component.Kind))
            {
                if (binding.Role != ShaftFitRole.InstallationPocket)
                    result.Issues.Add(new("nut-binding-role", "螺母宿主必须使用安装槽/孔绑定。"));
                if (binding.DepthMode != DepthMode.Blind)
                    result.Issues.Add(new("nut-depth-mode", "螺母安装槽/孔必须使用盲孔深度。"));
                continue;
            }

            if (binding.Role == ShaftFitRole.InstallationPocket)
            {
                result.Issues.Add(new("screw-installation-pocket", "螺丝组件不能使用螺母安装槽绑定。"));
                continue;
            }

            if (binding.Role == ShaftFitRole.Clearance && binding.DepthMode != DepthMode.ThroughTarget)
                result.Issues.Add(new("clearance-depth", "穿过通孔只能使用贯穿宿主深度模式。"));

            if (binding.Role == ShaftFitRole.ThreadEngagement && binding.BiteReduction <= 0)
                result.Issues.Add(new("bite-required", "咬合孔必须输入大于 0 的咬合缩减量，不能使用未校准默认值。"));

            var diameter = HoleDiameterCalculator.Calculate(spec, binding, component.PrintProfile).FinalDiameter;
            if (diameter <= 0)
                result.Issues.Add(new("diameter-positive", "最终孔径必须大于 0。"));

            if (binding.Role == ShaftFitRole.Clearance && diameter <= spec.NominalDiameter)
                result.Issues.Add(new("clearance-range", $"通孔最终直径 {diameter:0.###} mm 必须大于公称直径 {spec.NominalDiameter:0.###} mm。"));

            if (binding.Role == ShaftFitRole.ThreadEngagement && diameter >= spec.NominalDiameter)
                result.Issues.Add(new("engagement-range", $"咬合孔最终直径 {diameter:0.###} mm 必须小于公称直径 {spec.NominalDiameter:0.###} mm。"));
        }

        return result;
    }
}
