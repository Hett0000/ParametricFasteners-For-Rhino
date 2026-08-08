using Rhino;
using RhinoMM.Core.Domain;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Services;

internal static class ComponentRefreshCoordinator
{
    public static bool TryExecute(
        RhinoDoc doc,
        IReadOnlyCollection<Guid>? preferredComponentIds,
        out ComponentRefreshResult result,
        out string message)
    {
        if (!ComponentRefreshService.CleanMissingControlPoints(doc, out result, out message))
            return false;

        var selected = ComponentRepository.ReadSelectedControlPoints(doc);
        if (selected.Count > 0)
        {
            Activate(doc, selected);
            return true;
        }

        var preferred = (preferredComponentIds ?? Array.Empty<Guid>())
            .Distinct()
            .Select(id => ComponentRepository.TryReadComponent(doc, id, out var component)
                ? component
                : null)
            .Where(component => component is not null)
            .Cast<FastenerComponentData>()
            .ToArray();
        if (preferred.Length > 0)
            Activate(doc, preferred);
        else
            ComponentEditorSession.Forget(doc);
        return true;
    }

    private static void Activate(RhinoDoc doc, IReadOnlyList<FastenerComponentData> components) =>
        ComponentEditorSession.ActivateMany(
            doc,
            components,
            false,
            ComponentActivationIntent.SynchronizeOnly);
}
