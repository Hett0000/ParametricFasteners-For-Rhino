using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Geometry;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Services;

internal enum ComponentHealthState
{
    Healthy,
    MaintenanceRequired,
    RelinkRequired,
    PresentationDrift,
    Configuration,
    LegacyUnverified,
    Corrupt,
    BooleanFailure
}

internal sealed record IndexedComponentEntry(
    Guid ComponentId,
    Guid ControlPointObjectId,
    FastenerComponentData? Component,
    Point3d Location,
    IReadOnlySet<ComponentHealthState> States,
    IReadOnlyList<Guid> HostIds,
    IReadOnlyList<int> HostLayerIndices,
    string ShortId,
    string SearchText,
    string IssueText);

/// <summary>
/// Rebuildable, document-local runtime index. 3DM user strings remain the only
/// persistent source of truth; this service never writes document data.
/// </summary>
internal static class FastenerDocumentIndexService
{
    private sealed class DocumentIndex : IDisposable
    {
        public bool Dirty { get; set; } = true;
        public long Revision { get; set; }
        public RTree HostTree { get; set; } = new();
        public Dictionary<int, SmartPlacementHost> HostsByIndex { get; } = [];
        public List<IndexedComponentEntry> Components { get; } = [];

        public void Dispose() => HostTree.Dispose();
    }

