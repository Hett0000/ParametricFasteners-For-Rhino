using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class HoleDiameterCalculator
{
    public static HoleDiameterResult Calculate(
        FastenerSizeSpec spec,
        HoleTargetBinding binding,
        PrintProfileSnapshot profile)
    {
        var baseDiameter = binding.Role switch
        {
            ShaftFitRole.Clearance => spec.Clearance.For(binding.ClearanceFit),
            ShaftFitRole.ThreadEngagement => spec.NominalDiameter - binding.BiteReduction,
            _ => throw new ArgumentOutOfRangeException(nameof(binding.Role))
        };

        var final = baseDiameter + profile.HoleDiameterCorrection + binding.BindingOverride;
        return new HoleDiameterResult(
            baseDiameter,
            profile.HoleDiameterCorrection,
            binding.BindingOverride,
            final);
    }
}
