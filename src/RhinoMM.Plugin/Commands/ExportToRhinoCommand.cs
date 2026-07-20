using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.Commands;

public sealed class RhinoMMExportToRhinoCommand : Command
{
    public override string EnglishName => "RhinoMMExportToRhino";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode) => Execute(doc, mode);

    internal static Result Execute(RhinoDoc doc, RunMode mode)
    {
        var selectionResult = RhinoMMExportPrintCommand.SelectHosts(out var hosts);
        if (selectionResult != Result.Success)
            return selectionResult;
        if (!BooleanExportService.TryBuild(doc, hosts, out var result, out var message))
        {
            RhinoApp.WriteLine($"放入 Rhino 失败：{message}");
            return Result.Failure;
        }
        foreach (var warning in result.Warnings)
            RhinoApp.WriteLine($"放入 Rhino 警告：{warning}");

        var anchor = result.BottomCenter;
        using var previewMaterial = new DisplayMaterial(
            System.Drawing.Color.FromArgb(66, 133, 200),
            0.45);
        using var pointGetter = new GetPoint();
        pointGetter.SetCommandPrompt("移动布尔成果，单击确定放置位置");
        pointGetter.SetBasePoint(anchor, false);
        pointGetter.DynamicDraw += (_, e) => DrawPreview(e, result, anchor, previewMaterial);
        if (pointGetter.Get() != GetResult.Point)
            return pointGetter.CommandResult();

        var translation = Transform.Translation(pointGetter.Point() - anchor);
        return Commit(doc, result, translation, out message)
            ? WriteSuccess(message)
            : WriteFailure(message);
    }

    private static void DrawPreview(
        GetPointDrawEventArgs e,
        BooleanExportResult result,
        Point3d anchor,
        DisplayMaterial material)
    {
        var transform = Transform.Translation(e.CurrentPoint - anchor);
        e.Display.PushModelTransform(transform);
        try
        {
            foreach (var body in result.Bodies)
            {
                e.Display.DrawBrepShaded(body.Geometry, material);
                e.Display.DrawBrepWires(body.Geometry, System.Drawing.Color.FromArgb(34, 92, 155), 2);
            }
        }
        finally
        {
            e.Display.PopModelTransform();
        }
    }

    private static bool Commit(
        RhinoDoc doc,
        BooleanExportResult result,
        Transform translation,
        out string message)
    {
        // This method only runs inside a Rhino command. Rhino already owns the
        // command-level undo record, so opening a nested record here returns 0
        // on supported Rhino 8 builds and incorrectly prevents every write.
        var ids = new List<Guid>();
        var groupIndex = RhinoMath.UnsetIntIndex;
        try
        {
            foreach (var body in result.Bodies)
            {
                var geometry = body.Geometry.DuplicateBrep();
                geometry.Transform(translation);
                var attributes = PrepareAttributes(body.SourceAttributes);
                var id = doc.Objects.AddBrep(geometry, attributes);
                if (id == Guid.Empty)
                    throw new InvalidOperationException("无法将布尔成果写入 Rhino 文档。");
                ids.Add(id);
            }

            var groupName = $"参数化紧固件::导出成果::{Guid.NewGuid():D}";
            groupIndex = doc.Groups.Add(groupName, ids);
            if (groupIndex < 0)
                throw new InvalidOperationException("无法创建布尔成果组。");

            doc.Objects.UnselectAll(false);
            foreach (var id in ids)
                doc.Objects.Select(id, false);
            doc.Views.Redraw();
            message = $"已放入 {ids.Count} 个普通 Brep；原模型和参数化组件未修改。"
                + (result.Warnings.Count == 0 ? string.Empty : $" 警告：{string.Join(" ", result.Warnings)}");
            return true;
        }
        catch (Exception ex)
        {
            RollbackPartialCommit(doc, groupIndex, ids);
            message = $"写入失败，已回滚：{ex.Message}";
            return false;
        }
    }

    private static void RollbackPartialCommit(RhinoDoc doc, int groupIndex, IEnumerable<Guid> ids)
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
        RhinoApp.WriteLine(message);
        return Result.Success;
    }

    private static Result WriteFailure(string message)
    {
        RhinoApp.WriteLine(message);
        return Result.Failure;
    }
}
