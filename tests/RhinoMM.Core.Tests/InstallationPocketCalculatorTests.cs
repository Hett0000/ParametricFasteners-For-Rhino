using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class InstallationPocketCalculatorTests
{
    private static readonly FastenerCatalog Catalog = FastenerCatalog.LoadEmbedded();

    [Fact]
    public void HexNutPocketUsesAcrossFlatsAndSignedCompensation()
    {
        var component = new FastenerComponentData
        {
            Kind = FastenerKind.HexNut,
            Size = "M3",
            HeadEmbedDepth = 2.4,
            PrintProfile = new PrintProfileSnapshot("FDM", 0.2)
        };
        var binding = new HoleTargetBinding { BindingOverride = -0.05 };

        Assert.Equal(
            5.65,
            InstallationPocketCalculator.HexNutAcrossFlats(component, Catalog.Get("M3"), binding),
            6);
        Assert.Equal(2.4, InstallationPocketCalculator.RequiredDepth(component, Catalog.Get("M3")), 6);
    }

    [Fact]
    public void HeatSetPocketUsesSignedDiameterCompensationAndFixedChamfer()
    {
        var component = new FastenerComponentData
        {
            Kind = FastenerKind.HeatSetInsert,
            Size = "M3",
            Length = 5,
            InsertOuterDiameter = 4.6,
            InsertDiameterCompensation = -0.2,
            InsertDepthCompensation = 1
        };

        Assert.Equal(4.4, InstallationPocketCalculator.HeatSetFinalDiameter(component), 6);
        Assert.Equal(0.5, InstallationPocketCalculator.HeatSetChamferDepth(component), 6);
        Assert.Equal(5.4, InstallationPocketCalculator.HeatSetMouthDiameter(component), 6);
        Assert.Equal(5, InstallationPocketCalculator.RequiredDepth(component, Catalog.Get("M3")), 6);
        Assert.Equal(5, InstallationPocketCalculator.RequiredHostDepth(component, Catalog.Get("M3")), 6);
        Assert.Equal(6, InstallationPocketCalculator.CuttingDepth(component, Catalog.Get("M3")), 6);
    }

    [Fact]
    public void HeatSetChamferIsLimitedForShortInserts()
    {
        var component = new FastenerComponentData
        {
            Kind = FastenerKind.HeatSetInsert,
            Length = 0.6,
            InsertOuterDiameter = 2
        };

        Assert.Equal(0.3, InstallationPocketCalculator.HeatSetChamferDepth(component), 6);
    }

    [Fact]
    public void ValidatorRejectsHeatSetHoleNotLargerThanThread()
    {
        var component = new FastenerComponentData
        {
            Kind = FastenerKind.HeatSetInsert,
            Size = "M3",
            Length = 5,
            InsertOuterDiameter = 3.1,
            InsertDiameterCompensation = -0.2,
            Bindings =
            [
                new HoleTargetBinding
                {
                    TargetObjectId = Guid.NewGuid(),
                    Role = ShaftFitRole.InstallationPocket,
                    DepthMode = DepthMode.Blind,
                    BlindDepth = 5
                }
            ]
        };

        var validation = FastenerComponentValidator.Validate(component, Catalog.Get("M3"));

        Assert.Contains(validation.Issues, issue => issue.Code == "insert-final-diameter" && issue.IsError);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1000.1)]
    public void ValidatorRejectsOutOfRangeHeatSetDepthCompensation(double compensation)
    {
        var component = new FastenerComponentData
        {
            Kind = FastenerKind.HeatSetInsert,
            Size = "M3",
            Length = 5,
            InsertOuterDiameter = 4.6,
            InsertDepthCompensation = compensation,
            Bindings =
            [
                new HoleTargetBinding
                {
                    TargetObjectId = Guid.NewGuid(),
                    Role = ShaftFitRole.InstallationPocket,
                    DepthMode = DepthMode.Blind,
                    BlindDepth = 4.9
                }
            ]
        };

        var validation = FastenerComponentValidator.Validate(component, Catalog.Get("M3"));

        Assert.Contains(validation.Issues, issue => issue.Code == "insert-depth-compensation" && issue.IsError);
    }

    [Fact]
    public void SchemaV6HeatSetInsertKeepsLegacyZeroDepthCompensation()
    {
        var migrated = ComponentJson.Migrate(new FastenerComponentData
        {
            SchemaVersion = 6,
            Kind = FastenerKind.HeatSetInsert,
            InsertDepthCompensation = 3
        });

        Assert.Equal(9, migrated.SchemaVersion);
        Assert.Equal(0, migrated.InsertDepthCompensation);
    }

    [Fact]
    public void SchemaV5HexNutMigratesToOneInstallationPocket()
    {
        var preferredTarget = Guid.NewGuid();
        var data = new FastenerComponentData
        {
            SchemaVersion = 5,
            Kind = FastenerKind.HexNut,
            Size = "M3",
            Bindings =
            [
                new HoleTargetBinding { TargetObjectId = Guid.NewGuid() },
                new HoleTargetBinding
                {
                    TargetObjectId = preferredTarget,
                    IncludeHeadSeat = true,
                    IsPreviewVisible = false,
                    IsBooleanEnabled = false
                }
            ]
        };

        var migrated = ComponentJson.Migrate(data);
        var binding = Assert.Single(migrated.Bindings);

        Assert.Equal(9, migrated.SchemaVersion);
        Assert.Equal(2.4, migrated.HeadEmbedDepth, 6);
        Assert.Equal(preferredTarget, binding.TargetObjectId);
        Assert.Equal(ShaftFitRole.InstallationPocket, binding.Role);
        Assert.Equal(DepthMode.Blind, binding.DepthMode);
        Assert.Equal(2.4, binding.BlindDepth, 6);
        Assert.False(binding.IncludeHeadSeat);
        Assert.False(binding.IsPreviewVisible);
        Assert.False(binding.IsBooleanEnabled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1.2)]
    [InlineData(2.4)]
    [InlineData(4.0)]
    public void HexNutPocketDepthFollowsEmbedDepth(double embedDepth)
    {
        var component = new FastenerComponentData
        {
            Kind = FastenerKind.HexNut,
            Size = "M3",
            HeadEmbedDepth = embedDepth
        };

        Assert.Equal(
            embedDepth,
            InstallationPocketCalculator.RequiredHostDepth(component, Catalog.Get("M3")),
            6);
        Assert.Equal(
            embedDepth,
            InstallationPocketCalculator.CuttingDepth(component, Catalog.Get("M3")),
            6);
    }

    [Fact]
    public void ZeroDepthHexNutPocketIsValidAndDoesNotRequireBlindDepth()
    {
        var component = new FastenerComponentData
        {
            Kind = FastenerKind.HexNut,
            Size = "M3",
            HeadEmbedDepth = 0,
            Bindings =
            [
                new HoleTargetBinding
                {
                    TargetObjectId = Guid.NewGuid(),
                    Role = ShaftFitRole.InstallationPocket,
                    DepthMode = DepthMode.Blind,
                    BlindDepth = 0
                }
            ]
        };

        var validation = FastenerComponentValidator.Validate(component, Catalog.Get("M3"));

        Assert.True(validation.IsValid);
    }

    [Fact]
    public void SchemaV8HexNutMigratesFromLegacyZeroToFullEmbed()
    {
        var bindingId = Guid.NewGuid();
        var migrated = ComponentJson.Migrate(new FastenerComponentData
        {
            SchemaVersion = 8,
            Kind = FastenerKind.HexNut,
            Size = "M3",
            HeadEmbedDepth = 0,
            Bindings =
            [
                new HoleTargetBinding
                {
                    BindingId = bindingId,
                    TargetObjectId = Guid.NewGuid(),
                    Role = ShaftFitRole.InstallationPocket,
                    DepthMode = DepthMode.Blind,
                    BlindDepth = 2.4
                }
            ]
        });

        var binding = Assert.Single(migrated.Bindings);
        Assert.Equal(9, migrated.SchemaVersion);
        Assert.Equal(2.4, migrated.HeadEmbedDepth, 6);
        Assert.Equal(2.4, binding.BlindDepth, 6);
        Assert.Equal(bindingId, binding.BindingId);
    }
}
