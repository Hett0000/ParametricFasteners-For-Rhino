using Rhino;
using Rhino.Commands;
using RhinoMM.Plugin.Services;
using RhinoMM.Plugin.UI;

namespace RhinoMM.Plugin.Commands;

public sealed class ParametricFastenersPlaceCommand : Command
{
    public override string EnglishName => "ParametricFastenersPlace";

    protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode) =>
        SmartPlacementCommand.Execute(doc, mode);
}

public sealed class ParametricFastenersPlaceClassicCommand : Command
{
    public override string EnglishName => "ParametricFastenersPlaceClassic";

    protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode) =>
        RhinoMMPlaceHoleCommand.ExecuteClassic(doc, mode);
}

public sealed class ParametricFastenersEditCommand : Command
{
    public override string EnglishName => "ParametricFastenersEdit";

    protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode) =>
        RhinoMMEditHoleCommand.Execute(doc, mode);
}

public sealed class ParametricFastenersApplyUpdateCommand : Command
{
    public override string EnglishName => "ParametricFastenersApplyUpdate";

    protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode) =>
        RhinoMMApplyUpdateCommand.Execute(doc, mode);
}

public sealed class ParametricFastenersRefreshCommand : Command
{
    public override string EnglishName => "ParametricFastenersRefresh";

    protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode)
    {
        if (!ComponentRefreshCoordinator.TryExecute(doc, null, out var result, out var message))
        {
            RhinoApp.WriteLine(message);
            return Result.Failure;
        }

        RhinoApp.WriteLine(message);
        doc.Views.Redraw();
        return result.FailedComponents > 0 ? Result.Failure : Result.Success;
    }
}

public sealed class ParametricFastenersMoreCommand : Command
{
    public override string EnglishName => "ParametricFastenersMore";

    protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode)
    {
        RhinoMMPanel.OpenMoreMenu();
        return Result.Success;
    }
}

public sealed class ParametricFastenersAdoptCommand : Command
{
    public override string EnglishName => "ParametricFastenersAdopt";

    protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode) =>
        RhinoMMAdoptFastenerCommand.Execute(doc, mode);
}

public sealed class ParametricFastenersValidateCommand : Command
{
    public override string EnglishName => "ParametricFastenersValidate";

    protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode) =>
        RhinoMMValidateCommand.Execute(doc, mode);
}

public sealed class ParametricFastenersExportCommand : Command
{
    public override string EnglishName => "ParametricFastenersExport";

    protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode) =>
        RhinoMMExportPrintCommand.Execute(doc, mode);
}

public sealed class ParametricFastenersExportStlCommand : Command
{
    public override string EnglishName => "ParametricFastenersExportStl";

    protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode) =>
        RhinoMMExportPrintCommand.Execute(doc, mode, PrintExportFormat.Stl);
}

public sealed class ParametricFastenersExportStepCommand : Command
{
    public override string EnglishName => "ParametricFastenersExportStep";

    protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode) =>
        RhinoMMExportPrintCommand.Execute(doc, mode, PrintExportFormat.Step);
}

public sealed class ParametricFastenersExportToRhinoCommand : Command
{
    public override string EnglishName => "ParametricFastenersExportToRhino";

    protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode) =>
        RhinoMMExportToRhinoCommand.Execute(doc, mode, false);
}

public sealed class ParametricFastenersExportToRhinoWithFastenersCommand : Command
{
    public override string EnglishName => "ParametricFastenersExportToRhinoWithFasteners";

    protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode) =>
        RhinoMMExportToRhinoCommand.Execute(doc, mode, true);
}
