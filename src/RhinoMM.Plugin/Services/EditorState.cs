using RhinoMM.Core.Domain;

namespace RhinoMM.Plugin.Services;

public sealed class EditorState
{
    private static readonly Lazy<EditorState> Lazy = new(() => new EditorState());
    public static EditorState Current => Lazy.Value;

    public FastenerKind Kind { get; set; } = FastenerKind.SocketCap;
    public string Size { get; set; } = "M3";
    public double Length { get; set; } = 12;
    public double HeadEmbedDepth { get; set; }
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
        HeadEmbedDepth = Kind == FastenerKind.HexNut ? 0 : HeadEmbedDepth,
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
                _ => binding
            })
            .ToArray();
        return CreateDraft(existing.Placement, updatedBindings) with
        {
            ComponentId = existing.ComponentId,
            AdoptedSourceObjectId = existing.AdoptedSourceObjectId,
            FastenerOpacityPercent = existing.FastenerOpacityPercent,
            CutterOpacityPercent = existing.CutterOpacityPercent
        };
    }

    public void Load(FastenerComponentData component)
    {
        Kind = component.Kind;
        Size = component.Size;
        Length = component.Length;
        HeadEmbedDepth = component.HeadEmbedDepth;
        PrinterCorrection = component.PrintProfile.HoleDiameterCorrection;
        FastenerOpacityPercent = component.FastenerOpacityPercent;
        CutterOpacityPercent = component.CutterOpacityPercent;
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
