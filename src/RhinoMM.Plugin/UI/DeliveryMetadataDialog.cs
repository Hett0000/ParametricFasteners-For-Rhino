using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.UI;
using RhinoMM.Core.Domain;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.UI;

internal sealed class DeliveryMetadataDialog : Dialog
{
    private readonly RhinoDoc _doc;
    private readonly IReadOnlyList<FastenerComponentData> _components;
    private readonly TextBox _number = new();
    private readonly TextBox _group = new();
    private readonly TextArea _note = new() { Height = 72 };
    private readonly CheckBox _sequence = new() { Text = "批量生成装配编号" };
    private readonly TextBox _prefix = new() { Text = "PF-" };
    private readonly NumericStepper _start = new() { Value = 1, MinValue = 0, MaxValue = 999999, DecimalPlaces = 0 };
    private readonly Label _status = new() { Wrap = WrapMode.Word };

    private DeliveryMetadataDialog(RhinoDoc doc, IReadOnlyList<FastenerComponentData> components)
    {
        _doc = doc;
        _components = components;
        Title = components.Count == 1 ? "交付信息" : $"批量交付信息 · {components.Count} 个组件";
        Size = new Size(480, 360);
        MinimumSize = new Size(360, 320);
        Resizable = true;
        if (components.Count > 0)
        {
            _number.Text = components[0].Delivery.AssemblyNumber;
            _group.Text = components[0].Delivery.ProjectGroup;
            _note.Text = components[0].Delivery.UserNote;
        }
        _sequence.Visible = components.Count > 1;
        _prefix.Enabled = _start.Enabled = false;
        _sequence.CheckedChanged += (_, _) => _prefix.Enabled = _start.Enabled = _sequence.Checked == true;
        var apply = new Button { Text = "保存" };
        var cancel = new Button { Text = "取消" };
        apply.Click += (_, _) => Apply();
        cancel.Click += (_, _) => Close();
        var form = new DynamicLayout
        {
            Spacing = new Size(FastenerUiTheme.SpaceSmall, FastenerUiTheme.SpaceSmall),
            Rows =
            {
                new DynamicRow("装配编号", _number),
                new DynamicRow("项目分组", _group),
                new DynamicRow("用户备注", _note),
                new DynamicRow(_sequence),
                new DynamicRow("编号前缀", _prefix, "起始序号", _start)
            }
        };
        var header = FastenerUiTheme.CreateCard(new DynamicLayout
        {
            Spacing = new Size(0, 2),
            Rows =
            {
                new DynamicRow(FastenerUiTheme.SectionTitle(Title)),
                new DynamicRow(FastenerUiTheme.SecondaryLabel("交付信息随组件、复制和 3DM 文件传递。"))
            }
        });
        Content = FastenerUiTheme.CreateWindowShell(
            header,
            FastenerUiTheme.CreateCard(form),
            _status,
            FastenerUiTheme.CreateActionBar(cancel, apply));
        FastenerUiTheme.ApplyPrimary(apply, true);
        FastenerUiTheme.ApplySecondary(cancel);
        FastenerUiTheme.SetRole(_status, FastenerThemeRole.SecondaryText);
        FastenerUiTheme.WatchWindow(this);
        DefaultButton = apply;
        AbortButton = cancel;
    }

    public static void Show(RhinoDoc doc, IReadOnlyList<FastenerComponentData> components)
    {
        if (components.Count == 0)
        {
            RhinoMM.Plugin.Services.FastenerCommandText.WriteLine("请先选择一个或多个紧固件控制点。" );
            return;
        }
        using var dialog = new DeliveryMetadataDialog(doc, components);
        dialog.ShowModal(RhinoEtoApp.MainWindow);
    }

    private void Apply()
    {
        var start = (int)_start.Value;
        var drafts = _components.Select((component, index) => component with
        {
            Delivery = new DeliveryMetadata(
                _sequence.Checked == true ? $"{_prefix.Text}{start + index}" : _number.Text.Trim(),
                _group.Text.Trim(),
                _note.Text.Trim()),
            UpdatedAt = DateTimeOffset.UtcNow
        }).ToArray();
        if (!ComponentUpdateCoordinator.TryApplyDrafts(_doc, drafts, out _, out var message))
        {
            _status.Text = message;
            return;
        }
        RhinoMM.Plugin.Services.FastenerCommandText.WriteLine(message);
        Close();
    }
}
