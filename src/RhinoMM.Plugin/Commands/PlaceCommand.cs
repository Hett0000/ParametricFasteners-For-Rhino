using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using RhinoMM.Core.Domain;
using RhinoMM.Plugin.Geometry;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.Commands;

public sealed class RhinoMMPlaceHoleCommand : Command
{
    public override string EnglishName => "RhinoMMPlaceHole";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var clearanceTargets = SelectTargets("选择螺丝穿过的物体（正补偿通孔），按 Enter 跳过");
        var engagementTargets = SelectTargets("选择需要与螺丝咬合的物体（负补偿孔），按 Enter 跳过");
        if (clearanceTargets.Count + engagementTargets.Count == 0)
        {
            RhinoApp.WriteLine("至少需要选择一个被切割体。");
            return Result.Cancel;
        }

        var placementResult = GetPlacementPlane(out var plane);
        if (placementResult != Result.Success)
            return placementResult;

        var state = EditorState.Current;
        var bindings = new List<HoleTargetBinding>();
        bindings.AddRange(clearanceTargets.Select((id, index) => new HoleTargetBinding
        {
            TargetObjectId = id,
            Role = ShaftFitRole.Clearance,
            ClearanceFit = state.ClearanceFit,
            IncludeHeadSeat = index == 0
        }));
        bindings.AddRange(engagementTargets.Select((id, index) => new HoleTargetBinding
        {
            TargetObjectId = id,
            Role = ShaftFitRole.ThreadEngagement,
            BiteReduction = state.BiteReduction,
            IncludeHeadSeat = clearanceTargets.Count == 0 && index == 0
        }));

        state.LoadedComponentId = Guid.Empty;
        var draft = state.CreateDraft(FastenerGeometryFactory.FromPlane(plane), bindings);
        if (!FastenerComponentService.CreateOrReplace(doc, draft, out var saved, out var message))
        {
            RhinoApp.WriteLine(message);
            return Result.Failure;
        }

        state.Load(saved);
        RhinoApp.WriteLine(message);
        return Result.Success;
    }

    private static List<Guid> SelectTargets(string prompt)
    {
        using var go = new GetObject();
        go.SetCommandPrompt(prompt);
        go.GeometryFilter = ObjectType.Brep | ObjectType.Extrusion;
        go.GroupSelect = true;
        go.SubObjectSelect = false;
        go.AcceptNothing(true);
        go.EnablePreSelect(false, true);
        var result = go.GetMultiple(1, 0);
        if (result == GetResult.Nothing)
            return [];
        if (result != GetResult.Object)
            return [];
        return Enumerable.Range(0, go.ObjectCount).Select(i => go.Object(i).ObjectId).Distinct().ToList();
    }

    private static Result GetPlacementPlane(out Plane plane)
    {
        plane = Plane.WorldXY;
        using var faceGetter = new GetObject();
        faceGetter.SetCommandPrompt("选择放置面；按 Enter 改用起始点和轴向");
        faceGetter.GeometryFilter = ObjectType.Surface;
        faceGetter.SubObjectSelect = true;
        faceGetter.AcceptNothing(true);
        var faceResult = faceGetter.Get();
        if (faceResult == GetResult.Object)
        {
            var reference = faceGetter.Object(0);
            var face = reference.Face();
            var point = reference.SelectionPoint();
            if (face is null || !point.IsValid || !face.ClosestPoint(point, out var u, out var v))
                return Result.Failure;
            if (!face.FrameAt(u, v, out plane))
                plane = new Plane(point, face.NormalAt(u, v));
            return Result.Success;
        }
        if (faceResult != GetResult.Nothing)
            return Result.Cancel;

        var pointResult = RhinoGet.GetPoint("选择孔的起始点", false, out var origin);
        if (pointResult != Result.Success)
            return pointResult;
        using var axisGetter = new GetPoint();
        axisGetter.SetCommandPrompt("指定孔的轴向");
        axisGetter.SetBasePoint(origin, true);
        axisGetter.DrawLineFromPoint(origin, true);
        if (axisGetter.Get() != GetResult.Point)
            return Result.Cancel;
        var axis = axisGetter.Point() - origin;
        if (!axis.Unitize())
            return Result.Failure;
        plane = new Plane(origin, axis);
        return Result.Success;
    }
}
