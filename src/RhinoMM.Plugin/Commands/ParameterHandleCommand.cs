using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.UI;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Geometry;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.Services;
using DrawingColor = System.Drawing.Color;

namespace RhinoMM.Plugin.Commands;

public sealed class ParametricFastenersEditHandlesCommand : Command
{
    public override string EnglishName => "ParametricFastenersEditHandles";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var selected = ComponentRepository.ReadSelectedControlPoints(doc);
        if (selected.Count != 1)
        {
            RhinoApp.WriteLine("参数手柄需要单选一个有效控制点。");
            return Result.Nothing;
        }

        ViewportQuickEditorService.Hide(false);
        var component = selected[0];
        var lengthSnap = true;
        var rotationStep = 15.0;
        while (ComponentRepository.TryReadComponent(doc, component.ComponentId, out component))
        {
            using var picker = new ParameterHandlePicker(component, lengthSnap, rotationStep);
            var picked = picker.Get();
            if (picked is GetResult.Cancel or GetResult.Nothing)
                return Result.Success;
            if (picked == GetResult.Option)
            {
                lengthSnap = picker.LengthSnap;
                rotationStep = picker.RotationStep;
                continue;
            }
            if (picked != GetResult.Point || picker.Hovered == ParameterHandleKind.None)
                continue;

            if (!TryEdit(
                    doc,
                    component,
                    picker.Hovered,
                    lengthSnap,
                    rotationStep,
                    out var draft))
                continue;
            if (!FastenerGeometryPreparationService.TryPrepare(doc, draft, out var prepared, out var reason))
            {
                RhinoApp.WriteLine($"参数手柄更新失败：{reason}");
                continue;
            }
            if (!ComponentUpdateCoordinator.TryApplyDrafts(doc, [prepared!.Draft], out var saved, out var message))
            {
                RhinoApp.WriteLine($"参数手柄更新失败：{message}");
                continue;
            }
            component = saved[0];
            FastenerTemplateLibraryService.RecordSuccessfulOperation(
                FastenerTemplateData.FromComponent(component),
                FastenerOperationKind.Update,
                out _);
            RhinoApp.WriteLine(message);
        }
        return Result.Success;
    }

    private static bool TryEdit(
        RhinoDoc doc,
        FastenerComponentData component,
        ParameterHandleKind handle,
        bool lengthSnap,
        double rotationStep,
        out FastenerComponentData draft)
    {
        draft = component;
        var plane = FastenerGeometryFactory.ToPlane(component.Placement);
        var current = HandlePoint(component, handle, plane);
        using var getter = new GetPoint();
        getter.SetCommandPrompt(handle switch
        {
            ParameterHandleKind.Length => "定位螺杆末端，单击或输入长度",
            ParameterHandleKind.Embed => "定位嵌入深度，单击或输入深度",
            ParameterHandleKind.NutProtrusion => "定位螺母外侧端面，单击或输入末端露出量",
            _ => "定位旋转方向，单击或输入角度"
        });
        getter.SetBasePoint(current, false);
        getter.AcceptNumber(true, false);
        getter.AcceptNothing(true);
        if (handle != ParameterHandleKind.Rotation)
        {
            var origin = plane.Origin - plane.ZAxis * 100000;
            getter.Constrain(new Line(origin, origin + plane.ZAxis * 200000));
        }

        FastenerComponentData? previewDraft = null;
        PreparedFastenerGeometry? preview = null;
        string previewError = string.Empty;
        getter.DynamicDraw += (_, e) =>
        {
            var value = ValueFromPoint(component, handle, plane, e.CurrentPoint);
            previewDraft = ApplyValue(component, handle, value, plane, lengthSnap, rotationStep);
            if (FastenerGeometryPreparationService.TryPrepare(doc, previewDraft, out var result, out var error))
            {
                preview = result;
                previewError = string.Empty;
                DrawPrepared(e, result!);
            }
            else
            {
                preview = null;
                previewError = error;
            }
            e.Display.DrawPoint(e.CurrentPoint, PointStyle.ActivePoint, 7,
                preview is null ? DrawingColor.IndianRed : DrawingColor.DodgerBlue);
        };

        var result = getter.Get();
        if (result == GetResult.Cancel)
            return false;
        double value;
        if (result == GetResult.Number)
            value = getter.Number();
        else if (result == GetResult.Point)
            value = ValueFromPoint(component, handle, plane, getter.Point());
        else if (result == GetResult.Nothing && previewDraft is not null)
        {
            draft = previewDraft;
            return preview is not null;
        }
        else
            return false;

        draft = ApplyValue(component, handle, value, plane, lengthSnap, rotationStep);
        if (!FastenerGeometryPreparationService.TryPrepare(doc, draft, out _, out var finalError))
        {
            RhinoApp.WriteLine($"参数手柄预检失败：{(string.IsNullOrWhiteSpace(finalError) ? previewError : finalError)}");
            return false;
        }
        return true;
    }

    private static FastenerComponentData ApplyValue(
        FastenerComponentData component,
        ParameterHandleKind handle,
        double value,
        Plane plane,
        bool lengthSnap,
        double rotationStep)
    {
        return handle switch
        {
            ParameterHandleKind.Length => component with
            {
                Length = lengthSnap ? SnapLength(Math.Max(0.01, value)) : Math.Max(0.01, value)
            },
            ParameterHandleKind.Embed => component with
            {
                HeadEmbedDepth = SnapEmbed(component, Math.Max(0, value))
            },
            ParameterHandleKind.NutProtrusion => component with
            {
                NutTipProtrusion = SnapProtrusion(Math.Max(0, value))
            },
            ParameterHandleKind.Rotation => component with
            {
                Placement = FastenerPlacementEditing.RotateAroundAxis(
                    component.Placement,
                    RhinoMath.ToRadians(SnapAngle(value, rotationStep)))
            },
            _ => component
        };
    }

    private static double ValueFromPoint(
        FastenerComponentData component,
        ParameterHandleKind handle,
        Plane plane,
        Point3d point)
    {
        if (handle == ParameterHandleKind.Rotation)
        {
            var vector = point - plane.Origin;
            return RhinoMath.ToDegrees(Math.Atan2(vector * plane.YAxis, vector * plane.XAxis));
        }
        var depth = (point - plane.Origin) * plane.ZAxis;
        return handle switch
        {
            ParameterHandleKind.Length => depth - component.HeadEmbedDepth,
            ParameterHandleKind.Embed => depth,
            ParameterHandleKind.NutProtrusion =>
                component.HeadEmbedDepth + component.Length - depth,
            _ => 0
        };
    }

    private static Point3d HandlePoint(
        FastenerComponentData component,
        ParameterHandleKind handle,
        Plane plane)
    {
        return handle switch
        {
            ParameterHandleKind.Length => plane.Origin
                + plane.ZAxis * (component.HeadEmbedDepth + component.Length),
            ParameterHandleKind.Embed => plane.Origin + plane.ZAxis * component.HeadEmbedDepth,
            ParameterHandleKind.NutProtrusion => plane.Origin
                + plane.ZAxis * (component.HeadEmbedDepth + component.Length - component.NutTipProtrusion),
            ParameterHandleKind.Rotation => plane.Origin
                + plane.XAxis * Math.Max(RhinoMMPlugIn.Catalog.Get(component.Size).NominalDiameter * 2, 4),
            _ => plane.Origin
        };
    }

    private static double SnapLength(double value)
    {
        double[] common = [8, 10, 12, 15, 20, 25, 30, 35, 40, 45, 50, 60];
        var nearest = common.OrderBy(item => Math.Abs(item - value)).First();
        return Math.Abs(nearest - value) <= 0.5 ? nearest : value;
    }

    private static double SnapEmbed(FastenerComponentData component, double value)
    {
        var spec = RhinoMMPlugIn.Catalog.Get(component.Size);
        var flush = component.Kind == FastenerKind.HexNut
            ? HexNutDimensions.Resolve(component, spec).TotalHeight
            : HeadGeometryCalculator.GetHeadHeight(component.Kind, spec);
        if (Math.Abs(value) <= 0.2)
            return 0;
        return Math.Abs(value - flush) <= 0.2 ? flush : value;
    }

    private static double SnapProtrusion(double value)
    {
        if (Math.Abs(value) <= 0.25)
            return 0;
        return Math.Abs(value - 2) <= 0.25 ? 2 : value;
    }

    private static double SnapAngle(double degrees, double step) => step <= 0
        ? degrees
        : Math.Round(degrees / step) * step;

    private static void DrawPrepared(GetPointDrawEventArgs e, PreparedFastenerGeometry prepared)
    {
        var fastenerColor = DrawingColor.FromArgb(54, 139, 220);
        var cutterColor = DrawingColor.FromArgb(226, 139, 48);
        using var fastener = new DisplayMaterial(fastenerColor, 0.45);
        using var cutter = new DisplayMaterial(cutterColor, 0.65);
        foreach (var brep in prepared.Proxies)
            e.Display.DrawBrepShaded(brep, fastener);
        foreach (var build in prepared.Cutters)
        {
            foreach (var brep in build.Shafts.Concat(build.Heads))
                e.Display.DrawBrepShaded(brep, cutter);
        }
    }
}

