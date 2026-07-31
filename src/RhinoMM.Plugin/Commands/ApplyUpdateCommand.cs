using Rhino;
using Rhino.Commands;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.Commands;

public sealed class RhinoMMApplyUpdateCommand : Command
{
    public override string EnglishName => "RhinoMMApplyUpdate";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var existing = ComponentsFromSelection(doc);
        if (existing.Count == 0)
        {
            RhinoApp.WriteLine("请先选择一个或多个参数化紧固件控制点。");
            return Result.Nothing;
        }
        var unresolved = existing.Where(ComponentHostResolver.NeedsRelink).ToArray();
        if (unresolved.Length > 0)
        {
            RhinoApp.WriteLine("选中的组件存在待重新绑定宿主的副本；请先选择对应宿主并运行“刷新 / 清理”。");
            return Result.Failure;
        }

        var state = EditorState.Current;
        var drafts = existing.Select(component => state.CreateUpdateDraft(component)).ToArray();
        if (!FastenerComponentService.CreateOrReplaceMany(doc, drafts, out var saved, out var message))
        {
            RhinoApp.WriteLine(message);
            return Result.Failure;
        }
        ComponentEditorSession.ActivateMany(
            doc,
            saved,
            false,
            ComponentActivationIntent.SynchronizeOnly);
        RhinoApp.WriteLine(message);
        return Result.Success;
    }

    private static IReadOnlyList<RhinoMM.Core.Domain.FastenerComponentData> ComponentsFromSelection(RhinoDoc doc)
    {
        return ComponentRepository.ReadSelectedControlPoints(doc);
    }
}
