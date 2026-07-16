using Rhino;
using RhinoMM.Core.Domain;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Services;

public sealed class ComponentChangedEventArgs(RhinoDoc document, FastenerComponentData component) : EventArgs
{
    public RhinoDoc Document { get; } = document;
    public FastenerComponentData Component { get; } = component;
}

public static class ComponentEditorSession
{
    private static readonly Dictionary<uint, FastenerComponentData> ActiveComponents = [];

    public static event EventHandler<ComponentChangedEventArgs>? ActiveComponentChanged;

    public static void Activate(RhinoDoc doc, FastenerComponentData component, bool deselectComponent = false)
    {
        ActiveComponents[doc.RuntimeSerialNumber] = component;
        EditorState.Current.Load(component);
        if (deselectComponent)
        {
            var ids = ComponentRepository.FindComponentObjects(doc, component.ComponentId)
                .Select(obj => obj.Id)
                .ToArray();
            if (ids.Length > 0)
                doc.Objects.Select(ids, false);
            doc.Views.Redraw();
        }
        ActiveComponentChanged?.Invoke(null, new ComponentChangedEventArgs(doc, component));
    }

    public static bool TryActivateSelection(RhinoDoc doc, bool deselectComponent, out FastenerComponentData component)
    {
        if (!ComponentRepository.TryReadSelection(doc, out component))
            return false;
        Activate(doc, component, deselectComponent);
        return true;
    }

    public static bool TryGetActive(RhinoDoc doc, out FastenerComponentData component)
    {
        if (ActiveComponents.TryGetValue(doc.RuntimeSerialNumber, out var cached)
            && ComponentRepository.TryReadComponent(doc, cached.ComponentId, out component))
        {
            ActiveComponents[doc.RuntimeSerialNumber] = component;
            return true;
        }
        component = new FastenerComponentData();
        return false;
    }

    public static void Forget(RhinoDoc doc) => ActiveComponents.Remove(doc.RuntimeSerialNumber);
}
