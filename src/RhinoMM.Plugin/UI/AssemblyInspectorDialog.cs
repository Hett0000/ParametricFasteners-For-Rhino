using System.Linq.Expressions;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.UI;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.UI;

internal sealed class AssemblyInspectorDialog : Form
{
    private sealed class IssueRow
    {
        public IssueRow(AssemblyInspectionIssue issue)
        {
            Issue = issue;
            SelectedLengthText = issue.LengthSuggestion is { } suggestion
                ? AssemblyLengthChoiceService.Format(suggestion.CurrentLength)
                : string.Empty;
            LengthOptions = BuildLengthOptions(issue.LengthSuggestion);
            SelectedModeKey = "Keep";
            ModeOptions = BuildModeOptions(issue.SuggestedAssemblyMode);
        }

        public AssemblyInspectionIssue Issue { get; }
        public string Severity => Issue.Severity switch
        {
            AssemblyInspectionSeverity.Error => "错误",
            AssemblyInspectionSeverity.Warning => "警告",
            _ => "提示"
        };
        public string Component => Issue.ComponentId.ToString("N")[..8];
        public string Message => Issue.Message;
        public string Action => Issue.SuggestedAction;
        public string SelectedLengthText { get; private set; }
        public IReadOnlyList<ListItem> LengthOptions { get; }
        public string SelectedModeKey { get; private set; }
        public IReadOnlyList<ListItem> ModeOptions { get; }
        public bool CanEditLength => Issue.LengthSuggestion is not null;
        public bool CanEditMode => Issue.SuggestedAssemblyMode is not null;
        public bool HasValidLength => !CanEditLength || TrySelectedLength(out _);
        public bool HasLengthChange => CanEditLength
            && TrySelectedLength(out var value)
            && Math.Abs(value - Issue.LengthSuggestion!.CurrentLength) > 1e-6;
        public bool HasModeChange => CanEditMode
            && TrySelectedMode(out var mode)
            && mode == Issue.SuggestedAssemblyMode;

        public void SetSelectedLength(string text) => SelectedLengthText = text.Trim();
        public void SetSelectedMode(string? key) => SelectedModeKey = key ?? "Keep";

        public bool TrySelectedLength(out double value)
        {
            value = 0;
            if (!CanEditLength)
                return false;
            return AssemblyLengthChoiceService.TryParse(SelectedLengthText, out value);
        }

        public bool TrySelectedMode(out ScrewAssemblyMode mode)
        {
            mode = default;
            return CanEditMode
                && !string.Equals(SelectedModeKey, "Keep", StringComparison.Ordinal)
                && Enum.TryParse(SelectedModeKey, out mode);
        }

        private static IReadOnlyList<ListItem> BuildLengthOptions(FastenerLengthSuggestion? suggestion)
        {
            if (suggestion is null)
                return [];
            return AssemblyLengthChoiceService
                .BuildOptions(suggestion.CurrentLength, suggestion.SuggestedLength)
                .Select(choice =>
            {
                var labels = new List<string>();
                if (choice.IsCurrent)
                    labels.Add("当前");
                if (choice.IsSuggested)
                    labels.Add("建议");
                var suffix = labels.Count == 0 ? string.Empty : $"（{string.Join("/", labels)}）";
                return new ListItem
                {
                    Key = AssemblyLengthChoiceService.Format(choice.Value),
                    Text = $"{AssemblyLengthChoiceService.Format(choice.Value)}{suffix}"
                };
            }).ToArray();
        }

        private static IReadOnlyList<ListItem> BuildModeOptions(ScrewAssemblyMode? suggestedMode)
        {
            if (suggestedMode is null)
                return [];
            return
            [
                new ListItem { Key = "Keep", Text = "保留当前" },
                new ListItem
                {
                    Key = suggestedMode.Value.ToString(),
                    Text = $"采用{FastenerLabels.AssemblyMode(suggestedMode.Value)}"
                }
            ];
        }
    }

    private sealed class LengthEditorState
    {
        public IssueRow? Row { get; set; }
        public bool Configuring { get; set; }
    }

