using Rhino;
using Rhino.Commands;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Commands;

public sealed class RhinoMMValidateCommand : Command
{
    public override string EnglishName => "RhinoMMValidate";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode) => Execute(doc, mode);

    internal static Result Execute(RhinoDoc doc, RunMode mode)
    {
        var components = ComponentRepository.ReadAllControlPoints(doc, out var ignoredComponentCount).ToList();

        var errors = 0;
        foreach (var component in components)
        {
            var validation = FastenerComponentValidator.Validate(
                component,
                FastenerSpecResolver.Resolve(component, RhinoMMPlugIn.Catalog));
            foreach (var issue in validation.Issues)
            {
                if (issue.IsError) errors++;
                RhinoApp.WriteLine($"[{component.Size}/{component.ComponentId}] {(issue.IsError ? "错误" : "提示")}: {issue.Message}");
            }
            foreach (var binding in component.Bindings.Where(x => doc.Objects.FindId(x.TargetObjectId) is null))
            {
                errors++;
                RhinoApp.WriteLine($"[{component.Size}/{component.ComponentId}] 错误: 被切割体链接丢失 {binding.TargetObjectId}");
            }
        }

        errors += ignoredComponentCount;
        if (ignoredComponentCount > 0)
            RhinoApp.WriteLine($"发现 {ignoredComponentCount} 个缺少有效控制点的损坏组件。");
        RhinoApp.WriteLine($"参数化紧固件校验完成：{components.Count} 个有效组件，{errors} 个错误。");
        return errors == 0 ? Result.Success : Result.Failure;
    }
}
