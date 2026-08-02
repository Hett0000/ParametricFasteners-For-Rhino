using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public sealed record CounterboreBridgeProfile(
    double HeadRadius,
    double ShaftRadius,
    double LayerHeight,
    double SlotHalfLength,
    double Start,
    double FirstLayerEnd,
    double SecondLayerEnd);

public static class CounterboreBridgeCalculator
{
    public static CounterboreBridgeProfile Create(
        FastenerComponentData component,
        FastenerSizeSpec spec,
        HoleTargetBinding binding,
        double headRadialPadding,
        double tolerance)
    {
        if (component.Kind != FastenerKind.SocketCap)
            throw new InvalidOperationException("悬垂沉孔架桥仅支持内六角杯头螺丝。");
        if (!component.CounterboreBridgeEnabled)
            throw new InvalidOperationException("当前组件未启用悬垂沉孔架桥。");
        if (!double.IsFinite(component.CounterboreBridgeLayerHeight)
            || component.CounterboreBridgeLayerHeight is < 0.05 or > 1.0)
            throw new InvalidOperationException("悬垂沉孔架桥层高必须在 0.05–1.00 mm 之间。");

        var headRadius = spec.Head.SocketDiameter / 2 + headRadialPadding;
        var shaftDiameter = HoleDiameterCalculator
            .Calculate(component, spec, binding)
            .FinalDiameter;
        var shaftRadius = shaftDiameter / 2;
        if (shaftRadius <= 0)
            throw new InvalidOperationException("悬垂沉孔架桥的最终螺杆孔径必须大于 0。");

        // Layer two is a square whose side equals the compensated shaft-hole
        // diameter. Its corners must remain inside the counterbore.
        if (headRadius <= Math.Sqrt(2) * shaftRadius + tolerance)
        {
            throw new InvalidOperationException(
                $"最终螺杆孔径 {shaftDiameter:0.###} mm 过大，杯头沉孔内无法形成有效的双层架桥。");
        }

        var slotHalfLength = Math.Sqrt(
            Math.Max(0, headRadius * headRadius - shaftRadius * shaftRadius));
        var start = component.HeadEmbedDepth;
        var firstEnd = start + component.CounterboreBridgeLayerHeight;
        return new CounterboreBridgeProfile(
            headRadius,
            shaftRadius,
            component.CounterboreBridgeLayerHeight,
            slotHalfLength,
            start,
            firstEnd,
            firstEnd + component.CounterboreBridgeLayerHeight);
    }
}
