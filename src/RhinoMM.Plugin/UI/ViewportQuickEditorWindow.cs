using Eto.Drawing;
using Eto.Forms;
using Rhino;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Services;
using System.Globalization;

namespace RhinoMM.Plugin.UI;

internal sealed class ViewportQuickEditorWindow : Form
{
    private const int CompactWidth = 252;
    private const int CompactControlHeight = 22;
    private const int CompactActionHeight = 26;
    private const int ControlOffsetX = 36;
    private const int ControlOffsetY = 40;
    private static readonly double[] CommonLengths = [8, 10, 12, 15, 20, 25, 30, 35, 40, 45, 50, 60];
    private readonly RhinoDoc _doc;
    private readonly QuickEditPreviewConduit _preview = new();
    private readonly UITimer _previewTimer = new() { Interval = 0.08 };
    private readonly Label _title = FastenerUiTheme.PrimaryLabel();
    private readonly Label _status = FastenerUiTheme.SecondaryLabel();
    private readonly DropDown _size = new();
    private readonly NumericStepper _length = Number(0.5);
    private readonly NumericStepper _embed = Number(0.1);
    private readonly NumericStepper _outerDiameter = Number(0.1);
    private readonly NumericStepper _depthCompensation = Number(0.1, 0, 1000);
    private readonly DropDown _assembly = new();
    private readonly CheckBox _lockingNut = new() { Text = "尼龙防松" };
    private readonly Button _commonLength = new() { Text = "▾", Width = 26, ToolTip = "选择常用长度" };
    private readonly Button _zero = new() { Text = "0", Width = 28 };
    private readonly Button _flush = new() { Text = "齐平", Width = 42 };
    private readonly Button _confirm = new() { Text = "确定" };
    private readonly Button _cancel = new() { Text = "取消" };
    private FastenerComponentData _component;
    private PreparedFastenerGeometry? _prepared;
    private bool _loading;

    public ViewportQuickEditorWindow(RhinoDoc doc, FastenerComponentData component)
    {
        _doc = doc;
        _component = component;
        Title = "参数化紧固件快速编辑";
        WindowStyle = WindowStyle.None;
        ShowInTaskbar = false;
        Topmost = true;
        Resizable = false;
        ClientSize = new Size(CompactWidth, BaseHeight(component.Kind));
        Padding = new Padding(1);
        FastenerUiTheme.SetRole(this, FastenerThemeRole.CardBorder);

        foreach (var spec in RhinoMMPlugIn.Catalog.Sizes)
            _size.Items.Add(new ListItem { Key = spec.Designation, Text = spec.Designation });
        foreach (var mode in Enum.GetValues<ScrewAssemblyMode>())
        {
            _assembly.Items.Add(new ListItem
            {
                Key = mode.ToString(),
                Text = mode switch
                {
                    ScrewAssemblyMode.EngagementOnly => "只咬合",
                    ScrewAssemblyMode.NutFastened => "螺母固定",
                    _ => "螺纹咬合"
                }
            });
        }

        foreach (var button in new[] { _commonLength, _zero, _flush, _cancel })
            FastenerUiTheme.ApplySecondary(button);
        FastenerUiTheme.ApplyPrimary(_confirm, true);
        ApplyCompactMetrics();
        _status.Visible = false;
        Content = FastenerUiTheme.CreateCard(BuildContent(), 5);
        WireEvents();
        LoadComponent(component);
        _previewTimer.Elapsed += (_, _) =>
        {
            _previewTimer.Stop();
            RebuildPreview();
        };
        KeyDown += WindowKeyDown;
        RhinoApp.AppSettingsChanged += RhinoAppSettingsChanged;
        Closed += (_, _) =>
        {
            RhinoApp.AppSettingsChanged -= RhinoAppSettingsChanged;
            _previewTimer.Stop();
            _preview.Set(null);
            _doc.Views.Redraw();
        };
        FastenerUiTheme.ApplyTree(this);
    }

    private void RhinoAppSettingsChanged(object? sender, EventArgs e) =>
        Application.Instance.AsyncInvoke(() =>
        {
            FastenerUiTheme.RefreshPalette();
            FastenerUiTheme.ApplyTree(this);
        });

    public Guid ComponentId => _component.ComponentId;
    public uint DocumentSerialNumber => _doc.RuntimeSerialNumber;

    public event EventHandler? DismissedByUser;

