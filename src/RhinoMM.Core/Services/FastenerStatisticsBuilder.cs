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
        var items = components
            .SelectMany(component => ExpandPhysicalItems(component))
            .ToArray();
        var summary = items
            .GroupBy(item => new StatisticsKey(
                item.Kind,
                item.NutStyle,
                item.Size,
                item.Length,
                item.OuterDiameter))
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
        var nutCount = items.Count(item => FastenerKindTraits.IsNut(item.Kind));
        return new FastenerStatisticsReport(
            scope,
            items.Length,
            items.Length - nutCount,
            nutCount,
            summary.Length,
            summary,
            components);
    }

    private static IEnumerable<StatisticsItem> ExpandPhysicalItems(FastenerComponentData component)
    {
        yield return new StatisticsItem(
            component.Kind,
            component.Kind == FastenerKind.HexNut ? component.HexNutStyle : null,
            component.Size,
            FastenerKindTraits.UsesLengthInStatistics(component.Kind)
                ? Math.Round(component.Length, 3)
                : null,
            component.Kind == FastenerKind.HeatSetInsert
                ? Math.Round(component.InsertOuterDiameter, 3)
                : null);
        if (FastenerKindTraits.IsScrew(component.Kind)
            && component.AssemblyMode == ScrewAssemblyMode.NutFastened)
        {
            yield return new StatisticsItem(
                FastenerKind.HexNut,
                component.PairedNutStyle,
                component.Size,
                null,
                null);
        }
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

    private sealed record StatisticsItem(
        FastenerKind Kind,
        HexNutStyle? NutStyle,
        string Size,
        double? Length,
        double? OuterDiameter);
}
