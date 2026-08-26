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
using RhinoMM.Plugin.UI;
using Eto.Forms;
using DrawingColor = System.Drawing.Color;

namespace RhinoMM.Plugin.Commands;

internal static class SmartPlacementCommand
{
    public static Result Execute(RhinoDoc doc, RunMode mode) => Execute(doc, mode, null);

    public static Result Execute(
        RhinoDoc doc,
        RunMode mode,
        FastenerTemplateData? operationTemplate)
    {
        SmartPlacementService placementService;
        try
        {
            placementService = operationTemplate is null
                ? new SmartPlacementService(doc, EditorState.Current)
                : new SmartPlacementService(doc, operationTemplate);
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"无法启动智能放置：{ex.Message}");
            return Result.Failure;
        }

        using var placementLifetime = placementService;
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
            if (preview?.IsValid != true || preview.Draft is null || preview.Prepared is null)
            {
                if (!getter.TryConfirmEngagementHost(preview, out preview)
                    || preview?.IsValid != true
                    || preview.Draft is null
                    || preview.Prepared is null)
                {
                    RhinoApp.WriteLine(preview?.Message ?? "当前位置无法生成紧固件。");
                    continue;
                }
            }

            if (!FastenerComponentService.CreateOrReplacePrepared(
                    doc,
                    preview.Prepared,
                    preview.DocumentRevision,
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
            ViewportOperationFeedback.Show(doc, [saved]);
            FastenerTemplateLibraryService.RecordSuccessfulOperation(
                FastenerTemplateData.FromComponent(saved),
                FastenerOperationKind.Placement,
                out _);
            RhinoApp.WriteLine(
                placed.Count == 1
                    ? message
                    : $"已连续放置 {placed.Count} 颗紧固件。");
        }

        if (placed.Count == 0)
            return Result.Cancel;

        ComponentEditorSession.Activate(
            doc,
            placed[^1],
            true,
            ComponentActivationIntent.SynchronizeOnly);
        RhinoApp.WriteLine(
            placed.Count == 1
                ? "智能放置完成：已生成 1 颗紧固件。"
                : $"智能放置完成：共生成 {placed.Count} 颗紧固件，可使用一次撤销恢复。");
        return Result.Success;
    }
}

