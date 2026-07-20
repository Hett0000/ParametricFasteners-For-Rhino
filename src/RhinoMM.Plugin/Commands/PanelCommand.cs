using Rhino;
using Rhino.Commands;
using Rhino.UI;
using RhinoMM.Plugin.Services;
using RhinoMM.Plugin.UI;

namespace RhinoMM.Plugin.Commands;

public sealed class RhinoMMPanelCommand : Command
{
    public override string EnglishName => "RhinoMMPanel";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        ComponentEditorSession.TryActivateSelectionSet(doc, true, out _);
        Panels.OpenPanel(typeof(RhinoMMPanel).GUID);
        return Result.Success;
    }
}

public sealed class ParametricFastenersCommand : Command
{
    public override string EnglishName => "ParametricFasteners";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        ComponentEditorSession.TryActivateSelectionSet(doc, true, out _);
        Panels.OpenPanel(typeof(RhinoMMPanel).GUID);
        return Result.Success;
    }
}
