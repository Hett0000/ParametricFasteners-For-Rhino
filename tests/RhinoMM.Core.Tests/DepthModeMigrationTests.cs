using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class DepthModeMigrationTests
{
    private static readonly FastenerCatalog Catalog = FastenerCatalog.LoadEmbedded();

    [Fact]
    public void SchemaNineLPlusTwoDMigratesToEquivalentCustomExtension()
    {
        var legacy = Component(
            DepthMode.FastenerLengthPlusTwoDiameters,
            0) with
        {
            SmartBindingProfile = Profile(
                DepthMode.FastenerLengthPlusTwoDiameters,
                0)
        };

        var migrated = ComponentJson.Migrate(legacy);
        var binding = Assert.Single(migrated.Bindings);

        Assert.Equal(FastenerComponentData.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(DepthMode.FastenerLengthPlusCustom, binding.DepthMode);
        Assert.Equal(6, binding.BlindDepth);
        Assert.Equal(
            20,
            HoleDepthCalculator.GetLimit(migrated, Catalog.Get("M3"), binding),
            6);
        Assert.Equal(
            DepthMode.FastenerLengthPlusCustom,
            migrated.SmartBindingProfile!.EngagementDepthMode);
        Assert.Equal(6, migrated.SmartBindingProfile.EngagementBlindDepth);
    }

    [Fact]
    public void SchemaNineAbsoluteBlindDepthMigratesWithoutMovingHoleBottom()
    {
        var legacy = Component(DepthMode.Blind, 10) with
        {
            SmartBindingProfile = Profile(DepthMode.Blind, 10)
        };

        var migrated = ComponentJson.Migrate(legacy);
        var binding = Assert.Single(migrated.Bindings);

        Assert.Equal(DepthMode.FastenerLengthPlusCustom, binding.DepthMode);
        Assert.Equal(-4, binding.BlindDepth);
        Assert.Equal(
            10,
            HoleDepthCalculator.GetLimit(migrated, Catalog.Get("M3"), binding),
            6);
        Assert.Equal(-4, migrated.SmartBindingProfile!.EngagementBlindDepth);
    }

    [Fact]
    public void ExistingLPlusOneDAndThroughModesRemainUnchanged()
    {
        var lPlusOne = Component(DepthMode.FastenerLengthPlusOneDiameter, 99);
        var through = Component(DepthMode.ThroughTarget, 99);

        Assert.Equal(
            DepthMode.FastenerLengthPlusOneDiameter,
            ComponentJson.Migrate(lPlusOne).Bindings[0].DepthMode);
        Assert.Equal(
            DepthMode.ThroughTarget,
            ComponentJson.Migrate(through).Bindings[0].DepthMode);
    }

    [Theory]
    [InlineData(-4, false)]
    [InlineData(-14, true)]
    public void CustomExtensionAllowsLegacyNegativeValuesOnlyWhenFinalDepthIsValid(
        double extension,
        bool expectsError)
    {
        var component = Component(
            DepthMode.FastenerLengthPlusCustom,
            extension) with
        {
            SchemaVersion = FastenerComponentData.CurrentSchemaVersion,
            Bindings =
            [
                Component(DepthMode.FastenerLengthPlusCustom, extension).Bindings[0] with
                {
                    IncludeHeadSeat = true
                }
            ]
        };

        var validation = FastenerComponentValidator.Validate(
            component,
            Catalog.Get("M3"));

        Assert.Equal(
            expectsError,
            validation.Issues.Any(issue => issue.Code == "engagement-custom-depth"));
    }

    private static FastenerComponentData Component(
        DepthMode mode,
        double depth) => new()
    {
        SchemaVersion = 9,
        Kind = FastenerKind.SocketCap,
        Size = "M3",
        Length = 12,
        HeadEmbedDepth = 2,
        AutoRecognizeHosts = true,
        Bindings =
        [
            new HoleTargetBinding
            {
                TargetObjectId = Guid.NewGuid(),
                Role = ShaftFitRole.ThreadEngagement,
                BiteReduction = 0.35,
                DepthMode = mode,
                BlindDepth = depth
            }
        ]
    };

    private static SmartBindingProfile Profile(
        DepthMode mode,
        double depth) => new(
            ClearanceFitClass.Normal,
            0.35,
            mode,
            depth,
            true,
            true,
            true,
            true);
}
