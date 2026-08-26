using Eto.Drawing;
using Eto.Forms;
using Rhino;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Services;
using System.Linq.Expressions;

namespace RhinoMM.Plugin.UI;

internal sealed class BatchPlacementDialog : Dialog<bool>
{
    private sealed class LengthEditorState
    {
        public BatchPlacementPreflightItem? Row { get; set; }
        public bool Configuring { get; set; }
    }

    private const int DialogWidth = 720;
    private const int MinimumDialogWidth = 560;
    private const int MinimumDialogHeight = 300;
    private const int FooterHeight = 44;
    private const int ActionHeight = 30;

    private readonly RhinoDoc _doc;
    private readonly BatchPlacementPreflightResult _result;
    private readonly BatchPlacementPreviewConduit _conduit;
    private readonly CheckBox _skipFailures = new() { Text = "跳过失败点，仅创建有效项" };
    private readonly Button _create = new() { Width = 100, Height = ActionHeight };
    private readonly GridView _grid = new()
    {
        AllowMultipleSelection = false,
        Height = 132
    };
    private readonly Label _detail = FastenerUiTheme.SecondaryLabel("选择一行可在视口中定位；双击可缩放查看。");
    private readonly DropDown _filter = new() { Width = 92 };
    private readonly Button _adoptSuggested = new() { Text = "全部采用建议", Width = 104, Height = ActionHeight };
    private readonly Button _restoreTemplate = new() { Text = "恢复模板长度", Width = 104, Height = ActionHeight };
    private readonly Button _switchMode = new() { Width = 112, Height = ActionHeight, Visible = false };
    private readonly GridColumn _coordinateColumn;
    private readonly GridColumn _messageColumn;
    private readonly GridColumn _lengthColumn;
    private IReadOnlyList<BatchPlacementPreflightItem> _visibleItems = [];

    public BatchPlacementDialog(
        RhinoDoc doc,
        BatchPlacementPreflightResult result,
        BatchPlacementPreviewConduit conduit,
        string templateSummary)
    {
        FastenerUiTheme.RefreshPalette();
        _doc = doc;
        _result = result;
        _conduit = conduit;
        Title = "点集批量放置预检";
        Resizable = true;
        MinimumSize = new Size(MinimumDialogWidth, MinimumDialogHeight);
        ClientSize = new Size(DialogWidth, InitialHeight(result.Items.Count));
        Padding = new Padding(0);
        FastenerUiTheme.SetRole(this, FastenerThemeRole.Canvas);

        var title = FastenerUiTheme.Register(new Label
        {
            Text = $"{templateSummary} · {result.Items.Count} 个点位",
            Font = new Font(SystemFont.Bold, 10),
            Wrap = WrapMode.None,
            ToolTip = $"当前批量模板：{templateSummary}；共 {result.Items.Count} 个点位。"
        }, FastenerThemeRole.PrimaryText);
        var valid = StatusLabel($"有效 {result.ValidCount}", FastenerThemeRole.StatusSuccess);
        var warning = StatusLabel($"警告 {result.WarningCount}", FastenerThemeRole.StatusWarning);
        var failure = StatusLabel($"失败 {result.FailureCount}", FastenerThemeRole.StatusError);
        var supplemental = FastenerUiTheme.SecondaryLabel(
            (result.DuplicateCount > 0 ? $"合并重复 {result.DuplicateCount}" : string.Empty)
            + (result.DuplicateCount > 0 && result.UnsupportedCount > 0 ? " · " : string.Empty)
            + (result.UnsupportedCount > 0 ? $"忽略不支持来源 {result.UnsupportedCount}" : string.Empty));
        supplemental.Visible = result.DuplicateCount > 0 || result.UnsupportedCount > 0;
        _filter.Items.Add(new ListItem { Key = "All", Text = "全部状态" });
        _filter.Items.Add(new ListItem { Key = BatchPlacementStatus.Valid.ToString(), Text = "仅有效" });
        _filter.Items.Add(new ListItem { Key = BatchPlacementStatus.Warning.ToString(), Text = "仅警告" });
        _filter.Items.Add(new ListItem { Key = BatchPlacementStatus.Failure.ToString(), Text = "仅失败" });
        _filter.SelectedKey = "All";
        _filter.SelectedIndexChanged += (_, _) => ApplyFilter();

        var statusRow = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = FastenerUiTheme.SpaceLarge,
            VerticalContentAlignment = VerticalAlignment.Center,
            Items =
            {
                valid,
                warning,
                failure,
                supplemental,
                null,
                _filter
            }
        };
        var header = FastenerUiTheme.CreateCard(new StackLayout
        {
            Orientation = Orientation.Vertical,
            Spacing = FastenerUiTheme.SpaceSmall,
            Items = { title, statusRow }
        });

