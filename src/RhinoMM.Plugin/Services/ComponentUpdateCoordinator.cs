using Rhino;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.UI;

namespace RhinoMM.Plugin.Services;

internal static class ComponentUpdateCoordinator
{
    public static bool TryApplyTemplate(
        RhinoDoc doc,
        IReadOnlyList<FastenerComponentData> components,
        FastenerUpdateTemplate template,
        out IReadOnlyList<FastenerComponentData> saved,
        out string message)
    {
        saved = [];
        if (!ValidateTargets(doc, components, template.Kind, out message))
            return false;
        var drafts = components.Select(component => template.ApplyTo(component)).ToArray();
        return TryApplyDrafts(doc, drafts, out saved, out message);
    }

    public static bool TryApplyDrafts(
        RhinoDoc doc,
        IReadOnlyList<FastenerComponentData> drafts,
        out IReadOnlyList<FastenerComponentData> saved,
        out string message)
    {
        saved = [];
        if (drafts.Count == 0)
        {
            message = "没有需要更新的参数化紧固件。";
            return false;
        }
        if (!FastenerComponentService.CreateOrReplaceMany(doc, drafts, out saved, out message))
            return false;
        RestoreSelection(doc, saved);
        ComponentEditorSession.ActivateMany(
            doc,
            saved,
            false,
            ComponentActivationIntent.SynchronizeOnly);
        ViewportOperationFeedback.Show(doc, saved);
        return true;
    }

    private static bool ValidateTargets(
        RhinoDoc doc,
        IReadOnlyList<FastenerComponentData> components,
        FastenerKind targetKind,
        out string message)
    {
        if (components.Count == 0)
        {
            message = "请先选择一个或多个参数化紧固件控制点。";
            return false;
        }
        if (components.Any(ComponentHostResolver.NeedsRelink)
            || components.Any(component => component.Bindings.Any(binding =>
                binding.TargetObjectId == Guid.Empty
                || doc.Objects.FindId(binding.TargetObjectId) is null)))
        {
            message = "选中的组件存在待重新绑定宿主的副本；请先运行“刷新 / 清理”。";
            return false;
        }
        if (components.Count > 1)
        {
            var targetIsNut = FastenerKindTraits.IsNut(targetKind);
            if (components.Any(component => FastenerKindTraits.IsNut(component.Kind) != targetIsNut))
            {
                message = "批量更新不能在螺丝与螺母类别之间转换；请重新放置对应类型。";
                return false;
            }
            if (targetIsNut && components.Any(component =>
                    component.Bindings.Count != 1
                    || component.Bindings[0].Role != ShaftFitRole.InstallationPocket))
            {
                message = "批量更新螺母要求每个组件都具有一个有效的安装宿主。";
                return false;
            }
        }
        message = string.Empty;
        return true;
    }

    private static void RestoreSelection(
        RhinoDoc doc,
        IReadOnlyList<FastenerComponentData> components)
    {
        doc.Objects.UnselectAll(false);
        foreach (var component in components)
        {
            var controlPoint = ComponentRepository.FindControlPoint(doc, component.ComponentId);
            if (controlPoint is not null)
                doc.Objects.Select(controlPoint.Id, false);
        }
        doc.Views.Redraw();
    }
}
