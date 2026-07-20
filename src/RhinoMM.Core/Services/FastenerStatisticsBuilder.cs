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
            .ThenBy(component => component.Kind == FastenerKind.HexNut ? 0 : component.Length)
            .ThenBy(component => component.ComponentId)
            .ToArray();
        var summary = components
            .GroupBy(component => new StatisticsKey(
                component.Kind,
                component.Size,
                component.Kind == FastenerKind.HexNut
                    ? null
                    : Math.Round(component.Length, 3)))
            .Select(group => new FastenerStatisticsRow(
                group.Key.Kind,
                group.Key.Size,
                group.Key.Length,
                group.Count()))
            .OrderBy(row => row.Kind)
            .ThenBy(row => NominalSize(row.Size))
            .ThenBy(row => row.Length ?? 0)
            .ToArray();
        var nutCount = components.Count(component => component.Kind == FastenerKind.HexNut);
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

    private sealed record StatisticsKey(FastenerKind Kind, string Size, double? Length);
}
