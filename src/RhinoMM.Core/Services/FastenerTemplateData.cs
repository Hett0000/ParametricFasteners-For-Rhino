using System.Globalization;
using System.Text;
using System.Text.Json;
using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public sealed record FastenerTemplateData
{
    public const int CurrentSchemaVersion = 5;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public FastenerKind Kind { get; init; } = FastenerKind.SocketCap;
    public HexNutStyle NutStyle { get; init; } = HexNutStyle.Standard;
    public string Size { get; init; } = "M3";
    public double Length { get; init; } = 12;
    public double HeadEmbedDepth { get; init; }
    public double InsertOuterDiameter { get; init; }
    public double InsertDiameterCompensation { get; init; }
    public double InsertDepthCompensation { get; init; } = 1;
    public double PrinterCorrection { get; init; } = 0.2;
    public double BiteReduction { get; init; } = 0.35;
    public DepthMode EngagementDepthMode { get; init; } =
        DepthMode.FastenerLengthPlusOneDiameter;
    public double EngagementBlindDepth { get; init; } = 3;
    public bool CounterboreBridgeEnabled { get; init; }
    public double CounterboreBridgeLayerHeight { get; init; } = 0.2;
    public bool EngagementEntryChamferEnabled { get; init; }
    public double EngagementEntryChamferSize { get; init; } = 0.5;
    public EngagementEntryChamferMode EngagementEntryChamferMode { get; init; } =
        EngagementEntryChamferMode.AxialFortyFive;
    public double EngagementOnlyAlignmentDepth { get; init; } = 3;
    public double EngagementOnlyAlignmentDiameterCompensation { get; init; } = 0.2;
    public ScrewAssemblyMode AssemblyMode { get; init; } =
        ScrewAssemblyMode.ThreadEngagement;
    public HexNutStyle PairedNutStyle { get; init; } = HexNutStyle.Standard;
    public double NutTipProtrusion { get; init; } = 2;
    public double NutPocketCompensation { get; init; } = 0.2;
    public bool? ClearancePreviewVisible { get; init; }
    public bool? ClearanceBooleanEnabled { get; init; }
    public bool? EngagementPreviewVisible { get; init; }
    public bool? EngagementBooleanEnabled { get; init; }
    public bool? InstallationPreviewVisible { get; init; }
    public bool? InstallationBooleanEnabled { get; init; }
    public bool? NutPocketPreviewVisible { get; init; }
    public bool? NutPocketBooleanEnabled { get; init; }
    public Guid? CustomDefinitionId { get; init; }
    public string CustomDefinitionName { get; init; } = "";
    public FastenerDefinitionSnapshot? CustomDefinitionSnapshot { get; init; }

    public FastenerUpdateTemplate ToUpdateTemplate(
        double fastenerOpacityPercent,
        double cutterOpacityPercent) => new(
        Kind,
        NutStyle,
        Size,
        Length,
        HeadEmbedDepth,
        InsertOuterDiameter,
        InsertDiameterCompensation,
        InsertDepthCompensation,
        PrinterCorrection,
        ClearanceFitClass.Normal,
        BiteReduction,
        EngagementDepthMode,
        EngagementBlindDepth,
        fastenerOpacityPercent,
        cutterOpacityPercent,
        CounterboreBridgeEnabled,
        CounterboreBridgeLayerHeight,
        AssemblyMode == ScrewAssemblyMode.EngagementOnly,
        ClearancePreviewVisible,
        ClearanceBooleanEnabled,
        EngagementPreviewVisible,
        EngagementBooleanEnabled,
        InstallationPreviewVisible,
        InstallationBooleanEnabled,
        AssemblyMode,
        PairedNutStyle,
        NutTipProtrusion,
        NutPocketCompensation,
        NutPocketPreviewVisible,
        NutPocketBooleanEnabled,
        CustomDefinitionId,
        CustomDefinitionName,
        CustomDefinitionSnapshot,
        EngagementEntryChamferEnabled,
        EngagementEntryChamferSize,
        EngagementEntryChamferMode,
        EngagementOnlyAlignmentDepth,
        EngagementOnlyAlignmentDiameterCompensation);

    public string Signature()
    {
        var value = Normalize();
        var builder = new StringBuilder();
        void Add(object? item) => builder.Append(item).Append('|');
        void AddNumber(double item) => Add(item.ToString("R", CultureInfo.InvariantCulture));

        Add(value.Kind);
        Add(value.NutStyle);
        Add(value.Size.Trim().ToUpperInvariant());
        AddNumber(value.Length);
        AddNumber(value.HeadEmbedDepth);
        AddNumber(value.InsertOuterDiameter);
        AddNumber(value.InsertDiameterCompensation);
        AddNumber(value.InsertDepthCompensation);
        AddNumber(value.PrinterCorrection);
        AddNumber(value.BiteReduction);
        Add(value.EngagementDepthMode);
        AddNumber(value.EngagementBlindDepth);
        Add(value.CounterboreBridgeEnabled);
        AddNumber(value.CounterboreBridgeLayerHeight);
        Add(value.EngagementEntryChamferEnabled);
        AddNumber(value.EngagementEntryChamferSize);
        Add(value.EngagementEntryChamferMode);
        AddNumber(value.EngagementOnlyAlignmentDepth);
        AddNumber(value.EngagementOnlyAlignmentDiameterCompensation);
        Add(value.AssemblyMode);
        Add(value.PairedNutStyle);
        AddNumber(value.NutTipProtrusion);
        AddNumber(value.NutPocketCompensation);
        Add(value.ClearancePreviewVisible);
        Add(value.ClearanceBooleanEnabled);
        Add(value.EngagementPreviewVisible);
        Add(value.EngagementBooleanEnabled);
        Add(value.InstallationPreviewVisible);
        Add(value.InstallationBooleanEnabled);
        Add(value.NutPocketPreviewVisible);
        Add(value.NutPocketBooleanEnabled);
        Add(value.CustomDefinitionId);
        Add(value.CustomDefinitionName);
        Add(value.CustomDefinitionSnapshot?.Revision);
        return builder.ToString();
    }

    public FastenerTemplateData Normalize()
    {
        var isScrew = FastenerKindTraits.IsScrew(Kind);
        var isHexNut = Kind == FastenerKind.HexNut;
        var isInsert = Kind == FastenerKind.HeatSetInsert;
        var assembly = isScrew ? AssemblyMode : ScrewAssemblyMode.ThreadEngagement;
        return this with
        {
            SchemaVersion = CurrentSchemaVersion,
            NutStyle = isHexNut ? NutStyle : HexNutStyle.Standard,
            Length = FastenerKindTraits.UsesLengthInStatistics(Kind) ? Length : 0,
            HeadEmbedDepth = FastenerKindTraits.SupportsEmbedDepth(Kind) ? HeadEmbedDepth : 0,
            InsertOuterDiameter = isInsert ? InsertOuterDiameter : 0,
            InsertDiameterCompensation = isInsert ? InsertDiameterCompensation : 0,
            InsertDepthCompensation = isInsert ? InsertDepthCompensation : 0,
            PrinterCorrection = isInsert ? 0 : PrinterCorrection,
            BiteReduction = isScrew ? BiteReduction : 0.35,
            EngagementDepthMode = isScrew
                ? EngagementDepthMode
                : DepthMode.FastenerLengthPlusOneDiameter,
            EngagementBlindDepth = isScrew ? EngagementBlindDepth : 3,
            CounterboreBridgeEnabled = Kind == FastenerKind.SocketCap
                && HeadEmbedDepth > 0
                && CounterboreBridgeEnabled,
            CounterboreBridgeLayerHeight = Kind == FastenerKind.SocketCap
                ? CounterboreBridgeLayerHeight
                : 0.2,
            EngagementEntryChamferEnabled = isScrew
                && assembly == ScrewAssemblyMode.ThreadEngagement
                && EngagementEntryChamferEnabled,
            EngagementEntryChamferSize = double.IsFinite(EngagementEntryChamferSize)
                && EngagementEntryChamferSize is >= 0.05 and <= 5.0
                    ? EngagementEntryChamferSize
                    : 0.5,
            EngagementEntryChamferMode = Enum.IsDefined(EngagementEntryChamferMode)
                ? EngagementEntryChamferMode
                : EngagementEntryChamferMode.AxialFortyFive,
            EngagementOnlyAlignmentDepth = isScrew
                && double.IsFinite(EngagementOnlyAlignmentDepth)
                && EngagementOnlyAlignmentDepth is >= 0 and <= 1000
                    ? EngagementOnlyAlignmentDepth
                    : 0,
            EngagementOnlyAlignmentDiameterCompensation = isScrew
                && double.IsFinite(EngagementOnlyAlignmentDiameterCompensation)
                && EngagementOnlyAlignmentDiameterCompensation is >= 0 and <= 5
                    ? EngagementOnlyAlignmentDiameterCompensation
                    : 0.2,
            AssemblyMode = assembly,
            PairedNutStyle = isScrew
                ? PairedNutStyle
                : HexNutStyle.Standard,
            NutTipProtrusion = isScrew
                ? NutTipProtrusion
                : 2,
            NutPocketCompensation = isScrew
                ? NutPocketCompensation
                : 0.2,
            NutPocketPreviewVisible = isScrew
                ? NutPocketPreviewVisible
                : null,
            NutPocketBooleanEnabled = isScrew
                ? NutPocketBooleanEnabled
                : null,
            ClearancePreviewVisible = isScrew ? ClearancePreviewVisible : null,
            ClearanceBooleanEnabled = isScrew ? ClearanceBooleanEnabled : null,
            EngagementPreviewVisible = isScrew ? EngagementPreviewVisible : null,
            EngagementBooleanEnabled = isScrew ? EngagementBooleanEnabled : null,
            InstallationPreviewVisible = FastenerKindTraits.IsNut(Kind)
                ? InstallationPreviewVisible
                : null,
            InstallationBooleanEnabled = FastenerKindTraits.IsNut(Kind)
                ? InstallationBooleanEnabled
                : null,
            CustomDefinitionId = CustomDefinitionSnapshot is null ? null : CustomDefinitionId,
            CustomDefinitionName = CustomDefinitionSnapshot is null ? string.Empty : CustomDefinitionName.Trim()
        };
    }

    public static FastenerTemplateData FromUpdateTemplate(FastenerUpdateTemplate template) =>
        new FastenerTemplateData()
        {
            Kind = template.Kind,
            NutStyle = template.NutStyle,
            Size = template.Size,
            Length = template.Length,
            HeadEmbedDepth = template.HeadEmbedDepth,
            InsertOuterDiameter = template.InsertOuterDiameter,
            InsertDiameterCompensation = template.InsertDiameterCompensation,
            InsertDepthCompensation = template.InsertDepthCompensation,
            PrinterCorrection = template.PrinterCorrection,
            BiteReduction = template.BiteReduction,
            EngagementDepthMode = template.EngagementDepthMode,
            EngagementBlindDepth = template.EngagementBlindDepth,
            CounterboreBridgeEnabled = template.CounterboreBridgeEnabled,
            CounterboreBridgeLayerHeight = template.CounterboreBridgeLayerHeight,
            EngagementEntryChamferEnabled = template.EngagementEntryChamferEnabled,
            EngagementEntryChamferSize = template.EngagementEntryChamferSize,
            EngagementEntryChamferMode = template.EngagementEntryChamferMode,
            EngagementOnlyAlignmentDepth = template.EngagementOnlyAlignmentDepth,
            EngagementOnlyAlignmentDiameterCompensation =
                template.EngagementOnlyAlignmentDiameterCompensation,
            AssemblyMode = template.AssemblyMode,
            PairedNutStyle = template.PairedNutStyle,
            NutTipProtrusion = template.NutTipProtrusion,
            NutPocketCompensation = template.NutPocketCompensation,
            ClearancePreviewVisible = template.ClearancePreviewVisible,
            ClearanceBooleanEnabled = template.ClearanceBooleanEnabled,
            EngagementPreviewVisible = template.EngagementPreviewVisible,
            EngagementBooleanEnabled = template.EngagementBooleanEnabled,
            InstallationPreviewVisible = template.InstallationPreviewVisible,
            InstallationBooleanEnabled = template.InstallationBooleanEnabled,
            NutPocketPreviewVisible = template.NutPocketPreviewVisible,
            NutPocketBooleanEnabled = template.NutPocketBooleanEnabled,
            CustomDefinitionId = template.CustomDefinitionId,
            CustomDefinitionName = template.CustomDefinitionName,
            CustomDefinitionSnapshot = template.CustomDefinitionSnapshot
        }.Normalize();

    public static FastenerTemplateData FromComponent(FastenerComponentData component)
    {
        var clearance = component.Bindings.FirstOrDefault(binding =>
            binding.Role == ShaftFitRole.Clearance);
        var engagement = component.Bindings.FirstOrDefault(binding =>
            binding.Role == ShaftFitRole.ThreadEngagement);
        var installation = component.Bindings.FirstOrDefault(binding =>
            binding.Role == ShaftFitRole.InstallationPocket);
        var nutPocket = component.Bindings.FirstOrDefault(binding =>
            binding.Role == ShaftFitRole.NutPocket);
        var profile = component.SmartBindingProfile;
        return new FastenerTemplateData
        {
            Kind = component.Kind,
            NutStyle = component.HexNutStyle,
            Size = component.Size,
            Length = component.Length,
            HeadEmbedDepth = component.HeadEmbedDepth,
            InsertOuterDiameter = component.InsertOuterDiameter,
            InsertDiameterCompensation = component.InsertDiameterCompensation,
            InsertDepthCompensation = component.InsertDepthCompensation,
            PrinterCorrection = component.PrintProfile.HoleDiameterCorrection,
            BiteReduction = engagement?.BiteReduction ?? profile?.BiteReduction ?? 0.35,
            EngagementDepthMode = engagement?.DepthMode
                ?? profile?.EngagementDepthMode
                ?? DepthMode.FastenerLengthPlusOneDiameter,
            EngagementBlindDepth = engagement?.BlindDepth
                ?? profile?.EngagementBlindDepth
                ?? 3,
            CounterboreBridgeEnabled = component.CounterboreBridgeEnabled,
            CounterboreBridgeLayerHeight = component.CounterboreBridgeLayerHeight,
            EngagementEntryChamferEnabled = component.EngagementEntryChamferEnabled,
            EngagementEntryChamferSize = component.EngagementEntryChamferSize,
            EngagementEntryChamferMode = component.EngagementEntryChamferMode,
            EngagementOnlyAlignmentDepth = component.EngagementOnlyAlignmentDepth,
            EngagementOnlyAlignmentDiameterCompensation =
                component.EngagementOnlyAlignmentDiameterCompensation,
            AssemblyMode = component.AssemblyMode,
            PairedNutStyle = component.PairedNutStyle,
            NutTipProtrusion = component.NutTipProtrusion,
            NutPocketCompensation = component.NutPocketCompensation,
            ClearancePreviewVisible = clearance?.IsPreviewVisible
                ?? profile?.ClearancePreviewVisible,
            ClearanceBooleanEnabled = clearance?.IsBooleanEnabled
                ?? profile?.ClearanceBooleanEnabled,
            EngagementPreviewVisible = engagement?.IsPreviewVisible
                ?? profile?.EngagementPreviewVisible,
            EngagementBooleanEnabled = engagement?.IsBooleanEnabled
                ?? profile?.EngagementBooleanEnabled,
            InstallationPreviewVisible = installation?.IsPreviewVisible,
            InstallationBooleanEnabled = installation?.IsBooleanEnabled,
            NutPocketPreviewVisible = nutPocket?.IsPreviewVisible
                ?? profile?.NutPocketPreviewVisible,
            NutPocketBooleanEnabled = nutPocket?.IsBooleanEnabled
                ?? profile?.NutPocketBooleanEnabled,
            CustomDefinitionId = component.CustomDefinitionId,
            CustomDefinitionName = component.CustomDefinitionName,
            CustomDefinitionSnapshot = component.CustomDefinitionSnapshot
        }.Normalize();
    }

    public static bool TryDeserialize(string json, out FastenerTemplateData data)
    {
        try
        {
            data = JsonSerializer.Deserialize<FastenerTemplateData>(json, JsonOptions())
                ?? new FastenerTemplateData();
            if (data.SchemaVersion is < 1 or > CurrentSchemaVersion)
                return false;
            if (data.SchemaVersion < 5)
            {
                data = data with
                {
                    EngagementOnlyAlignmentDepth = 0,
                    EngagementOnlyAlignmentDiameterCompensation = 0.2
                };
            }
            data = data with { SchemaVersion = CurrentSchemaVersion };
            data = data.Normalize();
            return true;
        }
        catch
        {
            data = new FastenerTemplateData();
            return false;
        }
    }

    public static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
}

public sealed record FastenerTemplateEntry(
    Guid Id,
    string Name,
    FastenerTemplateData Data,
    DateTimeOffset UpdatedAt,
    AssemblySchemeOptions? Scheme = null);

public sealed record FastenerTemplateLibraryDocument
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public IReadOnlyList<FastenerTemplateEntry> Recent { get; init; } = [];
    public IReadOnlyList<FastenerTemplateEntry> Favorites { get; init; } = [];
    public FastenerTemplateData? LastPlacement { get; init; }
    public FastenerTemplateData? LastUpdate { get; init; }
}
