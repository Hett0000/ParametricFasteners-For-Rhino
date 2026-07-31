using Rhino;
using Rhino.Commands;
using Rhino.UI;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.Services;
using RhinoMM.Plugin.UI;

namespace RhinoMM.Plugin.Commands;

public sealed class RhinoMMEditHoleCommand : Command
{
    public override string EnglishName => "RhinoMMEditHole";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode) => Execute(doc, mode);

    internal static Result Execute(RhinoDoc doc, RunMode mode)
    {
        var components = ComponentRepository.ReadSelectedControlPoints(doc);
        if (components.Count == 0)
        {
            RhinoApp.WriteLine("请选择一个或多个参数化紧固件控制点后重试。");
            return Result.Nothing;
        }

        ComponentEditorSession.ActivateMany(doc, components, false);
        RhinoApp.WriteLine(
            components.Count == 1
                ? $"已读取 {components[0].Size} / {components[0].Kind}。请在面板修改后点击“应用更新”。"
                : $"已读取 {components.Count} 个控制点；基础参数将批量应用。");
        Panels.OpenPanel(typeof(RhinoMMPanel).GUID);
        return Result.Success;
    }
}