    public void LoadComponent(FastenerComponentData component)
    {
        _component = component;
        _loading = true;
        _size.SelectedKey = component.Size;
        _length.Value = component.Length;
        _embed.Value = component.HeadEmbedDepth;
        _outerDiameter.Value = component.InsertOuterDiameter;
        _depthCompensation.Value = component.InsertDepthCompensation;
        _assembly.SelectedKey = component.AssemblyMode.ToString();
        _lockingNut.Checked = component.HexNutStyle == HexNutStyle.NylonInsertLocking;
        _flush.Text = component.Kind == FastenerKind.HexNut ? "全埋" : "齐平";
        UpdateTitle(component);
        _loading = false;
        _status.Visible = false;
        Content = FastenerUiTheme.CreateCard(BuildContent(), 5);
        UpdateWindowSize();
        FastenerUiTheme.ApplyTree(this);
        SchedulePreview();
    }

    public bool TryReposition()
    {
        var view = _doc.Views.ActiveView;
        if (view is null)
            return false;
        var plane = RhinoMM.Plugin.Geometry.FastenerGeometryFactory.ToPlane(_component.Placement);
        var client = view.ActiveViewport.WorldToClient(plane.Origin);
        if (!client.IsValid)
            return false;
        var screen = view.ScreenRectangle;
        if (client.X < 0 || client.Y < 0 || client.X > screen.Width || client.Y > screen.Height)
            return false;
        var x = screen.Left + (int)Math.Round(client.X) + ControlOffsetX;
        var y = screen.Top + (int)Math.Round(client.Y) + ControlOffsetY;
        var maxX = Math.Max(screen.Left, screen.Right - Width - 6);
        var maxY = Math.Max(screen.Top, screen.Bottom - Height - 6);
        Location = new Point(
            Math.Clamp(x, screen.Left + 6, maxX),
            Math.Clamp(y, screen.Top + 6, maxY));
        return true;
    }

    public void CloseProgrammatically()
    {
        _preview.Set(null);
        Close();
    }

    private Control BuildContent()
    {
        var root = new DynamicLayout { Spacing = new Size(4, 4) };
        root.AddRow(_title);
        if (FastenerKindTraits.IsScrew(_component.Kind))
        {
            root.AddRow(new TableLayout
            {
                Spacing = new Size(4, 0),
                Rows =
                {
                    new TableRow(
                        InlineField("规格", _size, 58),
                        InlineField("长度", _length, 62),
                        _commonLength)
                }
            });
            root.AddRow(new TableLayout
            {
                Spacing = new Size(4, 0),
                Rows =
                {
                    new TableRow(
                        new TableCell(InlineField("嵌入", _embed, 72), true),
                        _zero,
                        _flush)
                }
            });
            root.AddRow(InlineField("装配", _assembly));
        }
        else if (_component.Kind == FastenerKind.HexNut)
        {
            root.AddRow(new TableLayout
            {
                Spacing = new Size(5, 0),
                Rows =
                {
                    new TableRow(
                        InlineField("规格", _size, 62),
                        new TableCell(_lockingNut, true))
                }
            });
            root.AddRow(new TableLayout
            {
                Spacing = new Size(4, 0),
                Rows =
                {
                    new TableRow(
                        new TableCell(InlineField("嵌入", _embed, 72), true),
                        _zero,
                        _flush)
                }
            });
        }
        else
        {
            root.AddRow(new TableLayout
            {
                Spacing = new Size(4, 0),
                Rows =
                {
                    new TableRow(
                        InlineField("规格", _size, 58),
                        new TableCell(InlineField("长度", _length, 64), true))
                }
            });
            root.AddRow(new TableLayout
            {
                Spacing = new Size(4, 0),
                Rows =
                {
                    new TableRow(
                        new TableCell(InlineField("外径", _outerDiameter, 64), true),
                        new TableCell(InlineField("深补", _depthCompensation, 64), true))
                }
            });
        }
        root.AddRow(_status);
        root.AddRow(new TableLayout
        {
            Spacing = new Size(4, 0),
            Rows =
            {
                new TableRow(
                    new TableCell(_confirm, true),
                    new TableCell(_cancel, true))
            }
        });
        return root;
    }

