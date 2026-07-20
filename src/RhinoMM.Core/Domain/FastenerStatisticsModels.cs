namespace RhinoMM.Core.Domain;

public enum FastenerStatisticsScope
{
    Selected,
    All
}

public sealed record FastenerStatisticsRow(
    FastenerKind Kind,
    string Size,
    double? Length,
    double? OuterDiameter,
    int Quantity);

public sealed record FastenerStatisticsReport(
    FastenerStatisticsScope Scope,
    int TotalCount,
    int ScrewCount,
    int NutCount,
    int MaterialCount,
    IReadOnlyList<FastenerStatisticsRow> SummaryRows,
    IReadOnlyList<FastenerComponentData> Components);