    private sealed class ModeEditorState
    {
        public IssueRow? Row { get; set; }
        public bool Configuring { get; set; }
    }

    private static readonly Dictionary<uint, WeakReference<AssemblyInspectorDialog>> OpenWindows = [];
    private readonly RhinoDoc _doc;
    private readonly Label _summary = FastenerUiTheme.PrimaryLabel("尚未检查");
    private readonly Label _freshness = FastenerUiTheme.SecondaryLabel("检查器只读取模型，不会自动修改组件。");
    private readonly NumericStepper _wall = Number(0.8);
    private readonly NumericStepper _spacing = Number(0.8);
    private readonly NumericStepper _radial = Number(2);
    private readonly NumericStepper _axial = Number(10);
    private readonly GridView _grid = new() { AllowMultipleSelection = true };
    private readonly Label _detail = FastenerUiTheme.SecondaryLabel();
    private readonly Button _locate = new() { Text = "定位", Width = FastenerUiTheme.DialogButtonWidth };
    private readonly Button _delete = new() { Text = "删除组件（0）", Width = 112 };
    private readonly Button _adopt = new() { Text = "应用建议（0）", Width = 124 };
    private readonly Button _refresh = new() { Text = "重新检查", Width = 96 };
    private readonly Button _close = new() { Text = "关闭", Width = FastenerUiTheme.DialogButtonWidth };
    private readonly FastenerOperationStatusView _operation = new();
    private readonly GridColumn _issueColumn;
    private readonly GridColumn _actionColumn;
    private readonly GridColumn _lengthColumn;
    private readonly GridColumn _modeColumn;
    private AssemblyInspectionReport _report = new([], 0, DateTimeOffset.Now);
    private IReadOnlyList<IssueRow> _rows = [];
    private FastenerUiOperationContext? _operationContext;
    private bool _busy;
    private bool _stale = true;

    private AssemblyInspectorDialog(RhinoDoc doc)
    {
        _doc = doc;
        Title = "装配检查器";
        ClientSize = new Size(760, 500);
        MinimumSize = new Size(560, 360);
        Resizable = true;
        ShowInTaskbar = false;
        _summary.Font = new Eto.Drawing.Font(SystemFont.Bold, FastenerUiTheme.TitleFontSize);
        AddColumn("级别", 56, row => row.Severity);
        AddColumn("组件", 70, row => row.Component);
        _issueColumn = AddColumn("问题", 260, row => row.Message);
        _actionColumn = AddColumn("建议", 210, row => row.Action);
        _lengthColumn = new GridColumn
        {
            HeaderText = "采用长度 mm",
            Width = 120,
            MinWidth = 112,
            Editable = true,
            DataCell = CreateLengthEditorCell()
        };
        _grid.Columns.Add(_lengthColumn);
        _modeColumn = new GridColumn
        {
            HeaderText = "替代装配",
            Width = 118,
            MinWidth = 108,
            Editable = true,
            DataCell = CreateModeEditorCell()
        };
        _grid.Columns.Add(_modeColumn);
        _grid.SelectionChanged += (_, _) => SelectionChanged();
        _grid.MouseDoubleClick += (_, _) => Locate();
        var selectMenu = new ButtonMenuItem { Text = "选中组件" };
        var locateMenu = new ButtonMenuItem { Text = "定位" };
        var deleteMenu = new ButtonMenuItem { Text = "删除组件" };
        selectMenu.Click += (_, _) => SelectionChanged();
        locateMenu.Click += (_, _) => Locate();
        deleteMenu.Click += (_, _) => DeleteSelectedComponents();
        _grid.ContextMenu = new ContextMenu(selectMenu, locateMenu, new SeparatorMenuItem(), deleteMenu);
        _locate.Click += (_, _) => Locate();
        _delete.Click += (_, _) => DeleteSelectedComponents();
        _adopt.Click += (_, _) => ApplySelectedLengths();
        _refresh.Click += (_, _) => RunInspection();
        _close.Click += (_, _) => Close();
        _operation.CancelRequested += (_, _) => _operationContext?.Cancel();
        Closing += (_, e) =>
        {
            if (_operation.State is not (FastenerOperationState.Preparing or FastenerOperationState.Running))
                return;
            _operationContext?.Cancel();
            e.Cancel = true;
        };

        var thresholdGrid = new DynamicLayout { Spacing = new Size(FastenerUiTheme.SpaceMedium, FastenerUiTheme.SpaceSmall) };
        thresholdGrid.AddRow(Field("最小壁厚", _wall), Field("孔间实体", _spacing));
        thresholdGrid.AddRow(Field("工具径向", _radial), Field("工具轴向", _axial));
        var header = FastenerUiTheme.CreateCard(new DynamicLayout
        {
            Spacing = new Size(0, FastenerUiTheme.SpaceSmall),
            Rows = { new DynamicRow(_summary), new DynamicRow(_freshness), new DynamicRow(thresholdGrid) }
        });
        var body = FastenerUiTheme.CreateCard(new TableLayout
        {
            Spacing = new Size(0, FastenerUiTheme.SpaceSmall),
            Rows = { new TableRow(_grid) { ScaleHeight = true }, new TableRow(_detail) }
        }, FastenerUiTheme.SpaceSmall);
        Content = FastenerUiTheme.CreateWindowShell(
            header,
            body,
            _operation,
            FastenerUiTheme.CreateActionBar(_locate, _delete, _adopt, _close, _refresh));
        FastenerUiTheme.ApplyPrimary(_refresh, true);
        FastenerUiTheme.ApplySecondary(_close);
        FastenerUiTheme.ApplySecondary(_locate);
        FastenerUiTheme.ApplySecondary(_delete);
        FastenerUiTheme.ApplySecondary(_adopt);
        FastenerUiTheme.WatchWindow(this);
        SizeChanged += (_, _) => ResizeColumns();
        Closed += (_, _) => Unsubscribe();
        Subscribe();
        RunInspection();
    }

