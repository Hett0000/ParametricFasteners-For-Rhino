namespace RhinoMM.Core.Domain;

public enum AssemblySuggestionStatus
{
    Available,
    Warning,
    Blocked
}

public enum AssemblySuggestionPolicy
{
    TemplatePreferred
}

public sealed record AssemblyHostMeasurement(
    Guid HostId,
    double Entry,
    double Exit,
    int Order)
{
    public double Thickness => Math.Max(0, Exit - Entry);
}

public sealed record AssemblyMeasurementSnapshot(
    Guid PlacementHostId,
    IReadOnlyList<AssemblyHostMeasurement> Hosts,
    bool IsReliable,
    string Message)
{
    public AssemblyHostMeasurement? FirstHost => Hosts.Count == 0 ? null : Hosts[0];
    public AssemblyHostMeasurement? LastHost => Hosts.Count == 0 ? null : Hosts[^1];
}

public sealed record AssemblySuggestionOption(
    ScrewAssemblyMode AssemblyMode,
    double SuggestedLength,
    string Explanation,
    bool IsTemplateMode,
    bool IsCurrentLengthValid);

public sealed record AssemblySuggestion(
    AssemblySuggestionStatus Status,
    ScrewAssemblyMode CurrentMode,
    double CurrentLength,
    ScrewAssemblyMode RecommendedMode,
    double SuggestedLength,
    string MeasurementBasis,
    string Risk,
    IReadOnlyList<AssemblySuggestionOption> Options)
{
    public bool IsBlocked => Status == AssemblySuggestionStatus.Blocked;
    public bool ChangesAssemblyMode => RecommendedMode != CurrentMode;
}

public sealed record AssemblySchemeOptions(
    AssemblySuggestionPolicy SuggestionPolicy = AssemblySuggestionPolicy.TemplatePreferred,
    bool AdaptiveLength = false,
    int? OutputFormats = null,
    bool? IncludeFastenerSolids = null);
