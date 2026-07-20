using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class InstallationPocketCalculator
{
    public static double HexNutAcrossFlats(
        FastenerComponentData component,
        FastenerSizeSpec spec,
        HoleTargetBinding binding) =>
        spec.Head.NutAcrossFlats
        + component.PrintProfile.HoleDiameterCorrection
        + binding.BindingOverride;

    public static double HeatSetFinalDiameter(FastenerComponentData component) =>
        component.InsertOuterDiameter + component.InsertDiameterCompensation;

    public static double HeatSetChamferDepth(FastenerComponentData component) =>
        Math.Min(0.5, component.Length / 2);

    public static double HeatSetMouthDiameter(FastenerComponentData component) =>
        HeatSetFinalDiameter(component) + HeatSetChamferDepth(component) * 2;

    public static double RequiredDepth(FastenerComponentData component, FastenerSizeSpec spec) =>
        component.Kind == FastenerKind.HexNut ? spec.Head.NutThickness : component.Length;
}
