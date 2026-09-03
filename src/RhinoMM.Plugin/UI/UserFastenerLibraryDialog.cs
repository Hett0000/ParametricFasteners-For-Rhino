using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.UI;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.UI;

internal sealed class UserFastenerLibraryDialog : Dialog
{
    private readonly RhinoDoc _doc;
    private readonly ListBox _list = new();
    private readonly TextBox _name = new();
    private readonly TextBox _source = new();
    private readonly TextBox _revision = new();
    private readonly TextArea _notes = new() { Height = 48 };
    private readonly NumericStepper _diameter = Number();
    private readonly NumericStepper _pitch = Number();
    private readonly NumericStepper _primaryWidth = Number();
    private readonly NumericStepper _primaryHeight = Number();
    private readonly Label _dimensionLabel = new() { Text = "主要尺寸" };
    private readonly Label _status = new() { Wrap = WrapMode.Word };
    private readonly Panel _responsiveBody = new();
    private readonly Button _clone = new() { Text = "从当前内置规格复制" };
    private readonly Button _save = new() { Text = "保存定义" };
    private readonly Button _delete = new() { Text = "删除" };
    private readonly Button _apply = new() { Text = "应用到面板" };
    private readonly Button _upgrade = new() { Text = "所选组件使用最新版" };
    private readonly Button _import = new() { Text = "导入…" };
    private readonly Button _exportJson = new() { Text = "导出 JSON" };
    private readonly Button _exportCsv = new() { Text = "导出 CSV" };
    private bool _wideLayout;
    private UserFastenerDefinition? _editing;

    private UserFastenerLibraryDialog(RhinoDoc doc)
    {
        _doc = doc;
        Title = "自定义标准件库 Beta";
        Size = new Size(780, 540);
        MinimumSize = new Size(420, 430);
        Resizable = true;
        _list.ItemTextBinding = Binding.Property<UserFastenerDefinition, string>(item => item.Name);
        _list.SelectedIndexChanged += (_, _) => LoadSelected();
        var close = new Button { Text = "关闭" };
        _clone.Click += (_, _) => CloneBuiltIn();
        _save.Click += (_, _) => Save();
        _delete.Click += (_, _) => Delete();
        _apply.Click += (_, _) => ApplyToPanel();
        _upgrade.Click += (_, _) => UpgradeSelected();
        _import.Click += (_, _) => ImportJson();
        _exportJson.Click += (_, _) => Export(false);
        _exportCsv.Click += (_, _) => Export(true);
        close.Click += (_, _) => Close();
        foreach (var button in new[] { _clone, _save, _delete, _apply, _upgrade, _import, _exportJson, _exportCsv, close })
            FastenerUiTheme.ApplySecondary(button);
        FastenerUiTheme.ApplyPrimary(_save, true);
        var summary = FastenerUiTheme.CreateCard(new DynamicLayout
        {
            Spacing = new Size(0, 2),
            Rows =
            {
                new DynamicRow(FastenerUiTheme.SectionTitle("自定义标准件库 Beta")),
                new DynamicRow(FastenerUiTheme.SecondaryLabel("从内置规格复制后编辑；组件保存完整尺寸快照，不依赖本机用户库。"))
            }
        });
        Content = FastenerUiTheme.CreateWindowShell(
            summary,
            _responsiveBody,
            _status,
            FastenerUiTheme.CreateActionBar(close));
        FastenerUiTheme.ApplySecondary(close);
        SizeChanged += (_, _) => RebuildResponsiveLayout();
        FastenerUiTheme.WatchWindow(this);
        AbortButton = close;
        RebuildResponsiveLayout(force: true);
        Reload();
    }

