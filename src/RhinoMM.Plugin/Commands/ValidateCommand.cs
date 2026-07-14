using Rhino;
using Rhino.Commands;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Commands;

public sealed class RhinoMMValidateCommand : Command
{
    public override string EnglishName => "RhinoMMValidate";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var components = doc.Objects
            .Select(obj => ComponentRepository.TryRead(obj, out var data) ? data : null)
            .Where(data => data is not null)
            .GroupBy(data => data!.ComponentId)
            .Select(group => group.First()!)
            .ToList();

        var errors = 0;
        foreach (var component in components)
        {
            var validation = FastenerComponentValidator.Validate(component, RhinoMMPlugIn.Catalog.Get(component.Size));
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

        RhinoApp.WriteLine($"RhinoMM 校验完成：{components.Count} 个组件，{errors} 个错误。");
        return errors == 0 ? Result.Success : Result.Failure;
    }
}
