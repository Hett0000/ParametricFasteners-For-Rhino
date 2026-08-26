using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.UI;
using RhinoMM.Core.Domain;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.UI;

public sealed class GlobalDisplaySettingsDialog : Dialog
{
    private readonly RhinoDoc? _document;
    private readonly GlobalDisplaySettings _originalSettings;
    private readonly IReadOnlyList<FastenerComponentData> _originalComponents;
    private readonly int _ignoredComponents;
    private readonly Slider _fastenerOpacity = new() { MinValue = 0, MaxValue = 100 };
    private readonly Slider _cutterOpacity = new() { MinValue = 0, MaxValue = 100 };
    private readonly Label _fastenerValue = new() { Width = 42 };
    private readonly Label _cutterValue = new() { Width = 42 };
    private readonly Label _status = new() { Wrap = WrapMode.Word, Height = 38 };
    private readonly UITimer _previewTimer = new() { Interval = 0.08 };
    private readonly Button _confirm = new() { Text = "确定" };
    private readonly Button _cancel = new() { Text = "取消" };
    private uint _undoRecord;
    private bool _loading = true;
    private bool _previewApplied;
    private bool _accepted;
    private bool _restored;

    private GlobalDisplaySettingsDialog(RhinoDoc? document)
    {
        FastenerUiTheme.RefreshPalette();
        _document = document;
        _originalSettings = GlobalDisplaySettingsService.Current;
        _originalComponents = document is null
            ? []
            : ComponentRepository.ReadAllControlPoints(document, out _ignoredComponents);

        Title = "全局显示";
        Size = new Size(420, 270);
        MinimumSize = new Size(300, 250);
        Resizable = true;
        FastenerUiTheme.SetRole(this, FastenerThemeRole.Canvas);
        FastenerUiTheme.SetRole(_fastenerValue, FastenerThemeRole.PrimaryText);
        FastenerUiTheme.SetRole(_cutterValue, FastenerThemeRole.PrimaryText);
        FastenerUiTheme.SetRole(_status, FastenerThemeRole.SecondaryText);
        FastenerUiTheme.ApplyPrimary(_confirm, true);
        FastenerUiTheme.ApplySecondary(_cancel);

        _fastenerOpacity.Value = (int)Math.Round(_originalSettings.FastenerOpacityPercent);
        _cutterOpacity.Value = (int)Math.Round(_originalSettings.CutterOpacityPercent);
        UpdateLabels();
        _loading = false;

        _fastenerOpacity.ValueChanged += (_, _) => SchedulePreview();
        _cutterOpacity.ValueChanged += (_, _) => SchedulePreview();
        _previewTimer.Elapsed += (_, _) =>
        {
            _previewTimer.Stop();
            ApplyPreview();
        };
        _confirm.Click += (_, _) => Confirm();
        _cancel.Click += (_, _) => Close();
        DefaultButton = _confirm;
        AbortButton = _cancel;

        var description = FastenerUiTheme.Register(new Label
        {
            Text = "应用于当前文档全部参数化组件，并作为后续新建组件的默认值。",
            Wrap = WrapMode.Word
        }, FastenerThemeRole.SecondaryText);
        var controls = new StackLayout
        {
            Orientation = Orientation.Vertical,
            Spacing = FastenerUiTheme.SpaceMedium,
            Items =
            {
                Field("紧固件本体不透明度", _fastenerOpacity, _fastenerValue),
                Field("切割模块不透明度", _cutterOpacity, _cutterValue),
                description
            }
        };
        var actions = new TableLayout
        {
            Spacing = new Size(FastenerUiTheme.SpaceSmall, 0),
            Rows =
            {
                new TableRow(
                    new TableCell(_cancel, true),
                    new TableCell(_confirm, true))
            }
        };
        Content = new TableLayout
        {
            Padding = new Padding(FastenerUiTheme.SpaceMedium),
            Spacing = new Size(0, FastenerUiTheme.SpaceSmall),
            Rows =
            {
                new TableRow(FastenerUiTheme.CreateCard(controls)),
                new TableRow(FastenerUiTheme.CreateCard(_status, FastenerUiTheme.SpaceSmall)),
                new TableRow(FastenerUiTheme.CreateCard(actions, FastenerUiTheme.SpaceSmall))
            }
        };

        FastenerUiTheme.WatchWindow(this);
        Shown += (_, _) =>
        {
            if (_document is not null)
                _undoRecord = _document.BeginUndoRecord("参数化紧固件：全局显示");
            UpdateStatus("拖动滑块可实时预览；确定保存，取消恢复。");
        };
        Closed += (_, _) =>
        {
            _previewTimer.Stop();
            if (!_accepted)
                RestoreOriginal();
        };
    }

