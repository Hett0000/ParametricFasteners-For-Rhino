using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class FastenerStatisticsBuilderTests
{
    [Fact]
    public void Build_GroupsScrewsByKindSizeAndLength()
    {
        var report = FastenerStatisticsBuilder.Build(
        [
            Component(FastenerKind.SocketCap, "M3", 12),
            Component(FastenerKind.SocketCap, "M3", 12),
            Component(FastenerKind.SocketCap, "M3", 20),
            Component(FastenerKind.Countersunk, "M3", 12)
        ], FastenerStatisticsScope.All);

        Assert.Equal(4, report.TotalCount);
        Assert.Equal(4, report.ScrewCount);
        Assert.Equal(0, report.NutCount);
        Assert.Equal(3, report.MaterialCount);
        Assert.Equal(2, report.SummaryRows.Single(row =>
            row.Kind == FastenerKind.SocketCap && row.Size == "M3" && row.Length == 12).Quantity);
    }

    [Fact]
    public void Build_IgnoresLengthForHexNuts()
    {
        var report = FastenerStatisticsBuilder.Build(
        [
            Component(FastenerKind.HexNut, "M5", 1),
            Component(FastenerKind.HexNut, "M5", 99)
        ], FastenerStatisticsScope.Selected);

        var row = Assert.Single(report.SummaryRows);
        Assert.Null(row.Length);
        Assert.Equal(2, row.Quantity);
        Assert.Equal(2, report.NutCount);
        Assert.Equal(FastenerStatisticsScope.Selected, report.Scope);
    }

    [Fact]
    public void Build_DeduplicatesComponentIdsAndSortsNominalSizes()
    {
        var m10 = Component(FastenerKind.SocketCap, "M10", 12);
        var m2 = Component(FastenerKind.SocketCap, "M2", 12);
        var report = FastenerStatisticsBuilder.Build(
            [m10, m2, m2],
            FastenerStatisticsScope.All);

        Assert.Equal(2, report.TotalCount);
        Assert.Equal(["M2", "M10"], report.SummaryRows.Select(row => row.Size));
    }

    [Fact]
    public void Build_HandlesEmptyInput()
    {
        var report = FastenerStatisticsBuilder.Build([], FastenerStatisticsScope.All);

        Assert.Equal(0, report.TotalCount);
        Assert.Empty(report.SummaryRows);
        Assert.Empty(report.Components);
    }

    private static FastenerComponentData Component(FastenerKind kind, string size, double length) => new()
    {
        ComponentId = Guid.NewGuid(),
        Kind = kind,
        Size = size,
        Length = length
    };
}
