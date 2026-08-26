using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class HeadGapTests
{
    private static readonly FastenerSizeSpec M3 = FastenerCatalog.LoadEmbedded().Get("M3");

    [Fact]
    public void NegativeOffset_IsValidForScrewWhenShaftStillReachesHost()
    {
        var component = EngagementOnlyScrew(-2, 20);

        var validation = FastenerComponentValidator.Validate(component, M3);

        Assert.True(validation.IsValid, string.Join(" | ", validation.Issues.Select(issue => issue.Message)));
    }

    [Theory]
    [InlineData(-20)]
    [InlineData(-21)]
    public void GapAtOrBeyondShaftLength_IsRejected(double offset)
    {
        var validation = FastenerComponentValidator.Validate(
            EngagementOnlyScrew(offset, 20),
            M3);

        var issue = Assert.Single(validation.Issues, item => item.Code == "head-gap-reach");
        Assert.Contains("螺杆无法进入宿主", issue.Message);
    }

    [Fact]
    public void NegativeOffset_IsRejectedForHexNut()
    {
        var component = new FastenerComponentData
        {
            Kind = FastenerKind.HexNut,
            Size = "M3",
            HeadEmbedDepth = -1,
            Bindings =
            [
                new HoleTargetBinding
                {
                    TargetObjectId = Guid.NewGuid(),
                    Role = ShaftFitRole.InstallationPocket,
                    DepthMode = DepthMode.Blind,
                    BlindDepth = -1
                }
            ]
        };

        var validation = FastenerComponentValidator.Validate(component, M3);

        Assert.Contains(validation.Issues, issue => issue.Code == "head-gap-unsupported");
    }

    [Fact]
    public void NegativeOffset_ShiftsShaftAndHoleDepthsOutward()
    {
        var component = EngagementOnlyScrew(-2, 20);
        var clearance = new HoleTargetBinding
        {
            TargetObjectId = Guid.NewGuid(),
            Role = ShaftFitRole.Clearance,
            DepthMode = DepthMode.ThroughTarget
        };
        var oneDiameter = component.Bindings[0] with
        {
            DepthMode = DepthMode.FastenerLengthPlusOneDiameter
        };
        var custom = component.Bindings[0] with
        {
            DepthMode = DepthMode.FastenerLengthPlusCustom,
            BlindDepth = 2
        };

        Assert.Equal(18, HoleDepthCalculator.GetLimit(component, M3, clearance), 6);
        Assert.Equal(21, HoleDepthCalculator.GetLimit(component, M3, oneDiameter), 6);
        Assert.Equal(20, HoleDepthCalculator.GetLimit(component, M3, custom), 6);
    }

    [Fact]
    public void NegativeOffset_ShiftsPairedNutWithShaftTip()
    {
        var component = EngagementOnlyScrew(-2, 20) with
        {
            AssemblyMode = ScrewAssemblyMode.NutFastened,
            NutTipProtrusion = 2
        };

        var range = PairedNutAssemblyCalculator.AxialRange(component, M3);

        Assert.Equal(18, range.ShaftTip, 6);
        Assert.Equal(16, range.OuterFace, 6);
        Assert.Equal(13.6, range.InnerFace, 6);
    }

    [Fact]
    public void NegativeOffset_DisablesCounterboreBridgeInTemplate()
    {
        var normalized = new FastenerTemplateData
        {
            Kind = FastenerKind.SocketCap,
            Size = "M3",
            Length = 20,
            HeadEmbedDepth = -2,
            CounterboreBridgeEnabled = true,
            CounterboreBridgeLayerHeight = 0.2
        }.Normalize();

        Assert.False(normalized.CounterboreBridgeEnabled);
    }

    [Fact]
    public void Summary_UsesUnambiguousGapLabel()
    {
        var component = EngagementOnlyScrew(-2, 20);

        Assert.Contains("离面2", FastenerSelectionSummaryFormatter.Compact(component));
        Assert.Contains("头部状态：离面 2 mm", FastenerSelectionSummaryFormatter.Full(component));
        Assert.Equal("离面 2 mm", FastenerLabels.HeadOffset(-2));
    }

    private static FastenerComponentData EngagementOnlyScrew(double offset, double length) => new()
    {
        Kind = FastenerKind.SocketCap,
        Size = "M3",
        Length = length,
        HeadEmbedDepth = offset,
        AssemblyMode = ScrewAssemblyMode.EngagementOnly,
        Bindings =
        [
            new HoleTargetBinding
            {
                TargetObjectId = Guid.NewGuid(),
                Role = ShaftFitRole.ThreadEngagement,
                DepthMode = DepthMode.ThroughTarget,
                BiteReduction = 0.35
            }
        ]
    };
}