        _grid.Columns.Add(Column("#", 36, item => item.NumberText));
        _grid.Columns.Add(Column("来源", 60, item => item.Candidate.SourceLabel));
        _coordinateColumn = Column("坐标", 180, item => item.CoordinateText);
        _grid.Columns.Add(_coordinateColumn);
        _grid.Columns.Add(Column("状态", 64, item => item.StatusText));
        _messageColumn = Column("识别结果 / 原因", 260, item => item.Message);
        _grid.Columns.Add(_messageColumn);
        _lengthColumn = new GridColumn
        {
            HeaderText = "采用长度 mm",
            Width = 120,
            MinWidth = 112,
            Editable = true,
            DataCell = CreateLengthEditorCell()
        };
        _grid.Columns.Add(_lengthColumn);
        _visibleItems = result.Items;
        _grid.DataStore = _visibleItems;
        _grid.SelectionChanged += (_, _) => LocateSelection(false);
        _grid.MouseDoubleClick += (_, _) => LocateSelection(true);
        _skipFailures.CheckedChanged += (_, _) => UpdateCreateState();
        _adoptSuggested.Click += (_, _) => SetAllLengths(useSuggestion: true);
        _restoreTemplate.Click += (_, _) => SetAllLengths(useSuggestion: false);
        var suggestedModes = result.Items
            .Select(item => item.Suggestion)
            .Where(item => item is not null && !item.IsBlocked && item.ChangesAssemblyMode)
            .Select(item => item!.RecommendedMode)
            .Distinct()
            .ToArray();
        var allHaveSameAlternative = result.Items.Count > 0
            && result.Items.All(item => item.Suggestion is { IsBlocked: false, ChangesAssemblyMode: true })
            && suggestedModes.Length == 1;
        if (allHaveSameAlternative)
        {
            _switchMode.Visible = true;
            _adoptSuggested.Visible = false;
            _restoreTemplate.Visible = false;
            _switchMode.Text = $"整批改为{FastenerLabels.AssemblyMode(suggestedModes[0])}";
            _switchMode.ToolTip = "关闭当前预检并使用同一替代装配方式重新测量全部点位；不会立即写入模型。";
            _switchMode.Click += (_, _) =>
            {
                RequestedAssemblyMode = suggestedModes[0];
                Close(true);
            };
        }
        FastenerUiTheme.ApplySecondary(_adoptSuggested);
        FastenerUiTheme.ApplySecondary(_restoreTemplate);
        FastenerUiTheme.ApplySecondary(_switchMode);

        _detail.Wrap = WrapMode.None;
        _detail.ToolTip = _detail.Text;
        var detailCard = FastenerUiTheme.CreateCard(_detail, FastenerUiTheme.SpaceSmall);

        var cancel = new Button { Text = "取消", Width = 80, Height = ActionHeight };
        FastenerUiTheme.ApplySecondary(cancel);
        cancel.Height = ActionHeight;
        cancel.Click += (_, _) => Close(false);
        FastenerUiTheme.ApplyPrimary(_create, true);
        _create.Height = ActionHeight;
        _create.Click += (_, _) => Close(true);

        Control failureControl;
        if (result.FailureCount > 0)
        {
            failureControl = _skipFailures;
        }
        else
        {
            failureControl = FastenerUiTheme.SecondaryLabel(
                result.CreatableCount > 0 ? $"全部 {result.CreatableCount} 个点位均可创建" : "没有可创建的点位");
        }

