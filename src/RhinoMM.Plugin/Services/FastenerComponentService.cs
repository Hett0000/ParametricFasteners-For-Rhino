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
    public static bool CreateOrReplace(RhinoDoc doc, FastenerComponentData draft, out FastenerComponentData saved, out string message)
    {
        saved = draft;
        message = string.Empty;
        FastenerSizeSpec spec;
        try
        {
            spec = RhinoMMPlugIn.Catalog.Get(draft.Size);
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }

        var validation = FastenerComponentValidator.Validate(draft, spec);
        if (!validation.IsValid)
        {
            message = string.Join(System.Environment.NewLine, validation.Issues.Where(x => x.IsError).Select(x => x.Message));
            return false;
        }

        IReadOnlyList<Brep> proxies;
        var cutters = new List<(HoleTargetBinding Binding, Brep Shaft, Brep? Head)>();
        var warnings = new List<string>();
        try
        {
            proxies = FastenerGeometryFactory.CreateProxy(draft, spec);
            foreach (var binding in draft.Bindings)
            {
                var target = doc.Objects.FindId(binding.TargetObjectId);
                if (target is null)
                    throw new InvalidOperationException($"被切割体已丢失：{binding.TargetObjectId}");
                if (!FastenerGeometryFactory.TryGetTargetInterval(
                        target.Geometry, draft.Placement, doc.ModelAbsoluteTolerance, out var targetInterval, out var usedFallback))
                    throw new InvalidOperationException($"无法计算被切割体的轴向范围：{target.Id}");
                if (usedFallback)
                    warnings.Add($"{TargetName(target)} 的切割范围使用了包围盒估算。");

                var padding = Math.Max(0.2, doc.ModelAbsoluteTolerance * 10);
                var start = targetInterval.Min - padding;
                var end = targetInterval.Max + padding;
                var limit = FastenerGeometryFactory.DepthLimit(draft, spec, binding);
                if (!double.IsPositiveInfinity(limit))
                {
                    if (limit < targetInterval.Min - doc.ModelAbsoluteTolerance)
                        throw new InvalidOperationException($"螺杆深度 {limit:0.###} mm 无法到达咬合体“{TargetName(target)}”。");
                    start = Math.Max(start, -padding);
                    end = Math.Min(end, limit + padding);
                    if (limit >= targetInterval.Max - doc.ModelAbsoluteTolerance)
                        warnings.Add($"{TargetName(target)} 的咬合孔将自然贯穿。");
                }
                var shaft = FastenerGeometryFactory.CreateShaftCutter(draft, spec, binding, start, end);
                var head = binding.IncludeHeadSeat ? FastenerGeometryFactory.CreateHeadSeatCutter(draft, spec) : null;
                cutters.Add((binding, shaft, head));
            }
        }
        catch (Exception ex)
        {
            message = $"几何生成失败：{ex.Message}";
            return false;
        }

        var undo = doc.BeginUndoRecord("参数化紧固件：更新组件");
        var ownsUndoRecord = undo != 0;
        try
        {
            ComponentPresentationService.RemoveGroup(doc, draft.ComponentId);
            foreach (var old in ComponentRepository.FindComponentObjects(doc, draft.ComponentId).ToList())
                doc.Objects.Delete(old, true);

            var proxyIds = new List<Guid>();
            foreach (var proxy in proxies)
            {
                var attributes = ComponentRepository.CreateAttributes(draft, "Proxy");
                ComponentPresentationService.ConfigureAttributes(doc, attributes, draft, false, true);
                var id = doc.Objects.AddBrep(proxy, attributes);
                if (id == Guid.Empty)
                    throw new InvalidOperationException("无法写入螺丝代理体。");
                proxyIds.Add(id);
            }

            var bindings = new List<HoleTargetBinding>();
            var componentObjectIds = new List<Guid>(proxyIds);
            foreach (var item in cutters)
            {
                var cutterAttributes = ComponentRepository.CreateAttributes(
                    draft, "Cutter", item.Binding.TargetObjectId, item.Binding.BindingId);
                ComponentPresentationService.ConfigureAttributes(
                    doc, cutterAttributes, draft, true, item.Binding.IsPreviewVisible);
                var cutterId = doc.Objects.AddBrep(item.Shaft, cutterAttributes);
                if (cutterId == Guid.Empty)
                    throw new InvalidOperationException("无法写入孔切割体。");
                componentObjectIds.Add(cutterId);
                if (item.Head is not null)
                {
                    var headAttributes = ComponentRepository.CreateAttributes(
                        draft, "HeadCutter", item.Binding.TargetObjectId, item.Binding.BindingId);
                    ComponentPresentationService.ConfigureAttributes(
                        doc, headAttributes, draft, true, item.Binding.IsPreviewVisible);
                    var headId = doc.Objects.AddBrep(item.Head, headAttributes);
                    if (headId == Guid.Empty)
                        throw new InvalidOperationException("无法写入头部切割体。");
                    componentObjectIds.Add(headId);
                }
                bindings.Add(item.Binding with { CutterObjectId = cutterId });
            }

            saved = draft with
            {
                ProxyObjectId = proxyIds[0],
                Bindings = bindings,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            foreach (var obj in ComponentRepository.FindComponentObjects(doc, saved.ComponentId).ToList())
            {
                var attributes = obj.Attributes.Duplicate();
                ComponentRepository.Write(attributes, saved,
                    obj.Attributes.GetUserString(ComponentRepository.RoleKey) ?? "Proxy",
                    Guid.TryParse(obj.Attributes.GetUserString(ComponentRepository.TargetIdKey), out var targetId) ? targetId : Guid.Empty,
                    Guid.TryParse(obj.Attributes.GetUserString(ComponentRepository.BindingIdKey), out var bindingId) ? bindingId : Guid.Empty);
                doc.Objects.ModifyAttributes(obj, attributes, true);
            }

            ComponentPresentationService.RecreateGroup(doc, saved.ComponentId, componentObjectIds);

            doc.Views.Redraw();
            message = $"已生成 {saved.Size}，绑定 {saved.Bindings.Count} 个被切割体。";
            if (warnings.Count > 0)
                message += $" {string.Join(" ", warnings.Distinct())}";
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
                ? $"更新失败，已回滚：{ex.Message}"
                : $"更新失败：{ex.Message}。当前操作由 Rhino 命令撤销记录管理。";
            return false;
        }
        finally
        {
            if (ownsUndoRecord && undo != 0)
                doc.EndUndoRecord(undo);
        }
    }

    private static string TargetName(RhinoObject target) =>
        string.IsNullOrWhiteSpace(target.Attributes.Name)
            ? $"实体 {target.Id.ToString("N")[..8]}"
            : target.Attributes.Name;
}
