using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;

namespace RhinoMM.Plugin.Services;

public sealed class EditorState
{
    private static readonly Lazy<EditorState> Lazy = new(() => new EditorState());
    public static EditorState Current => Lazy.Value;

    public FastenerKind Kind { get; set; } = FastenerKind.SocketCap;
    public HexNutStyle HexNutStyle { get; set; } = HexNutStyle.Standard;
    public string Size { get; set; } = "M3";
    public double Length { get; set; } = 12;
    public double HeadEmbedDepth { get; set; }
    public bool CounterboreBridgeEnabled { get; set; }
    public double CounterboreBridgeLayerHeight { get; set; } = 0.2;
    public double InsertOuterDiameter { get; set; }
    public double InsertDiameterCompensation { get; set; }
    public double InsertDepthCompensation { get; set; } = 1;
    public double PrinterCorrection { get; set; } = 0.2;
    public ShaftFitRole DefaultRole { get; set; } = ShaftFitRole.Clearance;
    public ClearanceFitClass ClearanceFit { get; set; } = ClearanceFitClass.Normal;
    public double BiteReduction { get; set; } = 0.35;
    public DepthMode EngagementDepthMode { get; set; } =
        DepthMode.FastenerLengthPlusOneDiameter;
    public double EngagementBlindDepth { get; set; } = 3;
    public double FastenerOpacityPercent { get; set; } = 70;
    public double CutterOpacityPercent { get; set; } = 35;
    public bool EngagementOnly { get; set; }
    public Guid LoadedComponentId { get; set; }

    public FastenerComponentData CreateDraft(PlacementFrame placement, IReadOnlyList<HoleTargetBinding> bindings) => new()
    {
        ComponentId = LoadedComponentId == Guid.Empty ? Guid.NewGuid() : LoadedComponentId,
        Kind = Kind,
        HexNutStyle = Kind == FastenerKind.HexNut
            ? HexNutStyle
            : HexNutStyle.Standard,
        Size = Size,
        Length = Length,
        HeadEmbedDepth = FastenerKindTraits.SupportsEmbedDepth(Kind) ? HeadEmbedDepth : 0,
        CounterboreBridgeEnabled = Kind == FastenerKind.SocketCap
            && CounterboreBridgeEnabled,
        CounterboreBridgeLayerHeight = CounterboreBridgeLayerHeight,
        InsertOuterDiameter = Kind == FastenerKind.HeatSetInsert ? InsertOuterDiameter : 0,
        InsertDiameterCompensation = Kind == FastenerKind.HeatSetInsert ? InsertDiameterCompensation : 0,
        InsertDepthCompensation = Kind == FastenerKind.HeatSetInsert ? InsertDepthCompensation : 0,
        Placement = placement,
        PrintProfile = new PrintProfileSnapshot("当前 FDM 配置", PrinterCorrection),
        FastenerOpacityPercent = FastenerOpacityPercent,
        CutterOpacityPercent = CutterOpacityPercent,
        EngagementOnly = !FastenerKindTraits.IsNut(Kind) && EngagementOnly,
        Bindings = bindings,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    public FastenerComponentData CreateUpdateDraft(
        FastenerComponentData existing,
        IReadOnlyList<HoleTargetBinding>? bindings = null) =>
        CaptureUpdateTemplate().ApplyTo(existing, bindings);

    internal FastenerUpdateTemplate CaptureUpdateTemplate() => new(
        Kind,
        HexNutStyle,
        Size,
        Length,
        HeadEmbedDepth,
        InsertOuterDiameter,
        InsertDiameterCompensation,
        InsertDepthCompensation,
        PrinterCorrection,
        ClearanceFit,
        BiteReduction,
        EngagementDepthMode,
        EngagementBlindDepth,
        FastenerOpacityPercent,
        CutterOpacityPercent,
        CounterboreBridgeEnabled,
        CounterboreBridgeLayerHeight,
        EngagementOnly);

    public void Load(FastenerComponentData component)
    {
        Kind = component.Kind;
        HexNutStyle = component.HexNutStyle;
        Size = component.Size;
        Length = component.Length;
        HeadEmbedDepth = component.HeadEmbedDepth;
        CounterboreBridgeEnabled = component.CounterboreBridgeEnabled;
        CounterboreBridgeLayerHeight = component.CounterboreBridgeLayerHeight;
        InsertOuterDiameter = component.InsertOuterDiameter;
        InsertDiameterCompensation = component.InsertDiameterCompensation;
        InsertDepthCompensation = component.InsertDepthCompensation;
        EngagementOnly = component.EngagementOnly;
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
            {
                BiteReduction = engagementBinding.BiteReduction;
                EngagementDepthMode = engagementBinding.DepthMode;
                EngagementBlindDepth = engagementBinding.BlindDepth;
            }
            else if (component.SmartBindingProfile is { } profile)
            {
                BiteReduction = profile.BiteReduction;
                EngagementDepthMode = profile.EngagementDepthMode;
                EngagementBlindDepth = profile.EngagementBlindDepth;
            }
        }
    }
}
