using Rhino;
using Rhino.ApplicationSettings;
using Rhino.Commands;
using Rhino.Display;
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
using DrawingColor = System.Drawing.Color;

namespace RhinoMM.Plugin.Commands;

internal static class SmartPlacementCommand
{
    public static Result Execute(RhinoDoc doc, RunMode mode)
    {
        var state = EditorState.Current;
        SmartPlacementService placementService;
        try
        {
            placementService = new SmartPlacementService(doc, state);
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"无法启动智能放置：{ex.Message}");
            return Result.Failure;
        }

        using var getter = new SmartPlacementGetter(placementService);
        var placed = new List<FastenerComponentData>();
        while (true)
        {
            var result = getter.Get();
            if (result == GetResult.Option)
            {
                if (getter.HandleOption())
                {
                    RhinoApp.WriteLine("已切换到经典放置。");
                    return RhinoMMPlaceHoleCommand.ExecuteClassic(doc, mode);
                }
                continue;
            }

            if (result is GetResult.Nothing or GetResult.Cancel)
                break;
            if (result != GetResult.Point)
                continue;

            var preview = getter.RefreshForCommit();
            if (preview?.IsValid != true || preview.Draft is null)
            {
                RhinoApp.WriteLine(preview?.Message ?? "当前位置无法生成紧固件。");
                continue;
            }

            if (!FastenerComponentService.CreateOrReplace(
                    doc,
                    preview.Draft,
                    out var saved,
                    out var message))
            {
                RhinoApp.WriteLine($"该位置放置失败：{message}");
                continue;
            }

            if (!FastenerGeometryParameters.Match(preview.Draft, saved))
            {
                using var suppression = ComponentLifecycleService.Suppress(doc, saved.ComponentId);
                foreach (var obj in ComponentRepository.FindComponentObjects(doc, saved.ComponentId).ToList())
                    doc.Objects.Delete(obj, true);
                RhinoApp.WriteLine("放置失败：最终组件参数与点击时的预览参数不一致，已取消该组件。");
                continue;
            }

            placed.Add(saved);
            RhinoApp.WriteLine(
                placed.Count == 1
                    ? message
                    : $"已连续放置 {placed.Count} 颗紧固件。");
        }

        if (placed.Count == 0)
            return Result.Cancel;

        ComponentEditorSession.Activate(doc, placed[^1], true);
        RhinoApp.WriteLine(
            placed.Count == 1
                ? "智能放置完成：已生成 1 颗紧固件。"
                : $"智能放置完成：共生成 {placed.Count} 颗紧固件，可使用一次撤销恢复。");
        return Result.Success;
    }
}

internal sealed class SmartPlacementGetter : GetPoint
{
    private static readonly DrawingColor FastenerColor = DrawingColor.FromArgb(62, 126, 196);
    private static readonly DrawingColor ClearanceColor = DrawingColor.FromArgb(37, 156, 201);
    private static readonly DrawingColor EngagementColor = DrawingColor.FromArgb(232, 145, 48);
    private static readonly DrawingColor InstallationColor = DrawingColor.FromArgb(144, 103, 190);
    private static readonly DrawingColor InvalidColor = DrawingColor.FromArgb(220, 74, 70);
    private readonly SmartPlacementService _service;
    private readonly int _recognitionOption;
    private readonly int _flipOption;
    private readonly int _classicOption;
    private bool _flipDirection;
    private RhinoViewport? _lastViewport;
    private System.Drawing.Point _lastWindowPoint;
    private Point3d _lastGetterPoint = Point3d.Unset;
    private OsnapModes _lastOsnapMode;
    private Guid _lastSnapObjectId;
    private long _lastDraftRevision = -1;
    private bool _hasLastInput;

