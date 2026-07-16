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
        FastenerComponentDataFromSelection(doc, out var existing);
        if (existing is null)
        {
            RhinoApp.WriteLine("请先选择一个参数化紧固件或切割模块。");
            return Result.Nothing;
        }

        var state = EditorState.Current;
        var draft = state.CreateDraft(existing.Placement, existing.Bindings) with
        {
            ComponentId = existing.ComponentId,
            AdoptedSourceObjectId = existing.AdoptedSourceObjectId
        };
        if (!FastenerComponentService.CreateOrReplace(doc, draft, out var saved, out var message))
        {
            RhinoApp.WriteLine(message);
            return Result.Failure;
        }
        ComponentEditorSession.Activate(doc, saved);
        RhinoApp.WriteLine(message);
        return Result.Success;
    }

    private static void FastenerComponentDataFromSelection(RhinoDoc doc, out RhinoMM.Core.Domain.FastenerComponentData? data)
    {
        if (ComponentRepository.TryReadSelection(doc, out var selected))
            data = selected;
        else if (ComponentEditorSession.TryGetActive(doc, out var active))
            data = active;
        else
            data = null;
    }
}