internal enum ParameterHandleKind
{
    None,
    Length,
    Embed,
    NutProtrusion,
    Rotation
}

internal sealed class ParameterHandlePicker : GetPoint
{
    private readonly FastenerComponentData _component;
    private readonly IReadOnlyList<(ParameterHandleKind Kind, Point3d Point, string Label)> _handles;

    private OptionToggle _lengthSnap;
    private OptionDouble _rotationStep;

    public ParameterHandlePicker(
        FastenerComponentData component,
        bool lengthSnap,
        double rotationStep)
    {
        _component = component;
        var plane = FastenerGeometryFactory.ToPlane(component.Placement);
        var list = new List<(ParameterHandleKind, Point3d, string)>();
        if (FastenerKindTraits.SupportsEmbedDepth(component.Kind))
        {
            list.Add((ParameterHandleKind.Embed,
                plane.Origin + plane.ZAxis * component.HeadEmbedDepth,
                component.Kind == FastenerKind.HexNut ? "嵌入" : "头部嵌入"));
        }
        if (FastenerKindTraits.UsesLengthInStatistics(component.Kind))
        {
            list.Add((ParameterHandleKind.Length,
                plane.Origin + plane.ZAxis * (component.HeadEmbedDepth + component.Length),
                "长度"));
        }
        if (component.AssemblyMode == ScrewAssemblyMode.NutFastened)
        {
            list.Add((ParameterHandleKind.NutProtrusion,
                plane.Origin + plane.ZAxis *
                (component.HeadEmbedDepth + component.Length - component.NutTipProtrusion),
                "露出"));
        }
        if (component.Kind == FastenerKind.HexNut
            || component.AssemblyMode == ScrewAssemblyMode.NutFastened
            || component.CounterboreBridgeEnabled)
        {
            var radius = Math.Max(RhinoMMPlugIn.Catalog.Get(component.Size).NominalDiameter * 2, 4);
            list.Add((ParameterHandleKind.Rotation, plane.Origin + plane.XAxis * radius, "旋转"));
        }
        _handles = list;
        SetCommandPrompt("单击参数手柄；Esc 退出");
        AcceptNothing(true);
        _lengthSnap = new OptionToggle(lengthSnap, "关闭", "开启");
        _rotationStep = new OptionDouble(Math.Max(0, rotationStep), 0, 180);
        AddOptionToggle(new LocalizeStringPair("LengthSnap", "长度吸附"), ref _lengthSnap);
        AddOptionDouble(new LocalizeStringPair("AngleStep", "角度步长"), ref _rotationStep, "°");
    }

