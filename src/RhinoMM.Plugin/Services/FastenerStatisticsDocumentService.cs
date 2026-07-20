using Rhino;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Services;

public sealed record FastenerStatisticsSnapshot(
    FastenerStatisticsReport Report,
    int SelectedCount,
    int AllCount,
    int IgnoredComponentCount);

public static class FastenerStatisticsDocumentService
{
    public static FastenerStatisticsSnapshot Build(RhinoDoc doc, FastenerStatisticsScope scope)
    {
        var selected = ComponentRepository.ReadSelectedControlPoints(doc);
        var all = ComponentRepository.ReadAllControlPoints(doc, out var ignoredComponentCount);
        var source = scope == FastenerStatisticsScope.Selected ? selected : all;
        return new FastenerStatisticsSnapshot(
            FastenerStatisticsBuilder.Build(source, scope),
            selected.Count,
            all.Count,
            ignoredComponentCount);
    }
}
