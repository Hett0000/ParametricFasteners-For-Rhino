using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using Xunit;

namespace RhinoMM.Core.Tests;

public class HoleDiameterCalculatorTests
{
    private static readonly FastenerCatalog Catalog = FastenerCatalog.LoadEmbedded();

    [Fact]
    public void CatalogContainsM16()
    {
        var spec = Catalog.Get("M1.6");
        Assert.Equal(1.6, spec.NominalDiameter);
        Assert.Equal(0.35, spec.CoarsePitch);
    }

    [Fact]
    public void ClearanceUsesStandardPlusCorrections()
    {
        var binding = new HoleTargetBinding
        {
            TargetObjectId = Guid.NewGuid(),
            Role = ShaftFitRole.Clearance,
            ClearanceFit = ClearanceFitClass.Normal,
            BindingOverride = 0.05
        };

        var result = HoleDiameterCalculator.Calculate(
            Catalog.Get("M3"), binding, new PrintProfileSnapshot("PLA", 0.15));

        Assert.Equal(3.6, result.FinalDiameter, 6);
    }

    [Fact]
    public void EngagementUsesNominalMinusBitePlusCorrections()
    {
        var binding = new HoleTargetBinding
        {
            TargetObjectId = Guid.NewGuid(),
            Role = ShaftFitRole.ThreadEngagement,
            BiteReduction = 0.35,
            BindingOverride = -0.05
        };

        var result = HoleDiameterCalculator.Calculate(
            Catalog.Get("M3"), binding, new PrintProfileSnapshot("PETG", 0.1));

        Assert.Equal(2.7, result.FinalDiameter, 6);
    }

    [Fact]
    public void EngagementRequiresCalibratedBite()
    {
        var component = new FastenerComponentData
        {
            Size = "M3",
            Bindings = [new HoleTargetBinding
            {
                TargetObjectId = Guid.NewGuid(),
                Role = ShaftFitRole.ThreadEngagement,
                BiteReduction = 0
            }]
        };

        var validation = FastenerComponentValidator.Validate(component, Catalog.Get("M3"));
        Assert.Contains(validation.Issues, issue => issue.Code == "bite-required");
    }

    [Fact]
    public void OnlyOneHeadSeatIsAllowed()
    {
        var component = new FastenerComponentData
        {
            Size = "M4",
            Bindings =
            [
                new HoleTargetBinding { TargetObjectId = Guid.NewGuid(), IncludeHeadSeat = true },
                new HoleTargetBinding { TargetObjectId = Guid.NewGuid(), IncludeHeadSeat = true }
            ]
        };

        var validation = FastenerComponentValidator.Validate(component, Catalog.Get("M4"));
        Assert.Contains(validation.Issues, issue => issue.Code == "head-seat");
    }
}
