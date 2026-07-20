using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.UI;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Geometry;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.Commands;

public sealed class RhinoMMPlaceHoleCommand : Command
{
    public override string EnglishName => "RhinoMMPlaceHole";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode) => Execute(doc, mode);

    internal static Result Execute(RhinoDoc doc, RunMode mode)
    {
        var state = EditorState.Current;
        if (FastenerKindTraits.UsesSingleHostPlacement(state.Kind))
            return ExecuteSingleHostPlacement(doc, state);

        var clearanceTargets = SelectTargets("选择螺丝穿过的物体（正补偿通孔）");
        var engagementTargets = SelectTargets("选择需要与螺丝咬合的物体（负补偿孔）");
        if (clearanceTargets.Count + engagementTargets.Count == 0)
        {
            RhinoApp.WriteLine("至少需要选择一个被切割体。");
            return Result.Cancel;
        }

        var targetIds = clearanceTargets.Concat(engagementTargets).Distinct().ToArray();
        var placementResult = GetPlacementPlane(
            doc,
            targetIds,
            out var plane,
            out var placementObjectId,
            out var continuousPointPlacement);
        if (placementResult != Result.Success)
            return placementResult;

        if (!TryPlaceAt(doc, state, clearanceTargets, engagementTargets, plane, placementObjectId, out var saved, out var message))
        {
            RhinoApp.WriteLine(message);
            return Result.Failure;
        }

        var placed = new List<FastenerComponentData> { saved };
        if (continuousPointPlacement)
        {
            var reusableAxis = plane.ZAxis;
            while (true)
            {
                using var pointGetter = new GetPoint();
                pointGetter.SetCommandPrompt("继续捕捉放置点，按 Enter、右键或 Esc 完成");
                pointGetter.AcceptNothing(true);
                var pointResult = pointGetter.Get();
                if (pointResult is GetResult.Nothing or GetResult.Cancel)
                    break;
                if (pointResult != GetResult.Point)
                    continue;

                var origin = pointGetter.Point();
                var axis = reusableAxis;
                var nextPlacementObjectId = FindClosestPlacementHost(doc, targetIds, origin, out var hostBrep);
                if (hostBrep?.IsSolid == true)
                    OrientAxisIntoHost(hostBrep, origin, ref axis, doc.ModelAbsoluteTolerance);
                var nextPlane = new Plane(origin, axis);
                if (!TryPlaceAt(
                        doc,
                        state,
                        clearanceTargets,
                        engagementTargets,
                        nextPlane,
                        nextPlacementObjectId,
                        out var nextSaved,
                        out var nextMessage))
                {
                    RhinoApp.WriteLine($"该点放置失败：{nextMessage}");
                    continue;
                }
                placed.Add(nextSaved);
                RhinoApp.WriteLine($"已连续放置 {placed.Count} 颗紧固件。");
            }
        }

        ComponentEditorSession.Activate(doc, placed[^1], true);
        RhinoApp.WriteLine(
            placed.Count == 1
                ? message
                : $"连续放置完成：共生成 {placed.Count} 颗紧固件，可使用一次撤销恢复。");
        return Result.Success;
    }

    private static Result ExecuteSingleHostPlacement(RhinoDoc doc, EditorState state)
    {
        using var getter = new GetObject();
        getter.SetCommandPrompt(
            state.Kind == FastenerKind.HexNut
                ? "单击封闭宿主表面放置六角螺母槽"
                : "单击封闭宿主表面放置热熔螺母孔");
        getter.GeometryFilter = ObjectType.Surface;
        getter.SubObjectSelect = true;
        getter.GroupSelect = false;
        getter.SetCustomGeometryFilter(IsHostGeometry);
        getter.EnablePreSelect(false, true);
        if (getter.Get() != GetResult.Object)
            return Result.Cancel;

        var reference = getter.Object(0);
        var face = reference.Face();
        var selectionPoint = reference.SelectionPoint();
        if (face is null || !face.Brep.IsSolid)
        {
            RhinoApp.WriteLine("螺母安装槽/孔只能放置到封闭实体宿主上。");
            return Result.Failure;
        }
        if (!selectionPoint.IsValid || !face.ClosestPoint(selectionPoint, out var u, out var v))
            return Result.Failure;
        var origin = face.PointAt(u, v);
        if (!face.FrameAt(u, v, out var frame))
            frame = new Plane(origin, face.NormalAt(u, v));
        var normal = face.NormalAt(u, v);
        if (face.OrientationIsReversed)
            normal.Reverse();
        normal.Reverse();
        var xAxis = frame.XAxis;
        var yAxis = Vector3d.CrossProduct(normal, xAxis);
        if (!yAxis.Unitize())
            return Result.Failure;
        var plane = new Plane(origin, xAxis, yAxis);

        var preset = PlacementPresetService.Current;
        var depth = state.Kind == FastenerKind.HexNut
            ? RhinoMMPlugIn.Catalog.Get(state.Size).Head.NutThickness
            : state.Length;
        var heatSetPreset = HeatSetInsertPresetService.Current;
        var preview = state.Kind == FastenerKind.HeatSetInsert
            ? heatSetPreset.PreviewVisible
            : preset.ClearancePreviewVisible;
        var booleanEnabled = state.Kind == FastenerKind.HeatSetInsert
            ? heatSetPreset.BooleanEnabled
            : preset.ClearanceBooleanEnabled;
        var binding = new HoleTargetBinding
        {
            TargetObjectId = reference.ObjectId,
            Role = ShaftFitRole.InstallationPocket,
            DepthMode = DepthMode.Blind,
            BlindDepth = depth,
            IsPreviewVisible = preview,
            IsBooleanEnabled = booleanEnabled
        };
        state.LoadedComponentId = Guid.Empty;
        var draft = state.CreateDraft(FastenerGeometryFactory.FromPlane(plane), [binding]) with
        {
            PrintProfile = new PrintProfileSnapshot("当前 FDM 配置", preset.PrinterCorrection)
        };
        if (!FastenerComponentService.CreateOrReplace(doc, draft, out var saved, out var message))
        {
            RhinoApp.WriteLine(message);
            return Result.Failure;
        }
        ComponentEditorSession.Activate(doc, saved, true);
        RhinoApp.WriteLine(message);
        return Result.Success;
    }

    private static bool TryPlaceAt(
        RhinoDoc doc,
        EditorState state,
        IReadOnlyCollection<Guid> clearanceTargets,
        IReadOnlyCollection<Guid> engagementTargets,
        Plane plane,
        Guid placementObjectId,
        out FastenerComponentData saved,
        out string message)
    {
        var preset = PlacementPresetService.Current;
        var bindings = new List<HoleTargetBinding>();
        var headSeatTarget = clearanceTargets.Contains(placementObjectId)
            ? placementObjectId
            : engagementTargets.Contains(placementObjectId)
                ? placementObjectId
                : clearanceTargets.FirstOrDefault(engagementTargets.FirstOrDefault());
        bindings.AddRange(clearanceTargets.Select(id =>
            preset.CreateClearanceBinding(id, id == headSeatTarget)));
        bindings.AddRange(engagementTargets.Select(id =>
            preset.CreateEngagementBinding(id, id == headSeatTarget)));

        state.LoadedComponentId = Guid.Empty;
        var draft = state.CreateDraft(FastenerGeometryFactory.FromPlane(plane), bindings) with
        {
            PrintProfile = new PrintProfileSnapshot("当前 FDM 配置", preset.PrinterCorrection)
        };
        return FastenerComponentService.CreateOrReplace(doc, draft, out saved, out message);
    }

    private static List<Guid> SelectTargets(string prompt)
    {
        using var go = new GetObject();
        go.SetCommandPrompt($"{prompt}；单击一个对象立即完成，或切换“多选”");
        go.GeometryFilter = ObjectType.Brep | ObjectType.Extrusion;
        go.GroupSelect = false;
        go.SubObjectSelect = false;
        go.SetCustomGeometryFilter(IsHostGeometry);
        go.AcceptNothing(true);
        go.EnablePreSelect(false, true);
        var multiSelect = new OptionToggle(
            false,
            new LocalizeStringPair("Single", "单选"),
            new LocalizeStringPair("Multiple", "多选"));
        go.AddOptionToggle(new LocalizeStringPair("MultiSelect", "多选"), ref multiSelect);
        GetResult result;
        do
        {
            result = multiSelect.CurrentValue ? go.GetMultiple(1, 0) : go.Get();
        }
        while (result == GetResult.Option);
        var selected = result == GetResult.Object
            ? Enumerable.Range(0, go.ObjectCount).Select(i => go.Object(i).ObjectId).Distinct().ToList()
            : [];
        multiSelect.Dispose();
        return selected;
    }

    private static Result GetPlacementPlane(
        RhinoDoc doc,
        IReadOnlyCollection<Guid> targetIds,
        out Plane plane,
        out Guid placementObjectId,
        out bool continuousPointPlacement)
    {
        plane = Plane.WorldXY;
        placementObjectId = Guid.Empty;
        continuousPointPlacement = false;
        using var faceGetter = new GetObject();
        faceGetter.SetCommandPrompt("选择放置面，或选择“捕捉点”使用端点、中点、圆心等对象捕捉");
        faceGetter.GeometryFilter = ObjectType.Surface;
        faceGetter.SubObjectSelect = true;
        faceGetter.GroupSelect = false;
        faceGetter.SetCustomGeometryFilter(IsHostGeometry);
        var snapPointOption = faceGetter.AddOption(new LocalizeStringPair("SnapPoint", "捕捉点"));
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
        if (faceResult != GetResult.Option || faceGetter.OptionIndex() != snapPointOption)
            return Result.Cancel;

        continuousPointPlacement = true;
        var pointResult = RhinoGet.GetPoint("捕捉放置点（支持端点、中点、圆心、交点和节点）", false, out var origin);
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
        placementObjectId = FindClosestPlacementHost(doc, targetIds, origin, out var hostBrep);
        if (hostBrep?.IsSolid == true)
            OrientAxisIntoHost(hostBrep, origin, ref axis, doc.ModelAbsoluteTolerance);
        plane = new Plane(origin, axis);
        return Result.Success;
    }

    private static Guid FindClosestPlacementHost(
        RhinoDoc doc,
        IEnumerable<Guid> targetIds,
        Point3d point,
        out Brep? closestBrep)
    {
        closestBrep = null;
        var closestId = Guid.Empty;
        var closestDistance = double.PositiveInfinity;
        var maximumDistance = Math.Max(0.2, doc.ModelAbsoluteTolerance * 10);
        foreach (var targetId in targetIds)
        {
            var target = doc.Objects.FindId(targetId);
            var brep = target?.Geometry switch
            {
                Brep value => value,
                Extrusion extrusion => extrusion.ToBrep(),
                _ => null
            };
            if (brep is null)
                continue;
            var distance = point.DistanceTo(brep.ClosestPoint(point));
            if (distance > maximumDistance || distance >= closestDistance)
                continue;
            closestDistance = distance;
            closestId = targetId;
            closestBrep = brep;
        }
        return closestId;
    }

    private static void OrientAxisIntoHost(Brep host, Point3d origin, ref Vector3d axis, double tolerance)
    {
        var sampleDistance = Math.Max(0.2, tolerance * 10);
        var positiveInside = host.IsPointInside(origin + axis * sampleDistance, tolerance, true);
        var negativeInside = host.IsPointInside(origin - axis * sampleDistance, tolerance, true);
        if (!positiveInside && negativeInside)
            axis.Reverse();
    }

    private static bool IsHostGeometry(RhinoObject obj, GeometryBase geometry, ComponentIndex componentIndex) =>
        string.IsNullOrWhiteSpace(obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey));
}