    private void RebuildResponsiveLayout(bool force = false)
    {
        var wide = ClientSize.Width >= 720;
        if (!force && wide == _wideLayout)
            return;
        _wideLayout = wide;
        _responsiveBody.Content = null;
        var listCard = FastenerUiTheme.CreateCard(new DynamicLayout
        {
            Spacing = new Size(0, FastenerUiTheme.SpaceSmall),
            Rows =
            {
                new DynamicRow(FastenerUiTheme.SectionTitle("定义")),
                new DynamicRow(_list),
                new DynamicRow(_clone),
                new DynamicRow(_import, _exportJson, _exportCsv)
            }
        });
        _list.Height = wide ? 280 : 108;
        var editor = new DynamicLayout { Spacing = new Size(FastenerUiTheme.SpaceSmall, FastenerUiTheme.SpaceSmall) };
        editor.AddRow(FastenerUiTheme.SectionTitle("尺寸与来源"));
        editor.AddRow("名称", _name);
        if (wide)
        {
            editor.AddRow("来源", _source, "修订", _revision);
            editor.AddRow("公称直径", _diameter, "螺距", _pitch);
            editor.AddRow(_dimensionLabel, _primaryWidth, "高度", _primaryHeight);
        }
        else
        {
            editor.AddRow("来源", _source);
            editor.AddRow("修订", _revision);
            editor.AddRow("公称直径", _diameter, "螺距", _pitch);
            editor.AddRow(_dimensionLabel, _primaryWidth, "高度", _primaryHeight);
        }
        editor.AddRow("备注", _notes);
        editor.AddRow(_delete, null, _apply, _upgrade, _save);
        var editorCard = FastenerUiTheme.CreateCard(editor);
        _responsiveBody.Content = wide
            ? new TableLayout
            {
                Spacing = new Size(FastenerUiTheme.SpaceMedium, 0),
                Rows = { new TableRow(new TableCell(listCard, true), new TableCell(editorCard, true)) { ScaleHeight = true } }
            }
            : new DynamicLayout
            {
                Spacing = new Size(0, FastenerUiTheme.SpaceMedium),
                Rows = { new DynamicRow(listCard), new DynamicRow(editorCard) }
            };
        FastenerUiTheme.ApplyTree(_responsiveBody);
    }

    public static void Show(RhinoDoc doc)
    {
        using var dialog = new UserFastenerLibraryDialog(doc);
        dialog.ShowModal(RhinoEtoApp.MainWindow);
    }

    private void Reload(Guid? select = null)
    {
        _list.DataStore = UserFastenerLibraryService.Current.Definitions;
        var values = UserFastenerLibraryService.Current.Definitions;
        if (values.Count == 0)
        {
            _editing = null;
            return;
        }
        _list.SelectedIndex = select.HasValue
            ? Math.Max(0, values.ToList().FindIndex(item => item.DefinitionId == select))
            : 0;
    }

    private void LoadSelected()
    {
        if (_list.SelectedValue is not UserFastenerDefinition item)
            return;
        _editing = item;
        _name.Text = item.Name;
        _source.Text = item.Source;
        _revision.Text = item.Revision;
        _notes.Text = item.Notes;
        _diameter.Value = item.SizeSpec.NominalDiameter;
        _pitch.Value = item.SizeSpec.CoarsePitch;
        var h = item.SizeSpec.Head;
        (_primaryWidth.Value, _primaryHeight.Value, _dimensionLabel.Text) = item.Kind switch
        {
            FastenerKind.SocketCap => (h.SocketDiameter, h.SocketHeight, "杯头直径"),
            FastenerKind.Countersunk => (h.CountersunkDiameter, h.SocketHeight, "沉头直径"),
            FastenerKind.HexBolt => (h.HexAcrossFlats, h.HexHeight, "六角对边"),
            FastenerKind.HexNut => (h.NutAcrossFlats, h.NutThickness, "螺母对边"),
            _ => (item.DefaultInsertOuterDiameter, item.DefaultLength, "热熔外径")
        };
    }

    private void CloneBuiltIn()
    {
        try
        {
            var state = EditorState.Current;
            _editing = UserFastenerLibraryService.CreateFromBuiltIn(state.Kind, state.Size);
            _list.SelectedIndex = -1;
            LoadDefinition(_editing);
            _status.Text = "已创建未保存副本；修改名称与尺寸后保存。";
        }
        catch (Exception ex) { _status.Text = ex.Message; }
    }

    private void LoadDefinition(UserFastenerDefinition item)
    {
        _name.Text = item.Name;
        _source.Text = item.Source;
        _revision.Text = item.Revision;
        _notes.Text = item.Notes;
        _diameter.Value = item.SizeSpec.NominalDiameter;
        _pitch.Value = item.SizeSpec.CoarsePitch;
        var h = item.SizeSpec.Head;
        _primaryWidth.Value = item.Kind == FastenerKind.HexNut ? h.NutAcrossFlats : h.SocketDiameter;
        _primaryHeight.Value = item.Kind == FastenerKind.HexNut ? h.NutThickness : h.SocketHeight;
    }