    public static void Show(RhinoDoc doc)
    {
        if (OpenWindows.TryGetValue(doc.RuntimeSerialNumber, out var weak)
            && weak.TryGetTarget(out var existing))
        {
            existing.BringToFront();
            return;
        }
        var window = new AssemblyInspectorDialog(doc);
        OpenWindows[doc.RuntimeSerialNumber] = new WeakReference<AssemblyInspectorDialog>(window);
        window.Owner = RhinoEtoApp.MainWindow;
        window.Show();
    }

    private void RunInspection()
    {
        var profile = new AssemblyInspectionProfile(_wall.Value, _spacing.Value, _radial.Value, _axial.Value);
        if (!AssemblyInspectionSettingsService.Save(profile, out var error))
        {
            ShowMessage(error, FastenerOperationState.Failure);
            return;
        }
        SetBusy(true);
        _operation.Set(new FastenerOperationProgress(FastenerOperationState.Preparing, "准备装配检查"));
        _operationContext = new FastenerUiOperationContext(progress => _operation.Set(progress));
        var report = AssemblyInspectionService.Inspect(_doc, profile: profile, operation: _operationContext);
        if (_operationContext.IsCancellationRequested)
        {
            ShowMessage("装配检查已取消；未修改模型。", FastenerOperationState.Cancelled);
            SetBusy(false);
            return;
        }
        _report = report;
        _stale = false;
        _rows = _report.Issues.Select(issue => new IssueRow(issue)).ToArray();
        _grid.DataStore = _rows;
        _summary.Text = $"组件 {_report.ComponentCount} · 错误 {_report.ErrorCount} · 警告 {_report.WarningCount} · 提示 {_report.InfoCount}";
        _freshness.Text = $"检查完成于 {_report.InspectedAt:HH:mm:ss} · 模型变化后结果会自动标记过期";
        _detail.Text = _report.Issues.Count == 0 ? "未发现确定性装配问题。" : "选择问题查看完整原因和建议。";
        UpdateLengthApplyState();
        _locate.Enabled = false;
        _delete.Enabled = false;
        ShowMessage("装配检查完成。", _report.ErrorCount > 0
            ? FastenerOperationState.Warning
            : FastenerOperationState.Success);
        SetBusy(false);
    }

