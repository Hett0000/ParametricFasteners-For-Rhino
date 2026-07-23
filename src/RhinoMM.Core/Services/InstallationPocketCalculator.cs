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

    public static double RequiredHostDepth(FastenerComponentData component, FastenerSizeSpec spec) =>
        component.Kind == FastenerKind.HexNut ? spec.Head.NutThickness : component.Length;

    public static double CuttingDepth(FastenerComponentData component, FastenerSizeSpec spec) =>
        component.Kind == FastenerKind.HeatSetInsert
            ? component.Length + component.InsertDepthCompensation
            : RequiredHostDepth(component, spec);

    // Compatibility wrapper for callers that need the physical installation depth.
    public static double RequiredDepth(FastenerComponentData component, FastenerSizeSpec spec) =>
        RequiredHostDepth(component, spec);
}
