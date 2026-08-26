using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.UI;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.UI;

internal sealed class OutputCenterDialog : Dialog
{
    private readonly RhinoDoc _doc;
    private readonly DropDown _scope = new();
    private readonly TextBox _project = new();
    private readonly TextBox _directory = new();
    private readonly CheckBox _rhino = new() { Text = "Rhino .3dm" };
    private readonly CheckBox _step = new() { Text = "STEP", Checked = true };
    private readonly CheckBox _stl = new() { Text = "STL" };
    private readonly CheckBox _excel = new() { Text = "Excel", Checked = true };
    private readonly CheckBox _csv = new() { Text = "CSV" };
    private readonly CheckBox _fasteners = new() { Text = "3DM / STEP 包含紧固件渲染实体" };
    private readonly CheckBox _warnings = new() { Text = "确认警告后继续输出", Visible = false };
    private readonly StackLayout _layerChecks = new() { Spacing = 2 };
    private readonly Panel _layerHost = new();
    private readonly Label _summary = FastenerUiTheme.PrimaryLabel("多格式原子交付");
    private readonly Label _hint = FastenerUiTheme.SecondaryLabel("全部格式使用同一布尔快照；失败或取消不会保留半套文件。");
    private readonly FastenerOperationStatusView _operation = new();
    private readonly Button _browse = new() { Text = "浏览…", Width = 72 };
    private readonly Button _export = new() { Text = "生成交付包", Width = 108 };
    private readonly Button _cancel = new() { Text = "关闭", Width = FastenerUiTheme.DialogButtonWidth };
    private FastenerUiOperationContext? _operationContext;

    private OutputCenterDialog(RhinoDoc doc)
    {
        _doc = doc;
        Title = "参数化紧固件输出中心";
        ClientSize = new Size(620, 500);
        MinimumSize = new Size(500, 390);
        Resizable = true;
        _summary.Font = new Font(SystemFont.Bold, FastenerUiTheme.TitleFontSize);
        _scope.Items.Add(new ListItem { Key = DeliveryScope.CurrentSelection.ToString(), Text = "当前选择" });
        _scope.Items.Add(new ListItem { Key = DeliveryScope.Layers.ToString(), Text = "指定图层" });
        _scope.Items.Add(new ListItem { Key = DeliveryScope.AllReferencedHosts.ToString(), Text = "全部有效宿主" });
        var saved = OutputCenterSettingsService.Current;
        _scope.SelectedKey = saved.Scope.ToString();
        _scope.SelectedIndexChanged += (_, _) => UpdateScope();
        _project.Text = string.IsNullOrWhiteSpace(doc.Path) ? "参数化紧固件" : Path.GetFileNameWithoutExtension(doc.Path);
        _directory.Text = Directory.Exists(saved.LastDirectory)
            ? saved.LastDirectory
            : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        _rhino.Checked = saved.Formats.HasFlag(DeliveryOutputFormat.Rhino3dm);
        _step.Checked = saved.Formats.HasFlag(DeliveryOutputFormat.Step);
        _stl.Checked = saved.Formats.HasFlag(DeliveryOutputFormat.Stl);
        _excel.Checked = saved.Formats.HasFlag(DeliveryOutputFormat.Excel);
        _csv.Checked = saved.Formats.HasFlag(DeliveryOutputFormat.Csv);
        _fasteners.Checked = saved.IncludeFasteners;
        if (FastenerTemplateLibraryService.ActiveScheme is { } scheme)
        {
            if (scheme.OutputFormats is { } schemeFormats)
            {
                var selected = (DeliveryOutputFormat)schemeFormats;
                _rhino.Checked = selected.HasFlag(DeliveryOutputFormat.Rhino3dm);
                _step.Checked = selected.HasFlag(DeliveryOutputFormat.Step);
                _stl.Checked = selected.HasFlag(DeliveryOutputFormat.Stl);
                _excel.Checked = selected.HasFlag(DeliveryOutputFormat.Excel);
                _csv.Checked = selected.HasFlag(DeliveryOutputFormat.Csv);
            }
            if (scheme.IncludeFastenerSolids.HasValue)
                _fasteners.Checked = scheme.IncludeFastenerSolids.Value;
        }
        foreach (var layer in doc.Layers.Where(layer => !layer.IsDeleted))
            _layerChecks.Items.Add(new CheckBox { Text = layer.FullPath, Tag = layer.Index, ToolTip = layer.FullPath });

        _browse.Click += (_, _) => Browse();
        _export.Click += (_, _) => Export();
        _cancel.Click += (_, _) => Close();
        _operation.CancelRequested += (_, _) => _operationContext?.Cancel();
        Closing += (_, e) =>
        {
            if (_operation.State is not (FastenerOperationState.Preparing or FastenerOperationState.Running))
                return;
            _operationContext?.Cancel();
            e.Cancel = true;
        };

        var destination = FastenerUiTheme.CreateCard(new DynamicLayout
        {
            Spacing = new Size(FastenerUiTheme.SpaceMedium, FastenerUiTheme.SpaceSmall),
            Rows =
            {
                new DynamicRow(FastenerUiTheme.SectionTitle("交付目标")),
                new DynamicRow(Field("项目名", _project)),
                new DynamicRow(Field("输出目录", new TableLayout
                {
                    Spacing = new Size(FastenerUiTheme.SpaceSmall, 0),
                    Rows = { new TableRow(new TableCell(_directory, true), _browse) }
                }))
            }
        });
        var range = FastenerUiTheme.CreateCard(new DynamicLayout
        {
            Spacing = new Size(0, FastenerUiTheme.SpaceSmall),
            Rows =
            {
                new DynamicRow(FastenerUiTheme.SectionTitle("输出范围")),
                new DynamicRow(_scope),
                new DynamicRow(_layerHost)
            }
        });
        var formats = FastenerUiTheme.CreateCard(new DynamicLayout
        {
            Spacing = new Size(0, FastenerUiTheme.SpaceSmall),
            Rows =
            {
                new DynamicRow(FastenerUiTheme.SectionTitle("交付格式")),
                new DynamicRow(_rhino, _step, _stl, _excel, _csv),
                new DynamicRow(_fasteners),
                new DynamicRow(_warnings)
            }
        });
        var header = FastenerUiTheme.CreateCard(new DynamicLayout
        {
            Spacing = new Size(0, 2),
            Rows = { new DynamicRow(_summary), new DynamicRow(_hint) }
        });
        var body = new DynamicLayout
        {
            Spacing = new Size(0, FastenerUiTheme.SpaceMedium),
            Rows = { new DynamicRow(destination), new DynamicRow(range), new DynamicRow(formats) }
        };
        Content = FastenerUiTheme.CreateWindowShell(
            header,
            new Scrollable { Content = body, ExpandContentWidth = true, Border = BorderType.None },
            _operation,
            FastenerUiTheme.CreateActionBar(_cancel, _export));
        FastenerUiTheme.ApplyPrimary(_export, true);
        FastenerUiTheme.ApplySecondary(_cancel);
        FastenerUiTheme.ApplySecondary(_browse);
        FastenerUiTheme.WatchWindow(this);
        DefaultButton = _export;
        AbortButton = _cancel;
        UpdateScope();
    }

