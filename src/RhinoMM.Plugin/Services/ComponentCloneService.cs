using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Geometry;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Services;

internal static class ComponentCloneService
{
    public static bool Normalize(
        RhinoDoc doc,
        IReadOnlyList<ComponentClonePlan> plans,
        out ComponentCloneResult result,
        out string message)
    {
        result = new ComponentCloneResult([], 0, 0, []);
        if (plans.Count == 0)
        {
            message = "没有发现需要规范化的参数化紧固件副本。";
            return true;
        }

        var prepared = new List<PreparedClone>();
        foreach (var plan in plans)
        {
            if (!TryPrepare(doc, plan, out var clone, out var error))
            {
                message = error;
                return false;
            }
            prepared.Add(clone!);
        }

        var undo = doc.BeginUndoRecord($"参数化紧固件：规范化 {plans.Count} 个复制组件");
        var ownsUndo = undo != 0;
        var createdComponentIds = prepared.Select(item => item.Draft.ComponentId).ToArray();
        try
        {
            foreach (var source in prepared
                         .Select(item => item.Plan.Source)
                         .GroupBy(component => component.ComponentId)
                         .Select(group => group.First()))
                ComponentPresentationService.PromoteLegacyMaterialsForCopy(doc, source);

            var resolved = prepared.Where(item => !item.Unresolved).ToArray();
            var savedById = new Dictionary<Guid, FastenerComponentData>();
            if (resolved.Length > 0)
            {
                if (!FastenerComponentService.CreateOrReplaceMany(
                        doc,
                        resolved.Select(item => item.Draft).ToArray(),
                        out var saved,
                        out var createMessage))
                    throw new InvalidOperationException(createMessage);
                foreach (var component in saved)
                    savedById[component.ComponentId] = component;
            }

            foreach (var item in prepared.Where(item => item.Unresolved))
            {
                var saved = CreateUnresolved(doc, item);
                savedById[saved.ComponentId] = saved;
            }

            // Keep the raw Rhino copies until every new component has been created.
            // This makes manual rollback possible even inside Rhino's command undo record.
            foreach (var item in prepared)
                DeleteRawCopies(doc, item.Plan);
            ComponentPresentationService.CleanupUnusedLegacyMaterials(doc);

            foreach (var item in prepared)
            {
                var saved = savedById[item.Draft.ComponentId];
                RestoreExternalGroups(doc, saved.ComponentId, item.ExternalGroupIndices);
                ComponentEditorSession.UpdateCache(doc, saved);
            }

            var components = prepared.Select(item => savedById[item.Draft.ComponentId]).ToArray();
            var unresolved = components.Where(ComponentHostResolver.NeedsRelink).ToArray();
            result = new ComponentCloneResult(
                components,
                prepared.Sum(item => item.RelinkedBindings),
                unresolved.Length,
                unresolved.Select(item => item.ComponentId).ToArray());
            doc.Objects.UnselectAll(false);
            foreach (var component in components)
            {
                var point = ComponentRepository.FindControlPoint(doc, component.ComponentId);
                if (point is not null)
                    doc.Objects.Select(point.Id, false);
            }
            ComponentEditorSession.ActivateMany(
                doc,
                components,
                false,
                ComponentActivationIntent.SynchronizeOnly);
            doc.Views.Redraw();
            message = $"已创建 {components.Length} 个独立参数化副本，重新绑定 {result.RelinkedBindings} 个宿主。";
            if (unresolved.Length > 0)
                message += $" 其中 {unresolved.Length} 个副本待重新绑定；请选择对应宿主后运行“刷新 / 清理”。";
            return true;
        }
        catch (Exception ex)
        {
            foreach (var componentId in createdComponentIds)
                RemoveCreatedComponent(doc, componentId);
            if (ownsUndo && undo != 0)
            {
                doc.EndUndoRecord(undo);
                undo = 0;
                doc.Undo();
            }
            message = $"复制组件规范化失败，未修改原组件：{ex.Message}";
            return false;
        }
        finally
        {
            if (ownsUndo && undo != 0)
                doc.EndUndoRecord(undo);
        }
    }

