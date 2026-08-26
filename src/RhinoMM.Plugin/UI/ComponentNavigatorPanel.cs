using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.UI;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Commands;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.UI;

[System.Runtime.InteropServices.Guid("5198F2BC-B0BB-46FA-9695-415280AEBEA5")]
public sealed class ComponentNavigatorPanel : Panel, IPanel
{
    private sealed record DisplayRow(IndexedComponentEntry Source, IReadOnlySet<ComponentHealthState> States)
    {
        public string Status => StatusLabel(States);
        public string Type => Source.Component is { } component
            ? FastenerLabels.ShortKind(component)
            : "数据损坏";
        public string Size => Source.Component?.Size ?? "—";
        public string Assembly => Source.Component is { Kind: FastenerKind.SocketCap or FastenerKind.Countersunk or FastenerKind.HexBolt } component
            ? FastenerLabels.AssemblyMode(component.AssemblyMode)
            : "安装槽";
        public string Hosts => Source.HostIds.Count.ToString();
        public string Id => Source.ShortId;
        public string Issue => States.Contains(ComponentHealthState.BooleanFailure)
            ? "实际布尔失败"
            : Source.IssueText;
    }

    private readonly TextBox _search = new() { PlaceholderText = "搜索类型、规格、宿主或短 ID" };
    private readonly DropDown _kind = new();
    private readonly DropDown _size = new();
    private readonly DropDown _assembly = new();
    private readonly DropDown _host = new();
    private readonly DropDown _layer = new();
    private readonly DropDown _status = new();
    private readonly Label _summary = new() { Text = "未建立索引", Font = new Font(SystemFont.Bold, 10) };
    private readonly Label _message = new() { Text = "筛选不会覆盖主面板模板。", Wrap = WrapMode.Word };
    private readonly GridView _grid = new() { AllowMultipleSelection = true };
    private readonly Button _select = new() { Text = "选择结果" };
    private readonly Button _update = new() { Text = "应用模板" };
    private readonly Button _statistics = new() { Text = "统计" };
    private readonly Button _export = new() { Text = "导出 ▾" };
    private readonly Button _deepCheck = new() { Text = "深度检查" };
    private readonly Button _assemblyCheck = new() { Text = "装配检查" };
    private readonly Button _deliveryInfo = new() { Text = "交付信息" };
    private readonly FastenerOperationStatusView _operation = new();
    private readonly Panel _filters = new();
    private readonly Panel _actions = new();
    private readonly UITimer _refreshTimer = new() { Interval = 0.35 };
    private readonly UITimer _deepTimer = new() { Interval = 0.01 };
    private readonly Dictionary<Guid, ComponentBooleanHealthResult> _deepResults = [];
    private IReadOnlyList<DisplayRow> _filtered = [];
    private Queue<IndexedComponentEntry>? _deepQueue;
    private uint _documentSerial;
    private long _indexRevision = -1;
    private bool _cancelDeepCheck;
    private int _deepTotal;

