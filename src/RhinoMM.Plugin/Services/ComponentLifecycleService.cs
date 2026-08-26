using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Plugin.Geometry;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Services;

public static class ComponentLifecycleService
{
    private static readonly Dictionary<(uint Document, Guid Component), int> Suppressed = [];
    private static readonly Dictionary<(uint Document, uint Event), PendingTransform> PendingTransforms = [];
    private static readonly Dictionary<uint, PendingAddedBatch> PendingAddedBatches = [];
    private static bool _initialized;
    private static bool _redirectingSelection;
    private static bool _synchronizingTransform;

    public static void Initialize()
    {
        if (_initialized)
            return;
        RhinoDoc.DeleteRhinoObject += DeleteRhinoObject;
        RhinoDoc.AddRhinoObject += AddRhinoObject;
        RhinoDoc.SelectObjects += SelectObjects;
        RhinoDoc.BeforeTransformObjects += BeforeTransformObjects;
        RhinoDoc.AfterTransformObjects += AfterTransformObjects;
        Command.BeginCommand += BeginCommand;
        Command.EndCommand += EndCommand;
        _initialized = true;
    }

    public static void Shutdown()
    {
        if (!_initialized)
            return;
        RhinoDoc.DeleteRhinoObject -= DeleteRhinoObject;
        RhinoDoc.AddRhinoObject -= AddRhinoObject;
        RhinoDoc.SelectObjects -= SelectObjects;
        RhinoDoc.BeforeTransformObjects -= BeforeTransformObjects;
        RhinoDoc.AfterTransformObjects -= AfterTransformObjects;
        Command.BeginCommand -= BeginCommand;
        Command.EndCommand -= EndCommand;
        foreach (var pending in PendingTransforms.Values)
            pending.Dispose();
        PendingTransforms.Clear();
        PendingAddedBatches.Clear();
        Suppressed.Clear();
        _redirectingSelection = false;
        _synchronizingTransform = false;
        _initialized = false;
    }

    public static IDisposable Suppress(RhinoDoc doc, Guid componentId)
    {
        var key = (doc.RuntimeSerialNumber, componentId);
        Suppressed.TryGetValue(key, out var count);
        Suppressed[key] = count + 1;
        return new SuppressionScope(key);
    }

    private static void DeleteRhinoObject(object? sender, RhinoObjectEventArgs e)
    {
        var doc = sender as RhinoDoc ?? e.TheObject.Document;
        if (doc is null
            || doc.UndoActive
            || doc.RedoActive
            || e.TheObject.Attributes.GetUserString(ComponentRepository.RoleKey) != "ControlPoint"
            || !Guid.TryParse(
                e.TheObject.Attributes.GetUserString(ComponentRepository.ComponentIdKey),
                out var componentId)
            || IsSuppressed(doc, componentId)
            || IsPendingTransform(doc, componentId))
            return;

        using var suppression = Suppress(doc, componentId);
        ComponentPresentationService.RemoveGroup(doc, componentId);
        foreach (var obj in ComponentRepository.FindComponentObjects(doc, componentId).ToList())
        {
            if (obj.Id != e.ObjectId)
                doc.Objects.Delete(obj, true);
        }
        ComponentEditorSession.ForgetComponent(doc, componentId);
        doc.Views.Redraw();
    }

    private static void AddRhinoObject(object? sender, RhinoObjectEventArgs e)
    {
        if (_synchronizingTransform)
            return;
        var doc = sender as RhinoDoc ?? e.TheObject.Document;
        if (doc is null)
            return;

        if (PendingAddedBatches.TryGetValue(doc.RuntimeSerialNumber, out var batch))
            batch.AddedObjectIds.Add(e.ObjectId);

        foreach (var pending in PendingTransforms.Values.Where(item => item.DocumentSerial == doc.RuntimeSerialNumber))
        {
            if (pending.Kind == ComponentTransformKind.Copy)
            {
                if (Guid.TryParse(
                        e.TheObject.Attributes.GetUserString(ComponentRepository.ComponentIdKey),
                        out var copiedComponentId)
                    && pending.Components.Any(item => item.Data.ComponentId == copiedComponentId))
                {
                    pending.AddedPluginObjectIds.Add(e.ObjectId);
                    continue;
                }
                if (string.IsNullOrWhiteSpace(
                        e.TheObject.Attributes.GetUserString(ComponentRepository.ComponentIdKey)))
                    pending.AddedOrdinaryObjectIds.Add(e.ObjectId);
            }

            if (!string.IsNullOrWhiteSpace(
                    e.TheObject.Attributes.GetUserString(ComponentRepository.ComponentIdKey)))
                continue;
            foreach (var target in pending.Targets.Where(item => !pending.TargetIdMap.ContainsKey(item.OldObjectId)))
            {
                if (IsTargetMatch(e.TheObject, target, doc.ModelAbsoluteTolerance))
                {
                    pending.TargetIdMap[target.OldObjectId] = e.ObjectId;
                    break;
                }
            }
        }
    }

