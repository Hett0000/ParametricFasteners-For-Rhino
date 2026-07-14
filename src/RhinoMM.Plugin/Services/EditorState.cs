using RhinoMM.Core.Domain;

namespace RhinoMM.Plugin.Services;

public sealed class EditorState
{
    private static readonly Lazy<EditorState> Lazy = new(() => new EditorState());
    public static EditorState Current => Lazy.Value;

    public FastenerKind Kind { get; set; } = FastenerKind.SocketCap;
    public string Size { get; set; } = "M3";
    public double Length { get; set; } = 12;
    public double PrinterCorrection { get; set; } = 0.2;
    public ShaftFitRole DefaultRole { get; set; } = ShaftFitRole.Clearance;
    public ClearanceFitClass ClearanceFit { get; set; } = ClearanceFitClass.Normal;
    public double BiteReduction { get; set; } = 0.35;
    public Guid LoadedComponentId { get; set; }

    public FastenerComponentData CreateDraft(PlacementFrame placement, IReadOnlyList<HoleTargetBinding> bindings) => new()
    {
        ComponentId = LoadedComponentId == Guid.Empty ? Guid.NewGuid() : LoadedComponentId,
        Kind = Kind,
        Size = Size,
        Length = Length,
        Placement = placement,
        PrintProfile = new PrintProfileSnapshot("当前 FDM 配置", PrinterCorrection),
        Bindings = bindings,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    public void Load(FastenerComponentData component)
    {
        Kind = component.Kind;
        Size = component.Size;
        Length = component.Length;
        PrinterCorrection = component.PrintProfile.HoleDiameterCorrection;
        LoadedComponentId = component.ComponentId;
        if (component.Bindings.Count > 0)
        {
            DefaultRole = component.Bindings[0].Role;
            ClearanceFit = component.Bindings[0].ClearanceFit;
            BiteReduction = component.Bindings[0].BiteReduction;
        }
    }
}
