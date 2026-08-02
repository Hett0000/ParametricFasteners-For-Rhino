using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class LockingNutDimensionsTests
{
    private static readonly FastenerCatalog Catalog = FastenerCatalog.LoadEmbedded();

    [Theory]
    [InlineData("M2", 4.0, 3.0, true)]
    [InlineData("M2.5", 5.0, 3.8, true)]
    [InlineData("M3", 5.5, 4.5, false)]
    [InlineData("M4", 7.0, 6.0, false)]
    [InlineData("M5", 8.0, 6.8, false)]
    [InlineData("M6", 10.0, 8.0, false)]
    [InlineData("M8", 13.0, 9.5, false)]
    [InlineData("M10", 16.0, 11.9, false)]
    [InlineData("M12", 18.0, 14.9, false)]
    public void LockingCatalog_ReturnsConfiguredEnvelope(
        string size,
        double acrossFlats,
        double height,
        bool engineeringExtension)
    {
        var dimensions = HexNutDimensions.Resolve(
            HexNutStyle.NylonInsertLocking,
            Catalog.Get(size));

        Assert.Equal(acrossFlats, dimensions.AcrossFlats, 6);
        Assert.Equal(height, dimensions.TotalHeight, 6);
        Assert.Equal(engineeringExtension, dimensions.IsEngineeringExtension);
        Assert.Equal(
            engineeringExtension ? "DIN 985 工程扩展" : "GB/T 889.1-2015",
            dimensions.Standard);
    }

    [Fact]
    public void LockingCatalog_StartsAtM2()
    {
        Assert.False(HexNutDimensions.Supports(
            HexNutStyle.NylonInsertLocking,
            "M1.6"));
        Assert.True(HexNutDimensions.Supports(
            HexNutStyle.NylonInsertLocking,
            "M2"));
        Assert.Throws<InvalidOperationException>(() => HexNutDimensions.Resolve(
            HexNutStyle.NylonInsertLocking,
            Catalog.Get("M1.6")));
    }

    [Fact]
    public void LockingPocket_UsesLockingEnvelopeAndSignedCompensation()
    {
        var component = new FastenerComponentData
        {
            Kind = FastenerKind.HexNut,
            HexNutStyle = HexNutStyle.NylonInsertLocking,
            Size = "M4",
            PrintProfile = new PrintProfileSnapshot("FDM", 0.2)
        };
        var binding = new HoleTargetBinding { BindingOverride = -0.05 };

        Assert.Equal(
            7.15,
            InstallationPocketCalculator.HexNutAcrossFlats(
                component,
                Catalog.Get("M4"),
                binding),
            6);
        Assert.Equal(
            6.0,
            InstallationPocketCalculator.HexNutHeight(component, Catalog.Get("M4")),
            6);
    }

    [Fact]
    public void SchemaFourteenHexNut_MigratesAsOrdinaryNut()
    {
        var migrated = ComponentJson.Migrate(new FastenerComponentData
        {
            SchemaVersion = 14,
            Kind = FastenerKind.HexNut,
            HexNutStyle = HexNutStyle.NylonInsertLocking
        });

        Assert.Equal(FastenerComponentData.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(HexNutStyle.Standard, migrated.HexNutStyle);
    }

    [Fact]
    public void Validator_RejectsM1Point6LockingNut()
    {
        var component = new FastenerComponentData
        {
            Kind = FastenerKind.HexNut,
            HexNutStyle = HexNutStyle.NylonInsertLocking,
            Size = "M1.6",
            HeadEmbedDepth = 3,
            Bindings =
            [
                new HoleTargetBinding
                {
                    TargetObjectId = Guid.NewGuid(),
                    Role = ShaftFitRole.InstallationPocket,
                    DepthMode = DepthMode.Blind,
                    BlindDepth = 3
                }
            ]
        };

        var validation = FastenerComponentValidator.Validate(component, Catalog.Get("M1.6"));

        Assert.Contains(validation.Issues, issue => issue.Code == "locking-nut-size");
    }
}
