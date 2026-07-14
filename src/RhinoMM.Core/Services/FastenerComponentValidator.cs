using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class FastenerComponentValidator
{
    public static ValidationResult Validate(FastenerComponentData component, FastenerSizeSpec spec)
    {
        var result = new ValidationResult();

        if (component.Length <= 0)
            result.Issues.Add(new("length", "螺丝长度必须大于 0。"));

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
