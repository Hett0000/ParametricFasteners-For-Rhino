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

        var prepared = new List<PreparedComponent>();
        var preparationErrors = new List<string>();
        foreach (var draft in drafts)
        {
            if (TryPrepare(doc, draft, out var component, out var error))
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

        var undo = doc.BeginUndoRecord(
            drafts.Count == 1 ? "参数化紧固件：更新组件" : $"参数化紧固件：批量更新 {drafts.Count} 个组件");
        var ownsUndoRecord = undo != 0;
        try
        {
            var saved = new List<FastenerComponentData>();
            foreach (var component in prepared)
                saved.Add(CommitPrepared(doc, component));
            ComponentPresentationService.CleanupUnusedLegacyMaterials(doc);
            savedComponents = saved;
            doc.Views.Redraw();

            var warningText = prepared
                .SelectMany(item => item.Warnings)
                .Distinct()
                .ToArray();
            message = drafts.Count == 1
                ? $"已生成 {saved[0].Size}，绑定 {saved[0].Bindings.Count} 个被切割体。"
                : $"已批量更新 {saved.Count} 个参数化紧固件。";
            if (warningText.Length > 0)
                message += $" {string.Join(" ", warningText)}";
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
            message = ownsUndoRecord
                ? $"更新失败，已回滚全部组件：{ex.Message}"
                : $"更新失败：{ex.Message}。当前操作由 Rhino 命令撤销记录管理。";
            return false;
        }
        finally
        {
            if (ownsUndoRecord && undo != 0)
                doc.EndUndoRecord(undo);
        }
    }

    private static bool TryPrepare(
        RhinoDoc doc,
        FastenerComponentData draft,
        out PreparedComponent? prepared,
        out string message)
    {
        prepared = null;
        message = string.Empty;
        try
        {
            if (!SmartHostBindingService.TryReconcile(
                    doc,
                    draft,
                    out var effectiveDraft,
                    out var bindingChanges,
                    out var bindingError))
            {
                message = $"宿主重识别失败：{bindingError}";
                return false;
            }

            var spec = RhinoMMPlugIn.Catalog.Get(effectiveDraft.Size);
            var validation = FastenerComponentValidator.Validate(effectiveDraft, spec);
            if (!validation.IsValid)
            {
                message = string.Join(
                    System.Environment.NewLine,
                    validation.Issues.Where(item => item.IsError).Select(item => item.Message));
                return false;
            }

            var proxies = FastenerGeometryFactory.CreateProxy(effectiveDraft, spec);
            var cutters = new List<CutterGeometryBuild>();
            var warnings = new List<string>();
            if (bindingChanges.HasChanges)
                warnings.Add(bindingChanges.ToString());
            foreach (var binding in effectiveDraft.Bindings)
            {
                if (!CutterGeometryService.TryBuild(
                        doc,
                        effectiveDraft,
                        spec,
                        binding,
                        out var cutter,
                        out var cutterError))
                    throw new InvalidOperationException(cutterError);
                cutters.Add(cutter!);
                warnings.AddRange(cutter!.Warnings);
            }
            prepared = new PreparedComponent(effectiveDraft, proxies, cutters, warnings);
            return true;
        }
        catch (Exception ex)
        {
            message = $"几何预检失败：{ex.Message}";
            return false;
        }
    }

    private static FastenerComponentData CommitPrepared(RhinoDoc doc, PreparedComponent prepared)
    {
        var draft = prepared.Draft;
        using var suppression = ComponentLifecycleService.Suppress(doc, draft.ComponentId);
        ComponentPresentationService.RemoveGroup(doc, draft.ComponentId);
        foreach (var old in ComponentRepository.FindComponentObjects(doc, draft.ComponentId).ToList())
            doc.Objects.Delete(old, true);

        var proxyIds = new List<Guid>();
        for (var proxyIndex = 0; proxyIndex < prepared.Proxies.Count; proxyIndex++)
        {
            var proxy = prepared.Proxies[proxyIndex];
            var attributes = ComponentRepository.CreateAttributes(draft, "Proxy");
            attributes.SetUserString(
                ComponentRepository.ProxyPartKey,
                proxyIndex == 0 && FastenerKindTraits.HasShaftProxy(draft.Kind) ? "Shaft" : "Head");
            ComponentPresentationService.ConfigureAttributes(doc, attributes, draft, false, true);
            var id = doc.Objects.AddBrep(proxy, attributes);
            if (id == Guid.Empty)
                throw new InvalidOperationException("无法写入螺丝代理体。");
            proxyIds.Add(id);
        }

        var bindings = new List<HoleTargetBinding>();
        var geometryObjectIds = new List<Guid>(proxyIds);
        foreach (var item in prepared.Cutters)
        {
            var cutterAttributes = ComponentRepository.CreateAttributes(
                draft, "Cutter", item.Binding.TargetObjectId, item.Binding.BindingId);
            ComponentPresentationService.ConfigureAttributes(
                doc, cutterAttributes, draft, true, item.Binding.IsPreviewVisible);
            var cutterId = doc.Objects.AddBrep(item.Shaft, cutterAttributes);
            if (cutterId == Guid.Empty)
                throw new InvalidOperationException("无法写入孔切割体。");
            geometryObjectIds.Add(cutterId);
            if (item.Head is not null)
            {
                var headAttributes = ComponentRepository.CreateAttributes(
                    draft, "HeadCutter", item.Binding.TargetObjectId, item.Binding.BindingId);
                ComponentPresentationService.ConfigureAttributes(
                    doc, headAttributes, draft, true, item.Binding.IsPreviewVisible);
                var headId = doc.Objects.AddBrep(item.Head, headAttributes);
                if (headId == Guid.Empty)
                    throw new InvalidOperationException("无法写入头部切割体。");
                geometryObjectIds.Add(headId);
            }
            bindings.Add(item.Binding with { CutterObjectId = cutterId });
        }

        var controlAttributes = ComponentRepository.CreateAttributes(draft, "ControlPoint");
        ComponentPresentationService.ConfigureControlPointAttributes(doc, controlAttributes);
        var controlPointId = doc.Objects.AddPoint(
            FastenerGeometryFactory.ToPlane(draft.Placement).Origin,
            controlAttributes);
        if (controlPointId == Guid.Empty)
            throw new InvalidOperationException("无法写入螺丝控制点。");

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
            doc.Objects.ModifyAttributes(obj, attributes, true);
        }
        ComponentPresentationService.RecreateGroup(doc, saved.ComponentId, geometryObjectIds);
        return saved;
    }

    private sealed record PreparedComponent(
        FastenerComponentData Draft,
        IReadOnlyList<Brep> Proxies,
        IReadOnlyList<CutterGeometryBuild> Cutters,
        IReadOnlyList<string> Warnings);
}