    public static void Show(RhinoDoc doc)
    {
        using var dialog = new OutputCenterDialog(doc);
        dialog.ShowModal(RhinoEtoApp.MainWindow);
    }

    private void UpdateScope()
    {
        var visible = _scope.SelectedKey == DeliveryScope.Layers.ToString();
        _layerHost.Visible = visible;
        _layerHost.Content = visible
            ? new Scrollable { Content = _layerChecks, Height = 92, ExpandContentWidth = true, Border = BorderType.None }
            : null;
    }

    private void Browse()
    {
        var dialog = new SelectFolderDialog { Title = "选择交付包保存位置", Directory = _directory.Text };
        if (dialog.ShowDialog(this) == DialogResult.Ok)
            _directory.Text = dialog.Directory;
    }

    private void Export()
    {
        var formats = DeliveryOutputFormat.None;
        if (_rhino.Checked == true) formats |= DeliveryOutputFormat.Rhino3dm;
        if (_step.Checked == true) formats |= DeliveryOutputFormat.Step;
        if (_stl.Checked == true) formats |= DeliveryOutputFormat.Stl;
        if (_excel.Checked == true) formats |= DeliveryOutputFormat.Excel;
        if (_csv.Checked == true) formats |= DeliveryOutputFormat.Csv;
        var layers = _layerChecks.Items.OfType<CheckBox>().Where(item => item.Checked == true)
            .Select(item => (int)item.Tag!).ToArray();
        var request = new DeliveryOutputRequest(
            _directory.Text,
            _project.Text,
            Enum.TryParse<DeliveryScope>(_scope.SelectedKey, out var scope) ? scope : DeliveryScope.CurrentSelection,
            layers,
            formats,
            _fasteners.Checked == true,
            _warnings.Checked == true);
        OutputCenterSettingsService.Save(new OutputCenterSettings(
            request.Scope, request.Formats, request.IncludeFastenerSolids, request.ParentDirectory));
        SetBusy(true);
        _operation.Set(new FastenerOperationProgress(FastenerOperationState.Preparing, "准备交付快照"));
        _operationContext = new FastenerUiOperationContext(progress => _operation.Set(progress));
        var success = OutputCenterService.TryExport(
            _doc, request, out var result, out var inspection, out var message, _operationContext);
        SetBusy(false);
        if (!success)
        {
            var cancelled = _operationContext.IsCancellationRequested;
            _operation.Set(new FastenerOperationProgress(
                cancelled ? FastenerOperationState.Cancelled : FastenerOperationState.Failure,
                message));
            if (inspection.WarningCount > 0 && inspection.ErrorCount == 0)
                _warnings.Visible = true;
            RhinoApp.WriteLine(message);
            return;
        }
        _operation.Set(new FastenerOperationProgress(FastenerOperationState.Success, message));
        RhinoApp.WriteLine(message);
        Close();
    }

    private void SetBusy(bool busy)
    {
        foreach (var control in new Control[] { _scope, _project, _directory, _rhino, _step, _stl, _excel, _csv, _fasteners, _warnings, _browse })
            control.Enabled = !busy;
        _export.Enabled = !busy;
        _cancel.Enabled = !busy;
    }

    private static Control Field(string label, Control control) => new TableLayout
    {
        Spacing = new Size(FastenerUiTheme.SpaceMedium, 0),
        Rows = { new TableRow(new Label { Text = label, Width = 72, VerticalAlignment = VerticalAlignment.Center }, new TableCell(control, true)) }
    };
}