    public SmartPlacementGetter(SmartPlacementService service)
    {
        _service = service;
        SetCommandPrompt("移动鼠标预览紧固件，单击放置；Enter、右键或 Esc 完成");
        AcceptNothing(true);
        EnableObjectSnapCursors(true);
        EnableSnapToCurves(true);
        ConstrainToConstructionPlane(false);
        _recognitionOption = AddOptionList(
            new LocalizeStringPair("Recognition", "识别模式"),
            [
                new LocalizeStringPair("Automatic", "自动"),
                new LocalizeStringPair("AllClearance", "全部通孔"),
                new LocalizeStringPair("AllEngagement", "全部咬合")
            ],
            0);
        _flipOption = AddOption(new LocalizeStringPair("Flip", "翻转方向"));
        _classicOption = AddOption(new LocalizeStringPair("Classic", "经典放置"));
    }

    public SmartPlacementPreview? Preview { get; private set; }

    public SmartPlacementRecognitionMode RecognitionMode { get; private set; } =
        SmartPlacementRecognitionMode.Automatic;

    public bool HandleOption()
    {
        var optionIndex = OptionIndex();
        if (optionIndex == _classicOption)
            return true;
        if (optionIndex == _flipOption)
        {
            _flipDirection = !_flipDirection;
            RhinoApp.WriteLine(_flipDirection ? "已翻转智能放置方向。" : "已恢复面法线朝内方向。");
            return false;
        }
        if (optionIndex == _recognitionOption)
        {
            RecognitionMode = Option().CurrentListOptionIndex switch
            {
                1 => SmartPlacementRecognitionMode.AllClearance,
                2 => SmartPlacementRecognitionMode.AllEngagement,
                _ => SmartPlacementRecognitionMode.Automatic
            };
            RhinoApp.WriteLine($"识别模式：{RecognitionLabel(RecognitionMode)}");
        }
        return false;
    }

    public SmartPlacementPreview? RefreshForCommit()
    {
        RebuildPreviewFromLastInput();
        return Preview;
    }

    protected override void OnMouseMove(GetPointMouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (InterruptMouseMove())
            return;
        UpdatePreview(e);
    }

    protected override void OnMouseDown(GetPointMouseEventArgs e)
    {
        UpdatePreview(e);
        base.OnMouseDown(e);
    }

    protected override void OnDynamicDraw(GetPointDrawEventArgs e)
    {
        base.OnDynamicDraw(e);
        if (_hasLastInput
            && _lastDraftRevision != SmartPlacementDraftChangeService.Revision)
        {
            RebuildPreviewFromLastInput();
        }
        var preview = Preview;
        if (preview is null)
            return;

        if (!preview.IsValid || preview.Draft is null)
        {
            if (preview.Anchor.IsValid)
            {
                e.Display.DrawPoint(
                    preview.Anchor,
                    PointStyle.ActivePoint,
                    6,
                    InvalidColor);
            }
            DrawStatusHud(e, CompactError(preview.Message), false);
            return;
        }

        var displaySettings = GlobalDisplaySettingsService.Current;
        var fastenerMaterial = new DisplayMaterial(
            FastenerColor,
            1 - Math.Clamp(displaySettings.FastenerOpacityPercent, 0, 100) / 100.0);
        foreach (var proxy in preview.Proxies)
        {
            e.Display.DrawBrepShaded(proxy, fastenerMaterial);
            e.Display.DrawBrepWires(proxy, FastenerColor, 2);
        }

        foreach (var cutter in preview.Cutters)
        {
            var color = RoleColor(cutter.Binding.Role);
            var material = new DisplayMaterial(
                color,
                1 - Math.Clamp(displaySettings.CutterOpacityPercent, 0, 100) / 100.0);
            foreach (var shaft in cutter.Shafts)
            {
                e.Display.DrawBrepShaded(shaft, material);
                e.Display.DrawBrepWires(shaft, color, 1);
            }
            foreach (var head in cutter.Heads)
            {
                e.Display.DrawBrepShaded(head, material);
                e.Display.DrawBrepWires(head, color, 1);
            }
        }

        foreach (var role in preview.HostRoles)
        {
            var host = _service.FindHost(role.Key);
            if (host is not null)
                e.Display.DrawBrepWires(host.Brep, RoleColor(role.Value), 3);
        }

        var plane = FastenerGeometryFactory.ToPlane(preview.Draft.Placement);
        var arrowLength = Math.Max(
            preview.Draft.Length * 0.35,
            RhinoMMPlugIn.Catalog.Get(preview.Draft.Size).NominalDiameter * 2);
        e.Display.DrawArrow(
            new Line(plane.Origin, plane.Origin + plane.ZAxis * arrowLength),
            FastenerColor,
            0.18,
            0.08);
        e.Display.DrawPoint(
            preview.Anchor,
            PointStyle.ActivePoint,
            6,
            FastenerColor);
        var hudText = preview.SnapLabel == "自由面"
            ? preview.Message
            : $"{preview.SnapLabel} · {preview.Message}";
        DrawStatusHud(e, hudText, true);
    }

