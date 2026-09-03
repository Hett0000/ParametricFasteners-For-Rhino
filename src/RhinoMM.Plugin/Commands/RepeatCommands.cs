using Rhino;
using Rhino.Commands;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.Services;
using RhinoMM.Plugin.UI;

namespace RhinoMM.Plugin.Commands;

public sealed class ParametricFastenersRepeatPlaceCommand : Command
{
    public override string EnglishName => "ParametricFastenersRepeatPlace";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var template = FastenerTemplateLibraryService.Current.LastPlacement;
        if (template is null)
        {
            RhinoMM.Plugin.Services.FastenerCommandText.WriteLine("还没有可重复的成功放置；请先完成一次智能放置。");
            return Result.Nothing;
        }
        RhinoMM.Plugin.Services.FastenerCommandText.WriteLine($"重复放置：{FastenerTemplateFormatter.Compact(template)}");
        return SmartPlacementCommand.Execute(doc, mode, template);
    }
}

public sealed class ParametricFastenersRepeatUpdateCommand : Command
{
    public override string EnglishName => "ParametricFastenersRepeatUpdate";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var data = FastenerTemplateLibraryService.Current.LastUpdate;
        if (data is null)
        {
            RhinoMM.Plugin.Services.FastenerCommandText.WriteLine("还没有可重复的成功更新；请先完成一次组件更新。");
            return Result.Nothing;
        }
        var components = ComponentRepository.ReadSelectedControlPoints(doc);
        if (components.Count == 0)
        {
            RhinoMM.Plugin.Services.FastenerCommandText.WriteLine("请先选择一个或多个参数化紧固件控制点。");
            return Result.Nothing;
        }
        var display = GlobalDisplaySettingsService.Current;
        var template = data.ToUpdateTemplate(
            display.FastenerOpacityPercent,
            display.CutterOpacityPercent);
        if (!ComponentUpdateCoordinator.TryApplyTemplate(
                doc,
                components,
                template,
                out _,
                out var message))
        {
            RhinoMM.Plugin.Services.FastenerCommandText.WriteLine(message);
            return Result.Failure;
        }
        FastenerTemplateLibraryService.RecordSuccessfulOperation(
            data,
            FastenerOperationKind.Update,
            out _);
        RhinoMM.Plugin.Services.FastenerCommandText.WriteLine($"{message} 重复模板：{FastenerTemplateFormatter.Compact(data)}");
        return Result.Success;
    }
}

public sealed class ParametricFastenersQuickEditCommand : Command
{
    public override string EnglishName => "ParametricFastenersQuickEdit";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (!ViewportQuickEditorService.ShowForCurrentSelection(doc, true))
        {
            RhinoMM.Plugin.Services.FastenerCommandText.WriteLine("请选择一个有效的参数化紧固件控制点。");
            return Result.Nothing;
        }
        return Result.Success;
    }
}
