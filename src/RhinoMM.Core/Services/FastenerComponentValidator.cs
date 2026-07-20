using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class FastenerComponentValidator
{
    public static ValidationResult Validate(FastenerComponentData component, FastenerSizeSpec spec)
    {
        var result = new ValidationResult();

        if (component.Length <= 0)
            result.Issues.Add(new("length", "螺丝长度必须大于 0。"));

        if (component.HeadEmbedDepth < 0)
            result.Issues.Add(new("head-embed-negative", "螺丝头嵌入深度不能小于 0。"));

        if (component.Kind == FastenerKind.HexNut && component.HeadEmbedDepth != 0)
            result.Issues.Add(new("nut-head-embed", "六角螺母不支持螺丝头嵌入深度。"));

        if (component.HeadEmbedDepth > 0 && component.Bindings.All(binding => !binding.IncludeHeadSeat))
            result.Issues.Add(new("head-seat-required", "螺丝头嵌入时必须指定一个头部切割宿主。"));

        if (component.FastenerOpacityPercent is < 0 or > 100)
            result.Issues.Add(new("fastener-opacity", "紧固件不透明度必须在 0–100% 之间。"));

        if (component.CutterOpacityPercent is < 0 or > 100)
            result.Issues.Add(new("cutter-opacity", "切割模块不透明度必须在 0–100% 之间。"));

        if (component.Kind == FastenerKind.HexNut && component.Bindings.Count > 0)
            result.Issues.Add(new("nut-binding", "螺母预览暂不应直接绑定轴孔；请使用螺丝组件定义孔位。", false));

        if (component.Bindings.Count(x => x.IncludeHeadSeat) > 1)
            result.Issues.Add(new("head-seat", "一个组件只能在一个被切割体上生成头部沉孔。"));

        foreach (var binding in component.Bindings)
        {
            if (binding.TargetObjectId == Guid.Empty)
                result.Issues.Add(new("target", "被切割体引用不能为空。"));

            if (binding.DepthMode == DepthMode.Blind && binding.BlindDepth <= 0)
                result.Issues.Add(new("blind-depth", "盲孔深度必须大于 0。"));

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
