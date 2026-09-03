using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input.Custom;
using Rhino.UI;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Services;
using RhinoMM.Plugin.UI;

namespace RhinoMM.Plugin.Commands;

public sealed class ParametricFastenersBatchPlaceCommand : Command
{
    public override string EnglishName => "ParametricFastenersBatchPlace";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        using var get = new GetObject();
        get.SetCommandPrompt(FastenerText.Translate("选择点、点云、曲线、圆形边或平面圆孔，按 Enter 预检批量放置"));
        get.GeometryFilter = ObjectType.Point | ObjectType.PointSet | ObjectType.Curve | ObjectType.Brep;
        get.GroupSelect = true;
        get.SubObjectSelect = true;
        get.GetMultiple(1, 0);
        if (get.CommandResult() != Result.Success)
            return get.CommandResult();

        var sources = Enumerable.Range(0, get.ObjectCount)
            .Select(get.Object)
            .ToArray();
        var candidates = BatchPlacementService.ExtractCandidates(
            sources,
            doc.ModelAbsoluteTolerance,
            out var duplicates,
            out var unsupported);
        if (candidates.Count == 0)
        {
            RhinoMM.Plugin.Services.FastenerCommandText.WriteLine("所选对象没有可用于批量放置的点位。");
            return Result.Nothing;
        }

        var template = FastenerTemplateData.FromUpdateTemplate(
            EditorState.Current.CaptureUpdateTemplate());
        BatchPlacementPreflightResult preflight;
        BatchPlacementDialog dialog;
        var conduit = new BatchPlacementPreviewConduit();
        while (true)
        {
            var activeTemplate = template;
            var preflightRun = FastenerProgressWindow.Run(
                "批量放置预检",
                $"准备检查 {candidates.Count} 个点位",
                operation => BatchPlacementService.Preflight(
                    doc,
                    candidates,
                    activeTemplate,
                    duplicates,
                    unsupported,
                    operation: operation));
            if (preflightRun.Cancelled)
            {
                RhinoMM.Plugin.Services.FastenerCommandText.WriteLine("批量预检已取消；模型未修改。");
                return Result.Cancel;
            }
            preflight = preflightRun.Result;
            conduit.Update(preflight.Items);
            conduit.Enabled = true;
            doc.Views.Redraw();
            dialog = new BatchPlacementDialog(
                doc,
                preflight,
                conduit,
                TemplateSummary(template));
            var accepted = dialog.ShowModal(RhinoEtoApp.MainWindow);
            if (!accepted)
            {
                dialog.Dispose();
                return Result.Cancel;
            }
            if (dialog.RequestedAssemblyMode is not { } requestedMode)
                break;
            dialog.Dispose();
            template = template with { AssemblyMode = requestedMode };
        }

        var creatableItems = preflight.Items
            .Where(item => item.Status != BatchPlacementStatus.Failure)
            .ToArray();
        if (creatableItems.Length == 0)
        {
            RhinoMM.Plugin.Services.FastenerCommandText.WriteLine("没有可创建的有效点位。");
            return Result.Nothing;
        }

        if (!BatchPlacementService.RevalidateAdoptedLengths(
                doc,
                creatableItems,
                template,
                SmartPlacementRecognitionMode.Automatic,
                out var creatable,
                out var revalidationMessage))
        {
            RhinoMM.Plugin.Services.FastenerCommandText.WriteLine($"批量放置失败：{revalidationMessage}");
            return Result.Failure;
        }

        if (!FastenerComponentService.CreateOrReplaceMany(
                doc,
                creatable,
                out var saved,
                out var message))
        {
            RhinoMM.Plugin.Services.FastenerCommandText.WriteLine($"批量放置失败：{message}");
            return Result.Failure;
        }
        ViewportOperationFeedback.Show(doc, saved);

        ComponentEditorSession.ActivateMany(
            doc,
            saved,
            true,
            ComponentActivationIntent.SynchronizeOnly);
        FastenerTemplateLibraryService.RecordSuccessfulOperation(
            template,
            FastenerOperationKind.Placement,
            out _);
        FastenerDocumentIndexService.Invalidate(doc);
        RhinoMM.Plugin.Services.FastenerCommandText.WriteLine(
            $"批量放置完成：创建 {saved.Count} 个组件"
            + (preflight.FailureCount > 0 && dialog.SkipFailures
                ? $"，跳过 {preflight.FailureCount} 个失败点"
                : string.Empty)
            + "；一次撤销可恢复本次操作。");
        dialog.Dispose();
        return Result.Success;
    }

    private static string TemplateSummary(FastenerTemplateData template)
    {
        var type = template.Kind == RhinoMM.Core.Domain.FastenerKind.HexNut
            && template.NutStyle == RhinoMM.Core.Domain.HexNutStyle.NylonInsertLocking
                ? "防松螺母"
                : FastenerLabels.ShortKind(template.Kind);
        var length = RhinoMM.Core.Services.FastenerKindTraits.UsesLengthInStatistics(template.Kind)
            ? $"×{template.Length:0.##}"
            : string.Empty;
        return $"{type} {template.Size}{length}";
    }
}