    private void SelectionChanged()
    {
        var selected = SelectedRows();
        var componentIds = selected.Select(row => row.Issue.ComponentId).Distinct().ToArray();
        _locate.Text = $"定位（{componentIds.Length}）";
        _delete.Text = $"删除组件（{componentIds.Length}）";
        _locate.Enabled = !_busy && !_stale && componentIds.Length > 0;
        _delete.Enabled = !_busy && !_stale && componentIds.Length > 0;
        UpdateLengthApplyState();
        var row = selected.FirstOrDefault();
        _detail.Text = row is null ? string.Empty : DetailText(row);
        _detail.ToolTip = _detail.Text;
        _doc.Objects.UnselectAll(false);
        foreach (var componentId in componentIds)
        {
            var control = ComponentRepository.FindControlPoint(_doc, componentId);
            if (control is not null)
                _doc.Objects.Select(control.Id, false);
        }
        _doc.Views.Redraw();
    }

    private void Locate()
    {
        var rows = SelectedRows();
        if (rows.Count == 0)
            return;
        var componentIds = rows.Select(row => row.Issue.ComponentId).Distinct().ToArray();
        _doc.Objects.UnselectAll(false);
        var box = BoundingBox.Empty;
        foreach (var componentId in componentIds)
        {
            var control = ComponentRepository.FindControlPoint(_doc, componentId);
            if (control is not null)
                _doc.Objects.Select(control.Id, false);
            foreach (var obj in ComponentRepository.FindComponentObjects(_doc, componentId))
                box.Union(obj.Geometry.GetBoundingBox(true));
        }
        if (!box.IsValid)
        {
            foreach (var row in rows)
                box.Union(new Point3d(row.Issue.LocationX, row.Issue.LocationY, row.Issue.LocationZ));
        }
        if (box.IsValid)
        {
            var amount = Math.Max(_doc.ModelAbsoluteTolerance * 100, Math.Max(2, box.Diagonal.Length * 0.15));
            box.Inflate(amount);
            _doc.Views.ActiveView?.ActiveViewport.ZoomBoundingBox(box);
        }
        _doc.Views.Redraw();
    }

    private IReadOnlyList<IssueRow> SelectedRows() =>
        _grid.SelectedItems.OfType<IssueRow>().ToArray();

    private void DeleteSelectedComponents()
    {
        if (_stale || _busy)
            return;
        var rows = SelectedRows();
        var componentIds = rows.Select(row => row.Issue.ComponentId).Distinct().ToArray();
        if (componentIds.Length == 0)
            return;
        var confirmation = MessageBox.Show(
            this,
            $"已选择 {rows.Count} 条问题，涉及 {componentIds.Length} 个组件。\n\n"
            + "将删除这些参数化组件及其控制点、代理体和切割模块，但不会删除宿主。是否继续？",
            "删除问题组件",
            MessageBoxButtons.YesNo,
            MessageBoxType.Warning);
        if (confirmation != DialogResult.Yes)
            return;
        SetBusy(true);
        if (!ComponentDeletionService.TryDeleteMany(
                _doc,
                componentIds,
                out _,
                out var message))
        {
            ShowMessage(message, FastenerOperationState.Failure);
            SetBusy(false);
            return;
        }
        RhinoMM.Plugin.Services.FastenerCommandText.WriteLine(message);
        SetBusy(false);
        RunInspection();
    }

