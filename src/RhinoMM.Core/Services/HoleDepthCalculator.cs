using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class HoleDepthCalculator
{
    public static double GetLimit(FastenerComponentData component, FastenerSizeSpec spec, HoleTargetBinding binding) =>
        binding.DepthMode switch
        {
            // Clearance cutters follow the current usable shaft reach while the
            // shaft is still inside the host. CutterGeometryService turns the
            // result into a through-hole once this limit reaches the back face.
            DepthMode.ThroughTarget when binding.Role == ShaftFitRole.Clearance =>
                component.HeadEmbedDepth + component.Length,
            DepthMode.FastenerLengthPlusOneDiameter => component.HeadEmbedDepth + component.Length + spec.NominalDiameter,
            DepthMode.FastenerLengthPlusTwoDiameters => component.HeadEmbedDepth + component.Length + 2 * spec.NominalDiameter,
            DepthMode.Blind => binding.BlindDepth,
            _ => double.PositiveInfinity
        };
}
