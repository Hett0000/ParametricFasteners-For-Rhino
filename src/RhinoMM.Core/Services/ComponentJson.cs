using System.Text.Json;
using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class ComponentJson
{
    private static readonly Lazy<FastenerCatalog> Catalog = new(FastenerCatalog.LoadEmbedded);

    public static string Serialize(FastenerComponentData data) =>
        JsonSerializer.Serialize(data, JsonOptions.Default);

    public static FastenerComponentData Deserialize(string json)
    {
        var data = JsonSerializer.Deserialize<FastenerComponentData>(json, JsonOptions.Default)
            ?? throw new InvalidOperationException("组件元数据为空或格式错误。");
        return Migrate(data);
    }

    public static int ReadStoredSchemaVersion(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return 0;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("schemaVersion", out var value)
                && value.TryGetInt32(out var version)
                ? version
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    public static FastenerComponentData Migrate(FastenerComponentData data)
    {
        if (data.SchemaVersion >= FastenerComponentData.CurrentSchemaVersion)
            return data;

        var sourceVersion = data.SchemaVersion;
        var migratedHeadEmbedDepth = MigrateHeadEmbedDepth(data, sourceVersion);
        var assemblyMode = sourceVersion < 16
            ? sourceVersion >= 12 && data.EngagementOnly
                ? ScrewAssemblyMode.EngagementOnly
                : ScrewAssemblyMode.ThreadEngagement
            : data.AssemblyMode;
        return data with
        {
            SchemaVersion = FastenerComponentData.CurrentSchemaVersion,
            HexNutStyle = sourceVersion < 15
                ? HexNutStyle.Standard
                : data.HexNutStyle,
            FastenerOpacityPercent = sourceVersion < 2 ? 70 : data.FastenerOpacityPercent,
            CutterOpacityPercent = sourceVersion < 2 ? 35 : data.CutterOpacityPercent,
            HoleDiameterFormula = sourceVersion < 14
                ? HoleDiameterFormula.LegacyStandardWithSharedCorrection
                : data.HoleDiameterFormula,
            HeadEmbedDepth = migratedHeadEmbedDepth,
            CounterboreBridgeEnabled = sourceVersion >= 11
                && data.Kind == FastenerKind.SocketCap
                && data.CounterboreBridgeEnabled,
            CounterboreBridgeLayerHeight = sourceVersion < 11
                || !double.IsFinite(data.CounterboreBridgeLayerHeight)
                || data.CounterboreBridgeLayerHeight <= 0
                    ? 0.2
                    : data.CounterboreBridgeLayerHeight,
            EngagementEntryChamferEnabled = sourceVersion >= 18
                && data.AssemblyMode == ScrewAssemblyMode.ThreadEngagement
                && data.EngagementEntryChamferEnabled,
            EngagementEntryChamferSize = sourceVersion < 18
                || !double.IsFinite(data.EngagementEntryChamferSize)
                || data.EngagementEntryChamferSize <= 0
                    ? 0.5
                    : data.EngagementEntryChamferSize,
            EngagementEntryChamferMode = sourceVersion < 19
                || !Enum.IsDefined(data.EngagementEntryChamferMode)
                    ? EngagementEntryChamferMode.AxialFortyFive
                    : data.EngagementEntryChamferMode,
            EngagementOnlyAlignmentDepth = sourceVersion < 20
                ? 0
                : data.EngagementOnlyAlignmentDepth,
            EngagementOnlyAlignmentDiameterCompensation = sourceVersion < 20
                ? 0.2
                : data.EngagementOnlyAlignmentDiameterCompensation,
            InsertDepthCompensation = sourceVersion < 7 ? 0 : data.InsertDepthCompensation,
            AssemblyMode = FastenerKindTraits.IsScrew(data.Kind)
                ? assemblyMode
                : ScrewAssemblyMode.ThreadEngagement,
            PairedNutStyle = sourceVersion < 16
                ? HexNutStyle.Standard
                : data.PairedNutStyle,
            NutTipProtrusion = sourceVersion < 16
                ? 2
                : data.NutTipProtrusion,
            NutPocketCompensation = sourceVersion < 16
                ? 0.2
                : data.NutPocketCompensation,
            Delivery = sourceVersion < 17 || data.Delivery is null
                ? new DeliveryMetadata()
                : data.Delivery,
            CustomDefinitionId = sourceVersion < 17 ? null : data.CustomDefinitionId,
            CustomDefinitionName = sourceVersion < 17 ? string.Empty : data.CustomDefinitionName,
            CustomDefinitionSnapshot = sourceVersion < 17 ? null : data.CustomDefinitionSnapshot,
            EngagementOnly = FastenerKindTraits.IsScrew(data.Kind)
                && assemblyMode == ScrewAssemblyMode.EngagementOnly,
            AutoRecognizeHosts = sourceVersion >= 8 && data.AutoRecognizeHosts,
            ConfirmedEngagementHostId = sourceVersion >= 21
                && FastenerKindTraits.IsScrew(data.Kind)
                && assemblyMode == ScrewAssemblyMode.ThreadEngagement
                    ? data.ConfirmedEngagementHostId
                    : Guid.Empty,
            SmartRecognitionMode = sourceVersion >= 8
                ? data.SmartRecognitionMode
                : SmartPlacementRecognitionMode.Automatic,
            SmartBindingProfile = MigrateSmartBindingProfile(
                data,
                sourceVersion,
                migratedHeadEmbedDepth),
            ControlPointObjectId = sourceVersion < 4 ? Guid.Empty : data.ControlPointObjectId,
            Bindings = MigrateBindings(data, sourceVersion, migratedHeadEmbedDepth)
        };
    }

    private static IReadOnlyList<HoleTargetBinding> MigrateBindings(
        FastenerComponentData data,
        int sourceVersion,
        double migratedHeadEmbedDepth)
    {
        var bindings = data.Bindings.Select(binding => binding with
        {
            IsPreviewVisible = sourceVersion < 2 || binding.IsPreviewVisible,
            IsBooleanEnabled = sourceVersion < 3 || binding.IsBooleanEnabled
        }).ToArray();
        if (sourceVersion == 12
            && data.EngagementOnly
            && !FastenerKindTraits.IsNut(data.Kind)
            && data.SmartBindingProfile is { } engagementProfile)
        {
            // Schema v12 could convert the head-seat clearance binding into the
            // only engagement binding while retaining the clearance module's
            // switches. The smart profile is the authoritative role template.
            bindings = bindings.Select(binding =>
                binding.Role == ShaftFitRole.ThreadEngagement
                    ? binding with
                    {
                        IsPreviewVisible = engagementProfile.EngagementPreviewVisible,
                        IsBooleanEnabled = engagementProfile.EngagementBooleanEnabled
                    }
                    : binding).ToArray();
        }
        if (data.Kind == FastenerKind.HexNut
            && bindings.Length > 0
            && sourceVersion < 9)
        {
            var selected = bindings.FirstOrDefault(binding => binding.IncludeHeadSeat) ?? bindings[0];
            var depth = LegacyHexNutEmbedDepth(data, selected.BlindDepth);
            return
            [
                selected with
                {
                    Role = ShaftFitRole.InstallationPocket,
                    DepthMode = DepthMode.Blind,
                    BlindDepth = depth,
                    IncludeHeadSeat = false
                }
            ];
        }

        if (sourceVersion >= 10 || FastenerKindTraits.IsNut(data.Kind))
            return bindings;

        var nominalDiameter = TryGetNominalDiameter(data.Size);
        return bindings.Select(binding =>
            binding.Role == ShaftFitRole.ThreadEngagement
                ? MigrateEngagementDepth(
                    binding,
                    data.Length,
                    migratedHeadEmbedDepth,
                    nominalDiameter)
                : binding).ToArray();
    }

    private static SmartBindingProfile? MigrateSmartBindingProfile(
        FastenerComponentData data,
        int sourceVersion,
        double migratedHeadEmbedDepth)
    {
        if (sourceVersion < 8 || data.SmartBindingProfile is not { } profile)
            return null;
        if (sourceVersion >= 10 || FastenerKindTraits.IsNut(data.Kind))
            return profile;
        var migrated = MigrateEngagementDepth(
            new HoleTargetBinding
            {
                Role = ShaftFitRole.ThreadEngagement,
                DepthMode = profile.EngagementDepthMode,
                BlindDepth = profile.EngagementBlindDepth
            },
            data.Length,
            migratedHeadEmbedDepth,
            TryGetNominalDiameter(data.Size));
        return profile with
        {
            EngagementDepthMode = migrated.DepthMode,
            EngagementBlindDepth = migrated.BlindDepth
        };
    }

    private static HoleTargetBinding MigrateEngagementDepth(
        HoleTargetBinding binding,
        double length,
        double headEmbedDepth,
        double nominalDiameter) => binding.DepthMode switch
        {
            DepthMode.FastenerLengthPlusTwoDiameters => binding with
            {
                DepthMode = DepthMode.FastenerLengthPlusCustom,
                BlindDepth = 2 * nominalDiameter
            },
            DepthMode.Blind => binding with
            {
                DepthMode = DepthMode.FastenerLengthPlusCustom,
                BlindDepth = binding.BlindDepth - headEmbedDepth - length
            },
            _ => binding
        };

    private static double TryGetNominalDiameter(string size)
    {
        try
        {
            return Catalog.Value.Get(size).NominalDiameter;
        }
        catch
        {
            return double.TryParse(
                size.TrimStart('M', 'm'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed)
                ? parsed
                : 0;
        }
    }

    private static double MigrateHeadEmbedDepth(FastenerComponentData data, int sourceVersion)
    {
        if (sourceVersion < 9 && data.Kind == FastenerKind.HexNut)
            return LegacyHexNutEmbedDepth(
                data,
                data.Bindings.FirstOrDefault()?.BlindDepth ?? 0);
        if (sourceVersion < 4)
            return LegacyHeadEmbedDepth(data);
        if (sourceVersion == 4 && data.Kind == FastenerKind.Countersunk)
        {
            try
            {
                var spec = Catalog.Value.Get(data.Size);
                var legacyHeight = HeadGeometryCalculator.GetLegacyCountersunkConeHeight(spec);
                if (Math.Abs(data.HeadEmbedDepth - legacyHeight) <= 0.01)
                    return HeadGeometryCalculator.GetHeadHeight(data.Kind, spec);
            }
            catch
            {
                return data.HeadEmbedDepth;
            }
        }
        return data.HeadEmbedDepth;
    }

    private static double LegacyHexNutEmbedDepth(
        FastenerComponentData data,
        double fallbackDepth)
    {
        try
        {
            return Catalog.Value.Get(data.Size).Head.NutThickness;
        }
        catch
        {
            return fallbackDepth > 0 ? fallbackDepth : 0.1;
        }
    }

    private static double LegacyHeadEmbedDepth(FastenerComponentData data)
    {
        if (data.Bindings.All(binding => !binding.IncludeHeadSeat))
            return 0;
        try
        {
            return HeadGeometryCalculator.GetHeadHeight(data.Kind, Catalog.Value.Get(data.Size));
        }
        catch
        {
            return 0;
        }
    }
}
