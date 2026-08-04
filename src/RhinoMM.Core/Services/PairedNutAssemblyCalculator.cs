using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public sealed record PairedNutAxialRange(
    double ShaftTip,
    double InnerFace,
    double OuterFace,
    double NutHeight);

public static class PairedNutAssemblyCalculator
{
    public static PairedNutAxialRange AxialRange(
        FastenerComponentData component,
        FastenerSizeSpec spec)
    {
        var dimensions = HexNutDimensions.Resolve(component.PairedNutStyle, spec);
        var tip = component.HeadEmbedDepth + component.Length;
        var outer = tip - component.NutTipProtrusion;
        return new PairedNutAxialRange(
            tip,
            outer - dimensions.TotalHeight,
            outer,
            dimensions.TotalHeight);
    }

    public static double PocketAcrossFlats(
        FastenerComponentData component,
        FastenerSizeSpec spec,
        HoleTargetBinding binding) =>
        HexNutDimensions.Resolve(component.PairedNutStyle, spec).AcrossFlats
        + component.NutPocketCompensation
        + binding.BindingOverride;
}
