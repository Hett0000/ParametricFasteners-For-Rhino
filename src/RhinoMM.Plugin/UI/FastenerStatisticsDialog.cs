using Eto.Drawing;
using Eto.Forms;
using System.Linq.Expressions;
using Rhino;
using Rhino.UI;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.UI;

public sealed class FastenerStatisticsDialog : Dialog
{
    private const string SelectedScopeKey = "Selected";
    private const string AllScopeKey = "All";
    private static string _lastDirectory =
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    private readonly RhinoDoc _document;
    private readonly CardSelector _scopeSelector = new();
    private readonly Panel _metricsHost = new();
    private readonly Label _totalValue = MetricValue();
    private readonly Label _screwValue = MetricValue();
    private readonly Label _nutValue = MetricValue();
    private readonly Label _materialValue = MetricValue();
    private readonly Label _status = new()
    {
        TextColor = FastenerUiTheme.SecondaryText,
        Wrap = WrapMode.Word,
        Height = 38
    };
    private readonly GridView _grid = new()
    {
        AllowMultipleSelection = false,
        ShowHeader = true
    };
    private readonly Button _exportButton = new() { Text = "保存 Excel", Height = FastenerUiTheme.ControlHeight };
    private FastenerStatisticsSnapshot? _snapshot;
    private int? _metricColumnCount;

