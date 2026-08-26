using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Geometry;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Services;

public static class FastenerComponentService
{
    public static bool CreateOrReplace(
        RhinoDoc doc,
        FastenerComponentData draft,
        out FastenerComponentData saved,
        out string message)
    {
        var success = CreateOrReplaceMany(doc, [draft], out var savedComponents, out message);
        saved = success ? savedComponents[0] : draft;
        return success;
    }

    public static bool CreateOrReplaceMany(
        RhinoDoc doc,
        IReadOnlyList<FastenerComponentData> drafts,
        out IReadOnlyList<FastenerComponentData> savedComponents,
        out string message)
    {
        savedComponents = [];
        message = string.Empty;
        if (drafts.Count == 0)
        {
            message = "没有需要更新的参数化紧固件。";
            return false;
        }
        if (drafts.Select(item => item.ComponentId).Distinct().Count() != drafts.Count)
        {
            message = "批量更新中包含重复的组件 ID。";
            return false;
        }

        var prepared = new List<PreparedFastenerGeometry>();
        var preparationErrors = new List<string>();
        var before = new List<FastenerComponentSnapshot>();
        foreach (var draft in drafts)
        {
            if (ComponentRepository.FindControlPoints(doc, draft.ComponentId).Any())
            {
                if (!FastenerComponentSnapshotService.TryCapture(
                        doc,
                        draft.ComponentId,
                        out var snapshot,
                        out var snapshotIssue))
                {
                    preparationErrors.Add(snapshotIssue?.ToString()
                        ?? $"{draft.Size} · {draft.ComponentId.ToString("N")[..8]}：无法读取控制点快照。");
                    continue;
                }
                before.Add(snapshot!);
            }
            if (FastenerGeometryPreparationService.TryPrepare(doc, draft, out var component, out var error))
                prepared.Add(component!);
            else
                preparationErrors.Add($"{draft.Size} · {draft.ComponentId.ToString("N")[..8]}：{error}");
        }
        if (preparationErrors.Count > 0)
        {
            message = "批量预检失败，未修改任何组件：" + System.Environment.NewLine
                + string.Join(System.Environment.NewLine, preparationErrors);
            return false;
        }

        var operationKind = before.Count == 0
            ? ComponentOperationKind.Create
            : ComponentOperationKind.Update;
        var operationPlan = new ComponentOperationPlan(operationKind, before, prepared, []);
        if (!operationPlan.IsValid)
        {
            message = "组件操作计划未通过完整预检，未写入任何对象。";
            return false;
        }

        return CommitPreparedMany(
            doc,
            operationPlan,
            drafts.Count,
            out savedComponents,
            out message);
    }

    internal static bool CreateOrReplacePrepared(
        RhinoDoc doc,
        PreparedFastenerGeometry prepared,
        long expectedDocumentRevision,
        out FastenerComponentData saved,
        out string message)
    {
        saved = prepared.Draft;
        message = string.Empty;
        if (FastenerDocumentIndexService.CurrentRevision(doc) != expectedDocumentRevision)
        {
            message = "精确预检后文档已发生变化，请再次确认放置位置。";
            return false;
        }
        if (prepared.Draft.Bindings.Any(binding =>
                binding.TargetObjectId == Guid.Empty
                || doc.Objects.FindId(binding.TargetObjectId) is null))
        {
            message = "精确预检使用的宿主已经失效，请重新确认放置位置。";
            return false;
        }
        var spec = FastenerSpecResolver.Resolve(prepared.Draft, RhinoMMPlugIn.Catalog);
        var currentSignature = ComponentReliabilitySignatureService.ParameterSignature(
            doc,
            prepared.Draft,
            spec);
        if (!string.Equals(currentSignature, prepared.ParameterSignature, StringComparison.Ordinal))
        {
            message = "参数或宿主签名已变化，请重新确认放置位置。";
            return false;
        }

        var plan = new ComponentOperationPlan(
            ComponentOperationKind.Create,
            [],
            [prepared],
            []);
        var success = CommitPreparedMany(doc, plan, 1, out var savedItems, out message);
        if (success)
            saved = savedItems[0];
        return success;
    }

