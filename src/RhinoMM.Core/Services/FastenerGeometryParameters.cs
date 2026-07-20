using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class FastenerGeometryParameters
{
    public static bool Match(FastenerComponentData left, FastenerComponentData right)
    {
        if (left.Kind != right.Kind
            || !string.Equals(left.Size, right.Size, StringComparison.Ordinal)
            || left.Length != right.Length
            || left.HeadEmbedDepth != right.HeadEmbedDepth
            || left.Placement != right.Placement
            || left.PrintProfile.HoleDiameterCorrection != right.PrintProfile.HoleDiameterCorrection
            || left.Bindings.Count != right.Bindings.Count)
            return false;

        var rightBindings = right.Bindings.ToDictionary(binding => binding.BindingId);
        return left.Bindings.All(binding =>
            rightBindings.TryGetValue(binding.BindingId, out var other)
            && BindingGeometryMatches(binding, other));
    }

    private static bool BindingGeometryMatches(HoleTargetBinding left, HoleTargetBinding right) =>
        left.TargetObjectId == right.TargetObjectId
        && left.Role == right.Role
        && left.ClearanceFit == right.ClearanceFit
        && left.BiteReduction == right.BiteReduction
        && left.BindingOverride == right.BindingOverride
        && left.DepthMode == right.DepthMode
        && left.BlindDepth == right.BlindDepth
        && left.IncludeHeadSeat == right.IncludeHeadSeat;
}
