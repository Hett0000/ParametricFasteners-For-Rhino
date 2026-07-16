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
        var clearanceTargets = SelectTargets("选择螺丝穿过的物体（正补偿通孔）");
        var engagementTargets = SelectTargets("选择需要与螺丝咬合的物体（负补偿孔）");
        if (clearanceTargets.Count + engagementTargets.Count == 0)
        {
            RhinoApp.WriteLine("至少需要选择一个被切割体。");
            return Result.Cancel;
        }

        var placementResult = GetPlacementPlane(out var plane, out var placementObjectId);
        if (placementResult != Result.Success)
            return placementResult;

        var state = EditorState.Current;
        var bindings = new List<HoleTargetBinding>();
        var headSeatTarget = clearanceTargets.Contains(placementObjectId)
            ? placementObjectId
            : engagementTargets.Contains(placementObjectId)
                ? placementObjectId
                : clearanceTargets.FirstOrDefault(engagementTargets.FirstOrDefault());
        bindings.AddRange(clearanceTargets.Select(id => new HoleTargetBinding
        {
            TargetObjectId = id,
            Role = ShaftFitRole.Clearance,
            ClearanceFit = state.ClearanceFit,
            IncludeHeadSeat = id == headSeatTarget
        }));
        bindings.AddRange(engagementTargets.Select(id => new HoleTargetBinding
        {
            TargetObjectId = id,
            Role = ShaftFitRole.ThreadEngagement,
            BiteReduction = state.BiteReduction,
            IncludeHeadSeat = id == headSeatTarget
        }));

        state.LoadedComponentId = Guid.Empty;
        var draft = state.CreateDraft(FastenerGeometryFactory.FromPlane(plane), bindings);
        if (!FastenerComponentService.CreateOrReplace(doc, draft, out var saved, out var message))
        {
            RhinoApp.WriteLine(message);
            return Result.Failure;
        }

        ComponentEditorSession.Activate(doc, saved);
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

    private static Result GetPlacementPlane(out Plane plane, out Guid placementObjectId)
    {
        plane = Plane.WorldXY;
        placementObjectId = Guid.Empty;
        using var faceGetter = new GetObject();
        faceGetter.SetCommandPrompt("选择放置面，或按 Enter 改用起始点和轴向");
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
            var faceOrigin = face.PointAt(u, v);
            if (!face.FrameAt(u, v, out var frame))
                frame = new Plane(faceOrigin, face.NormalAt(u, v));
            var normal = face.NormalAt(u, v);
            if (face.OrientationIsReversed)
                normal.Reverse();
            var inwardAxis = normal;
            if (face.Brep.IsSolid)
                inwardAxis.Reverse();
            else
            {
                var flip = false;
                var directionResult = RhinoGet.GetBool(
                    "开放曲面无法判断内外，是否翻转当前方向", true, "保持", "翻转", ref flip);
                if (directionResult is not Result.Success and not Result.Nothing)
                    return directionResult;
                if (flip)
                    inwardAxis.Reverse();
            }
            var xAxis = frame.XAxis;
            var yAxis = Vector3d.CrossProduct(inwardAxis, xAxis);
            if (!yAxis.Unitize())
                return Result.Failure;
            plane = new Plane(faceOrigin, xAxis, yAxis);
            placementObjectId = reference.ObjectId;
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