    private static bool CommitPreparedMany(
        RhinoDoc doc,
        ComponentOperationPlan operationPlan,
        int requestedCount,
        out IReadOnlyList<FastenerComponentData> savedComponents,
        out string message)
    {
        savedComponents = [];
        message = string.Empty;
        var prepared = operationPlan.Prepared;

        var undo = doc.BeginUndoRecord(
            requestedCount == 1 ? "参数化紧固件：更新组件" : $"参数化紧固件：批量更新 {requestedCount} 个组件");
        var ownsUndoRecord = undo != 0;
        var journal = new ComponentMutationJournal(doc, prepared.Select(item => item.Draft.ComponentId));
        try
        {
            var saved = new List<FastenerComponentData>();
            foreach (var component in operationPlan.Prepared)
                saved.Add(CommitPrepared(doc, component, journal));
            ComponentPresentationService.CleanupUnusedLegacyMaterials(doc);
            savedComponents = saved;
            doc.Views.Redraw();

            var warningText = prepared
                .SelectMany(item => item.Warnings)
                .Distinct()
                .ToArray();
            message = requestedCount == 1
                ? $"已生成 {saved[0].Size}，绑定 {saved[0].Bindings.Select(binding => binding.TargetObjectId).Distinct().Count()} 个被切割体。"
                : $"已批量更新 {saved.Count} 个参数化紧固件。";
            if (warningText.Length > 0)
                message += $" {string.Join(" ", warningText)}";
            journal.Complete();
            return true;
        }
        catch (Exception ex)
        {
            if (ownsUndoRecord && undo != 0)
            {
                doc.EndUndoRecord(undo);
                undo = 0;
                doc.Undo();
            }
            else
            {
                journal.Rollback();
            }
            message = ownsUndoRecord
                ? $"更新失败，已回滚全部组件：{ex.Message}"
                : $"更新失败，已通过内部事务恢复原组件：{ex.Message}";
            return false;
        }
        finally
        {
            if (ownsUndoRecord && undo != 0)
                doc.EndUndoRecord(undo);
        }
    }