    private void Save()
    {
        if (_editing is null)
        {
            _status.Text = "请先从内置规格复制一个定义。";
            return;
        }
        var h = _editing.SizeSpec.Head;
        h = _editing.Kind switch
        {
            FastenerKind.SocketCap => h with { SocketDiameter = _primaryWidth.Value, SocketHeight = _primaryHeight.Value },
            FastenerKind.Countersunk => h with { CountersunkDiameter = _primaryWidth.Value, SocketHeight = _primaryHeight.Value },
            FastenerKind.HexBolt => h with { HexAcrossFlats = _primaryWidth.Value, HexHeight = _primaryHeight.Value },
            FastenerKind.HexNut => h with { NutAcrossFlats = _primaryWidth.Value, NutThickness = _primaryHeight.Value },
            _ => h
        };
        var item = _editing with
        {
            Name = _name.Text,
            Source = _source.Text,
            Revision = _revision.Text,
            Notes = _notes.Text,
            SizeSpec = _editing.SizeSpec with { NominalDiameter = _diameter.Value, CoarsePitch = _pitch.Value, Head = h },
            DefaultInsertOuterDiameter = _editing.Kind == FastenerKind.HeatSetInsert ? _primaryWidth.Value : _editing.DefaultInsertOuterDiameter,
            DefaultLength = _editing.Kind == FastenerKind.HeatSetInsert ? _primaryHeight.Value : _editing.DefaultLength
        };
        if (!UserFastenerLibraryService.Upsert(item, out var message))
        {
            _status.Text = message;
            return;
        }
        _editing = item;
        _status.Text = message;
        Reload(item.DefinitionId);
    }

    private void Delete()
    {
        if (_editing is null)
        {
            _status.Text = "没有选择定义。";
            return;
        }
        if (!UserFastenerLibraryService.Delete(_editing.DefinitionId, out var message))
        {
            _status.Text = message;
            return;
        }
        _status.Text = message;
        Reload();
    }

    private void ApplyToPanel()
    {
        if (_editing is null) return;
        UserFastenerLibraryService.ApplyToEditor(_editing);
        RhinoMMPanel.LoadExternalTemplate(
            FastenerTemplateData.FromUpdateTemplate(EditorState.Current.CaptureUpdateTemplate()),
            _editing.Name);
        _status.Text = "已载入主面板模板；不会自动修改场景组件。";
    }

    private void UpgradeSelected()
    {
        if (_editing is null) return;
        var components = ComponentRepository.ReadSelectedControlPoints(_doc)
            .Where(component => component.CustomDefinitionId == _editing.DefinitionId).ToArray();
        if (components.Length == 0)
        {
            _status.Text = "选中的组件没有使用当前自定义定义。";
            return;
        }
        var snapshot = _editing.Snapshot();
        var drafts = components.Select(component => component with
        {
            Size = _editing.ThreadDesignation,
            CustomDefinitionName = _editing.Name,
            CustomDefinitionSnapshot = snapshot,
            UpdatedAt = DateTimeOffset.UtcNow
        }).ToArray();
        if (!ComponentUpdateCoordinator.TryApplyDrafts(_doc, drafts, out _, out var message))
        {
            _status.Text = message;
            return;
        }
        _status.Text = $"已显式升级 {drafts.Length} 个组件；其他已有组件未改变。";
    }

    private void ImportJson()
    {
        var dialog = new Eto.Forms.OpenFileDialog { Title = "导入用户标准件库", MultiSelect = false };
        dialog.Filters.Add(new FileFilter("JSON", ".json"));
        dialog.Filters.Add(new FileFilter("CSV", ".csv"));
        if (dialog.ShowDialog(this) != DialogResult.Ok) return;
        var content = File.ReadAllText(dialog.FileName);
        var success = Path.GetExtension(dialog.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase)
            ? UserFastenerLibraryService.ImportCsv(content, out _, out _, out var message)
            : UserFastenerLibraryService.ImportJson(content, out _, out _, out message);
        if (!success)
        {
            _status.Text = message;
            return;
        }
        _status.Text = message;
        Reload();
    }

    private void Export(bool csv)
    {
        var dialog = new Eto.Forms.SaveFileDialog
        {
            Title = csv ? "导出用户标准件尺寸表" : "导出用户标准件库",
            FileName = $"{FastenerText.Get(FastenerTextKey.ProductName)}_{FastenerText.Translate("用户库")}.{(csv ? "csv" : "json")}"
        };
        dialog.Filters.Add(new FileFilter(csv ? "CSV" : "JSON", csv ? ".csv" : ".json"));
        if (dialog.ShowDialog(this) != DialogResult.Ok) return;
        File.WriteAllText(dialog.FileName, csv ? UserFastenerLibraryService.ExportCsv() : UserFastenerLibraryService.ExportJson());
        _status.Text = $"已导出：{dialog.FileName}";
    }

    private static NumericStepper Number() => new() { MinValue = 0, MaxValue = 1000, DecimalPlaces = 3, Increment = 0.1, Width = 86 };
}
