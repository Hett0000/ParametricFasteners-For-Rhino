using Eto.Drawing;
using Eto.Forms;

namespace RhinoMM.Plugin.UI;

internal sealed class CardSelector : Panel
{
    private const int CardHeight = FastenerUiTheme.ControlHeight;
    private const int CardSpacing = 4;
    private readonly List<(string Key, ToggleButton Button)> _items = [];
    private bool _synchronizing;
    private int _columns = 3;
    private string? _selectedKey;

    public CardSelector() => Rebuild();

    public event EventHandler<EventArgs>? SelectedKeyChanged;

    public string? SelectedKey
    {
        get => _selectedKey;
        set => Select(value, false);
    }

    public void Add(string key, string text, string? toolTip = null)
    {
        var button = new ToggleButton
        {
            Text = text,
            Height = CardHeight,
            MinimumSize = new Size(0, CardHeight),
            ToolTip = toolTip ?? text
        };
        button.CheckedChanged += (_, _) => ButtonCheckedChanged(key, button);
        _items.Add((key, button));
        Rebuild();
    }

    public void SetColumns(int columns)
    {
        columns = Math.Max(1, columns);
        if (_columns == columns)
            return;
        _columns = columns;
        Rebuild();
    }

    public void SetText(string key, string text)
    {
        var index = _items.FindIndex(item => item.Key == key);
        if (index < 0 || _items[index].Button.Text == text)
            return;
        _items[index].Button.Text = text;
    }

    public void SetEnabled(string key, bool enabled)
    {
        var index = _items.FindIndex(item => item.Key == key);
        if (index >= 0)
        {
            _items[index].Button.Enabled = enabled;
            ApplySelectionStyle(_items[index].Button, _items[index].Key == _selectedKey);
        }
    }

    public void RefreshTheme()
    {
        foreach (var item in _items)
            ApplySelectionStyle(item.Button, item.Key == _selectedKey);
    }

    public void Select(string? key, bool raiseChanged)
    {
        if (key is not null && _items.All(item => item.Key != key))
            key = null;
        var changed = _selectedKey != key;
        _selectedKey = key;
        _synchronizing = true;
        foreach (var item in _items)
        {
            item.Button.Checked = item.Key == key;
            ApplySelectionStyle(item.Button, item.Key == key);
        }
        _synchronizing = false;
        if (changed && raiseChanged)
            SelectedKeyChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ButtonCheckedChanged(string key, ToggleButton button)
    {
        if (_synchronizing)
            return;
        if (button.Checked == true)
            Select(key, true);
        else if (_selectedKey == key)
            Select(key, false);
    }

    private void Rebuild()
    {
        // Rhino's Eto host does not always invalidate a realized TableLayout
        // after Rows.Clear()/Rows.Add(). Replacing the content keeps its native
        // preferred size valid when the panel crosses the responsive breakpoint.
        var layout = new TableLayout { Spacing = new Size(CardSpacing, CardSpacing) };
        for (var index = 0; index < _items.Count; index += _columns)
        {
            var row = _items
                .Skip(index)
                .Take(_columns)
                .Select(item => new TableCell(item.Button, true))
                .ToList();
            layout.Rows.Add(new TableRow(row));
        }

        var rowCount = (_items.Count + _columns - 1) / _columns;
        MinimumSize = new Size(
            0,
            rowCount == 0 ? 0 : rowCount * CardHeight + (rowCount - 1) * CardSpacing);
        Content = layout;
    }

    private static void ApplySelectionStyle(ToggleButton button, bool selected)
    {
        FastenerUiTheme.SetRole(
            button,
            selected ? FastenerThemeRole.PrimaryAction : FastenerThemeRole.SecondaryAction);
        if (!button.Enabled)
            button.TextColor = FastenerUiTheme.DisabledText;
        button.Font = selected ? SystemFonts.Bold() : SystemFonts.Default();
    }
}
