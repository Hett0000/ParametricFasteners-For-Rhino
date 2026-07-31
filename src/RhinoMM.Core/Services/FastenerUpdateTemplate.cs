using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public sealed record FastenerUpdateTemplate(
    FastenerKind Kind,
    string Size,
    double Length,
    double HeadEmbedDepth,
    double InsertOuterDiameter,
    double InsertDiameterCompensation,
    double InsertDepthCompensation,
    double PrinterCorrection,
    ClearanceFitClass ClearanceFit,
    double BiteReduction,
    DepthMode EngagementDepthMode,
    double EngagementBlindDepth,
    double FastenerOpacityPercent,
    double CutterOpacityPercent,
    bool? ClearancePreviewVisible = null,
    bool? ClearanceBooleanEnabled = null,
    bool? EngagementPreviewVisible = null,
    bool? EngagementBooleanEnabled = null,
    bool? InstallationPreviewVisible = null,
    bool? InstallationBooleanEnabled = null)
{
    public FastenerComponentData ApplyTo(
        FastenerComponentData existing,
        IReadOnlyList<HoleTargetBinding>? bindingOverrides = null)
    {
        var bindings = ApplyBindings(existing.Bindings);
        if (bindingOverrides is not null)
            bindings = ApplyPerBindingOverrides(bindings, bindingOverrides);

        var updated = existing with
        {
            Kind = Kind,
            Size = Size,
            Length = Length,
            HeadEmbedDepth = FastenerKindTraits.SupportsEmbedDepth(Kind) ? HeadEmbedDepth : 0,
            InsertOuterDiameter = Kind == FastenerKind.HeatSetInsert ? InsertOuterDiameter : 0,
            InsertDiameterCompensation = Kind == FastenerKind.HeatSetInsert
                ? InsertDiameterCompensation
                : 0,
            InsertDepthCompensation = Kind == FastenerKind.HeatSetInsert
                ? InsertDepthCompensation
                : 0,
            PrintProfile = existing.PrintProfile with
            {
                HoleDiameterCorrection = PrinterCorrection
            },
            FastenerOpacityPercent = FastenerOpacityPercent,
            CutterOpacityPercent = CutterOpacityPercent,
            Bindings = bindings,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        return updated with
        {
            SmartBindingProfile = UpdateSmartBindingProfile(updated)
        };
    }

    private IReadOnlyList<HoleTargetBinding> ApplyBindings(
        IReadOnlyList<HoleTargetBinding> bindings)
    {
        return bindings.Select(binding => binding.Role switch
        {
            ShaftFitRole.Clearance => ApplyVisibility(binding with
            {
                ClearanceFit = ClearanceFit
            }, ClearancePreviewVisible, ClearanceBooleanEnabled),
            ShaftFitRole.ThreadEngagement => ApplyVisibility(binding with
            {
                BiteReduction = BiteReduction,
                DepthMode = EngagementDepthMode,
                BlindDepth = EngagementBlindDepth
            }, EngagementPreviewVisible, EngagementBooleanEnabled),
            ShaftFitRole.InstallationPocket when Kind == FastenerKind.HeatSetInsert =>
                ApplyVisibility(binding with
                {
                    DepthMode = DepthMode.Blind,
                    BlindDepth = Length + InsertDepthCompensation,
                    IncludeHeadSeat = false
                }, InstallationPreviewVisible, InstallationBooleanEnabled),
            ShaftFitRole.InstallationPocket when Kind == FastenerKind.HexNut =>
                ApplyVisibility(binding with
                {
                    DepthMode = DepthMode.Blind,
                    BlindDepth = HeadEmbedDepth,
                    IncludeHeadSeat = false
                }, InstallationPreviewVisible, InstallationBooleanEnabled),
            _ => binding
        }).ToArray();
    }

    private static IReadOnlyList<HoleTargetBinding> ApplyPerBindingOverrides(
        IReadOnlyList<HoleTargetBinding> bindings,
        IReadOnlyList<HoleTargetBinding> overrides)
    {
        var overridesById = overrides.ToDictionary(binding => binding.BindingId);
        return bindings.Select(binding =>
        {
            if (!overridesById.TryGetValue(binding.BindingId, out var local))
                return binding;
            return binding.Role switch
            {
                ShaftFitRole.ThreadEngagement => binding with
                {
                    DepthMode = local.DepthMode,
                    BlindDepth = local.BlindDepth,
                    IsPreviewVisible = local.IsPreviewVisible,
                    IsBooleanEnabled = local.IsBooleanEnabled
                },
                ShaftFitRole.Clearance or ShaftFitRole.InstallationPocket => binding with
                {
                    IsPreviewVisible = local.IsPreviewVisible,
                    IsBooleanEnabled = local.IsBooleanEnabled
                },
                _ => binding
            };
        }).ToArray();
    }

    private static HoleTargetBinding ApplyVisibility(
        HoleTargetBinding binding,
        bool? previewVisible,
        bool? booleanEnabled) => binding with
    {
        IsPreviewVisible = previewVisible ?? binding.IsPreviewVisible,
        IsBooleanEnabled = booleanEnabled ?? binding.IsBooleanEnabled
    };

    private SmartBindingProfile? UpdateSmartBindingProfile(
        FastenerComponentData component)
    {
        if (!component.AutoRecognizeHosts)
            return component.SmartBindingProfile;
        var profile = component.SmartBindingProfile ?? new SmartBindingProfile(
            ClearanceFit,
            BiteReduction,
            EngagementDepthMode,
            EngagementBlindDepth,
            true,
            true,
            true,
            true);
        return profile with
        {
            ClearanceFit = ClearanceFit,
            BiteReduction = BiteReduction,
            EngagementDepthMode = EngagementDepthMode,
            EngagementBlindDepth = EngagementBlindDepth,
            ClearancePreviewVisible =
                ClearancePreviewVisible ?? profile.ClearancePreviewVisible,
            ClearanceBooleanEnabled =
                ClearanceBooleanEnabled ?? profile.ClearanceBooleanEnabled,
            EngagementPreviewVisible =
                EngagementPreviewVisible ?? profile.EngagementPreviewVisible,
            EngagementBooleanEnabled =
                EngagementBooleanEnabled ?? profile.EngagementBooleanEnabled
        };
    }
}
