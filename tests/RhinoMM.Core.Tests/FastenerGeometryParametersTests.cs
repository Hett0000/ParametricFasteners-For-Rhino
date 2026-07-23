using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class FastenerGeometryParametersTests
{
    [Fact]
    public void Match_IgnoresRuntimeIdsDisplayStateAndBooleanSwitches()
    {
        var component = CreateComponent();
        var displayOnly = component with
        {
            ProxyObjectId = Guid.NewGuid(),
            ControlPointObjectId = Guid.NewGuid(),
            FastenerOpacityPercent = 20,
            CutterOpacityPercent = 80,
            UpdatedAt = component.UpdatedAt.AddMinutes(1),
            Bindings = component.Bindings.Select(binding => binding with
            {
                CutterObjectId = Guid.NewGuid(),
                IsPreviewVisible = false,
                IsBooleanEnabled = false
            }).ToArray()
        };

        Assert.True(FastenerGeometryParameters.Match(component, displayOnly));
    }

    [Fact]
    public void Match_DetectsDepthDiameterAndPlacementChanges()
    {
        var component = CreateComponent();
        var changedDepth = component with
        {
            Bindings = component.Bindings.Select(binding => binding with
            {
                DepthMode = DepthMode.FastenerLengthPlusOneDiameter
            }).ToArray()
        };
        var changedCorrection = component with
        {
            PrintProfile = component.PrintProfile with { HoleDiameterCorrection = 0.3 }
        };
        var changedPlacement = component with
        {
            Placement = component.Placement with { OriginX = 2 }
        };
        var changedInsertDepth = component with { InsertDepthCompensation = 1 };

        Assert.False(FastenerGeometryParameters.Match(component, changedDepth));
        Assert.False(FastenerGeometryParameters.Match(component, changedCorrection));
        Assert.False(FastenerGeometryParameters.Match(component, changedPlacement));
        Assert.False(FastenerGeometryParameters.Match(component, changedInsertDepth));
    }

    private static FastenerComponentData CreateComponent() => new()
    {
        ComponentId = Guid.NewGuid(),
        Size = "M3",
        Length = 20,
        HeadEmbedDepth = 2,
        PrintProfile = new PrintProfileSnapshot("test", 0.2),
        Bindings =
        [
            new HoleTargetBinding
            {
                BindingId = Guid.NewGuid(),
                TargetObjectId = Guid.NewGuid(),
                Role = ShaftFitRole.ThreadEngagement,
                BiteReduction = 0.35,
                DepthMode = DepthMode.FastenerLengthPlusTwoDiameters,
                BlindDepth = 20,
                IncludeHeadSeat = true
            }
        ]
    };
}
