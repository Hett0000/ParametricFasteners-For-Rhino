using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public sealed record EngagementOnlyAlignmentProfile(
    double GuideDiameter,
    double EngagementDiameter,
    double GuideDepth,
    double TransitionLength);

public static class EngagementOnlyAlignmentCalculator
{
    private static readonly double TanThirtyDegrees = Math.Tan(Math.PI / 6);

    public static bool TryCreate(
        FastenerComponentData component,
        FastenerSizeSpec spec,
        HoleTargetBinding binding,
        out EngagementOnlyAlignmentProfile? profile,
        out string message)
    {
        profile = null;
        message = string.Empty;
        if (component.AssemblyMode != ScrewAssemblyMode.EngagementOnly)
        {
            message = "顶部对位孔仅支持“只咬合”装配方式。";
            return false;
        }
        var depth = component.EngagementOnlyAlignmentDepth;
        var compensation = component.EngagementOnlyAlignmentDiameterCompensation;
        if (!double.IsFinite(depth) || depth <= 0)
        {
            message = "顶部对位深度必须大于 0。";
            return false;
        }
        if (!double.IsFinite(compensation) || compensation < 0)
        {
            message = "顶部对位正补偿必须为非负有限数值。";
            return false;
        }

        var guideDiameter = spec.NominalDiameter + compensation + binding.BindingOverride;
        var engagementDiameter = HoleDiameterCalculator.Calculate(component, spec, binding).FinalDiameter;
        if (!double.IsFinite(guideDiameter) || guideDiameter <= 0
            || !double.IsFinite(engagementDiameter) || engagementDiameter <= 0)
        {
            message = "顶部对位孔或咬合孔的最终直径无效。";
            return false;
        }
        if (guideDiameter <= engagementDiameter)
        {
            message = "顶部对位孔直径必须大于咬合孔直径，才能形成60°圆台过渡。";
            return false;
        }

        var transitionLength = (guideDiameter - engagementDiameter) * 0.5 / TanThirtyDegrees;
        profile = new EngagementOnlyAlignmentProfile(
            guideDiameter,
            engagementDiameter,
            depth,
            transitionLength);
        return true;
    }
}