    public ComponentNavigatorPanel()
    {
        Style = Panels.EtoPanelStyleName;
        FastenerUiTheme.SetRole(this, FastenerThemeRole.Canvas);
        AddAll(_kind, "全部类型");
        AddAll(_size, "全部规格");
        AddAll(_assembly, "全部装配");
        AddAll(_host, "全部宿主");
        AddAll(_layer, "全部图层");
        AddAll(_status, "全部状态");
        foreach (var value in Enum.GetValues<FastenerKind>())
            _kind.Items.Add(new ListItem { Key = value.ToString(), Text = FastenerLabels.Kind(value) });
        foreach (var value in Enum.GetValues<ScrewAssemblyMode>())
            _assembly.Items.Add(new ListItem { Key = value.ToString(), Text = FastenerLabels.AssemblyMode(value) });
        foreach (var value in Enum.GetValues<ComponentHealthState>())
            _status.Items.Add(new ListItem { Key = value.ToString(), Text = StatusLabel(new HashSet<ComponentHealthState> { value }) });
        _kind.SelectedIndex = _size.SelectedIndex = _assembly.SelectedIndex =
            _host.SelectedIndex = _layer.SelectedIndex = _status.SelectedIndex = 0;

        AddGridColumn("状态", 62, row => row.Status);
        AddGridColumn("紧固件", 82, row => $"{row.Type} {row.Size}");
        AddGridColumn("装配", 68, row => row.Assembly);
        AddGridColumn("宿主", 34, row => row.Hosts);
        _grid.SelectionChanged += (_, _) => SelectSingleRow();
        _grid.MouseDoubleClick += (_, _) => ZoomSelectedRow();

        _search.TextChanged += (_, _) => ApplyFilters();
        foreach (var filter in new[] { _kind, _size, _assembly, _host, _layer, _status })
            filter.SelectedIndexChanged += (_, _) => ApplyFilters();
        _select.Click += (_, _) => SelectFiltered();
        _update.Click += (_, _) => ApplyCurrentTemplate();
        _statistics.Click += (_, _) => RunForFiltered("_-ParametricFastenersStatistics", controls: true);
        _export.Click += (_, _) => ShowExportMenu();
        _deepCheck.Click += (_, _) => ToggleDeepCheck();
        _assemblyCheck.Click += (_, _) => { if (RhinoDoc.ActiveDoc is { } doc) AssemblyInspectorDialog.Show(doc); };
        _deliveryInfo.Click += (_, _) => EditDeliveryInfo();
        _refreshTimer.Elapsed += (_, _) => PollDocument();
        _deepTimer.Elapsed += (_, _) => ProcessDeepCheck();
        _operation.CancelRequested += (_, _) => _cancelDeepCheck = true;
        RhinoApp.AppSettingsChanged += RhinoAppSettingsChanged;
        _refreshTimer.Start();
        foreach (var button in new[] { _select, _update, _statistics, _export, _deepCheck, _assemblyCheck, _deliveryInfo })
            FastenerUiTheme.ApplySecondary(button);
        var header = FastenerUiTheme.CreateCard(new DynamicLayout
        {
            Spacing = new Size(0, 2),
            Rows = { new DynamicRow(_summary), new DynamicRow(_search), new DynamicRow(_filters) }
        }, FastenerUiTheme.SpaceSmall);
        var detail = FastenerUiTheme.CreateCard(new DynamicLayout
        {
            Spacing = new Size(0, 2),
            Rows = { new DynamicRow(_message), new DynamicRow(_operation) }
        }, FastenerUiTheme.SpaceSmall);
        Content = new TableLayout
        {
            Padding = new Padding(6),
            Spacing = new Size(0, FastenerUiTheme.SpaceSmall),
            Rows =
            {
                header,
                new TableRow(_grid) { ScaleHeight = true },
                detail,
                _actions
            }
        };
        FastenerUiTheme.SetRole(Content, FastenerThemeRole.Canvas);
        SizeChanged += (_, _) => RebuildResponsiveChrome();
        RebuildResponsiveChrome();
        PollDocument();
    }

    private void RebuildResponsiveChrome()
    {
        var narrow = Width < 360;
        _filters.Content = null;
        _actions.Content = null;
        _filters.Content = narrow
            ? new DynamicLayout
            {
                Spacing = new Size(FastenerUiTheme.SpaceSmall, FastenerUiTheme.SpaceSmall),
                Rows =
                {
                    new DynamicRow(_kind, _size),
                    new DynamicRow(_assembly, _status),
                    new DynamicRow(_host, _layer)
                }
            }
            : new DynamicLayout
            {
                Spacing = new Size(FastenerUiTheme.SpaceSmall, FastenerUiTheme.SpaceSmall),
                Rows =
                {
                    new DynamicRow(_kind, _size, _status),
                    new DynamicRow(_assembly, _host, _layer)
                }
            };
        _actions.Content = new DynamicLayout
        {
            Spacing = new Size(2, 2),
            Rows =
            {
                new DynamicRow(_select, _update, _statistics, _export),
                new DynamicRow(_deepCheck, _assemblyCheck, _deliveryInfo)
            }
        };
        var width = Math.Max(Width - 24, 240);
        _grid.Columns[0].Width = 58;
        _grid.Columns[1].Width = Math.Max(82, (int)(width * 0.38));
        _grid.Columns[2].Width = Math.Max(62, (int)(width * 0.27));
        _grid.Columns[3].Width = Math.Max(38, width - _grid.Columns.Take(3).Sum(column => column.Width) - 8);
        FastenerUiTheme.ApplyTree(this);
    }

    public void PanelShown(uint documentSerialNumber, ShowPanelReason reason)
    {
        _refreshTimer.Start();
        PollDocument();
    }

    public void PanelHidden(uint documentSerialNumber, ShowPanelReason reason)
    {
        _refreshTimer.Stop();
        _deepTimer.Stop();
        _deepQueue = null;
    }

    public void PanelClosing(uint documentSerialNumber, bool onCloseDocument)
    {
        _refreshTimer.Stop();
        _deepTimer.Stop();
        _deepQueue = null;
        RhinoApp.AppSettingsChanged -= RhinoAppSettingsChanged;
    }

