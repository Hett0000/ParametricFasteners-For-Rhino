using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.UI;

namespace RhinoMM.Plugin.UI;

internal sealed record FastenerActionDescriptor(
    string Group,
    string Name,
    string Keywords,
    Action Execute)
{
    public string Display => $"{Group}　{Name}";
}

internal sealed class FastenerActionPalette : Form
{
    private const int ViewportTopOffset = 36;
    private const int ScreenMargin = 12;
    private static FastenerActionPalette? _current;
    private static Size _lastClientSize = new(360, 420);
    private readonly IReadOnlyList<FastenerActionDescriptor> _actions;
    private readonly TextBox _search = new() { PlaceholderText = "搜索操作…" };
    private readonly ListBox _list = new();
    private IReadOnlyList<FastenerActionDescriptor> _visible = [];

    private FastenerActionPalette(IReadOnlyList<FastenerActionDescriptor> actions)
    {
        _actions = actions;
        Title = "参数化紧固件操作";
        ClientSize = _lastClientSize;
        MinimumSize = new Size(300, 300);
        ShowInTaskbar = false;
        Resizable = true;
        _search.TextChanged += (_, _) => Filter();
        _search.KeyDown += KeyDownHandler;
        _list.KeyDown += KeyDownHandler;
        _list.MouseDoubleClick += (_, _) => ExecuteSelected();
        Content = new TableLayout
        {
            Padding = new Padding(FastenerUiTheme.SpaceLarge),
            Spacing = new Size(0, FastenerUiTheme.SpaceMedium),
            Rows =
            {
                new TableRow(FastenerUiTheme.CreateCard(new DynamicLayout
            {
                Spacing = new Size(0, FastenerUiTheme.SpaceSmall),
                Rows =
                {
                    new DynamicRow(FastenerUiTheme.PrimaryLabel("全部操作")),
                    new DynamicRow(_search)
                }
            })),
                new TableRow(FastenerUiTheme.CreateCard(_list, FastenerUiTheme.SpaceSmall)) { ScaleHeight = true }
            }
        };
        FastenerUiTheme.WatchWindow(this);
        Filter();
        Shown += (_, _) =>
        {
            RepositionToActiveViewport();
            _search.Focus();
        };
        Closed += (_, _) =>
        {
            _lastClientSize = ClientSize;
            if (ReferenceEquals(_current, this))
                _current = null;
        };
    }

    public static void Show(Control? anchor, IReadOnlyList<FastenerActionDescriptor> actions)
    {
        if (_current is { Visible: true } current)
        {
            current.RepositionToActiveViewport();
            current.BringToFront();
            current._search.Focus();
            return;
        }

        var palette = new FastenerActionPalette(actions) { Owner = RhinoEtoApp.MainWindow };
        _current = palette;
        palette.Show();
    }

    private void RepositionToActiveViewport()
    {
        var activeView = RhinoDoc.ActiveDoc?.Views.ActiveView;
        RectangleF target;
        if (activeView is not null && activeView.ScreenRectangle is { Width: > 0, Height: > 0 } view)
        {
            target = new RectangleF(view.Left, view.Top, view.Width, view.Height);
        }
        else
        {
            var ownerBounds = RhinoEtoApp.MainWindow.Bounds;
            target = new RectangleF(ownerBounds.X, ownerBounds.Y, ownerBounds.Width, ownerBounds.Height);
        }

        var center = new PointF(target.Left + target.Width / 2f, target.Top + target.Height / 2f);
        var screen = Screen.FromPoint(center) ?? Screen.PrimaryScreen;
        var area = screen.WorkingArea;
        var windowWidth = Math.Max(Width, ClientSize.Width);
        var windowHeight = Math.Max(Height, ClientSize.Height);
        var x = (int)Math.Round(target.Left + (target.Width - windowWidth) / 2f);
        var y = (int)Math.Round(target.Top + ViewportTopOffset);
        x = Math.Clamp(x, (int)Math.Ceiling(area.Left + ScreenMargin),
            Math.Max((int)Math.Ceiling(area.Left + ScreenMargin), (int)Math.Floor(area.Right - windowWidth - ScreenMargin)));
        y = Math.Clamp(y, (int)Math.Ceiling(area.Top + ScreenMargin),
            Math.Max((int)Math.Ceiling(area.Top + ScreenMargin), (int)Math.Floor(area.Bottom - windowHeight - ScreenMargin)));
        Location = new Point(x, y);
    }

    private void Filter()
    {
        var query = (_search.Text ?? string.Empty).Trim();
        _visible = _actions
            .Where(action => query.Length == 0
                || action.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || action.Group.Contains(query, StringComparison.OrdinalIgnoreCase)
                || action.Keywords.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        _list.Items.Clear();
        foreach (var action in _visible)
            _list.Items.Add(new ListItem { Key = action.Name, Text = action.Display });
        if (_visible.Count > 0)
            _list.SelectedIndex = 0;
    }

    private void KeyDownHandler(object? sender, KeyEventArgs e)
    {
        if (e.Key == Keys.Escape)
        {
            e.Handled = true;
            Close();
        }
        else if (e.Key == Keys.Enter)
        {
            e.Handled = true;
            ExecuteSelected();
        }
        else if (ReferenceEquals(sender, _search) && e.Key is Keys.Down or Keys.Up)
        {
            e.Handled = true;
            if (_visible.Count == 0)
                return;
            var offset = e.Key == Keys.Down ? 1 : -1;
            _list.SelectedIndex = Math.Clamp(_list.SelectedIndex + offset, 0, _visible.Count - 1);
        }
    }

    private void ExecuteSelected()
    {
        if (_list.SelectedIndex < 0 || _list.SelectedIndex >= _visible.Count)
            return;
        var action = _visible[_list.SelectedIndex];
        Close();
        Application.Instance.AsyncInvoke(action.Execute);
    }
}
