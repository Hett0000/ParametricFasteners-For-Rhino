using RhinoMM.Core.Domain;

namespace RhinoMM.Plugin.Services;

internal sealed record PlacementCutterPreset(
    double PrinterCorrection,
    ClearanceFitClass ClearanceFit,
    double BiteReduction,
    DepthMode EngagementDepthMode,
    double EngagementBlindDepth,
    bool ClearancePreviewVisible,
    bool ClearanceBooleanEnabled,
    bool EngagementPreviewVisible,
    bool EngagementBooleanEnabled)
{
    public static PlacementCutterPreset Default { get; } = new(
        0.2,
        ClearanceFitClass.Normal,
        0.35,
        DepthMode.FastenerLengthPlusOneDiameter,
        3,
        true,
        true,
        true,
        true);

    public HoleTargetBinding CreateClearanceBinding(Guid targetId, bool includeHeadSeat) => new()
    {
        TargetObjectId = targetId,
        Role = ShaftFitRole.Clearance,
        ClearanceFit = ClearanceFit,
        DepthMode = DepthMode.ThroughTarget,
        IncludeHeadSeat = includeHeadSeat,
        IsPreviewVisible = ClearancePreviewVisible,
        IsBooleanEnabled = ClearanceBooleanEnabled
    };

    public HoleTargetBinding CreateEngagementBinding(Guid targetId, bool includeHeadSeat) => new()
    {
        TargetObjectId = targetId,
        Role = ShaftFitRole.ThreadEngagement,
        BiteReduction = BiteReduction,
        DepthMode = EngagementDepthMode,
        BlindDepth = EngagementBlindDepth,
        IncludeHeadSeat = includeHeadSeat,
        IsPreviewVisible = EngagementPreviewVisible,
        IsBooleanEnabled = EngagementBooleanEnabled
    };

    public SmartBindingProfile ToSmartBindingProfile() => new(
        ClearanceFit,
        BiteReduction,
        EngagementDepthMode,
        EngagementBlindDepth,
        ClearancePreviewVisible,
        ClearanceBooleanEnabled,
        EngagementPreviewVisible,
        EngagementBooleanEnabled);
}
