using RhinoMM.Core.Domain;

namespace RhinoMM.Plugin.Services;

internal sealed record BatchModuleTemplate(
    bool HasClearance,
    bool ClearancePreviewVisible,
    bool ClearanceBooleanEnabled,
    bool HasEngagement,
    DepthMode EngagementDepthMode,
    double EngagementBlindDepth,
    bool EngagementPreviewVisible,
    bool EngagementBooleanEnabled,
    bool HasInstallation,
    bool InstallationPreviewVisible,
    bool InstallationBooleanEnabled)
{
    public static BatchModuleTemplate FromComponents(
        IReadOnlyList<FastenerComponentData> components)
    {
        var bindings = components.SelectMany(component => component.Bindings).ToArray();
        var clearance = bindings.FirstOrDefault(binding => binding.Role == ShaftFitRole.Clearance);
        var engagement = bindings.FirstOrDefault(binding => binding.Role == ShaftFitRole.ThreadEngagement);
        var installation = bindings.FirstOrDefault(binding => binding.Role == ShaftFitRole.InstallationPocket);
        return new BatchModuleTemplate(
            clearance is not null,
            clearance?.IsPreviewVisible ?? true,
            clearance?.IsBooleanEnabled ?? true,
            engagement is not null,
            engagement?.DepthMode ?? DepthMode.ThroughTarget,
            engagement?.BlindDepth ?? 0,
            engagement?.IsPreviewVisible ?? true,
            engagement?.IsBooleanEnabled ?? true,
            installation is not null,
            installation?.IsPreviewVisible ?? true,
            installation?.IsBooleanEnabled ?? true);
    }

    public IReadOnlyList<HoleTargetBinding> Apply(
        IReadOnlyList<HoleTargetBinding> bindings)
    {
        return bindings.Select(binding => binding.Role switch
        {
            ShaftFitRole.Clearance when HasClearance => binding with
            {
                IsPreviewVisible = ClearancePreviewVisible,
                IsBooleanEnabled = ClearanceBooleanEnabled
            },
            ShaftFitRole.ThreadEngagement when HasEngagement => binding with
            {
                DepthMode = EngagementDepthMode,
                BlindDepth = EngagementBlindDepth,
                IsPreviewVisible = EngagementPreviewVisible,
                IsBooleanEnabled = EngagementBooleanEnabled
            },
            ShaftFitRole.InstallationPocket when HasInstallation => binding with
            {
                IsPreviewVisible = InstallationPreviewVisible,
                IsBooleanEnabled = InstallationBooleanEnabled
            },
            _ => binding
        }).ToArray();
    }

    public FastenerComponentData ApplySmartProfile(FastenerComponentData component)
    {
        if (!component.AutoRecognizeHosts || component.SmartBindingProfile is not { } profile)
            return component;
        if (HasClearance)
        {
            profile = profile with
            {
                ClearancePreviewVisible = ClearancePreviewVisible,
                ClearanceBooleanEnabled = ClearanceBooleanEnabled
            };
        }
        if (HasEngagement)
        {
            profile = profile with
            {
                EngagementDepthMode = EngagementDepthMode,
                EngagementBlindDepth = EngagementBlindDepth,
                EngagementPreviewVisible = EngagementPreviewVisible,
                EngagementBooleanEnabled = EngagementBooleanEnabled
            };
        }
        return component with { SmartBindingProfile = profile };
    }
}
