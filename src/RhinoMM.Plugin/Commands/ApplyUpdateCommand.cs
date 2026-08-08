using Rhino;
using Rhino.Commands;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.Commands;

public sealed class RhinoMMApplyUpdateCommand : Command
{
    public override string EnglishName => "RhinoMMApplyUpdate";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode) => Execute(doc, mode);

    internal static Result Execute(RhinoDoc doc, RunMode mode)
    {
        var existing = ComponentsFromSelection(doc);
        if (existing.Count == 0)
        {
            RhinoApp.WriteLine("请先选择一个或多个参数化紧固件控制点。");
            return Result.Nothing;
        }
        var state = EditorState.Current;
        var template = state.CaptureUpdateTemplate();
        if (!ComponentUpdateCoordinator.TryApplyTemplate(
                doc,
                existing,
                template,
                out _,
                out var message))
        {
            RhinoApp.WriteLine(message);
            return Result.Failure;
        }
        FastenerTemplateLibraryService.RecordSuccessfulOperation(
            FastenerTemplateData.FromUpdateTemplate(template),
            FastenerOperationKind.Update,
            out _);
        RhinoApp.WriteLine(message);
        return Result.Success;
    }

    private static IReadOnlyList<RhinoMM.Core.Domain.FastenerComponentData> ComponentsFromSelection(RhinoDoc doc)
    {
        return ComponentRepository.ReadSelectedControlPoints(doc);
    }
}
