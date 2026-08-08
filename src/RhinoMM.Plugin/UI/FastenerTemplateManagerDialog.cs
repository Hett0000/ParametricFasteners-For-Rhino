using System.Text;
using Eto.Drawing;
using Eto.Forms;
using Rhino.UI;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.UI;

internal sealed class FastenerTemplateManagerDialog : Dialog
{
    private readonly ListBox _list = new();
    private readonly Label _status = FastenerUiTheme.SecondaryLabel();

    private FastenerTemplateManagerDialog()
    {
        Title = "紧固件模板管理";
        ClientSize = new Size(460, 360);
        Resizable = true;
        Padding = new Padding(12);
        FastenerUiTheme.SetRole(this, FastenerThemeRole.Canvas);

        var rename = MakeButton("重命名", RenameSelected);
        var copy = MakeButton("复制", CopySelected);
        var up = MakeButton("上移", () => MoveSelected(-1));
        var down = MakeButton("下移", () => MoveSelected(1));
        var delete = MakeButton("删除", DeleteSelected);
        var import = MakeButton("导入 JSON", ImportJson);
        var export = MakeButton("导出 JSON", ExportJson);
        var close = MakeButton("关闭", Close);

        var actions = new DynamicLayout { Spacing = new Size(6, 6) };
        actions.AddRow(rename, copy, up, down, delete);
        actions.AddRow(import, export, null, close);
        Content = new TableLayout
        {
            Spacing = new Size(8, 8),
            Padding = new Padding(0),
            Rows =
            {
                new TableRow(FastenerUiTheme.SectionTitle("收藏模板")),
                new TableRow(_list) { ScaleHeight = true },
                new TableRow(_status),
                new TableRow(actions)
            }
        };
        FastenerTemplateLibraryService.Changed += LibraryChanged;
        Closed += (_, _) => FastenerTemplateLibraryService.Changed -= LibraryChanged;
        RefreshList();
        FastenerUiTheme.ApplyTree(this);
    }

    public static void Show()
    {
        var dialog = new FastenerTemplateManagerDialog();
        dialog.ShowModal(RhinoEtoApp.MainWindow);
    }

    private Button MakeButton(string text, Action action)
    {
        var button = new Button { Text = text, Height = FastenerUiTheme.ControlHeight };
        FastenerUiTheme.ApplySecondary(button);
        button.Click += (_, _) => action();
        return button;
    }

    private void LibraryChanged(object? sender, EventArgs e) =>
        Application.Instance.AsyncInvoke(RefreshList);

    private void RefreshList()
    {
        var selectedId = SelectedId();
        _list.Items.Clear();
        foreach (var entry in FastenerTemplateLibraryService.Current.Favorites)
        {
            _list.Items.Add(new ListItem
            {
                Key = entry.Id.ToString("D"),
                Text = $"{entry.Name}    {FastenerTemplateFormatter.Compact(entry.Data)}"
            });
        }
        if (selectedId != Guid.Empty)
            _list.SelectedKey = selectedId.ToString("D");
        _status.Text = _list.Items.Count == 0
            ? "暂无收藏模板；可在主面板点击 ☆ 收藏当前模板。"
            : $"共 {_list.Items.Count} 个收藏模板。最近使用记录不会导出。";
    }

    private Guid SelectedId() =>
        Guid.TryParse(_list.SelectedKey, out var id) ? id : Guid.Empty;

    private void RenameSelected()
    {
        var entry = SelectedEntry();
        if (entry is null)
            return;
        var name = PromptName("重命名模板", entry.Name);
        if (name is null)
            return;
        SetStatus(FastenerTemplateLibraryService.RenameFavorite(entry.Id, name, out var message), message);
    }

    private void CopySelected()
    {
        var id = SelectedId();
        if (id == Guid.Empty)
            return;
        var success = FastenerTemplateLibraryService.DuplicateFavorite(id, out var duplicate, out var message);
        RefreshList();
        if (success)
            _list.SelectedKey = duplicate.Id.ToString("D");
        SetStatus(success, message);
    }

    private void MoveSelected(int delta)
    {
        var id = SelectedId();
        if (id == Guid.Empty)
            return;
        SetStatus(FastenerTemplateLibraryService.MoveFavorite(id, delta, out var message), message);
        RefreshList();
        _list.SelectedKey = id.ToString("D");
    }

    private void DeleteSelected()
    {
        var id = SelectedId();
        if (id == Guid.Empty)
            return;
        SetStatus(FastenerTemplateLibraryService.DeleteFavorite(id, out var message), message);
    }

    private void ImportJson()
    {
        var dialog = new Eto.Forms.OpenFileDialog
        {
            Title = "导入紧固件收藏模板",
            MultiSelect = false,
            Filters = { new FileFilter("JSON 模板", ".json") }
        };
        if (dialog.ShowDialog(this) != DialogResult.Ok)
            return;
        try
        {
            var json = File.ReadAllText(dialog.FileName, Encoding.UTF8);
            var success = FastenerTemplateLibraryService.ImportFavoritesJson(
                json,
                out _,
                out _,
                out var message);
            SetStatus(success, message);
        }
        catch (Exception ex)
        {
            SetStatus(false, $"无法读取模板文件：{ex.Message}");
        }
    }

    private void ExportJson()
    {
        var dialog = new Eto.Forms.SaveFileDialog
        {
            Title = "导出紧固件收藏模板",
            FileName = "参数化紧固件-收藏模板.json",
            Filters = { new FileFilter("JSON 模板", ".json") }
        };
        if (dialog.ShowDialog(this) != DialogResult.Ok)
            return;
        var target = dialog.FileName;
        var temp = target + ".tmp";
        try
        {
            File.WriteAllText(temp, FastenerTemplateLibraryService.ExportFavoritesJson(), new UTF8Encoding(false));
            File.Move(temp, target, true);
            SetStatus(true, $"已导出 {FastenerTemplateLibraryService.Current.Favorites.Count} 个收藏模板。" );
        }
        catch (Exception ex)
        {
            if (File.Exists(temp))
                File.Delete(temp);
            SetStatus(false, $"模板导出失败：{ex.Message}");
        }
    }

    private FastenerTemplateEntry? SelectedEntry()
    {
        var id = SelectedId();
        var entry = FastenerTemplateLibraryService.Current.Favorites.FirstOrDefault(item => item.Id == id);
        if (entry is null)
            SetStatus(false, "请先选择一个收藏模板。" );
        return entry;
    }

    private static string? PromptName(string title, string current)
    {
        var text = new TextBox { Text = current };
        var dialog = new Dialog<string>
        {
            Title = title,
            ClientSize = new Size(340, 110),
            Padding = new Padding(12)
        };
        var ok = new Button { Text = "确定" };
        var cancel = new Button { Text = "取消" };
        ok.Click += (_, _) => dialog.Close(text.Text.Trim());
        cancel.Click += (_, _) => dialog.Close(string.Empty);
        dialog.DefaultButton = ok;
        dialog.AbortButton = cancel;
        dialog.Content = new DynamicLayout
        {
            Spacing = new Size(8, 8),
            Rows =
            {
                new DynamicRow(text),
                new DynamicRow(null, ok, cancel)
            }
        };
        return dialog.ShowModal(RhinoEtoApp.MainWindow);
    }

    private void SetStatus(bool success, string message)
    {
        _status.Text = message;
        FastenerUiTheme.SetRole(
            _status,
            success ? FastenerThemeRole.StatusSuccess : FastenerThemeRole.StatusError);
    }
}
