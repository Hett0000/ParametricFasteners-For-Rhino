using Rhino;
using Rhino.Commands;
using RhinoMM.Plugin.UI;

namespace RhinoMM.Plugin.Commands;

public sealed class RhinoMMStatisticsCommand : Command
{
    public override string EnglishName => "RhinoMMStatistics";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode) => Execute(doc, false);

    internal static Result Execute(RhinoDoc doc, bool focusExport)
    {
        FastenerStatisticsDialog.Show(doc, focusExport);
        return Result.Success;
    }
}

public sealed class ParametricFastenersStatisticsCommand : Command
{
    public override string EnglishName => "ParametricFastenersStatistics";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode) =>
        RhinoMMStatisticsCommand.Execute(doc, false);
}

public sealed class ParametricFastenersExportExcelCommand : Command
{
    public override string EnglishName => "ParametricFastenersExportExcel";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode) =>
        RhinoMMStatisticsCommand.Execute(doc, true);
}
