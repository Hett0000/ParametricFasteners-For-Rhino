using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Commands;

public sealed class RhinoMMExportPrintCommand : Command
{
    public override string EnglishName => "RhinoMMExportPrint";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        using var go = new GetObject();
        go.SetCommandPrompt("选择要导出的实体（将对临时副本应用参数化紧固件孔）");
        go.GeometryFilter = ObjectType.Brep | ObjectType.Extrusion;
        go.GroupSelect = true;
        go.GetMultiple(1, 0);
        if (go.CommandResult() != Result.Success)
            return go.CommandResult();

        var pathResult = RhinoGet.GetString("输入导出路径（.stl 或 .step/.stp）", false, ref _lastPath);
        if (pathResult != Result.Success)
            return pathResult;
        var extension = Path.GetExtension(_lastPath).ToLowerInvariant();
        if (extension is not ".stl" and not ".step" and not ".stp")
        {
            RhinoApp.WriteLine("仅支持 .stl、.step 或 .stp。");
            return Result.Failure;
        }

        var allObjects = doc.Objects.ToList();
        var output = new List<Brep>();
        foreach (var hostRef in Enumerable.Range(0, go.ObjectCount).Select(go.Object))
        {
            var host = hostRef.Object();
            var hostBreps = ToBreps(host.Geometry);
            if (hostBreps.Count == 0)
            {
                RhinoApp.WriteLine($"无法转换实体：{host.Id}");
                return Result.Failure;
            }

            var cutterBreps = allObjects
                .Where(obj => string.Equals(obj.Attributes.GetUserString(ComponentRepository.TargetIdKey), host.Id.ToString("D"), StringComparison.OrdinalIgnoreCase))
                .SelectMany(obj => ToBreps(obj.Geometry))
                .ToList();

            var current = hostBreps;
            foreach (var cutter in cutterBreps)
            {
                var next = new List<Brep>();
                foreach (var body in current)
                {
                    var difference = Brep.CreateBooleanDifference(body, cutter, doc.ModelAbsoluteTolerance);
                    if (difference is null || difference.Length == 0)
                    {
                        RhinoApp.WriteLine($"布尔失败，已取消导出。宿主：{host.Id}");
                        return Result.Failure;
                    }
                    else
                        next.AddRange(difference);
                }
                current = next;
            }
            output.AddRange(current);
        }

        using var exportDoc = RhinoDoc.CreateHeadless(null);
        foreach (var brep in output)
            exportDoc.Objects.AddBrep(brep);
        var options = new FileWriteOptions { SuppressDialogBoxes = true };
        if (!exportDoc.WriteFile(_lastPath, options))
        {
            RhinoApp.WriteLine("写入导出文件失败。");
            return Result.Failure;
        }
        RhinoApp.WriteLine($"已在临时副本上完成布尔并导出：{_lastPath}。原模型未修改。");
        return Result.Success;
    }

    private static string _lastPath = Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory),
        "参数化紧固件-导出.stl");

    private static List<Brep> ToBreps(GeometryBase geometry) => geometry switch
    {
        Brep brep => [brep.DuplicateBrep()],
        Extrusion extrusion => [extrusion.ToBrep()],
        _ => []
    };
}