    private void WireEvents()
    {
        _size.SelectedIndexChanged += (_, _) => HandleSizeChanged();
        _length.ValueChanged += (_, _) =>
        {
            UpdateTitle();
            SchedulePreview();
        };
        _embed.ValueChanged += (_, _) => SchedulePreview();
        _outerDiameter.ValueChanged += (_, _) => SchedulePreview();
        _depthCompensation.ValueChanged += (_, _) => SchedulePreview();
        _assembly.SelectedIndexChanged += (_, _) => SchedulePreview();
        _lockingNut.CheckedChanged += (_, _) => LockingNutChanged();
        _commonLength.Click += (_, _) => ShowLengthMenu();
        _zero.Click += (_, _) => _embed.Value = 0;
        _flush.Click += (_, _) => _embed.Value = FlushDepth(_size.SelectedKey ?? _component.Size);
        _confirm.Click += (_, _) => ApplyUpdate();
        _cancel.Click += (_, _) => Dismiss();
    }

    private static NumericStepper Number(double increment, double minimum = 0, double maximum = 1000) => new()
    {
        MinValue = minimum,
        MaxValue = maximum,
        DecimalPlaces = 2,
        Increment = increment
    };

    private static Control InlineField(string label, Control control, int? controlWidth = null)
    {
        if (controlWidth.HasValue)
            control.Width = controlWidth.Value;
        return new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 3,
            VerticalContentAlignment = VerticalAlignment.Center,
            Items =
            {
                CompactLabel(label),
                new StackLayoutItem(control, !controlWidth.HasValue)
            }
        };
    }

    private static Label CompactLabel(string text)
    {
        var label = FastenerUiTheme.SecondaryLabel(text);
        label.Font = new Font(SystemFont.Default, 9);
        label.VerticalAlignment = VerticalAlignment.Center;
        return label;
    }

    private void SchedulePreview()
    {
        if (_loading)
            return;
        _prepared = null;
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void RebuildPreview()
    {
        var draft = BuildDraft();
        if (draft is null)
        {
            SetStatus(false, "参数不完整");
            _preview.Set(null);
            return;
        }
        if (!FastenerGeometryPreparationService.TryPrepare(
                _doc,
                draft,
                out var prepared,
                out var message))
        {
            _prepared = null;
            _preview.Set(null);
            SetStatus(false, Short(message), fullMessage: message);
            RhinoApp.WriteLine($"快速编辑预检：{message}");
            _doc.Views.Redraw();
            return;
        }
        _prepared = prepared;
        _preview.Set(prepared);
        UpdateTitle(prepared!.Draft);
        if (prepared.Warnings.Count == 0)
            SetStatus(true, string.Empty);
        else
        {
            var warningMessage = string.Join(" ", prepared.Warnings);
            SetStatus(true, Short(warningMessage), warning: true, fullMessage: warningMessage);
        }
        _doc.Views.Redraw();
    }

    private FastenerComponentData? BuildDraft()
    {
        if (string.IsNullOrWhiteSpace(_size.SelectedKey))
            return null;
        var assemblyMode = Enum.TryParse<ScrewAssemblyMode>(_assembly.SelectedKey, out var mode)
            ? mode
            : _component.AssemblyMode;
        var draft = _component with
        {
            HexNutStyle = _lockingNut.Checked == true
                ? HexNutStyle.NylonInsertLocking
                : HexNutStyle.Standard,
            Size = _size.SelectedKey,
            Length = _length.Value,
            HeadEmbedDepth = _embed.Value,
            InsertOuterDiameter = _outerDiameter.Value,
            InsertDepthCompensation = _depthCompensation.Value
        };
        if (assemblyMode != _component.AssemblyMode)
        {
            var templateData = FastenerTemplateData.FromComponent(draft) with
            {
                AssemblyMode = assemblyMode
            };
            var display = GlobalDisplaySettingsService.Current;
            draft = templateData.ToUpdateTemplate(
                display.FastenerOpacityPercent,
                display.CutterOpacityPercent).ApplyTo(_component);
        }
        return draft;
    }

    private void ApplyUpdate()
    {
        if (_prepared is null)
        {
            RebuildPreview();
            if (_prepared is null)
                return;
        }
        if (!ComponentUpdateCoordinator.TryApplyDrafts(
                _doc,
                [_prepared.Draft],
                out var saved,
                out var message))
        {
            SetStatus(false, Short(message));
            RhinoApp.WriteLine(message);
            return;
        }
        _component = saved[0];
        FastenerTemplateLibraryService.RecordSuccessfulOperation(
            FastenerTemplateData.FromComponent(_component),
            FastenerOperationKind.Update,
            out _);
        RhinoApp.WriteLine(message);
        ViewportQuickEditorService.SuppressForComponent(_component.ComponentId);
    }

    private void HandleSizeChanged()
    {
        if (_loading || string.IsNullOrWhiteSpace(_size.SelectedKey))
            return;
        if (_lockingNut.Checked == true && _size.SelectedKey == "M1.6")
            _size.SelectedKey = "M2";
        var oldFlush = FlushDepth(_component.Size);
        if (Math.Abs(_embed.Value - oldFlush) <= 1e-6)
            _embed.Value = FlushDepth(_size.SelectedKey);
        UpdateTitle();
        SchedulePreview();
    }

    private void LockingNutChanged()
    {
        if (_loading)
            return;
        if (_lockingNut.Checked == true && _size.SelectedKey == "M1.6")
            _size.SelectedKey = "M2";
        UpdateTitle();
        SchedulePreview();
    }

    private double FlushDepth(string size)
    {
        var spec = RhinoMMPlugIn.Catalog.Get(size);
        return _component.Kind == FastenerKind.HexNut
            ? HexNutDimensions.Resolve(
                _lockingNut.Checked == true
                    ? HexNutStyle.NylonInsertLocking
                    : HexNutStyle.Standard,
                spec).TotalHeight
            : HeadGeometryCalculator.GetHeadHeight(_component.Kind, spec);
    }

    private void ShowLengthMenu()
    {
        var menu = new ContextMenu();
        foreach (var value in CommonLengths)
        {
            var item = new ButtonMenuItem { Text = $"{value:0.##} mm" };
            item.Click += (_, _) => _length.Value = value;
            menu.Items.Add(item);
        }
        menu.Show(_commonLength);
    }

    private void WindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Keys.Enter)
        {
            e.Handled = true;
            ApplyUpdate();
        }
        else if (e.Key == Keys.Escape)
        {
            e.Handled = true;
            Dismiss();
        }
    }

    private void Dismiss()
    {
        DismissedByUser?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void SetStatus(
        bool success,
        string message,
        bool warning = false,
        string? fullMessage = null)
    {
        _status.Text = message;
        _status.ToolTip = fullMessage ?? message;
        _status.Visible = !string.IsNullOrWhiteSpace(message);
        _confirm.Enabled = success;
        FastenerUiTheme.SetRole(
            _status,
            warning ? FastenerThemeRole.StatusWarning : FastenerThemeRole.StatusError);
        UpdateWindowSize();
    }

    private void ApplyCompactMetrics()
    {
        var compact = new Font(SystemFont.Default, 9);
        _title.Font = new Font(SystemFont.Bold, 10);
        _title.Height = 16;
        _status.Font = compact;
        _status.Height = 16;
        foreach (var control in new Control[]
        {
            _size, _length, _embed, _outerDiameter, _depthCompensation,
            _assembly, _lockingNut, _commonLength, _zero, _flush, _confirm, _cancel
        })
        {
            if (control is CommonControl commonControl)
                commonControl.Font = compact;
            control.Height = CompactControlHeight;
        }
        _confirm.Height = CompactActionHeight;
        _cancel.Height = CompactActionHeight;
    }

    private void UpdateTitle(FastenerComponentData? component = null)
    {
        var size = _size.SelectedKey ?? component?.Size ?? _component.Size;
        var value = component?.Length ?? _length.Value;
        var shortKind = _component.Kind == FastenerKind.HexNut
            ? _lockingNut.Checked == true ? "防松螺母" : "六角螺母"
            : FastenerLabels.ShortKind(_component.Kind);
        _title.Text = _component.Kind == FastenerKind.HexNut
            ? $"{shortKind} {size}"
            : $"{shortKind} {size}X{CompactNumber(value)}";
        _title.ToolTip = FastenerSelectionSummaryFormatter.Compact(component ?? _component);
    }

    private void UpdateWindowSize()
    {
        var height = BaseHeight(_component.Kind) + (_status.Visible ? 20 : 0);
        ClientSize = new Size(CompactWidth, height);
    }

    private static int BaseHeight(FastenerKind kind) =>
        FastenerKindTraits.IsScrew(kind) ? 140 : 116;

    private static string CompactNumber(double value) =>
        value.ToString(Math.Abs(value - Math.Round(value)) <= 1e-9 ? "0" : "0.##", CultureInfo.InvariantCulture);

    private static string Short(string message)
    {
        var single = message.Replace(Environment.NewLine, " ").Trim();
        return single.Length <= 46 ? single : single[..45] + "…";
    }
}
