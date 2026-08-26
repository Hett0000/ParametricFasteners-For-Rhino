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
    InstallationPocket,
    NutPocket
}

public enum DepthMode
{
    ThroughTarget,
    FastenerLengthPlusOneDiameter,
    FastenerLengthPlusCustom,
    // Legacy values are retained for JSON compatibility and migrated by ComponentJson.
    FastenerLengthPlusTwoDiameters,
    Blind
}

public enum HexNutStyle
{
    Standard,
    NylonInsertLocking
}

public enum SmartPlacementRecognitionMode
{
    Automatic,
    AllClearance,
    AllEngagement
}

public enum ScrewAssemblyMode
{
    ThreadEngagement,
    EngagementOnly,
    NutFastened
}

public enum EngagementEntryChamferMode
{
    AxialFortyFive,
    SurfaceEqualDistance
}

public enum HoleDiameterFormula
{
    LegacyStandardWithSharedCorrection,
    NominalIndependent
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

public sealed record LockingNutSizeSpec(
    string Designation,
    double AcrossFlats,
    double TotalHeight,
    string Standard,
    bool IsEngineeringExtension);

public sealed record DeliveryMetadata(
    string AssemblyNumber = "",
    string ProjectGroup = "",
    string UserNote = "");

public sealed record FastenerDefinitionSnapshot(
    Guid DefinitionId,
    string Name,
    FastenerKind Kind,
    string ThreadDesignation,
    string Source,
    string Revision,
    string Notes,
    FastenerSizeSpec SizeSpec,
    LockingNutSizeSpec? LockingNutSpec = null,
    double DefaultLength = 0,
    double DefaultInsertOuterDiameter = 0,
    double DefaultInsertDiameterCompensation = 0,
    double DefaultInsertDepthCompensation = 0);

public sealed record UserFastenerDefinition
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public Guid DefinitionId { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "用户尺寸";
    public FastenerKind Kind { get; init; } = FastenerKind.SocketCap;
    public string ThreadDesignation { get; init; } = "M3";
    public string Source { get; init; } = "用户定义";
    public string Revision { get; init; } = "1";
    public string Notes { get; init; } = "";
    public FastenerSizeSpec SizeSpec { get; init; } = new(
        "M3",
        3,
        0.5,
        new ClearanceDimensions(3.2, 3.2, 3.2),
        new HeadDimensions(5.5, 3, 6, 90, 5.5, 2, 5.5, 2.4));
    public LockingNutSizeSpec? LockingNutSpec { get; init; }
    public double DefaultLength { get; init; } = 12;
    public double DefaultInsertOuterDiameter { get; init; }
    public double DefaultInsertDiameterCompensation { get; init; }
    public double DefaultInsertDepthCompensation { get; init; } = 1;

    public FastenerDefinitionSnapshot Snapshot() => new(
        DefinitionId,
        Name.Trim(),
        Kind,
        ThreadDesignation.Trim(),
        Source.Trim(),
        Revision.Trim(),
        Notes.Trim(),
        SizeSpec with { Designation = ThreadDesignation.Trim() },
        LockingNutSpec,
        DefaultLength,
        DefaultInsertOuterDiameter,
        DefaultInsertDiameterCompensation,
        DefaultInsertDepthCompensation);
}

public sealed record UserFastenerLibraryDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public IReadOnlyList<UserFastenerDefinition> Definitions { get; init; } = [];
}

public sealed record LockingNutCatalogDocument(
    int SchemaVersion,
    string StandardFamily,
    string Disclaimer,
    IReadOnlyList<LockingNutSizeSpec> Sizes);

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
    bool EngagementBooleanEnabled,
    bool NutPocketPreviewVisible = true,
    bool NutPocketBooleanEnabled = true,
    double EngagementOnlyAlignmentDepth = 0,
    double EngagementOnlyAlignmentDiameterCompensation = 0.2);

public sealed record FastenerComponentData
{
    public const int CurrentSchemaVersion = 21;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public Guid ComponentId { get; init; } = Guid.NewGuid();
    public FastenerKind Kind { get; init; } = FastenerKind.SocketCap;
    public HexNutStyle HexNutStyle { get; init; } = HexNutStyle.Standard;
    public string Size { get; init; } = "M3";
    public double Length { get; init; } = 12;
    public double HeadEmbedDepth { get; init; }
    public bool CounterboreBridgeEnabled { get; init; }
    public double CounterboreBridgeLayerHeight { get; init; } = 0.2;
    public bool EngagementEntryChamferEnabled { get; init; }
    public double EngagementEntryChamferSize { get; init; } = 0.5;
    public EngagementEntryChamferMode EngagementEntryChamferMode { get; init; } =
        EngagementEntryChamferMode.AxialFortyFive;
    public double EngagementOnlyAlignmentDepth { get; init; } = 3;
    public double EngagementOnlyAlignmentDiameterCompensation { get; init; } = 0.2;
    public double InsertOuterDiameter { get; init; }
    public double InsertDiameterCompensation { get; init; }
    public double InsertDepthCompensation { get; init; }
    public PlacementFrame Placement { get; init; } = PlacementFrame.WorldXY;
    public PrintProfileSnapshot PrintProfile { get; init; } = new("默认 FDM", 0.2);
    public double FastenerOpacityPercent { get; init; } = 70;
    public double CutterOpacityPercent { get; init; } = 35;
    public HoleDiameterFormula HoleDiameterFormula { get; init; } =
        HoleDiameterFormula.NominalIndependent;
    public ScrewAssemblyMode AssemblyMode { get; init; } =
        ScrewAssemblyMode.ThreadEngagement;
    public HexNutStyle PairedNutStyle { get; init; } = HexNutStyle.Standard;
    public double NutTipProtrusion { get; init; } = 2;
    public double NutPocketCompensation { get; init; } = 0.2;
    public bool EngagementOnly { get; init; }
    public bool AutoRecognizeHosts { get; init; }
    /// <summary>
    /// A user-confirmed engagement host for smart thread-engagement assemblies.
    /// Empty means that host classification remains automatic.
    /// </summary>
    public Guid ConfirmedEngagementHostId { get; init; }
    public SmartPlacementRecognitionMode SmartRecognitionMode { get; init; } =
        SmartPlacementRecognitionMode.Automatic;
    public SmartBindingProfile? SmartBindingProfile { get; init; }
    public IReadOnlyList<HoleTargetBinding> Bindings { get; init; } = Array.Empty<HoleTargetBinding>();
    public Guid ProxyObjectId { get; init; }
    public Guid ControlPointObjectId { get; init; }
    public Guid AdoptedSourceObjectId { get; init; }
    public DeliveryMetadata Delivery { get; init; } = new();
    public Guid? CustomDefinitionId { get; init; }
    public string CustomDefinitionName { get; init; } = "";
    public FastenerDefinitionSnapshot? CustomDefinitionSnapshot { get; init; }
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