    public static void Show(RhinoDoc? document)
    {
        using var dialog = new GlobalDisplaySettingsDialog(document);
        dialog.ShowModal(RhinoEtoApp.MainWindow);
    }

    private void SchedulePreview()
    {
        UpdateLabels();
        if (_loading)
            return;
        _previewTimer.Stop();
        _previewTimer.Start();
        SmartPlacementDraftChangeService.NotifyChanged();
        _document?.Views.Redraw();
    }

    private bool ApplyPreview()
    {
        var settings = CurrentSettings();
        GlobalDisplaySettingsService.SetCurrent(settings);
        SmartPlacementDraftChangeService.NotifyChanged();
        if (_document is null || _originalComponents.Count == 0)
        {
            _document?.Views.Redraw();
            UpdateStatus(
                _ignoredComponents > 0
                    ? "文档没有有效控制点组件；残留对象请先运行“刷新 / 清理”。"
                    : "当前文档没有组件；新建组件将使用此显示设置。");
            return true;
        }

        var drafts = _originalComponents.Select(component => component with
        {
            FastenerOpacityPercent = settings.FastenerOpacityPercent,
            CutterOpacityPercent = settings.CutterOpacityPercent,
            UpdatedAt = DateTimeOffset.UtcNow
        }).ToArray();
        if (!ComponentPresentationService.ApplyDisplaySettings(
                _document,
                drafts,
                out var saved,
                out var message,
                manageUndoRecord: false))
        {
            UpdateStatus(message);
            return false;
        }

        _previewApplied = true;
        ComponentEditorSession.UpdateCachedComponents(_document, saved);
        UpdateStatus(
            _ignoredComponents > 0
                ? $"{message} 另有 {_ignoredComponents} 个残留组件未处理。"
                : message);
        return true;
    }

    private void Confirm()
    {
        _previewTimer.Stop();
        if (!ApplyPreview())
            return;
        if (RhinoMMPlugIn.Instance is null)
        {
            UpdateStatus("插件设置尚未初始化，无法保存全局显示默认值。");
            return;
        }
        if (!GlobalDisplaySettingsService.Save(
                RhinoMMPlugIn.Instance.Settings,
                CurrentSettings(),
                out var message))
        {
            UpdateStatus(message);
            return;
        }

        EndUndoRecord();
        _accepted = true;
        Close();
    }

    private void RestoreOriginal()
    {
        if (_restored)
            return;
        _restored = true;
        GlobalDisplaySettingsService.SetCurrent(_originalSettings);
        SmartPlacementDraftChangeService.NotifyChanged();
        if (_document is null)
            return;

        if (_previewApplied && _undoRecord != 0)
        {
            EndUndoRecord();
            _document.Undo();
            ComponentEditorSession.UpdateCachedComponents(_document, _originalComponents);
        }
        else
        {
            EndUndoRecord();
            if (_previewApplied)
            {
                ComponentPresentationService.ApplyDisplaySettings(
                    _document,
                    _originalComponents,
                    out var restored,
                    out _,
                    manageUndoRecord: false);
                ComponentEditorSession.UpdateCachedComponents(_document, restored);
            }
        }
        _document.Views.Redraw();
    }

    private void EndUndoRecord()
    {
        if (_document is null || _undoRecord == 0)
            return;
        _document.EndUndoRecord(_undoRecord);
        _undoRecord = 0;
    }

    private GlobalDisplaySettings CurrentSettings() => new(
        _fastenerOpacity.Value,
        _cutterOpacity.Value);

    private void UpdateLabels()
    {
        _fastenerValue.Text = $"{_fastenerOpacity.Value}%";
        _cutterValue.Text = $"{_cutterOpacity.Value}%";
    }

    private void UpdateStatus(string text)
    {
        _status.Text = text;
        _status.ToolTip = text;
    }

    private static Control Field(
        string label,
        Slider slider,
        Label value) => new StackLayout
    {
        Orientation = Orientation.Vertical,
        Spacing = FastenerUiTheme.SpaceSmall,
        Items =
        {
            FastenerUiTheme.Register(new Label { Text = label }, FastenerThemeRole.PrimaryText),
            new TableLayout
            {
                Spacing = new Size(FastenerUiTheme.SpaceSmall, 0),
                Rows =
                {
                    new TableRow(
                        new TableCell(slider, true),
                        new TableCell(value))
                }
            }
        }
    };
}