    private void ApplySelectedLengths()
    {
        if (_stale)
        {
            ShowMessage("模型已变化，请重新检查后再采用建议。", FastenerOperationState.Warning);
            return;
        }
        var invalidRows = _rows.Where(row => row.CanEditLength && !row.HasValidLength).ToArray();
        if (invalidRows.Length > 0)
        {
            ShowMessage($"有 {invalidRows.Length} 个长度输入无效；请输入 0–1000 mm 范围内的大于 0 数值。", FastenerOperationState.Warning);
            return;
        }
        var pendingRows = _rows.Where(row => row.HasLengthChange || row.HasModeChange).ToArray();
        if (pendingRows.Length == 0)
            return;

        var drafts = new List<FastenerComponentData>();
        foreach (var group in pendingRows.GroupBy(row => row.Issue.ComponentId))
        {
            if (!ComponentRepository.TryReadComponent(_doc, group.Key, out var component)
                || ComponentRepository.FindControlPoint(_doc, group.Key) is null)
            {
                ShowMessage($"组件 {group.Key.ToString("N")[..8]} 已缺失或损坏，请重新检查。", FastenerOperationState.Failure);
                return;
            }
            if (ComponentHostResolver.NeedsRelink(component)
                || component.Bindings.Any(binding => binding.TargetObjectId == Guid.Empty
                    || _doc.Objects.FindId(binding.TargetObjectId) is null))
            {
                ShowMessage($"组件 {group.Key.ToString("N")[..8]} 需要重新绑定宿主，未应用任何长度。", FastenerOperationState.Failure);
                return;
            }
            var lengthRows = group.Where(row => row.HasLengthChange).ToArray();
            var values = lengthRows.Select(row =>
            {
                row.TrySelectedLength(out var value);
                return value;
            }).DistinctBy(value => Math.Round(value, 6)).ToArray();
            if (values.Length > 1)
            {
                ShowMessage($"组件 {group.Key.ToString("N")[..8]} 存在互相冲突的采用长度。", FastenerOperationState.Failure);
                return;
            }
            var modeValues = group.Where(row => row.HasModeChange).Select(row =>
            {
                row.TrySelectedMode(out var mode);
                return mode;
            }).Distinct().ToArray();
            if (modeValues.Length > 1)
            {
                ShowMessage($"组件 {group.Key.ToString("N")[..8]} 存在互相冲突的装配方式。", FastenerOperationState.Failure);
                return;
            }

            var targetLength = values.Length == 1 ? values[0] : component.Length;
            if (modeValues.Length == 0)
            {
                drafts.Add(component with { Length = targetLength, UpdatedAt = DateTimeOffset.UtcNow });
                continue;
            }

            var source = component with { AutoRecognizeHosts = true };
            var template = FastenerTemplateData.FromComponent(source) with
            {
                Length = targetLength,
                AssemblyMode = modeValues[0]
            };
            drafts.Add(template
                .ToUpdateTemplate(component.FastenerOpacityPercent, component.CutterOpacityPercent)
                .ApplyTo(source));
        }

        if (!ComponentUpdateCoordinator.TryApplyDrafts(_doc, drafts, out _, out var message))
        {
            ShowMessage(message, FastenerOperationState.Failure);
            return;
        }
        RhinoMM.Plugin.Services.FastenerCommandText.WriteLine(message);
        RunInspection();
    }

    private void MarkStale(RhinoDoc? changedDoc)
    {
        if (changedDoc?.RuntimeSerialNumber != _doc.RuntimeSerialNumber)
            return;
        if (_operationContext is not null && _operation.State == FastenerOperationState.Running)
            return;
        _stale = true;
        _adopt.Enabled = false;
        ReloadLengthEditors();
        _freshness.Text = "模型已变化 · 当前结果已过期，请重新检查";
        FastenerUiTheme.SetRole(_freshness, FastenerThemeRole.StatusWarning);
    }

    private void ObjectChanged(object? sender, RhinoObjectEventArgs args) =>
        MarkStale(sender as RhinoDoc ?? args.TheObject.Document);

    private void ObjectReplaced(object? sender, RhinoReplaceObjectEventArgs args) =>
        MarkStale(sender as RhinoDoc ?? args.OldRhinoObject?.Document ?? args.NewRhinoObject?.Document);

    private void DocumentClosed(object? sender, DocumentEventArgs args)
    {
        if (args.Document.RuntimeSerialNumber == _doc.RuntimeSerialNumber)
            Close();
    }