    private void UpdatePreview(GetPointMouseEventArgs e)
    {
        ObjRef? snapReference = null;
        try
        {
            snapReference = PointOnObject();
            _lastViewport = e.Viewport;
            _lastWindowPoint = e.WindowPoint;
            _lastGetterPoint = e.Point;
            _lastOsnapMode = OsnapEventType;
            _lastSnapObjectId = snapReference?.ObjectId ?? Guid.Empty;
            _hasLastInput = true;
            Preview = _service.Evaluate(
                _lastViewport,
                _lastWindowPoint,
                _lastGetterPoint,
                _lastOsnapMode,
                _lastSnapObjectId,
                RecognitionMode,
                _flipDirection);
            _lastDraftRevision = SmartPlacementDraftChangeService.Revision;
        }
        finally
        {
            snapReference?.Dispose();
        }
    }

    private void RebuildPreviewFromLastInput()
    {
        if (!_hasLastInput || _lastViewport is null)
            return;
        Preview = _service.Evaluate(
            _lastViewport,
            _lastWindowPoint,
            _lastGetterPoint,
            _lastOsnapMode,
            _lastSnapObjectId,
            RecognitionMode,
            _flipDirection);
        _lastDraftRevision = SmartPlacementDraftChangeService.Revision;
    }

    private static void DrawStatusHud(
        GetPointDrawEventArgs e,
        string text,
        bool valid)
    {
        var size = e.Viewport.Size;
        var x = Math.Max(1, size.Width) * 0.5f;
        var y = Math.Max(18, size.Height - 36);
        e.Display.DrawDot(
            x,
            y,
            text,
            valid ? DrawingColor.FromArgb(45, 88, 132) : InvalidColor,
            DrawingColor.White);
    }

    private static string CompactError(string message)
    {
        if (message.Contains("深度重合", StringComparison.Ordinal)
            || message.Contains("重叠", StringComparison.Ordinal))
            return "无法放置 · 宿主重合";
        if (message.Contains("捕捉点", StringComparison.Ordinal))
            return "无法放置 · 捕捉点不在有效面";
        if (message.Contains("鼠标下方", StringComparison.Ordinal)
            || message.Contains("没有检测到", StringComparison.Ordinal))
            return "无法放置 · 未命中封闭实体";
        if (message.Contains("方向", StringComparison.Ordinal)
            || message.Contains("轴线", StringComparison.Ordinal))
            return "无法放置 · 方向无效";
        return message.Length <= 28 ? message : $"{message[..27]}…";
    }

    private static DrawingColor RoleColor(ShaftFitRole role) => role switch
    {
        ShaftFitRole.Clearance => ClearanceColor,
        ShaftFitRole.ThreadEngagement => EngagementColor,
        ShaftFitRole.InstallationPocket => InstallationColor,
        _ => FastenerColor
    };

    private static string RecognitionLabel(SmartPlacementRecognitionMode mode) => mode switch
    {
        SmartPlacementRecognitionMode.AllClearance => "全部通孔",
        SmartPlacementRecognitionMode.AllEngagement => "全部咬合",
        _ => "自动"
    };
}