    public FastenerStatisticsDialog(RhinoDoc document, bool focusExport)
    {
        _document = document;
        Title = "紧固件统计";
        Size = new Size(620, 540);
        MinimumSize = new Size(300, 400);
        Padding = new Padding(0);
        Resizable = true;
        BackgroundColor = FastenerUiTheme.Canvas;

        _scopeSelector.Add(SelectedScopeKey, "当前选择（0）");
        _scopeSelector.Add(AllScopeKey, "全部组件（0）");
        _scopeSelector.SetColumns(2);
        _scopeSelector.SelectedKeyChanged += (_, _) => RefreshStatistics();

        _grid.Columns.Add(Column("类型", row => row.Type, 180));
        _grid.Columns.Add(Column("规格", row => row.Size, 90));
        _grid.Columns.Add(Column("长度 mm", row => row.Length, 100));
        _grid.Columns.Add(Column("数量", row => row.Quantity, 70));

        var refreshButton = new Button { Text = "刷新统计" };
        FastenerUiTheme.ApplySecondary(refreshButton);
        refreshButton.Click += (_, _) => RefreshStatistics();
        _exportButton.Click += (_, _) => ExportExcel();
        FastenerUiTheme.ApplyPrimary(_exportButton, true);
        var closeButton = new Button { Text = "关闭" };
        FastenerUiTheme.ApplySecondary(closeButton);
        closeButton.Click += (_, _) => Close();
        AbortButton = closeButton;
        DefaultButton = _exportButton;

        var scopeCard = FastenerUiTheme.CreateCard(new StackLayout
        {
            Orientation = Orientation.Vertical,
            Spacing = FastenerUiTheme.SpaceSmall,
            Items =
            {
                new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    Items =
                    {
                        FastenerUiTheme.SectionTitle("统计范围"),
                        new StackLayoutItem(null, true),
                        refreshButton
                    }
                },
                _scopeSelector
            }
        });
        var tableCard = FastenerUiTheme.CreateCard(new StackLayout
        {
            Orientation = Orientation.Vertical,
            Spacing = FastenerUiTheme.SpaceSmall,
            Items =
            {
                FastenerUiTheme.SectionTitle("规格汇总"),
                new StackLayoutItem(_grid, true)
            }
        });
        var actionRow = new TableLayout
        {
            Spacing = new Size(FastenerUiTheme.SpaceSmall, 0),
            Rows =
            {
                new TableRow(
                    new TableCell(closeButton, true),
                    new TableCell(_exportButton, true))
            }
        };
        var content = new TableLayout
        {
            BackgroundColor = FastenerUiTheme.Canvas,
            Padding = new Padding(FastenerUiTheme.SpaceMedium),
            Spacing = new Size(0, FastenerUiTheme.SpaceSmall),
            Rows =
            {
                new TableRow(scopeCard),
                new TableRow(_metricsHost),
                new TableRow(tableCard) { ScaleHeight = true },
                new TableRow(FastenerUiTheme.CreateCard(_status, FastenerUiTheme.SpaceSmall)),
                new TableRow(FastenerUiTheme.CreateCard(actionRow, FastenerUiTheme.SpaceSmall))
            }
        };
        Content = content;
        SizeChanged += (_, _) =>
        {
            RebuildMetrics();
            ResizeColumns();
        };
        Shown += (_, _) =>
        {
            RebuildMetrics(true);
            RefreshStatistics();
            if (focusExport)
                _exportButton.Focus();
        };
    }

    public static void Show(RhinoDoc document, bool focusExport)
    {
        using var dialog = new FastenerStatisticsDialog(document, focusExport);
        dialog.ShowModal(RhinoEtoApp.MainWindow);
    }

    private void RebuildMetrics(bool force = false)
    {
        var columns = ClientSize.Width >= 420 ? 4 : 2;
        if (!force && _metricColumnCount == columns)
            return;
        _metricColumnCount = columns;
        var cards = new[]
        {
            MetricCard("总数", _totalValue),
            MetricCard("螺丝", _screwValue),
            MetricCard("螺母", _nutValue),
            MetricCard("物料规格", _materialValue)
        };
        var layout = new TableLayout { Spacing = new Size(FastenerUiTheme.SpaceSmall, FastenerUiTheme.SpaceSmall) };
        for (var index = 0; index < cards.Length; index += columns)
        {
            var cells = cards.Skip(index).Take(columns).Select(card => new TableCell(card, true)).ToList();
            while (cells.Count < columns)
                cells.Add(new TableCell(new Panel(), true));
            layout.Rows.Add(new TableRow(cells));
        }
        _metricsHost.Content = layout;
    }

    private void RefreshStatistics()
    {
        var selectedSnapshot = FastenerStatisticsDocumentService.Build(
            _document,
            FastenerStatisticsScope.Selected);
        _scopeSelector.SetText(SelectedScopeKey, $"当前选择（{selectedSnapshot.SelectedCount}）");
        _scopeSelector.SetText(AllScopeKey, $"全部组件（{selectedSnapshot.AllCount}）");
        _scopeSelector.SetEnabled(SelectedScopeKey, selectedSnapshot.SelectedCount > 0);
        if (_scopeSelector.SelectedKey is null)
            _scopeSelector.Select(selectedSnapshot.SelectedCount > 0 ? SelectedScopeKey : AllScopeKey, false);
        if (selectedSnapshot.SelectedCount == 0 && _scopeSelector.SelectedKey == SelectedScopeKey)
            _scopeSelector.Select(AllScopeKey, false);

        var scope = _scopeSelector.SelectedKey == SelectedScopeKey
            ? FastenerStatisticsScope.Selected
            : FastenerStatisticsScope.All;
        _snapshot = scope == FastenerStatisticsScope.Selected
            ? selectedSnapshot
            : FastenerStatisticsDocumentService.Build(_document, FastenerStatisticsScope.All);
        var report = _snapshot.Report;
        _totalValue.Text = report.TotalCount.ToString();
        _screwValue.Text = report.ScrewCount.ToString();
        _nutValue.Text = report.NutCount.ToString();
        _materialValue.Text = report.MaterialCount.ToString();
        _grid.DataStore = report.SummaryRows.Select(row => new StatisticsRowView(
            FastenerLabels.Kind(row.Kind),
            row.Size,
            row.Length?.ToString("0.###") ?? "—",
            row.Quantity.ToString())).ToArray();
        _status.Text = _snapshot.IgnoredComponentCount > 0
            ? $"已忽略 {_snapshot.IgnoredComponentCount} 个缺少有效控制点或数据损坏的组件；请运行“刷新 / 清理”。"
            : report.TotalCount == 0 ? "当前范围没有可统计的紧固件。" : "统计已刷新。";
        _status.TextColor = _snapshot.IgnoredComponentCount > 0
            ? Color.FromArgb(184, 105, 0)
            : FastenerUiTheme.SecondaryText;
        ResizeColumns();
    }

    private void ExportExcel()
    {
        if (_snapshot is null)
            RefreshStatistics();
        var report = _snapshot!.Report;
        var documentName = DocumentName();
        var dialog = new Rhino.UI.SaveFileDialog
        {
            Title = "导出紧固件统计 Excel",
            DefaultExt = "xlsx",
            InitialDirectory = _lastDirectory,
            FileName = $"{SanitizeFileName(documentName)}_紧固件统计_{DateTime.Now:yyyyMMdd-HHmm}.xlsx",
            Filter = "Excel 工作簿 (*.xlsx)|*.xlsx"
        };
        if (!dialog.ShowSaveDialog())
            return;
        try
        {
            FastenerStatisticsWorkbookWriter.Write(
                dialog.FileName,
                documentName,
                DateTimeOffset.Now,
                report);
            _lastDirectory = Path.GetDirectoryName(dialog.FileName) ?? _lastDirectory;
            _status.Text = $"已导出 {report.TotalCount} 个紧固件：{dialog.FileName}";
            _status.TextColor = Color.FromArgb(36, 124, 68);
        }
        catch (Exception ex)
        {
            _status.Text = $"Excel 导出失败：{ex.Message}";
            _status.TextColor = Color.FromArgb(190, 45, 45);
        }
    }

    private string DocumentName() => string.IsNullOrWhiteSpace(_document.Path)
        ? "未命名"
        : Path.GetFileNameWithoutExtension(_document.Path);

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var result = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(result) ? "未命名" : result;
    }

    private void ResizeColumns()
    {
        if (_grid.Columns.Count != 4)
            return;
        var available = Math.Max(250, ClientSize.Width - 76);
        _grid.Columns[0].Width = (int)(available * 0.40);
        _grid.Columns[1].Width = (int)(available * 0.20);
        _grid.Columns[2].Width = (int)(available * 0.23);
        _grid.Columns[3].Width = Math.Max(48, available - _grid.Columns.Take(3).Sum(column => column.Width));
    }

    private static Panel MetricCard(string title, Label value) => FastenerUiTheme.CreateCard(new StackLayout
    {
        Orientation = Orientation.Vertical,
        Spacing = 2,
        Items = { FastenerUiTheme.SecondaryLabel(title), value }
    }, FastenerUiTheme.SpaceSmall);

    private static Label MetricValue() => new()
    {
        Text = "0",
        Font = new Font(SystemFont.Bold, 16),
        TextColor = FastenerUiTheme.PrimaryText
    };

    private static GridColumn Column(
        string header,
        Expression<Func<StatisticsRowView, string>> property,
        int width) => new()
    {
        HeaderText = header,
        DataCell = new TextBoxCell { Binding = Binding.Property(property) },
        Width = width,
        Resizable = true,
        Sortable = false
    };

    private sealed record StatisticsRowView(string Type, string Size, string Length, string Quantity);
}