    private void Subscribe()
    {
        RhinoDoc.AddRhinoObject += ObjectChanged;
        RhinoDoc.DeleteRhinoObject += ObjectChanged;
        RhinoDoc.ReplaceRhinoObject += ObjectReplaced;
        RhinoDoc.UndeleteRhinoObject += ObjectChanged;
        RhinoDoc.CloseDocument += DocumentClosed;
    }

    private void Unsubscribe()
    {
        RhinoDoc.AddRhinoObject -= ObjectChanged;
        RhinoDoc.DeleteRhinoObject -= ObjectChanged;
        RhinoDoc.ReplaceRhinoObject -= ObjectReplaced;
        RhinoDoc.UndeleteRhinoObject -= ObjectChanged;
        RhinoDoc.CloseDocument -= DocumentClosed;
        OpenWindows.Remove(_doc.RuntimeSerialNumber);
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _refresh.Enabled = !busy;
        _close.Enabled = !busy;
        _wall.Enabled = _spacing.Enabled = _radial.Enabled = _axial.Enabled = !busy;
        if (busy)
            _adopt.Enabled = _locate.Enabled = _delete.Enabled = false;
        else
            UpdateLengthApplyState();
        ReloadLengthEditors();
    }

    private void ShowMessage(string message, FastenerOperationState state)
    {
        _operation.Set(new FastenerOperationProgress(state, message));
        RhinoMM.Plugin.Services.FastenerCommandText.WriteLine(message);
    }

    private void ResizeColumns()
    {
        const int fixedColumns = 56 + 70 + 120 + 118 + 38;
        var available = Math.Max(260, ClientSize.Width - fixedColumns);
        _issueColumn.Width = Math.Max(130, (int)(available * 0.56));
        _actionColumn.Width = Math.Max(120, available - _issueColumn.Width);
        _lengthColumn.Width = 120;
        _modeColumn.Width = 118;
    }

    private CustomCell CreateLengthEditorCell() => new()
    {
        CreateCell = _ =>
        {
            var editor = new ComboBox
            {
                ReadOnly = false,
                AutoComplete = true,
                ShowBorder = true,
                Height = 24,
                Tag = new LengthEditorState()
            };
            editor.SelectedKeyChanged += (_, _) => LengthEditorChanged(editor, preferSelectedKey: true);
            editor.TextChanged += (_, _) => LengthEditorChanged(editor, preferSelectedKey: false);
            return editor;
        },
        ConfigureCell = (args, control) =>
        {
            if (control is not ComboBox editor || editor.Tag is not LengthEditorState state)
                return;
            state.Configuring = true;
            state.Row = args.Item as IssueRow;
            var row = state.Row;
            editor.DataStore = row?.LengthOptions ?? [];
            editor.Enabled = !_stale && !_busy && row?.CanEditLength == true;
            editor.SelectedIndex = -1;
            editor.Text = row?.CanEditLength == true ? row.SelectedLengthText : "—";
            FastenerUiTheme.SetRole(editor,
                row is { CanEditLength: true, HasValidLength: false }
                    ? FastenerThemeRole.StatusError
                    : FastenerThemeRole.PrimaryText);
            state.Configuring = false;
        }
    };

    private void LengthEditorChanged(ComboBox editor, bool preferSelectedKey)
    {
        if (editor.Tag is not LengthEditorState { Configuring: false, Row: { } row })
            return;
        var text = preferSelectedKey && !string.IsNullOrWhiteSpace(editor.SelectedKey)
            ? editor.SelectedKey
            : editor.Text;
        row.SetSelectedLength(text ?? string.Empty);
        FastenerUiTheme.SetRole(editor,
            row.HasValidLength ? FastenerThemeRole.PrimaryText : FastenerThemeRole.StatusError);
        UpdateLengthApplyState();
        if (ReferenceEquals(_grid.SelectedItem, row))
        {
            _detail.Text = DetailText(row);
            _detail.ToolTip = _detail.Text;
        }
    }

