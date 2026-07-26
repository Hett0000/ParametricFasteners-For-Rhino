namespace RhinoMM.Core.Domain;

public enum FastenerKind
{
    SocketCap,
    Countersunk,
    HexBolt,
    HexNut,
    HeatSetInsert
}

public enum ClearanceFitClass
{
    Close,
    Normal,
    Loose
}

public enum ShaftFitRole
{
    Clearance,
    ThreadEngagement,
    InstallationPocket
}

public enum DepthMode
{
    ThroughTarget,
    FastenerLengthPlusOneDiameter,
    FastenerLengthPlusTwoDiameters,
    Blind
}

public enum SmartPlacementRecognitionMode
{
    Automatic,
    AllClearance,
    AllEngagement
}

public sealed record ClearanceDimensions(double Close, double Normal, double Loose)
{
    public double For(ClearanceFitClass fitClass) => fitClass switch
    {
        ClearanceFitClass.Close => Close,
        ClearanceFitClass.Normal => Normal,
        ClearanceFitClass.Loose => Loose,
        _ => Normal
    };
}

public sealed record HeadDimensions(
    double SocketDiameter,
    double SocketHeight,
    double CountersunkDiameter,
    double CountersunkAngle,
    double HexAcrossFlats,
    double HexHeight,
    double NutAcrossFlats,
    double NutThickness);

public sealed record FastenerSizeSpec(
    string Designation,
    double NominalDiameter,
    double CoarsePitch,
    ClearanceDimensions Clearance,
    HeadDimensions Head);

public sealed record FastenerCatalogDocument(
    int SchemaVersion,
    string StandardFamily,
    string Disclaimer,
    IReadOnlyList<FastenerSizeSpec> Sizes);

public sealed record PrintProfileSnapshot(
    string Name,
    double HoleDiameterCorrection,
    string Material = "Generic",
    double NozzleDiameter = 0.4);

public sealed record PlacementFrame(
    double OriginX,
    double OriginY,
    double OriginZ,
    double XAxisX,
    double XAxisY,
    double XAxisZ,
    double YAxisX,
    double YAxisY,
    double YAxisZ,
    double ZAxisX,
    double ZAxisY,
    double ZAxisZ)
{
    public static PlacementFrame WorldXY { get; } = new(
        0, 0, 0,
        1, 0, 0,
        0, 1, 0,
        0, 0, 1);
}

public sealed record HoleTargetBinding
{
    public Guid BindingId { get; init; } = Guid.NewGuid();
    public Guid TargetObjectId { get; init; }
    public ShaftFitRole Role { get; init; } = ShaftFitRole.Clearance;
    public ClearanceFitClass ClearanceFit { get; init; } = ClearanceFitClass.Normal;
    public double BiteReduction { get; init; }
    public double BindingOverride { get; init; }
    public DepthMode DepthMode { get; init; } = DepthMode.ThroughTarget;
    public double BlindDepth { get; init; }
    public bool IncludeHeadSeat { get; init; }
    public bool IsPreviewVisible { get; init; } = true;
    public bool IsBooleanEnabled { get; init; } = true;
    public Guid CutterObjectId { get; init; }
}

public sealed record SmartBindingProfile(
    ClearanceFitClass ClearanceFit,
    double BiteReduction,
    DepthMode EngagementDepthMode,
    double EngagementBlindDepth,
    bool ClearancePreviewVisible,
    bool ClearanceBooleanEnabled,
    bool EngagementPreviewVisible,
    bool EngagementBooleanEnabled);

public sealed record FastenerComponentData
{
    public const int CurrentSchemaVersion = 8;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public Guid ComponentId { get; init; } = Guid.NewGuid();
    public FastenerKind Kind { get; init; } = FastenerKind.SocketCap;
    public string Size { get; init; } = "M3";
    public double Length { get; init; } = 12;
    public double HeadEmbedDepth { get; init; }
    public double InsertOuterDiameter { get; init; }
    public double InsertDiameterCompensation { get; init; }
    public double InsertDepthCompensation { get; init; }
    public PlacementFrame Placement { get; init; } = PlacementFrame.WorldXY;
    public PrintProfileSnapshot PrintProfile { get; init; } = new("默认 FDM", 0.2);
    public double FastenerOpacityPercent { get; init; } = 70;
    public double CutterOpacityPercent { get; init; } = 35;
    public bool AutoRecognizeHosts { get; init; }
    public SmartPlacementRecognitionMode SmartRecognitionMode { get; init; } =
        SmartPlacementRecognitionMode.Automatic;
    public SmartBindingProfile? SmartBindingProfile { get; init; }
    public IReadOnlyList<HoleTargetBinding> Bindings { get; init; } = Array.Empty<HoleTargetBinding>();
    public Guid ProxyObjectId { get; init; }
    public Guid ControlPointObjectId { get; init; }
    public Guid AdoptedSourceObjectId { get; init; }
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record HoleDiameterResult(
    double BaseDiameter,
    double PrinterCorrection,
    double BindingOverride,
    double FinalDiameter);

public sealed record ValidationIssue(string Code, string Message, bool IsError = true);

public sealed class ValidationResult
{
    public List<ValidationIssue> Issues { get; } = [];
    public bool IsValid => Issues.All(issue => !issue.IsError);
}
