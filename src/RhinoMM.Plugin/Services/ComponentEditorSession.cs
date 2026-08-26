using Rhino;
using RhinoMM.Core.Domain;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Services;

public enum ComponentActivationIntent
{
    LoadIntoEditor,
    SynchronizeOnly
}

public sealed class ComponentChangedEventArgs(RhinoDoc document, FastenerComponentData component) : EventArgs
{
    public RhinoDoc Document { get; } = document;
    public FastenerComponentData Component { get; } = component;
}

public sealed class ComponentSelectionChangedEventArgs(
    RhinoDoc document,
    IReadOnlyList<FastenerComponentData> components,
    ComponentActivationIntent intent) : EventArgs
{
    public RhinoDoc Document { get; } = document;
    public IReadOnlyList<FastenerComponentData> Components { get; } = components;
    public ComponentActivationIntent Intent { get; } = intent;
}

public static class ComponentEditorSession
{
    private static readonly Dictionary<uint, Guid> ActiveComponents = [];
    private static readonly Dictionary<uint, IReadOnlyList<Guid>> ActiveSelections = [];
    private static readonly Dictionary<uint, ComponentActivationIntent> ActiveIntents = [];
    private static readonly Dictionary<uint, long> DocumentRevisions = [];

    public static event EventHandler<ComponentChangedEventArgs>? ActiveComponentChanged;
    public static event EventHandler<ComponentSelectionChangedEventArgs>? ActiveSelectionChanged;

    public static void Activate(
        RhinoDoc doc,
        FastenerComponentData component,
        bool deselectComponent = false,
        ComponentActivationIntent intent = ComponentActivationIntent.LoadIntoEditor)
        => ActivateMany(doc, [component], deselectComponent, intent);

    public static void ActivateMany(
        RhinoDoc doc,
        IReadOnlyList<FastenerComponentData> components,
        bool deselectComponent = false,
        ComponentActivationIntent intent = ComponentActivationIntent.LoadIntoEditor)
    {
        if (components.Count == 0)
        {
            Forget(doc);
            return;
        }
        ActiveSelections[doc.RuntimeSerialNumber] = components.Select(component => component.ComponentId).ToArray();
        ActiveComponents[doc.RuntimeSerialNumber] = components[0].ComponentId;
        ActiveIntents[doc.RuntimeSerialNumber] = intent;
        DocumentRevisions[doc.RuntimeSerialNumber] = FastenerDocumentIndexService.CurrentRevision(doc);
        if (intent == ComponentActivationIntent.LoadIntoEditor)
            EditorState.Current.Load(components[0]);
        if (deselectComponent)
        {
            doc.Objects.UnselectAll(false);
            doc.Views.Redraw();
        }
        ActiveSelectionChanged?.Invoke(
            null,
            new ComponentSelectionChangedEventArgs(doc, components, intent));
        if (components.Count == 1 && intent == ComponentActivationIntent.LoadIntoEditor)
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
        if (ActiveComponents.TryGetValue(doc.RuntimeSerialNumber, out var componentId)
            && ComponentRepository.TryReadComponent(doc, componentId, out component))
        {
            DocumentRevisions[doc.RuntimeSerialNumber] = FastenerDocumentIndexService.CurrentRevision(doc);
            return true;
        }
        component = new FastenerComponentData();
        return false;
    }

    public static bool TryGetActiveSet(RhinoDoc doc, out IReadOnlyList<FastenerComponentData> components)
        => TryGetActiveSet(doc, out components, out _);

    public static bool TryGetActiveSet(
        RhinoDoc doc,
        out IReadOnlyList<FastenerComponentData> components,
        out ComponentActivationIntent intent)
    {
        intent = ActiveIntents.GetValueOrDefault(
            doc.RuntimeSerialNumber,
            ComponentActivationIntent.SynchronizeOnly);
        if (!ActiveSelections.TryGetValue(doc.RuntimeSerialNumber, out var cached))
        {
            components = [];
            return false;
        }
        var current = new List<FastenerComponentData>();
        foreach (var componentId in cached)
        {
            if (ComponentRepository.TryReadComponent(doc, componentId, out var component))
                current.Add(component);
        }
        if (current.Count == 0)
        {
            Forget(doc);
            components = [];
            return false;
        }
        ActiveSelections[doc.RuntimeSerialNumber] = current.Select(component => component.ComponentId).ToArray();
        ActiveComponents[doc.RuntimeSerialNumber] = current[0].ComponentId;
        DocumentRevisions[doc.RuntimeSerialNumber] = FastenerDocumentIndexService.CurrentRevision(doc);
        components = current;
        return true;
    }

    public static void UpdateCache(RhinoDoc doc, FastenerComponentData component)
    {
        ActiveComponents[doc.RuntimeSerialNumber] = component.ComponentId;
        DocumentRevisions[doc.RuntimeSerialNumber] = FastenerDocumentIndexService.CurrentRevision(doc);
    }

    public static void UpdateCachedComponents(
        RhinoDoc doc,
        IReadOnlyList<FastenerComponentData> components)
    {
        if (components.Count > 0)
            DocumentRevisions[doc.RuntimeSerialNumber] = FastenerDocumentIndexService.CurrentRevision(doc);
    }

    public static void ForgetComponent(RhinoDoc doc, Guid componentId)
    {
        if (!ActiveSelections.TryGetValue(doc.RuntimeSerialNumber, out var selection))
            return;
        var remaining = selection.Where(item => item != componentId).ToArray();
        if (remaining.Length == selection.Count)
            return;
        if (remaining.Length == 0)
        {
            Forget(doc);
            return;
        }
        ActiveSelections[doc.RuntimeSerialNumber] = remaining;
        ActiveComponents[doc.RuntimeSerialNumber] = remaining[0];
        var current = remaining
            .Select(id => ComponentRepository.TryReadComponent(doc, id, out var component) ? component : null)
            .Where(component => component is not null)
            .Cast<FastenerComponentData>()
            .ToArray();
        ActiveSelectionChanged?.Invoke(
            null,
            new ComponentSelectionChangedEventArgs(
                doc,
                current,
                ComponentActivationIntent.SynchronizeOnly));
    }

    public static void Forget(RhinoDoc doc)
    {
        ActiveComponents.Remove(doc.RuntimeSerialNumber);
        ActiveSelections.Remove(doc.RuntimeSerialNumber);
        ActiveIntents.Remove(doc.RuntimeSerialNumber);
        DocumentRevisions.Remove(doc.RuntimeSerialNumber);
        ActiveSelectionChanged?.Invoke(
            null,
            new ComponentSelectionChangedEventArgs(
                doc,
                [],
                ComponentActivationIntent.SynchronizeOnly));
    }
}
