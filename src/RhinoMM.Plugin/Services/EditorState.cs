using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;

namespace RhinoMM.Plugin.Services;

public sealed class EditorState
{
    private static readonly Lazy<EditorState> Lazy = new(() => new EditorState());
    public static EditorState Current => Lazy.Value;

    public FastenerKind Kind { get; set; } = FastenerKind.SocketCap;
    public string Size { get; set; } = "M3";
    public double Length { get; set; } = 12;
    public double HeadEmbedDepth { get; set; }
    public double InsertOuterDiameter { get; set; }
    public double InsertDiameterCompensation { get; set; }
    public double InsertDepthCompensation { get; set; } = 1;
    public double PrinterCorrection { get; set; } = 0.2;
    public ShaftFitRole DefaultRole { get; set; } = ShaftFitRole.Clearance;
    public ClearanceFitClass ClearanceFit { get; set; } = ClearanceFitClass.Normal;
    public double BiteReduction { get; set; } = 0.35;
    public double FastenerOpacityPercent { get; set; } = 70;
    public double CutterOpacityPercent { get; set; } = 35;
    public Guid LoadedComponentId { get; set; }

    public FastenerComponentData CreateDraft(PlacementFrame placement, IReadOnlyList<HoleTargetBinding> bindings) => new()
    {
        ComponentId = LoadedComponentId == Guid.Empty ? Guid.NewGuid() : LoadedComponentId,
        Kind = Kind,
        Size = Size,
        Length = Length,
        HeadEmbedDepth = FastenerKindTraits.SupportsEmbedDepth(Kind) ? HeadEmbedDepth : 0,
        InsertOuterDiameter = Kind == FastenerKind.HeatSetInsert ? InsertOuterDiameter : 0,
        InsertDiameterCompensation = Kind == FastenerKind.HeatSetInsert ? InsertDiameterCompensation : 0,
        InsertDepthCompensation = Kind == FastenerKind.HeatSetInsert ? InsertDepthCompensation : 0,
        Placement = placement,
        PrintProfile = new PrintProfileSnapshot("当前 FDM 配置", PrinterCorrection),
        FastenerOpacityPercent = FastenerOpacityPercent,
        CutterOpacityPercent = CutterOpacityPercent,
        Bindings = bindings,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    public FastenerComponentData CreateUpdateDraft(
        FastenerComponentData existing,
        IReadOnlyList<HoleTargetBinding>? bindings = null)
    {
        var updatedBindings = (bindings ?? existing.Bindings)
            .Select(binding => binding.Role switch
            {
                ShaftFitRole.Clearance => binding with { ClearanceFit = ClearanceFit },
                ShaftFitRole.ThreadEngagement => binding with { BiteReduction = BiteReduction },
                ShaftFitRole.InstallationPocket when Kind == FastenerKind.HeatSetInsert => binding with
                {
                    DepthMode = DepthMode.Blind,
                    BlindDepth = Length + InsertDepthCompensation,
                    IncludeHeadSeat = false
                },
                ShaftFitRole.InstallationPocket when Kind == FastenerKind.HexNut => binding with
                {
                    DepthMode = DepthMode.Blind,
                    BlindDepth = HeadEmbedDepth,
                    IncludeHeadSeat = false
                },
                _ => binding
            })
            .ToArray();
        var smartProfile = UpdateSmartBindingProfile(existing, updatedBindings);
        return CreateDraft(existing.Placement, updatedBindings) with
        {
            ComponentId = existing.ComponentId,
            AdoptedSourceObjectId = existing.AdoptedSourceObjectId,
            AutoRecognizeHosts = existing.AutoRecognizeHosts,
            SmartRecognitionMode = existing.SmartRecognitionMode,
            SmartBindingProfile = smartProfile
        };
    }

    private SmartBindingProfile? UpdateSmartBindingProfile(
        FastenerComponentData existing,
        IReadOnlyList<HoleTargetBinding> bindings)
    {
        if (!existing.AutoRecognizeHosts || existing.SmartBindingProfile is not { } profile)
            return existing.SmartBindingProfile;

        profile = profile with
        {
            ClearanceFit = ClearanceFit,
            BiteReduction = BiteReduction
        };
        var clearance = bindings.FirstOrDefault(item => item.Role == ShaftFitRole.Clearance);
        if (clearance is not null)
        {
            profile = profile with
            {
                ClearanceFit = clearance.ClearanceFit,
                ClearancePreviewVisible = clearance.IsPreviewVisible,
                ClearanceBooleanEnabled = clearance.IsBooleanEnabled
            };
        }
        var engagement = bindings.FirstOrDefault(item => item.Role == ShaftFitRole.ThreadEngagement);
        if (engagement is not null)
        {
            profile = profile with
            {
                BiteReduction = engagement.BiteReduction,
                EngagementDepthMode = engagement.DepthMode,
                EngagementBlindDepth = engagement.BlindDepth,
                EngagementPreviewVisible = engagement.IsPreviewVisible,
                EngagementBooleanEnabled = engagement.IsBooleanEnabled
            };
        }
        return profile;
    }

    public void Load(FastenerComponentData component)
    {
        Kind = component.Kind;
        Size = component.Size;
        Length = component.Length;
        HeadEmbedDepth = component.HeadEmbedDepth;
        InsertOuterDiameter = component.InsertOuterDiameter;
        InsertDiameterCompensation = component.InsertDiameterCompensation;
        InsertDepthCompensation = component.InsertDepthCompensation;
        PrinterCorrection = component.PrintProfile.HoleDiameterCorrection;
        LoadedComponentId = component.ComponentId;
        if (component.Bindings.Count > 0)
        {
            DefaultRole = component.Bindings[0].Role;
            var clearanceBinding = component.Bindings.FirstOrDefault(
                binding => binding.Role == ShaftFitRole.Clearance);
            if (clearanceBinding is not null)
                ClearanceFit = clearanceBinding.ClearanceFit;

            var engagementBinding = component.Bindings.FirstOrDefault(
                binding => binding.Role == ShaftFitRole.ThreadEngagement);
            if (engagementBinding is not null)
                BiteReduction = engagementBinding.BiteReduction;
        }
    }
}
