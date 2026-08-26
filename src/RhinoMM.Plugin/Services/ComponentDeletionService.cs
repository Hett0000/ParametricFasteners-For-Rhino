using Rhino;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Services;

internal static class ComponentDeletionService
{
    public static bool TryDeleteMany(
        RhinoDoc doc,
        IEnumerable<Guid> componentIds,
        out int deletedCount,
        out string message)
    {
        deletedCount = 0;
        var ids = componentIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
        {
            message = "没有选择需要删除的参数化组件。";
            return false;
        }

        var controls = new List<(Guid ComponentId, Guid ObjectId)>();
        foreach (var componentId in ids)
        {
            var matching = ComponentRepository.FindComponentObjects(doc, componentId)
                .Where(obj => obj.Attributes.GetUserString(ComponentRepository.RoleKey) == "ControlPoint")
                .ToArray();
            if (matching.Length != 1
                || !ComponentRepository.TryReadComponent(doc, componentId, out _))
            {
                message = $"组件 {componentId.ToString("N")[..8]} 缺少唯一有效控制点，未删除任何组件。";
                return false;
            }
            controls.Add((componentId, matching[0].Id));
        }

        var undo = doc.BeginUndoRecord($"参数化紧固件：删除 {ids.Length} 个问题组件");
        if (undo == 0)
        {
            message = "无法建立安全撤销记录，未删除任何组件。";
            return false;
        }
        var ended = false;
        try
        {
            foreach (var item in controls)
            {
                if (!doc.Objects.Delete(item.ObjectId, true))
                    throw new InvalidOperationException(
                        $"无法删除组件 {item.ComponentId.ToString("N")[..8]} 的控制点。");
            }
            doc.EndUndoRecord(undo);
            ended = true;
            deletedCount = ids.Length;
            foreach (var id in ids)
            {
                ComponentEditorSession.ForgetComponent(doc, id);
                ContextualEditSessionService.Cancel(doc, id);
            }
            doc.Views.Redraw();
            message = $"已删除 {deletedCount} 个参数化组件；宿主实体保持不变。";
            return true;
        }
        catch (Exception ex)
        {
            if (!ended)
            {
                doc.EndUndoRecord(undo);
                ended = true;
            }
            doc.Undo();
            doc.Views.Redraw();
            message = $"批量删除失败，已回滚全部修改：{ex.Message}";
            return false;
        }
        finally
        {
            if (!ended)
                doc.EndUndoRecord(undo);
        }
    }
}