        var footer = FastenerUiTheme.CreateCard(new TableLayout
        {
            Height = FooterHeight,
            Spacing = new Size(FastenerUiTheme.SpaceMedium, 0),
            Rows =
            {
                new TableRow(
                    new TableCell(failureControl, true),
                    new TableCell(_switchMode),
                    new TableCell(_restoreTemplate),
                    new TableCell(_adoptSuggested),
                    new TableCell(cancel),
                    new TableCell(_create))
            }
        }, FastenerUiTheme.SpaceSmall);

        var content = new TableLayout
        {
            Padding = new Padding(FastenerUiTheme.SpaceLarge),
            Spacing = new Size(0, FastenerUiTheme.SpaceMedium),
            Rows =
            {
                new TableRow(header),
                new TableRow(_grid) { ScaleHeight = true },
                new TableRow(detailCard),
                new TableRow(footer)
            }
        };
        FastenerUiTheme.SetRole(content, FastenerThemeRole.Canvas);
        Content = content;
        DefaultButton = _create;
        AbortButton = cancel;

        FastenerUiTheme.WatchWindow(this);
        SizeChanged += (_, _) => ResizeColumns();
        Shown += (_, _) =>
        {
            ResizeColumns();
        };
        UpdateCreateState();
    }

    public bool SkipFailures => _skipFailures.Checked == true;
    public ScrewAssemblyMode? RequestedAssemblyMode { get; private set; }

    protected override void OnClosed(EventArgs e)
    {
        _conduit.Enabled = false;
        _doc.Views.Redraw();
        base.OnClosed(e);
    }

    private static int InitialHeight(int itemCount) =>
        Math.Clamp(240 + (Math.Min(itemCount, 13) * 22), 320, 520);

    private static Label StatusLabel(string text, FastenerThemeRole role) =>
        FastenerUiTheme.Register(new Label
        {
            Text = text,
            Font = SystemFonts.Bold()
        }, role);

    private static GridColumn Column(
        string header,
        int width,
        Expression<Func<BatchPlacementPreflightItem, string>> value) => new()
    {
        HeaderText = header,
        Width = width,
        DataCell = new TextBoxCell
        {
            Binding = Binding.Property<BatchPlacementPreflightItem, string>(value)
        }
    };

    private void UpdateCreateState()
    {
        var creatable = _result.Items.Count(item => item.Status != BatchPlacementStatus.Failure
            && item.HasValidLength);
        var invalidLengths = _result.Items.Count(item => item.Status != BatchPlacementStatus.Failure
            && !item.HasValidLength);
        _create.Text = $"创建 {creatable} 项";
        _create.Enabled = _result.CreatableCount > 0
            && invalidLengths == 0
            && (_result.FailureCount == 0 || SkipFailures);
        _adoptSuggested.Enabled = _result.Items.Any(item => item.CanEditLength);
        _restoreTemplate.Enabled = _result.Items.Any(item => item.CanEditLength);
        _create.ToolTip = _create.Enabled
            ? $"重新验证后使用一次 Undo 创建 {creatable} 个组件"
            : _result.CreatableCount == 0
                ? "全部点位均预检失败，没有可创建的组件。"
                : invalidLengths > 0
                    ? $"有 {invalidLengths} 个采用长度无效，请输入 0–1000 mm 内的大于 0 数值。"
                : "存在失败点；勾选跳过失败点，或取消后修正点位。";
    }

    private void LocateSelection(bool zoom)
    {
        if (_grid.SelectedItem is not BatchPlacementPreflightItem selected)
            return;

        var point = selected.Candidate.Point;
        var fullDetail = $"#{selected.Candidate.Number} · {selected.Candidate.SourceLabel} · "
            + $"X {point.X:G17}, Y {point.Y:G17}, Z {point.Z:G17} · "
            + $"{selected.StatusText} · {selected.Message}"
            + (selected.CanEditLength
                ? $" · 当前 {selected.CurrentLength:0.##} · 建议 {selected.SuggestedLength:0.##} · 采用 {selected.AdoptedLengthText}"
                : string.Empty);
        _detail.Text = CompactLine(fullDetail, 62);
        _detail.ToolTip = fullDetail;
        _conduit.Update(_result.Items, selected.Candidate.Number);
        if (zoom && _doc.Views.ActiveView is { } view)
        {
            var radius = Math.Max(_doc.ModelAbsoluteTolerance * 100, 5);
            var box = new Rhino.Geometry.BoundingBox(
                selected.Candidate.Point - new Rhino.Geometry.Vector3d(radius, radius, radius),
                selected.Candidate.Point + new Rhino.Geometry.Vector3d(radius, radius, radius));
            view.ActiveViewport.ZoomBoundingBox(box);
        }
        _doc.Views.Redraw();
    }

    private void ResizeColumns()
    {
        const int gridChromeAndScrollbarGutter = 24;
        var available = ClientSize.Width
            - (FastenerUiTheme.SpaceLarge * 2)
            - gridChromeAndScrollbarGutter;
        if (available <= 0)
            return;

        const int fixedWidth = 36 + 60 + 64 + 120 + 8;
        var flexible = Math.Max(260, available - fixedWidth);
        var coordinate = Math.Clamp((int)(flexible * 0.38), 120, 210);
        var message = Math.Max(140, flexible - coordinate);
        _coordinateColumn.Width = coordinate;
        _messageColumn.Width = message;
        _lengthColumn.Width = 120;
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
            editor.SelectedKeyChanged += (_, _) => LengthEditorChanged(editor, true);
            editor.TextChanged += (_, _) => LengthEditorChanged(editor, false);
            return editor;
        },
        ConfigureCell = (args, control) =>
        {
            if (control is not ComboBox editor || editor.Tag is not LengthEditorState state)
                return;
            state.Configuring = true;
            state.Row = args.Item as BatchPlacementPreflightItem;
            var row = state.Row;
            editor.DataStore = row?.CanEditLength == true
                ? AssemblyLengthChoiceService.BuildOptions(row.CurrentLength, row.SuggestedLength)
                    .Select(choice =>
                    {
                        var labels = new List<string>();
                        if (choice.IsCurrent)
                            labels.Add("模板");
                        if (choice.IsSuggested)
                            labels.Add("建议");
                        var suffix = labels.Count == 0 ? string.Empty : $"（{string.Join("/", labels)}）";
                        return new ListItem
                        {
                            Key = AssemblyLengthChoiceService.Format(choice.Value),
                            Text = $"{AssemblyLengthChoiceService.Format(choice.Value)}{suffix}"
                        };
                    }).ToArray()
                : [];
            editor.Enabled = row?.CanEditLength == true && row.Status != BatchPlacementStatus.Failure;
            editor.SelectedIndex = -1;
            editor.Text = row?.CanEditLength == true ? row.AdoptedLengthText : "—";
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
        var value = preferSelectedKey && !string.IsNullOrWhiteSpace(editor.SelectedKey)
            ? editor.SelectedKey
            : editor.Text;
        row.SetAdoptedLength(value ?? string.Empty);
        FastenerUiTheme.SetRole(editor,
            row.HasValidLength ? FastenerThemeRole.PrimaryText : FastenerThemeRole.StatusError);
        UpdateCreateState();
        if (ReferenceEquals(_grid.SelectedItem, row))
            LocateSelection(false);
    }

    private void SetAllLengths(bool useSuggestion)
    {
        foreach (var item in _result.Items.Where(item => item.CanEditLength))
            item.SetAdoptedLength(AssemblyLengthChoiceService.Format(
                useSuggestion ? item.SuggestedLength : item.CurrentLength));
        _grid.ReloadData(Enumerable.Range(0, _visibleItems.Count));
        UpdateCreateState();
        LocateSelection(false);
    }

    private void ApplyFilter()
    {
        _visibleItems = Enum.TryParse<BatchPlacementStatus>(_filter.SelectedKey, out var status)
            ? _result.Items.Where(item => item.Status == status).ToArray()
            : _result.Items;
        _grid.DataStore = _visibleItems;
        _detail.Text = _visibleItems.Count == 0
            ? "当前筛选没有点位。"
            : $"当前显示 {_visibleItems.Count} / {_result.Items.Count} 个点位。";
        _conduit.Update(_result.Items);
        _doc.Views.Redraw();
    }

    private static string CompactLine(string text, int maxLength) =>
        text.Length <= maxLength ? text : $"{text[..Math.Max(1, maxLength - 1)]}…";
}
