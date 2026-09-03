using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.Commands;

public sealed class RhinoMMExportToRhinoCommand : Command
{
    public override string EnglishName => "RhinoMMExportToRhino";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode) => Execute(doc, mode, false);

    internal static Result Execute(RhinoDoc doc, RunMode mode, bool includeFastenerSolids)
    {
        var selectionResult = RhinoMMExportPrintCommand.SelectHosts(out var hosts);
        if (selectionResult != Result.Success)
            return selectionResult;
        var options = new RhinoPlacementExportOptions(includeFastenerSolids);
        if (!RhinoPlacementExportService.TryBuild(doc, hosts, options, out var result, out var message))
        {
            RhinoMM.Plugin.Services.FastenerCommandText.WriteLine($"放入 Rhino 失败：{message}");
            return Result.Failure;
        }
        foreach (var warning in result.Warnings)
            RhinoMM.Plugin.Services.FastenerCommandText.WriteLine($"放入 Rhino 警告：{warning}");

        var anchor = result.BottomCenter;
        using var previewMaterial = new DisplayMaterial(
            System.Drawing.Color.FromArgb(66, 133, 200),
            0.45);
        using var steelPreviewMaterial = new DisplayMaterial(
            System.Drawing.Color.FromArgb(169, 173, 178),
            0.08);
        using var brassPreviewMaterial = new DisplayMaterial(
            System.Drawing.Color.FromArgb(184, 138, 50),
            0.08);
        using var pointGetter = new GetPoint();
        pointGetter.SetCommandPrompt(FastenerText.Translate("移动导出成果，单击确定放置位置"));
        pointGetter.SetBasePoint(anchor, false);
        pointGetter.DynamicDraw += (_, e) => DrawPreview(
            e,
            result,
            anchor,
            previewMaterial,
            steelPreviewMaterial,
            brassPreviewMaterial);
        if (pointGetter.Get() != GetResult.Point)
            return pointGetter.CommandResult();

        var translation = Transform.Translation(pointGetter.Point() - anchor);
        return Commit(doc, result, translation, out message)
            ? WriteSuccess(message)
            : WriteFailure(message);
    }

    private static void DrawPreview(
        GetPointDrawEventArgs e,
        RhinoPlacementExportResult result,
        Point3d anchor,
        DisplayMaterial hostMaterial,
        DisplayMaterial steelMaterial,
        DisplayMaterial brassMaterial)
    {
        var transform = Transform.Translation(e.CurrentPoint - anchor);
        e.Display.PushModelTransform(transform);
        try
        {
            foreach (var body in result.BooleanResult.Bodies)
            {
                e.Display.DrawBrepShaded(body.Geometry, hostMaterial);
                e.Display.DrawBrepWires(body.Geometry, System.Drawing.Color.FromArgb(34, 92, 155), 2);
            }
            foreach (var body in result.FastenerBodies)
            {
                var material = body.MaterialKind == RhinoExportMaterialKind.Brass
                    ? brassMaterial
                    : steelMaterial;
                var wireColor = body.MaterialKind == RhinoExportMaterialKind.Brass
                    ? System.Drawing.Color.FromArgb(120, 78, 16)
                    : System.Drawing.Color.FromArgb(72, 80, 88);
                e.Display.DrawBrepShaded(body.Geometry, material);
                e.Display.DrawBrepWires(body.Geometry, wireColor, 1);
            }
        }
        finally
        {
            e.Display.PopModelTransform();
        }
    }

    private static bool Commit(
        RhinoDoc doc,
        RhinoPlacementExportResult result,
        Transform translation,
        out string message)
    {
        // This method only runs inside a Rhino command. Rhino already owns the
        // command-level undo record, so opening a nested record here returns 0
        // on supported Rhino 8 builds and incorrectly prevents every write.
        var ids = new List<Guid>();
        var groupIndex = RhinoMath.UnsetIntIndex;
        RhinoRenderExportResources? renderResources = null;
        try
        {
            foreach (var body in result.BooleanResult.Bodies)
            {
                var geometry = body.Geometry.DuplicateBrep();
                geometry.Transform(translation);
                var attributes = PrepareAttributes(body.SourceAttributes);
                var id = doc.Objects.AddBrep(geometry, attributes);
                if (id == Guid.Empty)
                    throw new InvalidOperationException("无法将布尔成果写入 Rhino 文档。");
                ids.Add(id);
            }

            if (result.FastenerBodies.Count > 0)
            {
                renderResources = RhinoRenderExportPresentationService.EnsureResources(doc);
                foreach (var body in result.FastenerBodies)
                {
                    var geometry = body.Geometry.DuplicateBrep();
                    geometry.Transform(translation);
                    var attributes = RhinoRenderExportPresentationService.CreateFastenerAttributes(
                        renderResources,
                        body);
                    var id = doc.Objects.AddBrep(geometry, attributes);
                    if (id == Guid.Empty)
                        throw new InvalidOperationException("无法将紧固件渲染实体写入 Rhino 文档。");
                    ids.Add(id);
                }
            }

            var groupName = $"参数化紧固件::导出成果::{Guid.NewGuid():D}";
            groupIndex = doc.Groups.Add(groupName, ids);
            if (groupIndex < 0)
                throw new InvalidOperationException("无法创建布尔成果组。");

            doc.Objects.UnselectAll(false);
            foreach (var id in ids)
                doc.Objects.Select(id, false);
            doc.Views.Redraw();
            message = $"已放入 {result.BooleanResult.Bodies.Count} 个布尔成果"
                + (result.FastenerBodies.Count > 0
                    ? $"和 {result.FastenerBodies.Count} 个紧固件渲染实体"
                    : string.Empty)
                + "；原模型和参数化组件未修改。"
                + (result.Warnings.Count == 0 ? string.Empty : $" 警告：{string.Join(" ", result.Warnings)}");
            return true;
        }
        catch (Exception ex)
        {
            RollbackPartialCommit(doc, groupIndex, ids, renderResources);
            message = $"写入失败，已回滚：{ex.Message}";
            return false;
        }
    }

    private static void RollbackPartialCommit(
        RhinoDoc doc,
        int groupIndex,
        IEnumerable<Guid> ids,
        RhinoRenderExportResources? renderResources)
    {
        if (groupIndex != RhinoMath.UnsetIntIndex && groupIndex >= 0)
        {
            try
            {
                doc.Groups.Delete(groupIndex);
            }
            catch
            {
                // Continue removing any objects that were already written.
            }
        }

        foreach (var id in ids)
        {
            if (id != Guid.Empty)
                doc.Objects.Delete(id, true);
        }
        RhinoRenderExportPresentationService.RollbackCreatedResources(doc, renderResources);
        doc.Views.Redraw();
    }

    private static Rhino.DocObjects.ObjectAttributes PrepareAttributes(
        Rhino.DocObjects.ObjectAttributes source)
    {
        var attributes = source.Duplicate();
        attributes.ObjectId = Guid.Empty;
        attributes.RemoveFromAllGroups();
        attributes.DeleteUserString(ComponentRepository.ComponentKey);
        attributes.DeleteUserString(ComponentRepository.ComponentIdKey);
        attributes.DeleteUserString(ComponentRepository.RoleKey);
        attributes.DeleteUserString(ComponentRepository.TargetIdKey);
        attributes.DeleteUserString(ComponentRepository.BindingIdKey);
        attributes.Name = string.IsNullOrWhiteSpace(source.Name)
            ? "布尔成果"
            : $"{source.Name} · 布尔成果";
        attributes.Visible = true;
        attributes.Mode = Rhino.DocObjects.ObjectMode.Normal;
        return attributes;
    }

    private static Result WriteSuccess(string message)
    {
        RhinoMM.Plugin.Services.FastenerCommandText.WriteLine(message);
        return Result.Success;
    }

    private static Result WriteFailure(string message)
    {
        RhinoMM.Plugin.Services.FastenerCommandText.WriteLine(message);
        return Result.Failure;
    }
}
