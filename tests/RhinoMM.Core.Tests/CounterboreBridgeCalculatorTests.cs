using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class CounterboreBridgeCalculatorTests
{
    private static readonly FastenerCatalog Catalog = FastenerCatalog.LoadEmbedded();

    [Theory]
    [InlineData(0.12)]
    [InlineData(0.16)]
    [InlineData(0.20)]
    [InlineData(0.28)]
    public void ProfileUsesCompensatedShaftDiameterAndTwoLayerHeight(double layerHeight)
    {
        var component = Component(layerHeight);
        var spec = Catalog.Get("M3");
        var binding = component.Bindings.Single();

        var profile = CounterboreBridgeCalculator.Create(
            component,
            spec,
            binding,
            0.2,
            0.001);
        var finalDiameter = HoleDiameterCalculator
            .Calculate(component, spec, binding)
            .FinalDiameter;

        Assert.Equal(finalDiameter / 2, profile.ShaftRadius, 6);
        Assert.Equal(component.HeadEmbedDepth, profile.Start, 6);
        Assert.Equal(component.HeadEmbedDepth + layerHeight, profile.FirstLayerEnd, 6);
        Assert.Equal(component.HeadEmbedDepth + layerHeight * 2, profile.SecondLayerEnd, 6);
        Assert.Equal(
            Math.Sqrt(
                profile.HeadRadius * profile.HeadRadius
                - profile.ShaftRadius * profile.ShaftRadius),
            profile.SlotHalfLength,
            6);
    }

    [Fact]
    public void RejectsFinalHoleThatCannotFitSquareBridgeLayer()
    {
        var component = Component(0.2) with
        {
            PrintProfile = new PrintProfileSnapshot("test", 5)
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CounterboreBridgeCalculator.Create(
                component,
                Catalog.Get("M3"),
                component.Bindings.Single(),
                0.2,
                0.001));

        Assert.Contains("无法形成有效的双层架桥", exception.Message);
    }

    [Fact]
    public void SchemaTenComponentsMigrateWithBridgeDisabledAndDefaultHeight()
    {
        var legacy = Component(0.7) with
        {
            SchemaVersion = 10,
            CounterboreBridgeEnabled = true,
            CounterboreBridgeLayerHeight = 0.7
        };

        var migrated = ComponentJson.Migrate(legacy);

        Assert.Equal(FastenerComponentData.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.False(migrated.CounterboreBridgeEnabled);
        Assert.Equal(0.2, migrated.CounterboreBridgeLayerHeight, 6);
    }

    [Theory]
    [InlineData(0.04)]
    [InlineData(1.01)]
    public void ValidatorRejectsLayerHeightOutsideSupportedRange(double layerHeight)
    {
        var component = Component(layerHeight);

        var result = FastenerComponentValidator.Validate(component, Catalog.Get("M3"));

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "counterbore-bridge-height");
    }

    private static FastenerComponentData Component(double layerHeight) => new()
    {
        Kind = FastenerKind.SocketCap,
        Size = "M3",
        Length = 20,
        HeadEmbedDepth = 3,
        CounterboreBridgeEnabled = true,
        CounterboreBridgeLayerHeight = layerHeight,
        PrintProfile = new PrintProfileSnapshot("test", 0.2),
        Bindings =
        [
            new HoleTargetBinding
            {
                TargetObjectId = Guid.NewGuid(),
                Role = ShaftFitRole.Clearance,
                ClearanceFit = ClearanceFitClass.Normal,
                IncludeHeadSeat = true
            }
        ]
    };
}
