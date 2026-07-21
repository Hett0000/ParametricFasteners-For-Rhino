using Rhino;
using RhinoMM.Core.Domain;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Services;

public sealed class ComponentChangedEventArgs(RhinoDoc document, FastenerComponentData component) : EventArgs
{
    public RhinoDoc Document { get; } = document;
    public FastenerComponentData Component { get; } = component;
}

public sealed class ComponentSelectionChangedEventArgs(
    RhinoDoc document,
    IReadOnlyList<FastenerComponentData> components) : EventArgs
{
    public RhinoDoc Document { get; } = document;
    public IReadOnlyList<FastenerComponentData> Components { get; } = components;
}

public static class ComponentEditorSession
{
    private static readonly Dictionary<uint, FastenerComponentData> ActiveComponents = [];
    private static readonly Dictionary<uint, IReadOnlyList<FastenerComponentData>> ActiveSelections = [];

    public static event EventHandler<ComponentChangedEventArgs>? ActiveComponentChanged;
    public static event EventHandler<ComponentSelectionChangedEventArgs>? ActiveSelectionChanged;

    public static void Activate(RhinoDoc doc, FastenerComponentData component, bool deselectComponent = false)
        => ActivateMany(doc, [component], deselectComponent);

    public static void ActivateMany(
        RhinoDoc doc,
        IReadOnlyList<FastenerComponentData> components,
        bool deselectComponent = false)
    {
        if (components.Count == 0)
        {
            Forget(doc);
            return;
        }
        ActiveSelections[doc.RuntimeSerialNumber] = components.ToArray();
        ActiveComponents[doc.RuntimeSerialNumber] = components[0];
        EditorState.Current.Load(components[0]);
        if (deselectComponent)
        {
            doc.Objects.UnselectAll(false);
            doc.Views.Redraw();
        }
        ActiveSelectionChanged?.Invoke(null, new ComponentSelectionChangedEventArgs(doc, components));
        if (components.Count == 1)
            ActiveComponentChanged?.Invoke(null, new ComponentChangedEventArgs(doc, components[0]));
    }

    public static bool TryActivateSelection(RhinoDoc doc, bool deselectComponent, out FastenerComponentData component)
    {
        if (!ComponentRepository.TryReadSelection(doc, out component))
            return false;
        Activate(doc, component, deselectComponent);
        return true;
    }

    public static bool TryActivateSelectionSet(
        RhinoDoc doc,
        bool deselectComponent,
        out IReadOnlyList<FastenerComponentData> components)
    {
        components = ComponentRepository.ReadSelectedControlPoints(doc);
        if (components.Count == 0)
            return false;
        ActivateMany(doc, components, deselectComponent);
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

    public static bool TryGetActiveSet(RhinoDoc doc, out IReadOnlyList<FastenerComponentData> components)
    {
        if (!ActiveSelections.TryGetValue(doc.RuntimeSerialNumber, out var cached))
        {
            components = [];
            return false;
        }
        var current = new List<FastenerComponentData>();
        foreach (var item in cached)
        {
            if (ComponentRepository.TryReadComponent(doc, item.ComponentId, out var component))
                current.Add(component);
        }
        if (current.Count == 0)
        {
            Forget(doc);
            components = [];
            return false;
        }
        ActiveSelections[doc.RuntimeSerialNumber] = current;
        ActiveComponents[doc.RuntimeSerialNumber] = current[0];
        components = current;
        return true;
    }

    public static void UpdateCache(RhinoDoc doc, FastenerComponentData component)
    {
        ActiveComponents[doc.RuntimeSerialNumber] = component;
        if (ActiveSelections.TryGetValue(doc.RuntimeSerialNumber, out var selection))
            ActiveSelections[doc.RuntimeSerialNumber] = selection
                .Select(item => item.ComponentId == component.ComponentId ? component : item)
                .ToArray();
    }

    public static void UpdateCachedComponents(
        RhinoDoc doc,
        IReadOnlyList<FastenerComponentData> components)
    {
        var updates = components.ToDictionary(component => component.ComponentId);
        if (ActiveComponents.TryGetValue(doc.RuntimeSerialNumber, out var active)
            && updates.TryGetValue(active.ComponentId, out var updatedActive))
            ActiveComponents[doc.RuntimeSerialNumber] = updatedActive;
        if (ActiveSelections.TryGetValue(doc.RuntimeSerialNumber, out var selection))
            ActiveSelections[doc.RuntimeSerialNumber] = selection
                .Select(item => updates.GetValueOrDefault(item.ComponentId, item))
                .ToArray();
    }

    public static void ForgetComponent(RhinoDoc doc, Guid componentId)
    {
        if (!ActiveSelections.TryGetValue(doc.RuntimeSerialNumber, out var selection))
            return;
        var remaining = selection.Where(item => item.ComponentId != componentId).ToArray();
        if (remaining.Length == selection.Count)
            return;
        if (remaining.Length == 0)
        {
            Forget(doc);
            return;
        }
        ActiveSelections[doc.RuntimeSerialNumber] = remaining;
        ActiveComponents[doc.RuntimeSerialNumber] = remaining[0];
        EditorState.Current.Load(remaining[0]);
        ActiveSelectionChanged?.Invoke(null, new ComponentSelectionChangedEventArgs(doc, remaining));
    }

    public static void Forget(RhinoDoc doc)
    {
        ActiveComponents.Remove(doc.RuntimeSerialNumber);
        ActiveSelections.Remove(doc.RuntimeSerialNumber);
        ActiveSelectionChanged?.Invoke(null, new ComponentSelectionChangedEventArgs(doc, []));
    }
}
