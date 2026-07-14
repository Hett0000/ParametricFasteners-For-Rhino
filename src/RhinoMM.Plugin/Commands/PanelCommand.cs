using Rhino;
using Rhino.Commands;
using Rhino.UI;
using RhinoMM.Plugin.UI;

namespace RhinoMM.Plugin.Commands;

public sealed class RhinoMMPanelCommand : Command
{
    public override string EnglishName => "RhinoMMPanel";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        Panels.OpenPanel(typeof(RhinoMMPanel).GUID);
        return Result.Success;
    }
}
