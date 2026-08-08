using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class FastenerSelectionSummaryFormatterTests
{
    [Theory]
    [InlineData(FastenerKind.SocketCap, "杯头")]
    [InlineData(FastenerKind.Countersunk, "沉头")]
    [InlineData(FastenerKind.HexBolt, "六角头")]
    [InlineData(FastenerKind.HexNut, "六角螺母")]
    [InlineData(FastenerKind.HeatSetInsert, "热熔螺母")]
    public void ShortKind_UsesCompactChineseLabels(FastenerKind kind, string expected) =>
        Assert.Equal(expected, FastenerLabels.ShortKind(kind));

    [Fact]
    public void ShortKind_DistinguishesNylonLockingNut() =>
        Assert.Equal(
            "防松螺母",
            FastenerLabels.ShortKind(new FastenerComponentData
            {
                Kind = FastenerKind.HexNut,
                HexNutStyle = HexNutStyle.NylonInsertLocking
            }));

    [Fact]
    public void ScrewSummary_UsesCompactRequestedFormat()
    {
        var component = Screw(
            FastenerKind.Countersunk,
            length: 30,
            new HoleTargetBinding { Role = ShaftFitRole.Clearance },
            new HoleTargetBinding
            {
                Role = ShaftFitRole.ThreadEngagement,
                DepthMode = DepthMode.FastenerLengthPlusOneDiameter
            });

        Assert.Equal("沉头 M3*30 +1切割 通1/咬1", FastenerSelectionSummaryFormatter.Compact(component));
    }

    [Theory]
    [InlineData(DepthMode.ThroughTarget, 0, "贯穿切割")]
    [InlineData(DepthMode.FastenerLengthPlusCustom, 3.5, "+3.5切割")]
    [InlineData(DepthMode.FastenerLengthPlusCustom, 0, "+0切割")]
    [InlineData(DepthMode.FastenerLengthPlusCustom, -2, "-2切割")]
    public void ScrewSummary_FormatsAllVisibleDepthModes(
        DepthMode mode,
        double extension,
        string expectedDepth)
    {
        var component = Screw(
            FastenerKind.SocketCap,
            length: 12.5,
            new HoleTargetBinding
            {
                Role = ShaftFitRole.ThreadEngagement,
                DepthMode = mode,
                BlindDepth = extension
            });

        Assert.Equal(
            $"杯头 M3*12.5 {expectedDepth} 通0/咬1",
            FastenerSelectionSummaryFormatter.Compact(component));
    }

    [Fact]
    public void ScrewSummary_OmitsDepthWhenThereIsNoEngagementHost()
    {
        var component = Screw(
            FastenerKind.HexBolt,
            length: 20,
            new HoleTargetBinding { Role = ShaftFitRole.Clearance });

        Assert.Equal("六角头 M3*20 通1/咬0", FastenerSelectionSummaryFormatter.Compact(component));
    }

    [Fact]
    public void ScrewSummary_ReportsMixedLegacyDepthSettings()
    {
        var component = Screw(
            FastenerKind.SocketCap,
            length: 12,
            new HoleTargetBinding
            {
                Role = ShaftFitRole.ThreadEngagement,
                DepthMode = DepthMode.ThroughTarget
            },
            new HoleTargetBinding
            {
                Role = ShaftFitRole.ThreadEngagement,
                DepthMode = DepthMode.FastenerLengthPlusOneDiameter
            });

        Assert.Equal("杯头 M3*12 混合切割 通0/咬2", FastenerSelectionSummaryFormatter.Compact(component));
    }

    [Fact]
    public void NutSummaries_RetainSpecializedParameters()
    {
        var hexNut = new FastenerComponentData
        {
            Kind = FastenerKind.HexNut,
            Size = "M4",
            HeadEmbedDepth = 3.2,
            Bindings =
            [
                new HoleTargetBinding { Role = ShaftFitRole.InstallationPocket }
            ]
        };
        var insert = new FastenerComponentData
        {
            Kind = FastenerKind.HeatSetInsert,
            Size = "M3",
            Length = 5,
            InsertOuterDiameter = 4.6,
            Bindings =
            [
                new HoleTargetBinding { Role = ShaftFitRole.InstallationPocket }
            ]
        };

        Assert.Equal("普通六角螺母 M4 嵌入3.2 安装1", FastenerSelectionSummaryFormatter.Compact(hexNut));
        Assert.Equal("热熔螺母 M3*5 Ø4.6 安装1", FastenerSelectionSummaryFormatter.Compact(insert));
    }

    [Fact]
    public void FullSummary_UsesCompleteCupHeadNameAndFormula()
    {
        var component = Screw(
            FastenerKind.SocketCap,
            length: 30,
            new HoleTargetBinding
            {
                Role = ShaftFitRole.ThreadEngagement,
                DepthMode = DepthMode.FastenerLengthPlusCustom,
                BlindDepth = -2
            });

        var result = FastenerSelectionSummaryFormatter.Full(component);

        Assert.Contains("内六角杯头螺丝 M3×30 mm", result);
        Assert.Contains("螺杆长度 -2 mm", result);
        Assert.Contains("咬合宿主 1", result);
    }

    private static FastenerComponentData Screw(
        FastenerKind kind,
        double length,
        params HoleTargetBinding[] bindings) => new()
        {
            Kind = kind,
            Size = "M3",
            Length = length,
            Bindings = bindings
        };
}
