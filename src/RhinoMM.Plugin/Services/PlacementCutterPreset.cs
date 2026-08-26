using RhinoMM.Core.Domain;

namespace RhinoMM.Plugin.Services;

internal sealed record PlacementCutterPreset(
    double PrinterCorrection,
    ClearanceFitClass ClearanceFit,
    double BiteReduction,
    bool CounterboreBridgeEnabled,
    double CounterboreBridgeLayerHeight,
    DepthMode EngagementDepthMode,
    double EngagementBlindDepth,
    bool ClearancePreviewVisible,
    bool ClearanceBooleanEnabled,
    bool EngagementPreviewVisible,
    bool EngagementBooleanEnabled,
    bool EngagementOnly,
    ScrewAssemblyMode AssemblyMode = ScrewAssemblyMode.ThreadEngagement,
    HexNutStyle PairedNutStyle = HexNutStyle.Standard,
    double NutTipProtrusion = 2,
    double NutPocketCompensation = 0.2,
    bool NutPocketPreviewVisible = true,
    bool NutPocketBooleanEnabled = true,
    bool EngagementEntryChamferEnabled = false,
    double EngagementEntryChamferSize = 0.5,
    EngagementEntryChamferMode EngagementEntryChamferMode =
        EngagementEntryChamferMode.AxialFortyFive,
    double EngagementOnlyAlignmentDepth = 3,
    double EngagementOnlyAlignmentDiameterCompensation = 0.2)
{
    public static PlacementCutterPreset Default { get; } = new(
        0.2,
        ClearanceFitClass.Normal,
        0.35,
        false,
        0.2,
        DepthMode.FastenerLengthPlusOneDiameter,
        3,
        true,
        true,
        true,
        true,
        false,
        EngagementEntryChamferEnabled: false,
        EngagementEntryChamferSize: 0.5,
        EngagementEntryChamferMode: EngagementEntryChamferMode.AxialFortyFive,
        EngagementOnlyAlignmentDepth: 3,
        EngagementOnlyAlignmentDiameterCompensation: 0.2);

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

    public HoleTargetBinding CreateNutPocketBinding(Guid targetId) => new()
    {
        TargetObjectId = targetId,
        Role = ShaftFitRole.NutPocket,
        DepthMode = DepthMode.ThroughTarget,
        IncludeHeadSeat = false,
        IsPreviewVisible = NutPocketPreviewVisible,
        IsBooleanEnabled = NutPocketBooleanEnabled
    };

    public SmartBindingProfile ToSmartBindingProfile() => new(
        ClearanceFit,
        BiteReduction,
        EngagementDepthMode,
        EngagementBlindDepth,
        ClearancePreviewVisible,
        ClearanceBooleanEnabled,
        EngagementPreviewVisible,
        EngagementBooleanEnabled,
        NutPocketPreviewVisible,
        NutPocketBooleanEnabled,
        EngagementOnlyAlignmentDepth,
        EngagementOnlyAlignmentDiameterCompensation);
}