    public ParameterHandleKind Hovered { get; private set; }
    public bool LengthSnap => _lengthSnap.CurrentValue;
    public double RotationStep => _rotationStep.CurrentValue;

    protected override void OnMouseMove(GetPointMouseEventArgs e)
    {
        base.OnMouseMove(e);
        var mouse = e.WindowPoint;
        Hovered = _handles
            .Select(handle => (handle.Kind, Distance: ScreenDistance(e.Viewport, handle.Point, mouse)))
            .Where(item => item.Distance <= 14)
            .OrderBy(item => item.Distance)
            .Select(item => item.Kind)
            .FirstOrDefault();
    }

    protected override void OnDynamicDraw(GetPointDrawEventArgs e)
    {
        base.OnDynamicDraw(e);
        var plane = FastenerGeometryFactory.ToPlane(_component.Placement);
        var end = plane.Origin + plane.ZAxis * (_component.HeadEmbedDepth + _component.Length);
        e.Display.DrawLine(plane.Origin, end, DrawingColor.FromArgb(90, 145, 205), 2);
        foreach (var handle in _handles)
        {
            var active = handle.Kind == Hovered;
            e.Display.DrawPoint(
                handle.Point,
                active ? PointStyle.ActivePoint : PointStyle.ControlPoint,
                active ? 9 : 7,
                active ? DrawingColor.DodgerBlue : DrawingColor.WhiteSmoke);
            e.Display.DrawDot(handle.Point, handle.Label,
                active ? DrawingColor.White : DrawingColor.Gainsboro,
                active ? DrawingColor.FromArgb(42, 105, 180) : DrawingColor.FromArgb(70, 75, 80));
        }
        if (_handles.Any(item => item.Kind == ParameterHandleKind.Rotation))
        {
            var radius = Math.Max(RhinoMMPlugIn.Catalog.Get(_component.Size).NominalDiameter * 2, 4);
            e.Display.DrawCircle(new Circle(plane, radius), DrawingColor.FromArgb(120, 170, 220), 1);
        }
    }

    private static double ScreenDistance(
        RhinoViewport viewport,
        Point3d point,
        System.Drawing.Point mouse)
    {
        var screen = viewport.WorldToClient(point);
        if (!screen.IsValid)
            return double.MaxValue;
        var dx = screen.X - mouse.X;
        var dy = screen.Y - mouse.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
