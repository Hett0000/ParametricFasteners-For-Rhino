using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input.Custom;
using RhinoMM.Plugin.Geometry;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.Commands;

public sealed class RhinoMMAdoptFastenerCommand : Command
{
    public override string EnglishName => "RhinoMMAdoptFastener";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        using var go = new GetObject();
        go.SetCommandPrompt("选择要转换为 RhinoMM 参数化螺丝的现有模型");
        go.GeometryFilter = ObjectType.Brep | ObjectType.Extrusion | ObjectType.InstanceReference;
        go.Get();
        if (go.CommandResult() != Result.Success)
            return go.CommandResult();

        var source = go.Object(0).Object();
        if (ComponentRepository.TryRead(source, out var existing))
        {
            EditorState.Current.Load(existing);
            RhinoApp.WriteLine("该对象已经是 RhinoMM 组件，参数已读取到面板。");
            return Result.Success;
        }

        var box = source.Geometry.GetBoundingBox(true);
        var origin = box.Center;
        var dimensions = new[]
        {
            (Length: box.Max.X - box.Min.X, Axis: Vector3d.XAxis),
            (Length: box.Max.Y - box.Min.Y, Axis: Vector3d.YAxis),
            (Length: box.Max.Z - box.Min.Z, Axis: Vector3d.ZAxis)
        };
        var axis = dimensions.OrderByDescending(x => x.Length).First().Axis;
        var plane = new Plane(origin, axis);
        var state = EditorState.Current;
        state.LoadedComponentId = Guid.Empty;
        var draft = state.CreateDraft(FastenerGeometryFactory.FromPlane(plane), []) with
        {
            AdoptedSourceObjectId = source.Id
        };

        if (!FastenerComponentService.CreateOrReplace(doc, draft, out var saved, out var message))
        {
            RhinoApp.WriteLine(message);
            return Result.Failure;
        }

        source.Attributes.Visible = false;
        doc.Objects.ModifyAttributes(source, source.Attributes, true);
        state.Load(saved);
        RhinoApp.WriteLine($"已按当前面板规格转换；原对象已隐藏并保留。{message}");
        return Result.Success;
    }
}
