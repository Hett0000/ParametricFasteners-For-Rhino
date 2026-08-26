namespace RhinoMM.Core.Domain;

public enum AssemblyInspectionSeverity
{
    Info,
    Warning,
    Error
}

public sealed record AssemblyInspectionProfile(
    double MinimumWallThickness = 0.8,
    double MinimumHoleLigament = 0.8,
    double ToolRadialClearance = 2.0,
    double ToolAxialClearance = 10.0)
{
    public bool IsValid => MinimumWallThickness >= 0
        && MinimumHoleLigament >= 0
        && ToolRadialClearance >= 0
        && ToolAxialClearance >= 0
        && double.IsFinite(MinimumWallThickness)
        && double.IsFinite(MinimumHoleLigament)
        && double.IsFinite(ToolRadialClearance)
        && double.IsFinite(ToolAxialClearance);
}

public sealed record FastenerLengthSuggestion(
    double CurrentLength,
    double SuggestedLength,
    double Difference,
    string Explanation);

public sealed record AssemblyInspectionIssue(
    Guid ComponentId,
    Guid HostId,
    AssemblyInspectionSeverity Severity,
    string Code,
    string Message,
    string SuggestedAction,
    double LocationX,
    double LocationY,
    double LocationZ,
    FastenerLengthSuggestion? LengthSuggestion = null,
    ScrewAssemblyMode? SuggestedAssemblyMode = null);

public sealed record AssemblyInspectionReport(
    IReadOnlyList<AssemblyInspectionIssue> Issues,
    int ComponentCount,
    DateTimeOffset InspectedAt)
{
    public int ErrorCount => Issues.Count(issue => issue.Severity == AssemblyInspectionSeverity.Error);
    public int WarningCount => Issues.Count(issue => issue.Severity == AssemblyInspectionSeverity.Warning);
    public int InfoCount => Issues.Count(issue => issue.Severity == AssemblyInspectionSeverity.Info);
    public bool CanDeliver => ErrorCount == 0;
}