    private static void SelectObjects(object? sender, RhinoObjectSelectionEventArgs e)
    {
        if (_redirectingSelection || !e.Selected || e.RhinoObjectCount == 0)
            return;

        var doc = e.Document;
        var selected = doc.Objects.GetSelectedObjects(false, false).ToArray();
        var selectedComponentIds = selected
            .Select(obj => obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey))
            .Where(value => Guid.TryParse(value, out _))
            .Select(Guid.Parse)
            .Distinct()
            .ToArray();
        var hasOrdinaryObject = selected.Any(obj => string.IsNullOrWhiteSpace(
            obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey)));

        if (hasOrdinaryObject && selectedComponentIds.Length > 0)
        {
            try
            {
                _redirectingSelection = true;
                foreach (var componentId in selectedComponentIds)
                {
                    foreach (var obj in ComponentRepository.FindComponentObjects(doc, componentId))
                    {
                        if (obj.Attributes.Visible)
                            doc.Objects.Select(obj.Id, true);
                    }
                }
                doc.Views.Redraw();
            }
            finally
            {
                _redirectingSelection = false;
            }
            SynchronizeRedirectedSelection(doc);
            return;
        }

        var proxyObjects = selected
            .Where(obj => obj.Attributes.GetUserString(ComponentRepository.RoleKey) is "Proxy" or "Cutter" or "HeadCutter")
            .ToArray();
        if (proxyObjects.Length == 0)
            return;

        var componentIds = proxyObjects
            .Select(obj => obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey))
            .Where(value => Guid.TryParse(value, out _))
            .Select(Guid.Parse)
            .Distinct()
            .ToArray();

        try
        {
            _redirectingSelection = true;
            var preserveExternalGroupSelection = proxyObjects.Any(obj =>
                (obj.GetGroupList() ?? []).Any(index =>
                {
                    var name = doc.Groups.GroupName(index);
                    return !string.IsNullOrWhiteSpace(name)
                        && !name.StartsWith("参数化紧固件::", StringComparison.Ordinal);
                }));
            foreach (var obj in proxyObjects)
            {
                if (!preserveExternalGroupSelection)
                    obj.Select(false);
            }
            foreach (var componentId in componentIds)
            {
                var controlPoint = ComponentRepository.FindControlPoint(doc, componentId);
                if (controlPoint is not null)
                    doc.Objects.Select(controlPoint.Id, true);
            }
            doc.Views.Redraw();
        }
        finally
        {
            _redirectingSelection = false;
        }
        SynchronizeRedirectedSelection(doc);
    }

    private static void SynchronizeRedirectedSelection(RhinoDoc doc)
    {
        var components = ComponentRepository.ReadSelectedControlPoints(doc);
        if (components.Count == 0)
            return;
        ComponentEditorSession.ActivateMany(
            doc,
            components,
            false,
            ComponentActivationIntent.SynchronizeOnly);
    }

    private static void BeforeTransformObjects(object? sender, RhinoTransformObjectsEventArgs e)
    {
        if (_synchronizingTransform)
            return;
        var doc = sender as RhinoDoc ?? e.Objects.FirstOrDefault()?.Document;
        if (doc is null)
            return;

        var transformedIds = e.Objects.Select(obj => obj.Id).ToHashSet();
        var componentIds = e.Objects
            .Select(obj => obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey))
            .Where(value => Guid.TryParse(value, out _))
            .Select(Guid.Parse)
            .Distinct()
            .ToArray();
        if (componentIds.Length == 0)
            return;

        var components = new List<PendingComponent>();
        foreach (var componentId in componentIds)
        {
            if (!ComponentRepository.TryReadComponent(doc, componentId, out var data))
                continue;
            components.Add(new PendingComponent(
                data,
                ComponentRepository.FindComponentObjects(doc, componentId).Select(obj => obj.Id).ToArray()));
        }
        if (components.Count == 0)
            return;

        var targetIds = components
            .SelectMany(item => item.Data.Bindings)
            .Select(binding => binding.TargetObjectId)
            .Where(transformedIds.Contains)
            .Distinct()
            .ToHashSet();
        var targets = new List<MovedTarget>();
        foreach (var targetId in targetIds)
        {
            var target = e.Objects.FirstOrDefault(obj => obj.Id == targetId) ?? doc.Objects.FindId(targetId);
            if (target is null)
                continue;
            var expected = target.Geometry.Duplicate();
            if (!expected.Transform(e.Transform))
            {
                expected.Dispose();
                continue;
            }
            targets.Add(new MovedTarget(
                target.Id,
                target.ObjectType,
                target.Attributes.Name,
                target.Attributes.LayerIndex,
                expected));
        }

        var key = (doc.RuntimeSerialNumber, e.TransformEventId);
        if (PendingTransforms.Remove(key, out var previous))
            previous.Dispose();
        PendingTransforms[key] = new PendingTransform(
            doc,
            e.ObjectsWillBeCopied ? ComponentTransformKind.Copy : ComponentTransformKind.Move,
            e.Transform,
            IsRigidTransform(e.Transform),
            transformedIds,
            components,
            targets);
    }

    private static void AfterTransformObjects(object? sender, RhinoAfterTransformObjectsEventArgs e)
    {
        if (_synchronizingTransform)
            return;
        var senderDocument = sender as RhinoDoc;
        var match = PendingTransforms.FirstOrDefault(pair =>
            pair.Key.Event == e.TransformEventId
            && (senderDocument is null || pair.Key.Document == senderDocument.RuntimeSerialNumber));
        if (match.Value is null || !PendingTransforms.Remove(match.Key, out var pending))
            return;
        CompletePendingTransform(pending);
    }

    private static void EndCommand(object? sender, CommandEventArgs e)
    {
        if (e.Document is not { } doc)
            return;

        var unfinished = PendingTransforms
            .Where(pair => pair.Key.Document == doc.RuntimeSerialNumber)
            .ToArray();
        foreach (var pair in unfinished)
        {
            if (PendingTransforms.Remove(pair.Key, out var pending))
                CompletePendingTransform(pending);
        }

        if (PendingAddedBatches.Remove(doc.RuntimeSerialNumber, out var batch)
            && e.CommandResult == Result.Success)
            CompleteAddedBatch(doc, batch);

        if (e.CommandEnglishName is not ("Undo" or "Redo"))
            return;
        if (ComponentEditorSession.TryGetActiveSet(doc, out var components))
            ComponentEditorSession.ActivateMany(
                doc,
                components,
                false,
                ComponentActivationIntent.SynchronizeOnly);
    }

    private static void CompletePendingTransform(PendingTransform pending)
    {
        try
        {
            _synchronizingTransform = true;
            if (pending.Kind == ComponentTransformKind.Copy)
                NormalizeCopiedComponents(pending);
            else
                foreach (var component in pending.Components)
                    SynchronizeMovedComponent(pending.Document, pending, component);
            if (!pending.IsRigidTransform)
                RhinoApp.WriteLine(
                    "参数化紧固件提示：已同步对象位置，但缩放、剪切及非刚性变换不会改变紧固件规格；后续应用更新将按原规格尺寸重建。旋转与镜像已完整支持。");
            pending.Document.Views.Redraw();
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"参数化紧固件移动同步失败：{ex.Message}");
        }
        finally
        {
            _synchronizingTransform = false;
            pending.Dispose();
        }
    }

    private static void BeginCommand(object? sender, CommandEventArgs e)
    {
        if (e.Document is not { } doc)
            return;
        var kind = e.CommandEnglishName switch
        {
            "Paste" => ComponentTransformKind.Paste,
            "Import" => ComponentTransformKind.Import,
            _ => (ComponentTransformKind?)null
        };
        if (kind is not null)
            PendingAddedBatches[doc.RuntimeSerialNumber] = new PendingAddedBatch(kind.Value);
    }

    private static void CompleteAddedBatch(RhinoDoc doc, PendingAddedBatch batch)
    {
        var added = batch.AddedObjectIds
            .Select(doc.Objects.FindId)
            .Where(obj => obj is not null)
            .Cast<RhinoObject>()
            .ToArray();
        var candidateHosts = added
            .Where(obj => string.IsNullOrWhiteSpace(
                    obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey))
                && obj.Geometry is Brep or Extrusion)
            .Select(obj => obj.Id)
            .ToArray();
        var plans = new List<ComponentClonePlan>();
        foreach (var group in added
                     .Where(obj => Guid.TryParse(
                         obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey), out _))
                     .GroupBy(obj => Guid.Parse(
                         obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey)!)))
        {
            var sourceObject = group.FirstOrDefault(obj =>
                    obj.Attributes.GetUserString(ComponentRepository.RoleKey) == "ControlPoint"
                    && obj.Geometry is Point
                    && ComponentRepository.TryReadControlPoint(obj, out _));
            if (!ComponentRepository.TryReadControlPoint(sourceObject, out var source))
                continue;
            plans.Add(new ComponentClonePlan(
                batch.Kind,
                source,
                Transform.Identity,
                group.Select(obj => obj.Id).ToArray(),
                candidateHosts,
                new Dictionary<Guid, Guid>()));
        }
        if (plans.Count == 0)
            return;
        if (ComponentCloneService.Normalize(doc, plans, out _, out var message))
            RhinoApp.WriteLine(message);
        else
            RhinoApp.WriteLine($"参数化紧固件{(batch.Kind == ComponentTransformKind.Paste ? "粘贴" : "导入")}同步失败：{message}");
    }

    private static void NormalizeCopiedComponents(PendingTransform pending)
    {
        var plans = new List<ComponentClonePlan>();
        foreach (var snapshot in pending.Components)
        {
            var rawIds = pending.AddedPluginObjectIds
                .Select(pending.Document.Objects.FindId)
                .Where(obj => obj is not null
                    && string.Equals(
                        obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey),
                        snapshot.Data.ComponentId.ToString("D"),
                        StringComparison.OrdinalIgnoreCase))
                .Select(obj => obj!.Id)
                .ToArray();
            if (rawIds.Length == 0)
            {
                rawIds = ComponentRepository.FindComponentObjects(
                        pending.Document, snapshot.Data.ComponentId)
                    .Where(obj => !snapshot.ObjectIds.Contains(obj.Id))
                    .Select(obj => obj.Id)
                    .ToArray();
            }
            if (rawIds.Length == 0)
                continue;
            plans.Add(new ComponentClonePlan(
                ComponentTransformKind.Copy,
                snapshot.Data,
                pending.Transform,
                rawIds,
                pending.AddedOrdinaryObjectIds.ToArray(),
                pending.TargetIdMap));
        }
        if (plans.Count == 0)
            return;
        if (ComponentCloneService.Normalize(pending.Document, plans, out _, out var message))
            RhinoApp.WriteLine(message);
        else
            RhinoApp.WriteLine($"参数化紧固件复制同步失败：{message}");
    }

    private static void SynchronizeMovedComponent(
        RhinoDoc doc,
        PendingTransform pending,
        PendingComponent snapshot)
    {
        using var suppression = Suppress(doc, snapshot.Data.ComponentId);
        foreach (var oldId in snapshot.ObjectIds.Where(id => !pending.TransformedObjectIds.Contains(id)))
        {
            if (doc.Objects.FindId(oldId) is not null
                && doc.Objects.Transform(oldId, pending.Transform, true) == Guid.Empty)
                throw new InvalidOperationException($"无法同步移动组件对象：{oldId}");
        }

        foreach (var target in pending.Targets.Where(item => !pending.TargetIdMap.ContainsKey(item.OldObjectId)))
        {
            var match = FindMovedTarget(doc, target, pending.TargetIdMap.Values.ToHashSet());
            if (match is not null)
                pending.TargetIdMap[target.OldObjectId] = match.Id;
        }

        if (!PlacementTransformService.TryTransform(
                snapshot.Data.Placement,
                pending.Transform,
                out var placement,
                out _))
            throw new InvalidOperationException("无法更新组件放置坐标。");

        var objects = ComponentRepository.FindComponentObjects(doc, snapshot.Data.ComponentId).ToList();
        var controlPoint = objects.FirstOrDefault(obj =>
            obj.Attributes.GetUserString(ComponentRepository.RoleKey) == "ControlPoint");
        var proxy = ComponentRepository.FindProxy(doc, snapshot.Data.ComponentId);
        if (controlPoint is null || proxy is null)
            throw new InvalidOperationException("移动后无法重新识别控制点或紧固件本体。");

        var movedBase = snapshot.Data with
        {
            Placement = placement
        };
        var assignedTargets = new HashSet<Guid>();
        var bindings = snapshot.Data.Bindings.Select(binding =>
        {
            var targetId = pending.TargetIdMap.TryGetValue(binding.TargetObjectId, out var movedTargetId)
                ? movedTargetId
                : binding.TargetObjectId;
            var cutter = objects.FirstOrDefault(obj =>
                obj.Attributes.GetUserString(ComponentRepository.RoleKey) == "Cutter"
                && Guid.TryParse(obj.Attributes.GetUserString(ComponentRepository.BindingIdKey), out var bindingId)
                && bindingId == binding.BindingId);
            if (!ComponentHostResolver.IsValidBinding(doc, movedBase, binding, targetId))
            {
                targetId = ComponentHostResolver.FindUnique(
                        doc,
                        movedBase,
                        binding,
                        ComponentHostResolver.AllOrdinaryHostIds(doc),
                        cutter?.Geometry,
                        assignedTargets)?.Id
                    ?? Guid.Empty;
            }
            if (targetId != Guid.Empty)
                assignedTargets.Add(targetId);
            return binding with
            {
                TargetObjectId = targetId,
                CutterObjectId = cutter?.Id ?? Guid.Empty
            };
        }).ToArray();

        var updated = snapshot.Data with
        {
            Placement = placement,
            ProxyObjectId = proxy.Id,
            ControlPointObjectId = controlPoint.Id,
            Bindings = bindings,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var hasPreparedSignature = FastenerGeometryPreparationService.TryPrepare(
            doc,
            updated,
            out var prepared,
            out var preparationMessage);
        if (!hasPreparedSignature && !ComponentHostResolver.NeedsRelink(updated))
            throw new InvalidOperationException($"移动后统一几何预检失败：{preparationMessage}");
        var bindingsById = updated.Bindings.ToDictionary(binding => binding.BindingId);
        foreach (var obj in objects)
        {
            var role = obj.Attributes.GetUserString(ComponentRepository.RoleKey) ?? "Proxy";
            var bindingId = Guid.TryParse(
                obj.Attributes.GetUserString(ComponentRepository.BindingIdKey),
                out var parsedBindingId)
                ? parsedBindingId
                : Guid.Empty;
            var targetId = bindingsById.TryGetValue(bindingId, out var binding)
                ? binding.TargetObjectId
                : Guid.Empty;
            var attributes = obj.Attributes.Duplicate();
            attributes.Name = ComponentRepository.ObjectName(updated, role);
            ComponentRepository.Write(attributes, updated, role, targetId, bindingId);
            if (role != "ControlPoint" && hasPreparedSignature)
            {
                var partIndex = int.TryParse(
                    attributes.GetUserString(ComponentRepository.PartIndexKey),
                    out var parsedPartIndex)
                    ? parsedPartIndex
                    : 0;
                ComponentRepository.WriteDerivedSignature(
                    attributes,
                    prepared!.PreparationSignature,
                    ComponentReliabilitySignatureService.GeometrySignature(obj.Geometry),
                    partIndex);
            }
            if (!doc.Objects.ModifyAttributes(obj, attributes, true))
                throw new InvalidOperationException($"无法更新移动后对象属性：{obj.Id}");
        }

        ComponentPresentationService.RecreateGroup(
            doc,
            updated.ComponentId,
            objects
                .Where(obj => obj.Attributes.GetUserString(ComponentRepository.RoleKey) != "ControlPoint")
                .Select(obj => obj.Id));
        ComponentEditorSession.UpdateCache(doc, updated);
        if (ComponentHostResolver.NeedsRelink(updated))
            RhinoApp.WriteLine(
                $"参数化紧固件 {updated.ComponentId.ToString("N")[..8]} 移动后需要重新绑定宿主；请选择对应宿主并运行“刷新 / 清理”。");
    }

    private static RhinoObject? FindMovedTarget(
        RhinoDoc doc,
        MovedTarget target,
        IReadOnlySet<Guid> excludedObjectIds)
    {
        if (doc.Objects.FindId(target.OldObjectId) is { } retained)
            return retained;
        var candidates = doc.Objects
            .Where(obj => obj.ObjectType == target.ObjectType
                && !excludedObjectIds.Contains(obj.Id)
                && string.IsNullOrWhiteSpace(obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey)))
            .OrderByDescending(obj => obj.Attributes.LayerIndex == target.LayerIndex)
            .ThenByDescending(obj => string.Equals(obj.Attributes.Name, target.Name, StringComparison.Ordinal))
            .ToArray();
        return candidates.FirstOrDefault(candidate =>
            IsTargetMatch(candidate, target, doc.ModelAbsoluteTolerance));
    }

    private static bool IsTargetMatch(RhinoObject candidate, MovedTarget target, double tolerance)
    {
        if (candidate.ObjectType != target.ObjectType
            || !string.IsNullOrWhiteSpace(candidate.Attributes.GetUserString(ComponentRepository.ComponentIdKey)))
            return false;
        if (GeometryBase.GeometryEquals(candidate.Geometry, target.ExpectedGeometry))
            return true;
        var actual = candidate.Geometry.GetBoundingBox(true);
        var expected = target.ExpectedGeometry.GetBoundingBox(true);
        var boxTolerance = Math.Max(tolerance * 10, 1e-6);
        return actual.Min.DistanceTo(expected.Min) <= boxTolerance
            && actual.Max.DistanceTo(expected.Max) <= boxTolerance;
    }

    private static bool IsRigidTransform(Transform transform)
    {
        return PlacementTransformService.TryTransform(
            PlacementFrame.WorldXY,
            transform,
            out _,
            out var isRigid) && isRigid;
    }

    private static bool IsPendingTransform(RhinoDoc doc, Guid componentId) =>
        _synchronizingTransform
        || PendingTransforms.Values.Any(item =>
            item.DocumentSerial == doc.RuntimeSerialNumber
            && item.Components.Any(component => component.Data.ComponentId == componentId));

    private static bool IsSuppressed(RhinoDoc doc, Guid componentId) =>
        Suppressed.ContainsKey((doc.RuntimeSerialNumber, componentId));

    private sealed record PendingComponent(FastenerComponentData Data, IReadOnlyList<Guid> ObjectIds);

    private sealed record MovedTarget(
        Guid OldObjectId,
        ObjectType ObjectType,
        string? Name,
        int LayerIndex,
        GeometryBase ExpectedGeometry) : IDisposable
    {
        public void Dispose() => ExpectedGeometry.Dispose();
    }

    private sealed class PendingTransform(
        RhinoDoc document,
        ComponentTransformKind kind,
        Transform transform,
        bool isRigidTransform,
        HashSet<Guid> transformedObjectIds,
        IReadOnlyList<PendingComponent> components,
        IReadOnlyList<MovedTarget> targets) : IDisposable
    {
        public RhinoDoc Document { get; } = document;
        public uint DocumentSerial => Document.RuntimeSerialNumber;
        public ComponentTransformKind Kind { get; } = kind;
        public Transform Transform { get; } = transform;
        public bool IsRigidTransform { get; } = isRigidTransform;
        public HashSet<Guid> TransformedObjectIds { get; } = transformedObjectIds;
        public IReadOnlyList<PendingComponent> Components { get; } = components;
        public IReadOnlyList<MovedTarget> Targets { get; } = targets;
        public Dictionary<Guid, Guid> TargetIdMap { get; } = [];
        public HashSet<Guid> AddedPluginObjectIds { get; } = [];
        public HashSet<Guid> AddedOrdinaryObjectIds { get; } = [];

        public void Dispose()
        {
            foreach (var target in Targets)
                target.Dispose();
        }
    }

    private sealed class PendingAddedBatch(ComponentTransformKind kind)
    {
        public ComponentTransformKind Kind { get; } = kind;
        public HashSet<Guid> AddedObjectIds { get; } = [];
    }

    private sealed class SuppressionScope((uint Document, Guid Component) key) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (!Suppressed.TryGetValue(key, out var count) || count <= 1)
                Suppressed.Remove(key);
            else
                Suppressed[key] = count - 1;
        }
    }
}
