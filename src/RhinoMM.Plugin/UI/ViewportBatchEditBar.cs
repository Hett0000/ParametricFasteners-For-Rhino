using Eto.Drawing;
using Eto.Forms;
using Rhino;
using RhinoMM.Core.Domain;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.UI;

internal sealed class ViewportBatchEditBar : Form
{
    private readonly RhinoDoc _doc;
    private IReadOnlyList<FastenerComponentData> _components;
    private readonly Label _summary = FastenerUiTheme.PrimaryLabel();
    private readonly Label _detail = FastenerUiTheme.SecondaryLabel();
    private readonly Button _apply = new() { Text = "应用模板" };
    private readonly Button _inspect = new() { Text = "装配检查" };
    private readonly Button _maintenance = new() { Text = "维护" };
    private readonly Button _clear = new() { Text = "取消选择" };
    private readonly CheckBox _adaptive = new() { Text = "长度自适应" };

    public ViewportBatchEditBar(RhinoDoc doc, IReadOnlyList<FastenerComponentData> components)
    {
        _doc = doc;
        _components = components;
        Title = "参数化紧固件批量编辑";
        WindowStyle = WindowStyle.None;
        ShowInTaskbar = false;
        Topmost = true;
        Resizable = false;
        ClientSize = new Size(458, 82);
        Padding = new Padding(1);
        FastenerUiTheme.SetRole(this, FastenerThemeRole.CardBorder);
        FastenerUiTheme.ApplyPrimary(_apply, true);
        foreach (var button in new[] { _inspect, _maintenance, _clear })
            FastenerUiTheme.ApplySecondary(button);
        _apply.Click += (_, _) => ApplyTemplate();
        _inspect.Click += (_, _) => AssemblyInspectorDialog.Show(_doc);
        _maintenance.Click += (_, _) => MaintenanceCenterDialog.Show(_doc);
        _clear.Click += (_, _) => _doc.Objects.UnselectAll(true);
        KeyDown += (_, e) =>
        {
            if (e.Key != Keys.Escape)
                return;
            e.Handled = true;
            _doc.Objects.UnselectAll(true);
        };
        Content = FastenerUiTheme.CreateCard(new DynamicLayout
        {
            Spacing = new Size(4, 4),
            Rows =
            {
                new DynamicRow(_summary),
                new DynamicRow(_detail),
                new DynamicRow(new TableLayout
                {
                    Spacing = new Size(4, 0),
                    Rows = { new TableRow(_adaptive, _apply, _inspect, _maintenance, _clear) }
                })
            }
        }, 5);
        FastenerUiTheme.WatchWindow(this);
        _adaptive.Checked = FastenerTemplateLibraryService.ActiveScheme?.AdaptiveLength == true;
        LoadComponents(components);
    }

    public uint DocumentSerialNumber => _doc.RuntimeSerialNumber;
    public IReadOnlyList<Guid> ComponentIds => _components.Select(item => item.ComponentId).ToArray();

    public void LoadComponents(IReadOnlyList<FastenerComponentData> components)
    {
        _components = components;
        var distributions = components
            .GroupBy(item => item.Size)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key}×{group.Count()}")
            .Take(3)
            .ToArray();
        var hosts = components.SelectMany(item => item.Bindings).Select(item => item.TargetObjectId).Where(id => id != Guid.Empty).Distinct().Count();
        var issues = components.Count(item => ComponentHostResolver.NeedsRelink(item)
            || item.Bindings.Any(binding => binding.TargetObjectId == Guid.Empty || _doc.Objects.FindId(binding.TargetObjectId) is null));
        _summary.Text = $"已选 {components.Count} 个 · {string.Join(" / ", distributions)}";
        _detail.Text = $"宿主 {hosts} · 异常 {issues} · 应用使用主面板当前创建模板";
        _apply.Enabled = issues == 0;
        _maintenance.Visible = issues > 0;
        _apply.ToolTip = issues == 0
            ? $"将主面板模板应用到 {components.Count} 个组件"
            : "存在损坏或待重绑组件，请先维护。";
    }

    public bool TryReposition()
    {
        var view = _doc.Views.ActiveView;
        if (view is null || _components.Count == 0)
            return false;
        var points = _components.Select(item => new Rhino.Geometry.Point3d(
            item.Placement.OriginX,
            item.Placement.OriginY,
            item.Placement.OriginZ)).ToArray();
        var box = new Rhino.Geometry.BoundingBox(points);
        var client = view.ActiveViewport.WorldToClient(box.Center);
        if (!client.IsValid)
            return false;
        var screen = view.ScreenRectangle;
        var x = screen.Left + (int)client.X + 28;
        var y = screen.Top + (int)client.Y + 34;
        Location = new Point(
            Math.Clamp(x, screen.Left + 6, Math.Max(screen.Left, screen.Right - Width - 6)),
            Math.Clamp(y, screen.Top + 6, Math.Max(screen.Top, screen.Bottom - Height - 6)));
        return true;
    }

    public void CloseProgrammatically() => Close();

    private void ApplyTemplate()
    {
        if (_adaptive.Checked == true)
        {
            var template = EditorState.Current.CaptureUpdateTemplate();
            if (!AdaptiveBatchUpdateService.TryApply(_doc, _components, template, out _, out var adaptiveMessage))
                RhinoApp.WriteLine($"自适应批量更新失败：{adaptiveMessage}");
            else
                RhinoApp.WriteLine(adaptiveMessage);
            return;
        }
        if (!RhinoMMPanel.ApplyCurrentTemplateToSelection(out var message))
            RhinoApp.WriteLine($"批量更新失败：{message}");
        else
            RhinoApp.WriteLine(message);
    }
}
