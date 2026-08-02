using System.Globalization;
using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class FastenerStatisticsBuilder
{
    public static FastenerStatisticsReport Build(
        IEnumerable<FastenerComponentData> source,
        FastenerStatisticsScope scope)
    {
        var components = source
            .GroupBy(component => component.ComponentId)
            .Select(group => group.First())
            .OrderBy(component => component.Kind)
            .ThenBy(component => NominalSize(component.Size))
            .ThenBy(component => component.Kind == FastenerKind.HexNut ? component.HexNutStyle : 0)
            .ThenBy(component => FastenerKindTraits.UsesLengthInStatistics(component.Kind) ? component.Length : 0)
            .ThenBy(component => component.Kind == FastenerKind.HeatSetInsert ? component.InsertOuterDiameter : 0)
            .ThenBy(component => component.ComponentId)
            .ToArray();
        var summary = components
            .GroupBy(component => new StatisticsKey(
                component.Kind,
                component.Kind == FastenerKind.HexNut ? component.HexNutStyle : null,
                component.Size,
                FastenerKindTraits.UsesLengthInStatistics(component.Kind)
                    ? Math.Round(component.Length, 3)
                    : null,
                component.Kind == FastenerKind.HeatSetInsert
                    ? Math.Round(component.InsertOuterDiameter, 3)
                    : null))
            .Select(group => new FastenerStatisticsRow(
                group.Key.Kind,
                group.Key.NutStyle,
                group.Key.Size,
                group.Key.Length,
                group.Key.OuterDiameter,
                group.Count()))
            .OrderBy(row => row.Kind)
            .ThenBy(row => NominalSize(row.Size))
            .ThenBy(row => row.Length ?? 0)
            .ThenBy(row => row.OuterDiameter ?? 0)
            .ToArray();
        var nutCount = components.Count(component => FastenerKindTraits.IsNut(component.Kind));
        return new FastenerStatisticsReport(
            scope,
            components.Length,
            components.Length - nutCount,
            nutCount,
            summary.Length,
            summary,
            components);
    }

    private static double NominalSize(string size) =>
        double.TryParse(
            size.TrimStart('M', 'm'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : double.MaxValue;

    private sealed record StatisticsKey(
        FastenerKind Kind,
        HexNutStyle? NutStyle,
        string Size,
        double? Length,
        double? OuterDiameter);
}
