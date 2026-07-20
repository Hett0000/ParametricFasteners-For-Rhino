using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Input.Custom;
using Rhino.UI;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.Commands;

public enum PrintExportFormat
{
    Stl,
    Step
}

public sealed class RhinoMMExportPrintCommand : Command
{
    public override string EnglishName => "RhinoMMExportPrint";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode) => Execute(doc, mode);

    internal static Result Execute(
        RhinoDoc doc,
        RunMode mode,
        PrintExportFormat? requestedFormat = null)
    {
        var selectionResult = SelectHosts(out var hosts);
        if (selectionResult != Result.Success)
            return selectionResult;
        if (!BooleanExportService.TryBuild(doc, hosts, out var result, out var message))
        {
            RhinoApp.WriteLine($"导出失败：{message}");
            return Result.Failure;
        }
        foreach (var warning in result.Warnings)
            RhinoApp.WriteLine($"导出警告：{warning}");

        if (!TryGetPath(requestedFormat, out var path))
            return Result.Cancel;

        using var exportDoc = RhinoDoc.CreateHeadless(null);
        foreach (var body in result.Bodies)
            exportDoc.Objects.AddBrep(body.Geometry);
        var options = new FileWriteOptions { SuppressDialogBoxes = true };
        if (!exportDoc.WriteFile(path, options))
        {
            RhinoApp.WriteLine("写入导出文件失败。");
            return Result.Failure;
        }
        _lastDirectory = Path.GetDirectoryName(path) ?? _lastDirectory;
        RhinoApp.WriteLine(
            $"已在临时副本上完成布尔并导出：{path}。原模型未修改。"
            + (result.Warnings.Count == 0 ? string.Empty : $" 警告：{string.Join(" ", result.Warnings)}"));
        return Result.Success;
    }

    internal static Result SelectHosts(out IReadOnlyList<RhinoObject> hosts)
    {
        using var go = new GetObject();
        go.SetCommandPrompt("选择要导出的实体（将在临时副本上应用参数化紧固件孔）");
        go.GeometryFilter = ObjectType.Brep | ObjectType.Extrusion;
        go.GroupSelect = true;
        go.SetCustomGeometryFilter((obj, _, _) => string.IsNullOrWhiteSpace(
            obj.Attributes.GetUserString(RhinoMM.Plugin.Persistence.ComponentRepository.ComponentIdKey)));
        go.GetMultiple(1, 0);
        if (go.CommandResult() != Result.Success)
        {
            hosts = [];
            return go.CommandResult();
        }
        hosts = Enumerable.Range(0, go.ObjectCount)
            .Select(index => go.Object(index).Object())
            .Where(obj => obj is not null)
            .DistinctBy(obj => obj.Id)
            .ToArray()!;
        return hosts.Count > 0 ? Result.Success : Result.Cancel;
    }

    private static bool TryGetPath(PrintExportFormat? requestedFormat, out string path)
    {
        var format = requestedFormat ?? PrintExportFormat.Stl;
        var extension = format == PrintExportFormat.Stl ? "stl" : "step";
        var dialog = new SaveFileDialog
        {
            Title = requestedFormat is null ? "布尔导出 STL / STEP" : $"导出 {extension.ToUpperInvariant()}",
            DefaultExt = extension,
            InitialDirectory = _lastDirectory,
            FileName = $"参数化紧固件-导出.{extension}",
            Filter = requestedFormat switch
            {
                PrintExportFormat.Stl => "STL 文件 (*.stl)|*.stl",
                PrintExportFormat.Step => "STEP 文件 (*.step;*.stp)|*.step;*.stp",
                _ => "STL 文件 (*.stl)|*.stl|STEP 文件 (*.step;*.stp)|*.step;*.stp"
            }
        };
        if (!dialog.ShowSaveDialog())
        {
            path = string.Empty;
            return false;
        }
        path = dialog.FileName;
        var selectedExtension = Path.GetExtension(path).ToLowerInvariant();
        return selectedExtension is ".stl" or ".step" or ".stp";
    }

    private static string _lastDirectory =
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory);
}