    private void RhinoAppSettingsChanged(object? sender, EventArgs e) =>
        Application.Instance.AsyncInvoke(() =>
        {
            FastenerUiTheme.RefreshPalette();
            FastenerUiTheme.ApplyTree(this);
            Invalidate();
        });

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _deepTimer.Stop();
            RhinoApp.AppSettingsChanged -= RhinoAppSettingsChanged;
        }
        base.Dispose(disposing);
    }

    private void PollDocument()
    {
        if (RhinoDoc.ActiveDoc is not { } doc)
        {
            _grid.DataStore = Array.Empty<DisplayRow>();
            _summary.Text = "没有活动文档";
            return;
        }
        var revision = FastenerDocumentIndexService.Revision(doc);
        if (_documentSerial == doc.RuntimeSerialNumber && _indexRevision == revision)
            return;
        _documentSerial = doc.RuntimeSerialNumber;
        _indexRevision = revision;
        _deepResults.Clear();
        RebuildFilterChoices(doc);
        ApplyFilters();
    }

    private void RebuildFilterChoices(RhinoDoc doc)
    {
        var entries = FastenerDocumentIndexService.Components(doc);
        Reset(_size, "全部规格", entries
            .Select(entry => entry.Component?.Size)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct()
            .OrderBy(value => value));
        Reset(_host, "全部宿主", entries.SelectMany(entry => entry.HostIds)
            .Distinct()
            .Select(id => new KeyValuePair<string, string>(id.ToString("D"), HostName(doc, id)))
            .OrderBy(item => item.Value));
        Reset(_layer, "全部图层", entries.SelectMany(entry => entry.HostLayerIndices)
            .Distinct()
            .Select(index => new KeyValuePair<string, string>(index.ToString(), doc.Layers.FindIndex(index)?.Name ?? $"图层 {index}"))
            .OrderBy(item => item.Value));
    }

    private void ApplyFilters()
    {
        if (RhinoDoc.ActiveDoc is not { } doc)
            return;
        var text = _search.Text?.Trim() ?? string.Empty;
        var rows = FastenerDocumentIndexService.Components(doc)
            .Select(entry =>
            {
                var states = new HashSet<ComponentHealthState>(entry.States);
                if (_deepResults.TryGetValue(entry.ComponentId, out var deep) && !deep.Success)
                    states.Add(ComponentHealthState.BooleanFailure);
                return new DisplayRow(entry, states);
            })
            .Where(row => string.IsNullOrWhiteSpace(text)
                || row.Source.SearchText.Contains(text, StringComparison.OrdinalIgnoreCase))
            .Where(row => MatchKind(row.Source.Component))
            .Where(row => MatchSize(row.Source.Component))
            .Where(row => MatchAssembly(row.Source.Component))
            .Where(row => MatchGuid(_host, row.Source.HostIds))
            .Where(row => MatchInt(_layer, row.Source.HostLayerIndices))
            .Where(row => _status.SelectedIndex <= 0
                || Enum.TryParse<ComponentHealthState>(_status.SelectedKey, out var state)
                && row.States.Contains(state))
            .OrderBy(row => row.States.Contains(ComponentHealthState.Healthy) ? 1 : 0)
            .ThenBy(row => row.Type)
            .ThenBy(row => row.Size)
            .ToArray();
        _filtered = rows;
        _grid.DataStore = rows;
        var total = FastenerDocumentIndexService.Components(doc).Count;
        var problems = rows.Count(row => !row.States.SetEquals([ComponentHealthState.Healthy]));
        var health = ComponentDocumentHealthService.Current(doc);
        _summary.Text = health is { HasProblems: true, IsStale: false }
            ? $"组件 {total} · 需重建 {health.NeedsRebuildCount} · 待重绑 {health.NeedsRelinkCount} · 损坏 {health.CorruptCount}"
            : $"组件 {total} · 当前 {rows.Length} · 问题 {problems}";
        _select.Enabled = rows.Length > 0;
        _update.Enabled = rows.Length > 0 && rows.All(row => row.Source.Component is not null
            && !row.States.Contains(ComponentHealthState.Corrupt)
            && !row.States.Contains(ComponentHealthState.NeedsRelink));
    }

    private void SelectSingleRow()
    {
        if (RhinoDoc.ActiveDoc is not { } doc || _grid.SelectedItem is not DisplayRow row)
            return;
        doc.Objects.UnselectAll(false);
        if (row.Source.ControlPointObjectId != Guid.Empty)
            doc.Objects.Select(row.Source.ControlPointObjectId, false);
        doc.Views.Redraw();
        _message.Text = string.IsNullOrWhiteSpace(row.Issue)
            ? $"{row.Type} {row.Size} · {row.Assembly} · {row.Id}"
            : $"{row.Id} · {row.Issue}";
    }

    private void ZoomSelectedRow()
    {
        if (RhinoDoc.ActiveDoc?.Views.ActiveView is not { } view
            || _grid.SelectedItem is not DisplayRow row)
            return;
        var radius = Math.Max(RhinoDoc.ActiveDoc.ModelAbsoluteTolerance * 100, 5);
        var vector = new Rhino.Geometry.Vector3d(radius, radius, radius);
        view.ActiveViewport.ZoomBoundingBox(new Rhino.Geometry.BoundingBox(
            row.Source.Location - vector,
            row.Source.Location + vector));
        RhinoDoc.ActiveDoc.Views.Redraw();
    }

    private void SelectFiltered()
    {
        if (RhinoDoc.ActiveDoc is not { } doc)
            return;
        doc.Objects.UnselectAll(false);
        foreach (var row in _filtered.Where(row => row.Source.ControlPointObjectId != Guid.Empty))
            doc.Objects.Select(row.Source.ControlPointObjectId, false);
        doc.Views.Redraw();
        _message.Text = $"已选择 {_filtered.Count(row => row.Source.ControlPointObjectId != Guid.Empty)} 个控制点；主面板模板保持不变。";
    }

    private void ApplyCurrentTemplate()
    {
        if (RhinoDoc.ActiveDoc is not { } doc)
            return;
        var components = _filtered
            .Select(row => row.Source.Component)
            .Where(component => component is not null)
            .Cast<FastenerComponentData>()
            .ToArray();
        if (!ComponentUpdateCoordinator.TryApplyTemplate(
                doc,
                components,
                EditorState.Current.CaptureUpdateTemplate(),
                out var saved,
                out var message))
        {
            _message.Text = message;
            RhinoApp.WriteLine(message);
            return;
        }
        FastenerDocumentIndexService.Invalidate(doc);
        _message.Text = $"已使用主面板模板更新 {saved.Count} 个组件。";
        RhinoApp.WriteLine(message);
    }

    private void EditDeliveryInfo()
    {
        if (RhinoDoc.ActiveDoc is not { } doc)
            return;
        var components = _filtered.Select(row => row.Source.Component)
            .Where(component => component is not null)
            .Cast<FastenerComponentData>()
            .ToArray();
        DeliveryMetadataDialog.Show(doc, components);
        FastenerDocumentIndexService.Invalidate(doc);
    }

    private void ShowExportMenu()
    {
        var menu = new ContextMenu();
        AddExportItem(menu, "放入 Rhino", "_-ParametricFastenersExportToRhino");
        AddExportItem(menu, "导出 STEP", "_-ParametricFastenersExportStep");
        AddExportItem(menu, "导出 STL", "_-ParametricFastenersExportStl");
        menu.Show(_export);
    }

    private void AddExportItem(ContextMenu menu, string text, string command)
    {
        var item = new ButtonMenuItem { Text = text };
        item.Click += (_, _) => RunForFiltered(command, controls: false);
        menu.Items.Add(item);
    }

    private void RunForFiltered(string command, bool controls)
    {
        if (RhinoDoc.ActiveDoc is not { } doc)
            return;
        doc.Objects.UnselectAll(false);
        var ids = controls
            ? _filtered.Select(row => row.Source.ControlPointObjectId)
            : _filtered.SelectMany(row => row.Source.HostIds);
        foreach (var id in ids.Where(id => id != Guid.Empty).Distinct())
            doc.Objects.Select(id, false);
        doc.Views.Redraw();
        RhinoApp.RunScript(command, false);
    }

    private void ToggleDeepCheck()
    {
        if (_deepQueue is not null)
        {
            _cancelDeepCheck = true;
            return;
        }
        var entries = _filtered
            .Select(row => row.Source)
            .Where(entry => entry.Component is not null)
            .ToArray();
        if (entries.Length == 0)
        {
            _message.Text = "当前筛选范围没有可检查的有效组件。";
            return;
        }
        _cancelDeepCheck = false;
        _deepQueue = new Queue<IndexedComponentEntry>(entries);
        _deepTotal = entries.Length;
        _deepCheck.Text = "取消深度检查";
        _message.Text = $"正在检查 {entries.Length} 个组件…";
        _operation.Set(new FastenerOperationProgress(FastenerOperationState.Running, "深度检查", 0, _deepTotal));
        _deepTimer.Start();
    }

    private void ProcessDeepCheck()
    {
        if (_deepQueue is null || RhinoDoc.ActiveDoc is not { } doc)
        {
            FinishDeepCheck("深度检查已停止。 ");
            return;
        }
        if (_cancelDeepCheck)
        {
            FinishDeepCheck("已取消深度检查；模型未修改。 ");
            return;
        }
        if (_deepQueue.Count == 0)
        {
            var failed = _deepResults.Values.Count(result => !result.Success);
            FinishDeepCheck($"深度检查完成：失败 {failed} 个。 ");
            ApplyFilters();
            return;
        }
        var entry = _deepQueue.Dequeue();
        var result = ComponentBooleanHealthService.Check(doc, entry.Component!);
        _deepResults[entry.ComponentId] = result;
        _message.Text = $"深度检查：剩余 {_deepQueue.Count} · 当前 {entry.ShortId} · {result.Message}";
        _operation.Set(new FastenerOperationProgress(
            FastenerOperationState.Running,
            "深度检查",
            _deepTotal - _deepQueue.Count,
            _deepTotal,
            entry.ShortId));
    }

    private void FinishDeepCheck(string message)
    {
        _deepTimer.Stop();
        _deepQueue = null;
        _deepCheck.Text = "深度检查";
        _message.Text = message;
        _operation.Set(new FastenerOperationProgress(
            message.Contains("取消", StringComparison.Ordinal) ? FastenerOperationState.Cancelled : FastenerOperationState.Success,
            message.Trim()));
    }

    private bool MatchKind(FastenerComponentData? component) =>
        _kind.SelectedIndex <= 0
        || component is not null && component.Kind.ToString() == _kind.SelectedKey;

    private bool MatchSize(FastenerComponentData? component) =>
        _size.SelectedIndex <= 0
        || component is not null && component.Size == _size.SelectedKey;

    private bool MatchAssembly(FastenerComponentData? component) =>
        _assembly.SelectedIndex <= 0
        || component is not null && component.AssemblyMode.ToString() == _assembly.SelectedKey;

    private static bool MatchGuid(DropDown filter, IReadOnlyList<Guid> values) =>
        filter.SelectedIndex <= 0
        || Guid.TryParse(filter.SelectedKey, out var id) && values.Contains(id);

    private static bool MatchInt(DropDown filter, IReadOnlyList<int> values) =>
        filter.SelectedIndex <= 0
        || int.TryParse(filter.SelectedKey, out var id) && values.Contains(id);

    private void AddGridColumn(string header, int width, Func<DisplayRow, string> getter) =>
        _grid.Columns.Add(new GridColumn
        {
            HeaderText = header,
            Width = width,
            DataCell = new TextBoxCell { Binding = Binding.Property<DisplayRow, string>(row => getter(row)) }
        });

    private static void AddAll(DropDown list, string text) =>
        list.Items.Add(new ListItem { Key = string.Empty, Text = text });

    private static void Reset(DropDown list, string allText, IEnumerable<string> values)
    {
        list.Items.Clear();
        AddAll(list, allText);
        foreach (var value in values)
            list.Items.Add(new ListItem { Key = value, Text = value });
        list.SelectedIndex = 0;
    }

    private static void Reset(DropDown list, string allText, IEnumerable<KeyValuePair<string, string>> values)
    {
        list.Items.Clear();
        AddAll(list, allText);
        foreach (var value in values)
            list.Items.Add(new ListItem { Key = value.Key, Text = value.Value });
        list.SelectedIndex = 0;
    }

    private static string HostName(RhinoDoc doc, Guid id)
    {
        var obj = doc.Objects.FindId(id);
        return obj is null
            ? $"丢失 {id.ToString("N")[..8]}"
            : string.IsNullOrWhiteSpace(obj.Attributes.Name)
                ? obj.Id.ToString("N")[..8]
                : obj.Attributes.Name;
    }

    private static string StatusLabel(IReadOnlySet<ComponentHealthState> states)
    {
        if (states.Contains(ComponentHealthState.Corrupt)) return "数据损坏";
        if (states.Contains(ComponentHealthState.BooleanFailure)) return "布尔失败";
        if (states.Contains(ComponentHealthState.NeedsRelink)) return "待重绑";
        if (states.Contains(ComponentHealthState.NeedsRebuild)) return "需重建";
        if (states.Contains(ComponentHealthState.InvalidParameters)) return "参数无效";
        if (states.Contains(ComponentHealthState.BooleanDisabled)) return "导出关闭";
        if (states.Contains(ComponentHealthState.PreviewHidden)) return "预览隐藏";
        if (states.Contains(ComponentHealthState.Warning)) return "提示";
        return "正常";
    }
}
