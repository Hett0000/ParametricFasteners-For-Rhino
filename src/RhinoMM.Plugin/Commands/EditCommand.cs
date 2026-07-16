using Rhino;
using Rhino.Commands;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.Commands;

public sealed class RhinoMMEditHoleCommand : Command
{
    public override string EnglishName => "RhinoMMEditHole";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (!ComponentRepository.TryReadSelection(doc, out var component))
        {
            RhinoApp.WriteLine("请选择参数化紧固件或孔切割模块后重试。");
            return Result.Nothing;
        }

        ComponentEditorSession.Activate(doc, component, true);
        RhinoApp.WriteLine($"已读取 {component.Size} / {component.Kind}。请在面板修改后点击“应用更新”。");
        RhinoApp.RunScript("_-ParametricFasteners", false);
        return Result.Success;
    }
}
