using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class HoleDepthCalculator
{
    public static double GetLimit(FastenerComponentData component, FastenerSizeSpec spec, HoleTargetBinding binding) =>
        binding.DepthMode switch
        {
            DepthMode.FastenerLengthPlusOneDiameter => component.HeadEmbedDepth + component.Length + spec.NominalDiameter,
            DepthMode.FastenerLengthPlusTwoDiameters => component.HeadEmbedDepth + component.Length + 2 * spec.NominalDiameter,
            DepthMode.Blind => binding.BlindDepth,
            _ => double.PositiveInfinity
        };
}
