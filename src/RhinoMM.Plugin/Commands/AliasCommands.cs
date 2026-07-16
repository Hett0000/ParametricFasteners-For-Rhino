using Rhino;
using Rhino.Commands;

namespace RhinoMM.Plugin.Commands;

public abstract class CommandAlias(string targetCommand) : Command
{
    protected override Result RunCommand(RhinoDoc doc, RunMode mode) =>
        RhinoApp.RunScript($"_-{targetCommand}", false) ? Result.Success : Result.Failure;
}

public sealed class ParametricFastenersPlaceCommand : CommandAlias
{
    public ParametricFastenersPlaceCommand() : base("RhinoMMPlaceHole") { }
    public override string EnglishName => "ParametricFastenersPlace";
}

public sealed class ParametricFastenersEditCommand : CommandAlias
{
    public ParametricFastenersEditCommand() : base("RhinoMMEditHole") { }
    public override string EnglishName => "ParametricFastenersEdit";
}

public sealed class ParametricFastenersAdoptCommand : CommandAlias
{
    public ParametricFastenersAdoptCommand() : base("RhinoMMAdoptFastener") { }
    public override string EnglishName => "ParametricFastenersAdopt";
}

public sealed class ParametricFastenersValidateCommand : CommandAlias
{
    public ParametricFastenersValidateCommand() : base("RhinoMMValidate") { }
    public override string EnglishName => "ParametricFastenersValidate";
}

public sealed class ParametricFastenersExportCommand : CommandAlias
{
    public ParametricFastenersExportCommand() : base("RhinoMMExportPrint") { }
    public override string EnglishName => "ParametricFastenersExport";
}
