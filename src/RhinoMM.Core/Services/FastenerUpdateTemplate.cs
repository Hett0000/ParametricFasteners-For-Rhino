using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public sealed record FastenerUpdateTemplate(
    FastenerKind Kind,
    HexNutStyle NutStyle,
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
    bool CounterboreBridgeEnabled = false,
    double CounterboreBridgeLayerHeight = 0.2,
    bool EngagementOnly = false,
    bool? ClearancePreviewVisible = null,
    bool? ClearanceBooleanEnabled = null,
    bool? EngagementPreviewVisible = null,
    bool? EngagementBooleanEnabled = null,
    bool? InstallationPreviewVisible = null,
    bool? InstallationBooleanEnabled = null,
    ScrewAssemblyMode AssemblyMode = ScrewAssemblyMode.ThreadEngagement,
    HexNutStyle PairedNutStyle = HexNutStyle.Standard,
    double NutTipProtrusion = 2,
    double NutPocketCompensation = 0.2,
    bool? NutPocketPreviewVisible = null,
    bool? NutPocketBooleanEnabled = null)
{
    public FastenerComponentData ApplyTo(
        FastenerComponentData existing,
        IReadOnlyList<HoleTargetBinding>? bindingOverrides = null)
    {
        var bindings = ApplyBindings(existing.Bindings);
        if (bindingOverrides is not null)
            bindings = ApplyPerBindingOverrides(bindings, bindingOverrides);
        var assemblyMode = FastenerKindTraits.IsScrew(Kind)
            ? AssemblyMode == ScrewAssemblyMode.ThreadEngagement && EngagementOnly
                ? ScrewAssemblyMode.EngagementOnly
                : AssemblyMode
            : ScrewAssemblyMode.ThreadEngagement;
        var engagementOnly = assemblyMode == ScrewAssemblyMode.EngagementOnly;
        if (engagementOnly)
        {
            var priorEngagement = existing.Bindings.FirstOrDefault(binding =>
                binding.Role == ShaftFitRole.ThreadEngagement);
            var engagementPreview = EngagementPreviewVisible
                ?? priorEngagement?.IsPreviewVisible
                ?? existing.SmartBindingProfile?.EngagementPreviewVisible
                ?? true;
            var engagementBoolean = EngagementBooleanEnabled
                ?? priorEngagement?.IsBooleanEnabled
                ?? existing.SmartBindingProfile?.EngagementBooleanEnabled
                ?? true;
            var headSeat = bindings.FirstOrDefault(binding => binding.IncludeHeadSeat)
                ?? bindings.FirstOrDefault(binding => binding.Role == ShaftFitRole.ThreadEngagement);
            if (headSeat is not null)
            {
                bindings =
                [
                    headSeat with
                    {
                        Role = ShaftFitRole.ThreadEngagement,
                        ClearanceFit = ClearanceFitClass.Normal,
                        BiteReduction = BiteReduction,
                        DepthMode = EngagementDepthMode,
                        BlindDepth = EngagementBlindDepth,
                        IncludeHeadSeat = true,
                        IsPreviewVisible = engagementPreview,
                        IsBooleanEnabled = engagementBoolean
                    }
                ];
            }
        }

        var updated = existing with
        {
            Kind = Kind,
            HexNutStyle = Kind == FastenerKind.HexNut
                ? NutStyle
                : RhinoMM.Core.Domain.HexNutStyle.Standard,
            Size = Size,
            Length = Length,
            HeadEmbedDepth = FastenerKindTraits.SupportsEmbedDepth(Kind) ? HeadEmbedDepth : 0,
            CounterboreBridgeEnabled = Kind == FastenerKind.SocketCap
                && CounterboreBridgeEnabled,
            CounterboreBridgeLayerHeight = CounterboreBridgeLayerHeight,
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
            HoleDiameterFormula = HoleDiameterFormula.NominalIndependent,
            AssemblyMode = assemblyMode,
            PairedNutStyle = assemblyMode == ScrewAssemblyMode.NutFastened
                ? PairedNutStyle
                : RhinoMM.Core.Domain.HexNutStyle.Standard,
            NutTipProtrusion = assemblyMode == ScrewAssemblyMode.NutFastened
                ? NutTipProtrusion
                : 2,
            NutPocketCompensation = assemblyMode == ScrewAssemblyMode.NutFastened
                ? NutPocketCompensation
                : 0.2,
            EngagementOnly = engagementOnly,
            AutoRecognizeHosts = existing.AutoRecognizeHosts
                || existing.AssemblyMode != assemblyMode
                && (existing.AssemblyMode == ScrewAssemblyMode.NutFastened
                    || assemblyMode == ScrewAssemblyMode.NutFastened),
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
            ShaftFitRole.NutPocket => ApplyVisibility(binding with
            {
                DepthMode = DepthMode.ThroughTarget,
                IncludeHeadSeat = false
            }, NutPocketPreviewVisible, NutPocketBooleanEnabled),
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
                ShaftFitRole.NutPocket => binding with
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
                EngagementBooleanEnabled ?? profile.EngagementBooleanEnabled,
            NutPocketPreviewVisible =
                NutPocketPreviewVisible ?? profile.NutPocketPreviewVisible,
            NutPocketBooleanEnabled =
                NutPocketBooleanEnabled ?? profile.NutPocketBooleanEnabled
        };
    }
}