    private CustomCell CreateModeEditorCell() => new()
    {
        CreateCell = _ =>
        {
            var editor = new ComboBox
            {
                ReadOnly = true,
                ShowBorder = true,
                Height = 24,
                Tag = new ModeEditorState()
            };
            editor.SelectedKeyChanged += (_, _) => ModeEditorChanged(editor);
            return editor;
        },
        ConfigureCell = (args, control) =>
        {
            if (control is not ComboBox editor || editor.Tag is not ModeEditorState state)
                return;
            state.Configuring = true;
            state.Row = args.Item as IssueRow;
            var row = state.Row;
            editor.DataStore = row?.ModeOptions ?? [];
            editor.Enabled = !_stale && !_busy && row?.CanEditMode == true;
            editor.SelectedKey = row?.CanEditMode == true ? row.SelectedModeKey : null;
            editor.Text = row?.CanEditMode == true ? editor.Text : "—";
            state.Configuring = false;
        }
    };

    private void ModeEditorChanged(ComboBox editor)
    {
        if (editor.Tag is not ModeEditorState { Configuring: false, Row: { } row })
            return;
        row.SetSelectedMode(editor.SelectedKey);
        UpdateLengthApplyState();
        if (ReferenceEquals(_grid.SelectedItem, row))
        {
            _detail.Text = DetailText(row);
            _detail.ToolTip = _detail.Text;
        }
    }

    private void UpdateLengthApplyState()
    {
        var invalid = _rows.Count(row => row.CanEditLength && !row.HasValidLength);
        var pending = _rows.Where(row => row.HasLengthChange || row.HasModeChange)
            .Select(row => row.Issue.ComponentId)
            .Distinct()
            .Count();
        _adopt.Text = $"应用建议（{pending}）";
        _adopt.Enabled = !_stale && !_busy && invalid == 0 && pending > 0
            && _operation.State is not (FastenerOperationState.Preparing or FastenerOperationState.Running);
        _adopt.ToolTip = invalid > 0
            ? $"有 {invalid} 个长度输入无效。"
            : pending > 0
                ? $"用一次 Undo 更新 {pending} 个组件。装配方式替代只在明确选择后应用。"
                : "修改采用长度，或明确选择替代装配方式后可批量应用。";
    }

    private string DetailText(IssueRow row)
    {
        var baseText = $"{row.Issue.Message}  {row.Issue.SuggestedAction}";
        if (row.Issue.LengthSuggestion is not { } suggestion)
        {
            if (row.Issue.SuggestedAssemblyMode is not { } suggestedMode)
                return baseText;
            var chosen = row.HasModeChange
                ? FastenerLabels.AssemblyMode(suggestedMode)
                : "保留当前";
            return $"{baseText}｜替代方式：{chosen}";
        }
        var selected = row.TrySelectedLength(out var value) ? $"{value:0.##}" : "无效";
        var difference = row.TrySelectedLength(out value)
            ? (value - suggestion.CurrentLength).ToString("+0.##;-0.##;+0")
            : "—";
        return $"当前 {suggestion.CurrentLength:0.##} mm｜建议 {suggestion.SuggestedLength:0.##} mm｜采用 {selected} mm｜差值 {difference} mm  {suggestion.Explanation}";
    }

    private void ReloadLengthEditors()
    {
        if (_rows.Count == 0)
            return;
        _grid.ReloadData(Enumerable.Range(0, _rows.Count));
    }

    private GridColumn AddColumn(string header, int width, Expression<Func<IssueRow, string>> value)
    {
        var column = new GridColumn
        {
            HeaderText = header,
            Width = width,
            DataCell = new TextBoxCell { Binding = Binding.Property<IssueRow, string>(value) }
        };
        _grid.Columns.Add(column);
        return column;
    }

    private static Control Field(string label, Control control) => new TableLayout
    {
        Spacing = new Size(FastenerUiTheme.SpaceSmall, 0),
        Rows = { new TableRow(FastenerUiTheme.SecondaryLabel(label), control, new TableCell(null, true)) }
    };

    private static NumericStepper Number(double value) => new()
    {
        Value = value,
        MinValue = 0,
        MaxValue = 1000,
        DecimalPlaces = 2,
        Increment = 0.1,
        Width = 76
    };
}
