using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class HoleDiameterCalculator
{
    public static HoleDiameterResult Calculate(
        FastenerComponentData component,
        FastenerSizeSpec spec,
        HoleTargetBinding binding)
    {
        var legacy = component.HoleDiameterFormula ==
            HoleDiameterFormula.LegacyStandardWithSharedCorrection;
        var baseDiameter = binding.Role switch
        {
            ShaftFitRole.Clearance when legacy => spec.Clearance.For(binding.ClearanceFit),
            ShaftFitRole.Clearance => spec.NominalDiameter,
            ShaftFitRole.ThreadEngagement => spec.NominalDiameter - binding.BiteReduction,
            _ => throw new ArgumentOutOfRangeException(nameof(binding.Role))
        };

        var printerCorrection = binding.Role == ShaftFitRole.Clearance || legacy
            ? component.PrintProfile.HoleDiameterCorrection
            : 0;
        var final = baseDiameter + printerCorrection + binding.BindingOverride;
        return new HoleDiameterResult(
            baseDiameter,
            printerCorrection,
            binding.BindingOverride,
            final);
    }
}