    private static FastenerComponentData CommitPrepared(
        RhinoDoc doc,
        PreparedFastenerGeometry prepared,
        ComponentMutationJournal journal)
    {
        var draft = prepared.Draft;
        using var suppression = ComponentLifecycleService.Suppress(doc, draft.ComponentId);
        var existingObjects = ComponentRepository.FindComponentObjects(doc, draft.ComponentId).ToList();
        var existingControl = existingObjects.SingleOrDefault(obj =>
            obj.Attributes.GetUserString(ComponentRepository.RoleKey) == "ControlPoint"
            && obj.Geometry is Point);

        var proxyIds = new List<Guid>();
        for (var proxyIndex = 0; proxyIndex < prepared.Proxies.Count; proxyIndex++)
        {
            var proxy = prepared.Proxies[proxyIndex];
            var attributes = ComponentRepository.CreateAttributes(draft, "Proxy");
            attributes.SetUserString(
                ComponentRepository.ProxyPartKey,
                proxyIndex == 0 && FastenerKindTraits.HasShaftProxy(draft.Kind) ? "Shaft" : "Head");
            ComponentRepository.WriteDerivedSignature(
                attributes,
                prepared.PreparationSignature,
                ComponentReliabilitySignatureService.GeometrySignature(proxy),
                proxyIndex);
            ComponentPresentationService.ConfigureAttributes(doc, attributes, draft, false, true);
            var id = doc.Objects.AddBrep(proxy, attributes);
            if (id == Guid.Empty)
                throw new InvalidOperationException("无法写入螺丝代理体。");
            journal.TrackCreated(id);
            proxyIds.Add(id);
        }

        var bindings = new List<HoleTargetBinding>();
        var geometryObjectIds = new List<Guid>(proxyIds);
        foreach (var item in prepared.Cutters)
        {
            var cutterIds = new List<Guid>();
            for (var shaftIndex = 0; shaftIndex < item.Shafts.Count; shaftIndex++)
            {
                var shaft = item.Shafts[shaftIndex];
                var cutterAttributes = ComponentRepository.CreateAttributes(
                    draft, "Cutter", item.Binding.TargetObjectId, item.Binding.BindingId);
                ComponentRepository.WriteDerivedSignature(
                    cutterAttributes,
                    prepared.PreparationSignature,
                    ComponentReliabilitySignatureService.GeometrySignature(shaft),
                    shaftIndex);
                ComponentPresentationService.ConfigureAttributes(
                    doc, cutterAttributes, draft, true, item.Binding.IsPreviewVisible);
                var cutterId = doc.Objects.AddBrep(shaft, cutterAttributes);
                if (cutterId == Guid.Empty)
                    throw new InvalidOperationException("无法写入孔切割体。");
                journal.TrackCreated(cutterId);
                cutterIds.Add(cutterId);
                geometryObjectIds.Add(cutterId);
            }
            for (var headIndex = 0; headIndex < item.Heads.Count; headIndex++)
            {
                var head = item.Heads[headIndex];
                var headAttributes = ComponentRepository.CreateAttributes(
                    draft, "HeadCutter", item.Binding.TargetObjectId, item.Binding.BindingId);
                ComponentRepository.WriteDerivedSignature(
                    headAttributes,
                    prepared.PreparationSignature,
                    ComponentReliabilitySignatureService.GeometrySignature(head),
                    headIndex);
                ComponentPresentationService.ConfigureAttributes(
                    doc, headAttributes, draft, true, item.Binding.IsPreviewVisible);
                var headId = doc.Objects.AddBrep(head, headAttributes);
                if (headId == Guid.Empty)
                    throw new InvalidOperationException("无法写入头部切割体。");
                journal.TrackCreated(headId);
                geometryObjectIds.Add(headId);
            }
            bindings.Add(item.Binding with
            {
                CutterObjectId = cutterIds.FirstOrDefault()
            });
        }

        ComponentPresentationService.RemoveGroup(doc, draft.ComponentId);
        foreach (var old in existingObjects.Where(obj => obj.Id != existingControl?.Id))
        {
            if (!journal.Delete(old))
                throw new InvalidOperationException($"无法替换旧的组件派生对象 {old.Id}。");
        }

        var controlPointId = existingControl?.Id ?? Guid.Empty;
        if (controlPointId == Guid.Empty)
        {
            var controlAttributes = ComponentRepository.CreateAttributes(draft, "ControlPoint");
            ComponentPresentationService.ConfigureControlPointAttributes(doc, controlAttributes);
            controlPointId = doc.Objects.AddPoint(
                FastenerGeometryFactory.ToPlane(draft.Placement).Origin,
                controlAttributes);
            if (controlPointId == Guid.Empty)
                throw new InvalidOperationException("无法写入螺丝控制点。");
            journal.TrackCreated(controlPointId);
        }

        var saved = draft with
        {
            ProxyObjectId = proxyIds[0],
            ControlPointObjectId = controlPointId,
            Bindings = bindings,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        foreach (var obj in ComponentRepository.FindComponentObjects(doc, saved.ComponentId).ToList())
        {
            var attributes = obj.Attributes.Duplicate();
            ComponentRepository.Write(
                attributes,
                saved,
                obj.Attributes.GetUserString(ComponentRepository.RoleKey) ?? "Proxy",
                Guid.TryParse(obj.Attributes.GetUserString(ComponentRepository.TargetIdKey), out var targetId)
                    ? targetId
                    : Guid.Empty,
                Guid.TryParse(obj.Attributes.GetUserString(ComponentRepository.BindingIdKey), out var bindingId)
                    ? bindingId
                    : Guid.Empty);
            if (!journal.ModifyAttributes(obj, attributes, true))
                throw new InvalidOperationException($"无法保存组件对象属性 {obj.Id}。");
        }
        ComponentPresentationService.RecreateGroup(doc, saved.ComponentId, geometryObjectIds);
        return saved;
    }

}