    private static bool TryPrepare(
        RhinoDoc doc,
        ComponentClonePlan plan,
        out PreparedClone? result,
        out string message)
    {
        result = null;
        message = string.Empty;
        try
        {
            var rawObjects = plan.RawObjectIds
                .Select(doc.Objects.FindId)
                .Where(obj => obj is not null)
                .Cast<RhinoObject>()
                .ToArray();
            if (!PlacementTransformService.TryTransform(
                    plan.Source.Placement,
                    plan.Transform,
                    out var placement,
                    out _))
                throw new InvalidOperationException("无法计算复制组件的放置坐标。");
            var rawPoint = rawObjects.FirstOrDefault(obj =>
                obj.Attributes.GetUserString(ComponentRepository.RoleKey) == "ControlPoint"
                && obj.Geometry is Point);
            if (rawPoint?.Geometry is Point point)
                placement = placement with
                {
                    OriginX = point.Location.X,
                    OriginY = point.Location.Y,
                    OriginZ = point.Location.Z
                };

            var componentId = Guid.NewGuid();
            var display = GlobalDisplaySettingsService.Current;
            var bindingIdMap = plan.Source.Bindings.ToDictionary(
                binding => binding.BindingId,
                _ => Guid.NewGuid());
            var initial = plan.Source with
            {
                ComponentId = componentId,
                Placement = placement,
                FastenerOpacityPercent = display.FastenerOpacityPercent,
                CutterOpacityPercent = display.CutterOpacityPercent,
                ProxyObjectId = Guid.Empty,
                ControlPointObjectId = Guid.Empty,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            var usedTargets = new HashSet<Guid>();
            var resolvedTargetsBySource = new Dictionary<Guid, Guid>();
            var relinked = 0;
            var unresolved = false;
            var bindings = new List<HoleTargetBinding>();
            foreach (var sourceBinding in plan.Source.Bindings)
            {
                var rawCutter = rawObjects.FirstOrDefault(obj =>
                    obj.Attributes.GetUserString(ComponentRepository.RoleKey) == "Cutter"
                    && Guid.TryParse(
                        obj.Attributes.GetUserString(ComponentRepository.BindingIdKey),
                        out var rawBindingId)
                    && rawBindingId == sourceBinding.BindingId);
                var targetId = resolvedTargetsBySource.TryGetValue(
                        sourceBinding.TargetObjectId,
                        out var sharedTarget)
                    ? sharedTarget
                    : ResolveTarget(
                        doc,
                        plan,
                        initial,
                        sourceBinding,
                        rawCutter?.Geometry,
                        usedTargets);
                if (targetId == Guid.Empty)
                    unresolved = true;
                else
                {
                    resolvedTargetsBySource[sourceBinding.TargetObjectId] = targetId;
                    usedTargets.Add(targetId);
                    if (targetId != sourceBinding.TargetObjectId)
                        relinked++;
                }
                bindings.Add(sourceBinding with
                {
                    BindingId = bindingIdMap[sourceBinding.BindingId],
                    TargetObjectId = targetId,
                    CutterObjectId = Guid.Empty
                });
            }

            if (unresolved)
                bindings = bindings.Select(binding => binding with
                {
                    TargetObjectId = Guid.Empty,
                    CutterObjectId = Guid.Empty
                }).ToList();
            var draft = initial with { Bindings = bindings };
            result = new PreparedClone(
                plan,
                draft,
                unresolved,
                relinked,
                CaptureExternalGroups(doc, rawObjects));
            return true;
        }
        catch (Exception ex)
        {
            message = $"无法准备复制组件 {plan.Source.ComponentId.ToString("N")[..8]}：{ex.Message}";
            return false;
        }
    }

    private static Guid ResolveTarget(
        RhinoDoc doc,
        ComponentClonePlan plan,
        FastenerComponentData component,
        HoleTargetBinding binding,
        GeometryBase? cutterGeometry,
        IReadOnlySet<Guid> usedTargets)
    {
        if (plan.DirectTargetMap.TryGetValue(binding.TargetObjectId, out var direct)
            && ComponentHostResolver.IsValidBinding(doc, component, binding, direct))
            return direct;

        var preferred = ComponentHostResolver.FindUnique(
            doc,
            component,
            binding,
            plan.CandidateHostIds,
            cutterGeometry,
            usedTargets);
        if (preferred is not null)
            return preferred.Id;

        var fallback = ComponentHostResolver.FindUnique(
            doc,
            component,
            binding,
            ComponentHostResolver.AllOrdinaryHostIds(doc),
            cutterGeometry,
            usedTargets);
        return fallback?.Id ?? Guid.Empty;
    }

    private static FastenerComponentData CreateUnresolved(RhinoDoc doc, PreparedClone item)
    {
        var draft = item.Draft;
        var spec = RhinoMMPlugIn.Catalog.Get(draft.Size);
        var ids = new List<Guid>();
        var proxies = FastenerGeometryFactory.CreateProxy(draft, spec);
        for (var index = 0; index < proxies.Count; index++)
        {
            var attributes = ComponentRepository.CreateAttributes(draft, "Proxy");
            attributes.SetUserString(
                ComponentRepository.ProxyPartKey,
                index == 0 && FastenerKindTraits.HasShaftProxy(draft.Kind) ? "Shaft" : "Head");
            ComponentPresentationService.ConfigureAttributes(doc, attributes, draft, false, true);
            var id = doc.Objects.AddBrep(proxies[index], attributes);
            if (id == Guid.Empty)
                throw new InvalidOperationException("无法写入待重绑组件代理体。");
            ids.Add(id);
        }
        var controlAttributes = ComponentRepository.CreateAttributes(draft, "ControlPoint");
        ComponentPresentationService.ConfigureControlPointAttributes(doc, controlAttributes);
        var controlId = doc.Objects.AddPoint(
            FastenerGeometryFactory.ToPlane(draft.Placement).Origin,
            controlAttributes);
        if (controlId == Guid.Empty)
            throw new InvalidOperationException("无法写入待重绑组件控制点。");

        var saved = draft with
        {
            ProxyObjectId = ids[0],
            ControlPointObjectId = controlId,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        foreach (var obj in ids.Select(doc.Objects.FindId).Append(doc.Objects.FindId(controlId)))
        {
            if (obj is null)
                continue;
            var role = obj.Attributes.GetUserString(ComponentRepository.RoleKey) ?? "Proxy";
            var attributes = obj.Attributes.Duplicate();
            ComponentRepository.Write(attributes, saved, role);
            if (!doc.Objects.ModifyAttributes(obj, attributes, true))
                throw new InvalidOperationException("无法保存待重绑组件参数。");
        }
        ComponentPresentationService.RecreateGroup(doc, saved.ComponentId, ids);
        return saved;
    }

    private static IReadOnlyList<ExternalGroupReference> CaptureExternalGroups(
        RhinoDoc doc,
        IEnumerable<RhinoObject> objects) => objects
        .SelectMany(obj => obj.GetGroupList() ?? [])
        .Distinct()
        .Select(index => new ExternalGroupReference(index, doc.Groups.GroupName(index)))
        .Where(group =>
        {
            var name = group.Name;
            return !string.IsNullOrWhiteSpace(name)
                && !name.StartsWith("参数化紧固件::", StringComparison.Ordinal);
        })
        .ToArray();

    private static void RestoreExternalGroups(
        RhinoDoc doc,
        Guid componentId,
        IReadOnlyList<ExternalGroupReference> groups)
    {
        if (groups.Count == 0)
            return;
        var objectIds = ComponentRepository.FindComponentObjects(doc, componentId)
            .Select(obj => obj.Id)
            .ToArray();
        for (var groupNumber = 0; groupNumber < groups.Count; groupNumber++)
        {
            var group = groups[groupNumber];
            if (!doc.Groups.IsDeleted(group.Index))
            {
                doc.Groups.AddToGroup(group.Index, objectIds);
                continue;
            }
            var replacementName = $"参数化紧固件复制组::{componentId:D}::{groupNumber + 1}";
            if (doc.Groups.Add(replacementName, objectIds) < 0)
                throw new InvalidOperationException($"无法恢复复制组：{group.Name}");
        }
    }

    private static void DeleteRawCopies(RhinoDoc doc, ComponentClonePlan plan)
    {
        using var suppression = ComponentLifecycleService.Suppress(doc, plan.Source.ComponentId);
        foreach (var id in plan.RawObjectIds.Distinct())
        {
            if (doc.Objects.FindId(id) is not null && !doc.Objects.Delete(id, true))
                throw new InvalidOperationException($"无法移除 Rhino 临时复制对象：{id}");
        }
    }

    private static void RemoveCreatedComponent(RhinoDoc doc, Guid componentId)
    {
        using var suppression = ComponentLifecycleService.Suppress(doc, componentId);
        ComponentPresentationService.RemoveGroup(doc, componentId);
        foreach (var obj in ComponentRepository.FindComponentObjects(doc, componentId).ToList())
            doc.Objects.Delete(obj, true);
        ComponentEditorSession.ForgetComponent(doc, componentId);
    }

    private sealed record PreparedClone(
        ComponentClonePlan Plan,
        FastenerComponentData Draft,
        bool Unresolved,
        int RelinkedBindings,
        IReadOnlyList<ExternalGroupReference> ExternalGroupIndices);

    private sealed record ExternalGroupReference(int Index, string? Name);
}