    private static readonly Dictionary<uint, DocumentIndex> Indexes = [];
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
            return;
        RhinoDoc.AddRhinoObject += DocumentChanged;
        RhinoDoc.DeleteRhinoObject += DocumentChanged;
        RhinoDoc.ReplaceRhinoObject += ObjectReplaced;
        RhinoDoc.UndeleteRhinoObject += DocumentChanged;
        RhinoDoc.ModifyObjectAttributes += AttributesChanged;
        RhinoDoc.EndOpenDocument += DocumentOpened;
        RhinoDoc.CloseDocument += DocumentClosed;
        Command.UndoRedo += UndoRedo;
        _initialized = true;
    }

    public static void Shutdown()
    {
        if (!_initialized)
            return;
        RhinoDoc.AddRhinoObject -= DocumentChanged;
        RhinoDoc.DeleteRhinoObject -= DocumentChanged;
        RhinoDoc.ReplaceRhinoObject -= ObjectReplaced;
        RhinoDoc.UndeleteRhinoObject -= DocumentChanged;
        RhinoDoc.ModifyObjectAttributes -= AttributesChanged;
        RhinoDoc.EndOpenDocument -= DocumentOpened;
        RhinoDoc.CloseDocument -= DocumentClosed;
        Command.UndoRedo -= UndoRedo;
        foreach (var index in Indexes.Values)
            index.Dispose();
        Indexes.Clear();
        _initialized = false;
    }

    public static long Revision(RhinoDoc doc) => Ensure(doc).Revision;

    internal static long CurrentRevision(RhinoDoc doc) => GetOrCreate(doc).Revision;

    public static IReadOnlyList<SmartPlacementHost> AllHosts(RhinoDoc doc) =>
        Ensure(doc).HostsByIndex.Values.Where(host => IsAvailableHost(doc, host)).ToArray();

    public static IReadOnlyList<SmartPlacementHost> QueryHosts(
        RhinoDoc doc,
        BoundingBox searchBox)
    {
        var index = Ensure(doc);
        var result = new List<SmartPlacementHost>();
        var ids = new HashSet<Guid>();
        index.HostTree.Search(searchBox, (_, args) =>
        {
            if (!index.HostsByIndex.TryGetValue(args.Id, out var host)
                || !IsAvailableHost(doc, host)
                || !ids.Add(host.ObjectId))
                return;
            result.Add(host);
        });

        // Rhino can temporarily detach cached RhinoObject wrappers from their
        // document while commands or table events are being processed. The
        // RTree remains the fast path, but a zero-result query must not disable
        // placement for an otherwise valid document. Recheck the indexed host
        // boxes through the current document before reporting a true miss.
        if (result.Count == 0)
        {
            foreach (var host in index.HostsByIndex.Values)
            {
                if (!BoxesIntersect(host.BoundingBox, searchBox)
                    || !IsAvailableHost(doc, host)
                    || !ids.Add(host.ObjectId))
                    continue;
                result.Add(host);
            }
        }

        // Last-resort compatibility path: use the exact host capture routine
        // that powered smart placement before 0.32. This covers document-table
        // timing edge cases where the lazily rebuilt index has not yet observed
        // a newly opened or replaced Rhino object. It runs only after both the
        // RTree and indexed-box paths return no candidates.
        if (result.Count == 0)
        {
            foreach (var host in SmartHostBindingService.CaptureHosts(doc))
            {
                if (!BoxesIntersect(host.BoundingBox, searchBox)
                    || !ids.Add(host.ObjectId))
                    continue;
                result.Add(host);
            }
        }
        return result;
    }

    public static IReadOnlyList<IndexedComponentEntry> Components(RhinoDoc doc) =>
        Ensure(doc).Components.ToArray();

    public static void Invalidate(RhinoDoc doc)
    {
        var index = GetOrCreate(doc);
        index.Dirty = true;
        index.Revision++;
    }

    private static DocumentIndex Ensure(RhinoDoc doc)
    {
        var index = GetOrCreate(doc);
        if (!index.Dirty)
            return index;
        Rebuild(doc, index);
        return index;
    }

    private static DocumentIndex GetOrCreate(RhinoDoc doc)
    {
        if (!Indexes.TryGetValue(doc.RuntimeSerialNumber, out var index))
        {
            index = new DocumentIndex();
            Indexes[doc.RuntimeSerialNumber] = index;
        }
        return index;
    }

    private static void Rebuild(RhinoDoc doc, DocumentIndex index)
    {
        index.HostTree.Dispose();
        index.HostTree = new RTree();
        index.HostsByIndex.Clear();
        index.Components.Clear();

        var hostCounter = 0;
        foreach (var obj in doc.Objects)
        {
            if (!TryCreateHost(obj, out var host))
                continue;
            var id = hostCounter++;
            index.HostsByIndex[id] = host;
            index.HostTree.Insert(host.BoundingBox, id);
        }

        BuildComponentEntries(doc, index.Components);
        index.Dirty = false;
        index.Revision++;
    }

    private static void BuildComponentEntries(
        RhinoDoc doc,
        ICollection<IndexedComponentEntry> result)
    {
        var groups = doc.Objects
            .Select(obj => new
            {
                Object = obj,
                Text = obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey)
            })
            .Where(item => Guid.TryParse(item.Text, out _))
            .GroupBy(item => Guid.Parse(item.Text!));

        foreach (var group in groups)
        {
            var objects = group.Select(item => item.Object).ToArray();
            var controls = objects.Where(obj =>
                obj.Attributes.GetUserString(ComponentRepository.RoleKey) == "ControlPoint"
                && obj.Geometry is Point).ToArray();
            var control = controls.Length == 1 ? controls[0] : null;
            if (control is null || !ComponentRepository.TryReadControlPoint(control, out var component))
            {
                var fallback = control?.Geometry is Point point
                    ? point.Location
                    : objects.Select(obj => obj.Geometry.GetBoundingBox(true))
                        .FirstOrDefault(box => box.IsValid).Center;
                result.Add(new IndexedComponentEntry(
                    group.Key,
                    control?.Id ?? Guid.Empty,
                    null,
                    fallback,
                    new HashSet<ComponentHealthState> { ComponentHealthState.Corrupt },
                    [],
                    [],
                    group.Key.ToString("N")[..8],
                    group.Key.ToString("N"),
                    "组件数据损坏或缺少有效控制点。"));
                continue;
            }

            var states = new HashSet<ComponentHealthState>();
            var issues = new List<string>();
            var validation = FastenerComponentValidator.Validate(
                component,
                FastenerSpecResolver.Resolve(component, RhinoMMPlugIn.Catalog));
            if (!validation.IsValid)
            {
                states.Add(ComponentHealthState.Corrupt);
                issues.AddRange(validation.Issues.Where(issue => issue.IsError).Select(issue => issue.Message));
            }
            var missingTargets = component.Bindings
                .Where(binding => binding.TargetObjectId == Guid.Empty
                    || doc.Objects.FindId(binding.TargetObjectId) is null)
                .ToArray();
            if (missingTargets.Length > 0 || ComponentHostResolver.NeedsRelink(component))
            {
                states.Add(ComponentHealthState.RelinkRequired);
                issues.Add("存在丢失或待重新绑定的宿主。 ");
            }
            var savedOrigin = FastenerGeometryFactory.ToPlane(component.Placement).Origin;
            var actualOrigin = ((Point)control.Geometry).Location;
            var derived = objects.Where(obj => obj.Id != control.Id).ToArray();
            var controlSourceSignature = control.Attributes.GetUserString(ComponentRepository.SourceSignatureKey);
            var hasCurrentHealthMetadata = !string.IsNullOrWhiteSpace(
                    control.Attributes.GetUserString(ComponentRepository.DerivedManifestKey))
                && !string.IsNullOrWhiteSpace(controlSourceSignature)
                && derived.All(obj => string.Equals(
                    obj.Attributes.GetUserString(ComponentRepository.SourceSignatureKey),
                    controlSourceSignature,
                    StringComparison.Ordinal));
            var geometryChanged = hasCurrentHealthMetadata && derived.Any(obj =>
            {
                var savedSignature = obj.Attributes.GetUserString(ComponentRepository.GeometrySignatureKey);
                return !string.IsNullOrWhiteSpace(savedSignature)
                    && savedSignature != ComponentReliabilitySignatureService.GeometrySignature(obj.Geometry);
            });
            if (actualOrigin.DistanceTo(savedOrigin) > Math.Max(doc.ModelAbsoluteTolerance, 1e-6)
                || geometryChanged)
            {
                states.Add(ComponentHealthState.MaintenanceRequired);
                issues.Add("控制点位置或派生对象几何需要同步。 ");
            }
            if (!hasCurrentHealthMetadata)
                states.Add(ComponentHealthState.LegacyUnverified);
            if (component.Bindings.Any(binding => !binding.IsBooleanEnabled || !binding.IsPreviewVisible))
                states.Add(ComponentHealthState.Configuration);
            if (states.Count == 0)
                states.Add(ComponentHealthState.Healthy);

            var hostIds = component.Bindings
                .Select(binding => binding.TargetObjectId)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToArray();
            var hostLayers = hostIds
                .Select(id => doc.Objects.FindId(id)?.Attributes.LayerIndex ?? -1)
                .Where(index => index >= 0)
                .Distinct()
                .ToArray();
            var type = component.Kind == FastenerKind.HexNut
                ? FastenerLabels.NutStyle(component.HexNutStyle)
                : FastenerLabels.Kind(component.Kind);
            var hostNames = hostIds.Select(id => HostName(doc.Objects.FindId(id)));
            var search = string.Join(" ", new[]
            {
                type,
                component.Size,
                component.AssemblyMode.ToString(),
                component.Delivery.AssemblyNumber,
                component.Delivery.ProjectGroup,
                component.Delivery.UserNote,
                component.CustomDefinitionName,
                group.Key.ToString("N"),
                string.Join(" ", hostNames)
            });
            result.Add(new IndexedComponentEntry(
                component.ComponentId,
                control.Id,
                component,
                ((Point)control.Geometry).Location,
                states,
                hostIds,
                hostLayers,
                component.ComponentId.ToString("N")[..8],
                search,
                issues.Count == 0 ? string.Empty : string.Join(" ", issues.Distinct())));
        }
    }

    private static bool TryCreateHost(RhinoObject obj, out SmartPlacementHost host)
    {
        host = null!;
        if (obj.Geometry is not (Brep or Extrusion)
            || !string.IsNullOrWhiteSpace(
                obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey)))
            return false;
        var brep = obj.Geometry switch
        {
            Brep source => source,
            Extrusion extrusion => extrusion.ToBrep(),
            _ => null
        };
        if (brep is null || !brep.IsSolid)
            return false;
        var box = brep.GetBoundingBox(true);
        if (!box.IsValid)
            return false;
        host = new SmartPlacementHost(obj.Id, obj, brep, box);
        return true;
    }

    private static bool IsAvailableHost(RhinoDoc doc, SmartPlacementHost host)
    {
        var obj = doc.Objects.FindId(host.ObjectId);
        if (obj is null || obj.IsDeleted || obj.IsLocked || obj.IsHidden)
            return false;
        var layer = doc.Layers.FindIndex(obj.Attributes.LayerIndex);
        return layer is null || (layer.IsVisible && !layer.IsLocked);
    }

    private static bool BoxesIntersect(BoundingBox left, BoundingBox right) =>
        left.IsValid
        && right.IsValid
        && left.Min.X <= right.Max.X && left.Max.X >= right.Min.X
        && left.Min.Y <= right.Max.Y && left.Max.Y >= right.Min.Y
        && left.Min.Z <= right.Max.Z && left.Max.Z >= right.Min.Z;

    private static string HostName(RhinoObject? host) => host is null
        ? "宿主丢失"
        : string.IsNullOrWhiteSpace(host.Attributes.Name)
            ? host.Id.ToString("N")[..8]
            : host.Attributes.Name;

    private static void DocumentChanged(object? sender, RhinoObjectEventArgs e)
    {
        var doc = sender as RhinoDoc ?? e.TheObject.Document;
        if (doc is not null)
            Invalidate(doc);
    }

    private static void ObjectReplaced(object? sender, RhinoReplaceObjectEventArgs e)
    {
        var doc = sender as RhinoDoc ?? e.OldRhinoObject?.Document ?? e.NewRhinoObject?.Document;
        if (doc is not null)
            Invalidate(doc);
    }

    private static void AttributesChanged(object? sender, RhinoModifyObjectAttributesEventArgs e)
    {
        var doc = sender as RhinoDoc ?? e.RhinoObject.Document;
        if (doc is not null)
            Invalidate(doc);
    }

    private static void DocumentOpened(object? sender, DocumentOpenEventArgs e) =>
        Invalidate(e.Document);

    private static void DocumentClosed(object? sender, DocumentEventArgs e)
    {
        if (Indexes.Remove(e.DocumentSerialNumber, out var index))
            index.Dispose();
    }

    private static void UndoRedo(object? sender, UndoRedoEventArgs e)
    {
        if ((e.IsEndUndo || e.IsEndRedo) && RhinoDoc.ActiveDoc is { } doc)
            Invalidate(doc);
    }
}
