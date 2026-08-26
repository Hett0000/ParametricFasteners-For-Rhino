using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class AssemblySuggestionCalculatorTests
{
    private static readonly FastenerSizeSpec M3 = FastenerCatalog.LoadEmbedded().Get("M3");

    [Fact]
    public void ThreadEngagement_PrefersCurrentModeAndShortestCommonLength()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var component = Screw(ScrewAssemblyMode.ThreadEngagement, 6);
        var measurement = Snapshot(first,
            new AssemblyHostMeasurement(first, 0, 4, 0),
            new AssemblyHostMeasurement(second, 5, 15, 1));

        var result = AssemblySuggestionCalculator.Create(component, M3, measurement);

        Assert.False(result.IsBlocked);
        Assert.Equal(ScrewAssemblyMode.ThreadEngagement, result.RecommendedMode);
        Assert.Equal(8, result.SuggestedLength);
        Assert.False(result.ChangesAssemblyMode);
    }

    [Fact]
    public void EngagementOnly_UsesLongestCommonLengthInsideHost()
    {
        var host = Guid.NewGuid();
        var component = Screw(ScrewAssemblyMode.EngagementOnly, 20);

        var result = AssemblySuggestionCalculator.Create(component, M3,
            Snapshot(host, new AssemblyHostMeasurement(host, 0, 18, 0)));

        Assert.Equal(ScrewAssemblyMode.EngagementOnly, result.RecommendedMode);
        Assert.Equal(15, result.SuggestedLength);
    }

    [Fact]
    public void InvalidCurrentMode_ReturnsConfirmableAlternative()
    {
        var host = Guid.NewGuid();
        var component = Screw(ScrewAssemblyMode.ThreadEngagement, 12);

        var result = AssemblySuggestionCalculator.Create(component, M3,
            Snapshot(host, new AssemblyHostMeasurement(host, 0, 18, 0)));

        Assert.Equal(ScrewAssemblyMode.EngagementOnly, result.RecommendedMode);
        Assert.True(result.ChangesAssemblyMode);
        Assert.Equal(AssemblySuggestionStatus.Warning, result.Status);
    }

    [Fact]
    public void OverlappingHosts_AreBlockedInsteadOfGuessed()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var result = AssemblySuggestionCalculator.Create(
            Screw(ScrewAssemblyMode.ThreadEngagement, 20),
            M3,
            Snapshot(first,
                new AssemblyHostMeasurement(first, 0, 8, 0),
                new AssemblyHostMeasurement(second, 7, 14, 1)));

        Assert.True(result.IsBlocked);
        Assert.Contains("重叠", result.MeasurementBasis);
    }

    private static FastenerComponentData Screw(ScrewAssemblyMode mode, double length) => new()
    {
        Kind = FastenerKind.SocketCap,
        Size = "M3",
        Length = length,
        AssemblyMode = mode,
        HeadEmbedDepth = 0,
        NutTipProtrusion = 2
    };

    private static AssemblyMeasurementSnapshot Snapshot(
        Guid placementHostId,
        params AssemblyHostMeasurement[] hosts) => new(
        placementHostId,
        hosts,
        true,
        "可靠测量");
}