internal sealed class SmartPlacementGetter : GetPoint, IDisposable
{
    private static readonly DrawingColor FastenerColor = DrawingColor.FromArgb(62, 126, 196);
    private static readonly DrawingColor InvalidColor = DrawingColor.FromArgb(220, 74, 70);
    private readonly SmartPlacementService _service;
    private readonly int _recognitionOption;
    private readonly int _classicOption;
    private RhinoViewport? _lastViewport;
    private System.Drawing.Point _lastWindowPoint;
    private Point3d _lastGetterPoint = Point3d.Unset;
    private OsnapModes _lastOsnapMode;
    private Guid _lastSnapObjectId;
    private long _lastDraftRevision = -1;
    private bool _hasLastInput;
    private string _lastReportedPreviewError = string.Empty;
    private readonly UITimer _previewTimer;
    private bool _previewPending;
    private bool _previewTimerScheduled;
    private System.Drawing.Point _lastEvaluatedWindowPoint;
    private OsnapModes _lastEvaluatedOsnapMode;
    private Guid _lastEvaluatedSnapObjectId;
    private long _lastEvaluatedDraftRevision = -1;

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
        _classicOption = AddOption(new LocalizeStringPair("Classic", "经典放置"));
        _previewTimer = new UITimer { Interval = 0.016 };
        _previewTimer.Elapsed += (_, _) => ConsumeLatestPreviewInput();
    }

    public SmartPlacementPreview? Preview { get; private set; }
    public SmartPlacementCursorPreview? CursorPreview { get; private set; }

    public SmartPlacementRecognitionMode RecognitionMode { get; private set; } =
        SmartPlacementRecognitionMode.Automatic;

    public bool HandleOption()
    {
        var optionIndex = OptionIndex();
        if (optionIndex == _classicOption)
            return true;
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

    void IDisposable.Dispose()
    {
        _previewTimer.Stop();
        _previewTimerScheduled = false;
        DisposePreviewGeometry(Preview);
        Preview = null;
        CursorPreview = null;
        base.Dispose();
    }

    public SmartPlacementPreview? RefreshForCommit()
    {
        _previewTimer.Stop();
        _previewTimerScheduled = false;
        _previewPending = false;
        if (!_hasLastInput || _lastViewport is null)
            return Preview;
        var cursor = CursorPreview;
        var exact = _service.EvaluateExact(
            _lastViewport,
            _lastWindowPoint,
            _lastGetterPoint,
            _lastOsnapMode,
            _lastSnapObjectId,
            RecognitionMode);
        if (cursor?.IsValid == true
            && exact.IsValid
            && !MatchesCursorConfirmation(cursor, exact))
        {
            ReplaceCursorPreview(_service.EvaluateCursor(
                _lastViewport,
                _lastWindowPoint,
                _lastGetterPoint,
                _lastOsnapMode,
                _lastSnapObjectId,
                RecognitionMode));
            var mismatch = SmartPlacementPreview.Invalid(
                exact.Anchor,
                "参数或宿主识别已变化，预览已刷新，请再次单击确认。",
                exact.ParameterSignature,
                exact.SnapLabel) with { AnchorSignature = exact.AnchorSignature };
            DisposePreviewGeometry(exact);
            return mismatch;
        }
        ReplacePreview(exact);
        if (!exact.IsValid && CursorPreview is { } currentCursor)
            ReplaceCursorPreview(currentCursor with { IsValid = false, Message = exact.Message });
        return Preview;
    }

    public bool TryConfirmEngagementHost(
        SmartPlacementPreview? failed,
        out SmartPlacementPreview? confirmed)
    {
        confirmed = failed;
        if (failed is null
            || failed.IsValid
            || RecognitionMode != SmartPlacementRecognitionMode.Automatic
            || !_hasLastInput
            || _lastViewport is null
            || !IsEngagementConfirmationFailure(failed.Message))
            return false;

        using var getter = new GetObject();
        getter.SetCommandPrompt("自动识别失败：点击后方封闭实体确认咬合体；Esc取消本次放置");
        getter.GeometryFilter = ObjectType.Brep | ObjectType.Extrusion;
        getter.SubObjectSelect = false;
        getter.EnablePreSelect(false, true);
        getter.DeselectAllBeforePostSelect = false;
        var result = getter.Get();
        if (result != GetResult.Object || getter.Object(0)?.Object() is not { } host)
            return false;
        if (!ComponentHostResolver.IsOrdinaryHost(host)
            || host.Geometry is Brep brep && !brep.IsSolid
            || host.Geometry is Extrusion extrusion && !extrusion.IsSolid)
        {
            confirmed = SmartPlacementPreview.Invalid(
                failed.Anchor,
                "确认对象必须是普通封闭实体。",
                failed.ParameterSignature,
                failed.SnapLabel);
            return false;
        }

        confirmed = _service.EvaluateExactWithConfirmedEngagement(
            _lastViewport,
            _lastWindowPoint,
            _lastGetterPoint,
            _lastOsnapMode,
            _lastSnapObjectId,
            RecognitionMode,
            host.Id);
        ReplacePreview(confirmed);
        if (confirmed.IsValid)
            RhinoApp.WriteLine($"已确认咬合宿主 {host.Id.ToString("N")[..8]}，该选择将随组件保存。");
        return confirmed.IsValid;
    }

    private static bool IsEngagementConfirmationFailure(string message) =>
        message.Contains("未检测到后方咬合宿主", StringComparison.Ordinal)
        || message.Contains("咬合宿主", StringComparison.Ordinal)
        || message.Contains("确认咬合体", StringComparison.Ordinal)
        || message.Contains("进入深度相同", StringComparison.Ordinal);

    protected override void OnMouseMove(GetPointMouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (InterruptMouseMove())
            return;
        CaptureLatestInput(e, schedule: true);
    }

    protected override void OnMouseDown(GetPointMouseEventArgs e)
    {
        CaptureLatestInput(e, schedule: false);
        _previewTimer.Stop();
        _previewTimerScheduled = false;
        base.OnMouseDown(e);
    }

    protected override void OnDynamicDraw(GetPointDrawEventArgs e)
    {
        base.OnDynamicDraw(e);
        if (_hasLastInput
            && _lastDraftRevision != SmartPlacementDraftChangeService.Revision)
            SchedulePreviewRefresh();
        var preview = CursorPreview;
        if (preview is null)
            return;

        if (!preview.IsValid)
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
        e.Display.PushModelTransform(preview.DisplayTransform);
        try
        {
            foreach (var proxy in preview.LocalProxies)
            {
                e.Display.DrawBrepShaded(proxy, fastenerMaterial);
                e.Display.DrawBrepWires(proxy, FastenerColor, 2);
            }
        }
        finally
        {
            e.Display.PopModelTransform();
        }
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

    private void CaptureLatestInput(GetPointMouseEventArgs e, bool schedule)
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
            if (schedule)
            {
                ClearPreparedPreview();
                SchedulePreviewRefresh();
            }
        }
        finally
        {
            snapReference?.Dispose();
        }
    }

    private void SchedulePreviewRefresh()
    {
        if (!_hasLastInput || _lastViewport is null)
            return;
        var revision = SmartPlacementDraftChangeService.Revision;
        var dx = _lastWindowPoint.X - _lastEvaluatedWindowPoint.X;
        var dy = _lastWindowPoint.Y - _lastEvaluatedWindowPoint.Y;
        var sameInput = _lastEvaluatedDraftRevision == revision
            && _lastEvaluatedOsnapMode == _lastOsnapMode
            && _lastEvaluatedSnapObjectId == _lastSnapObjectId
            && dx == 0
            && dy == 0;
        if (sameInput)
            return;
        _previewPending = true;
        if (!_previewTimerScheduled)
        {
            _previewTimerScheduled = true;
            _previewTimer.Start();
        }
    }

    private void ConsumeLatestPreviewInput()
    {
        _previewTimer.Stop();
        _previewTimerScheduled = false;
        if (!_previewPending || !_hasLastInput || _lastViewport is null)
            return;
        _previewPending = false;
        var preview = _service.EvaluateCursor(
            _lastViewport,
            _lastWindowPoint,
            _lastGetterPoint,
            _lastOsnapMode,
            _lastSnapObjectId,
            RecognitionMode);
        ReplaceCursorPreview(preview);
        ReportPreviewError(CursorPreview);
        _lastDraftRevision = SmartPlacementDraftChangeService.Revision;
        _lastEvaluatedDraftRevision = _lastDraftRevision;
        _lastEvaluatedWindowPoint = _lastWindowPoint;
        _lastEvaluatedOsnapMode = _lastOsnapMode;
        _lastEvaluatedSnapObjectId = _lastSnapObjectId;
        RhinoDoc.ActiveDoc?.Views.Redraw();
    }

    private void ReplacePreview(SmartPlacementPreview preview)
    {
        if (!ReferenceEquals(Preview, preview))
            DisposePreviewGeometry(Preview);
        Preview = preview;
    }

    private void ReplaceCursorPreview(SmartPlacementCursorPreview preview) => CursorPreview = preview;

    private void ClearPreparedPreview()
    {
        DisposePreviewGeometry(Preview);
        Preview = null;
    }

    private bool MatchesCursorConfirmation(
        SmartPlacementCursorPreview lightweight,
        SmartPlacementPreview exact)
    {
        var left = lightweight.AnchorSignature;
        var right = exact.AnchorSignature;
        var tolerance = Math.Max(RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0, 1e-6) * 2;
        var leftAxis = left.Axis;
        var rightAxis = right.Axis;
        if (!leftAxis.Unitize() || !rightAxis.Unitize())
            return false;
        return left.ParameterSignature == right.ParameterSignature
            && left.PlacementHostId == right.PlacementHostId
            && left.FaceIndex == right.FaceIndex
            && left.DocumentRevision == right.DocumentRevision
            && left.Anchor.DistanceTo(right.Anchor) <= tolerance
            && Vector3d.Multiply(leftAxis, rightAxis) >= 1 - 1e-7;
    }

    private static void DisposePreviewGeometry(SmartPlacementPreview? preview)
    {
        if (preview is null)
            return;
        foreach (var brep in preview.Proxies
                     .Concat(preview.Cutters.SelectMany(item => item.Shafts))
                     .Concat(preview.Cutters.SelectMany(item => item.Heads)))
            brep.Dispose();
    }

    private void ReportPreviewError(SmartPlacementCursorPreview? preview)
    {
        if (preview?.IsValid == true)
        {
            _lastReportedPreviewError = string.Empty;
            return;
        }
        var message = preview?.Message ?? string.Empty;
        if (string.IsNullOrWhiteSpace(message)
            || string.Equals(message, _lastReportedPreviewError, StringComparison.Ordinal))
            return;
        _lastReportedPreviewError = message;
        RhinoApp.WriteLine($"智能放置预览：{message}");
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

    private static string RecognitionLabel(SmartPlacementRecognitionMode mode) => mode switch
    {
        SmartPlacementRecognitionMode.AllClearance => "全部通孔",
        SmartPlacementRecognitionMode.AllEngagement => "全部咬合",
        _ => "自动"
    };
}
