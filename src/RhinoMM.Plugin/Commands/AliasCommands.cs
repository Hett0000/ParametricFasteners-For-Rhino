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
        MaintenanceCenterDialog.Show(doc);
        return Result.Success;
    }
}

public sealed class ParametricFastenersQuickRefreshCommand : Command
{
    public override string EnglishName => "ParametricFastenersQuickRefresh";
    protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode)
    {
        var success = ComponentMaintenanceService.QuickRefresh(doc, out var message);
        RhinoMM.Plugin.Services.FastenerCommandText.WriteLine(message);
        return success ? Result.Success : Result.Failure;
    }
}

public sealed class ParametricFastenersRelinkCommand : Command
{
    public override string EnglishName => "ParametricFastenersRelink";
    protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode)
    {
        RelinkWizardDialog.Show(doc);
        return Result.Success;
    }
}

public sealed class ParametricFastenersCleanupCommand : Command
{
    public override string EnglishName => "ParametricFastenersCleanup";
    protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode)
    {
        var success = ComponentMaintenanceService.CleanupResiduals(doc, out var message);
        RhinoMM.Plugin.Services.FastenerCommandText.WriteLine(message);
        return success ? Result.Success : Result.Failure;
    }
}

public sealed class ParametricFastenersAssemblyInspectorCommand : Command
{
    public override string EnglishName => "ParametricFastenersAssemblyInspector";
    protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode)
    {
        AssemblyInspectorDialog.Show(doc);
        return Result.Success;
    }
}

public sealed class ParametricFastenersAssemblySuggestionsCommand : Command
{
    public override string EnglishName => "ParametricFastenersAssemblySuggestions";
    protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode)
    {
        AssemblyInspectorDialog.Show(doc);
        return Result.Success;
    }
}

public sealed class ParametricFastenersAdaptiveUpdateCommand : Command
{
    public override string EnglishName => "ParametricFastenersAdaptiveUpdate";
    protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode)
    {
        var components = Persistence.ComponentRepository.ReadSelectedControlPoints(doc);
        var template = EditorState.Current.CaptureUpdateTemplate();
        var success = AdaptiveBatchUpdateService.TryApply(doc, components, template, out _, out var message);
        RhinoMM.Plugin.Services.FastenerCommandText.WriteLine(message);
        return success ? Result.Success : Result.Failure;
    }
}

public sealed class ParametricFastenersOutputCenterCommand : Command
{
    public override string EnglishName => "ParametricFastenersOutputCenter";
    protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode)
    {
        OutputCenterDialog.Show(doc);
        return Result.Success;
    }
}

public sealed class ParametricFastenersCustomLibraryCommand : Command
{
    public override string EnglishName => "ParametricFastenersCustomLibrary";
    protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode)
    {
        UserFastenerLibraryDialog.Show(doc);
        return Result.Success;
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

public sealed class ParametricFastenersNavigatorCommand : Command
{
    public override string EnglishName => "ParametricFastenersNavigator";

    protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode)
    {
        Rhino.UI.Panels.OpenPanel(typeof(ComponentNavigatorPanel).GUID);
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
