using System.Globalization;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.Display;
using Rhino.UI;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.UI;

internal sealed class ViewportQuickEditorWindow : Form
{
    private const int CompactWidth = 268;
    private const int ControlHeight = FastenerUiMetrics.QuickControlHeight;
    private const int StatusHeight = 20;
    private const int TextContentWidth = CompactWidth - 12;
    private const int OffsetX = 36;
    private const int OffsetY = 40;
    private static readonly double[] CommonLengths = [8, 10, 12, 15, 20, 25, 30, 35, 40, 45, 50, 60];
    private readonly RhinoDoc _doc;
    private readonly QuickEditPreviewConduit _preview = new();
    private readonly UITimer _previewTimer = new() { Interval = 0.08 };
    private readonly UITimer _deleteTimer = new() { Interval = 2.0 };
    private readonly Label _title = FastenerUiTheme.PrimaryLabel();
    private readonly Label _summary = FastenerUiTheme.SecondaryLabel();
    private readonly Label _health = FastenerUiTheme.SecondaryLabel();
    private readonly Label _status = FastenerUiTheme.SecondaryLabel();
    private readonly DropDown _size = new();
    private readonly NumericStepper _length = Number(0.5);
    private readonly NumericStepper _embed = Number(0.1, -500, 500);
    private readonly NumericStepper _outerDiameter = Number(0.1);
    private readonly NumericStepper _depthCompensation = Number(0.1, 0, 1000);
    private readonly DropDown _assembly = new();
    private readonly CheckBox _lockingNut = new() { Text = "尼龙防松" };
    private readonly Button _commonLength = Tool("▾", "选择常用长度");
    private readonly Button _zero = Tool("0", "头底贴面");
    private readonly Button _flush = Tool("齐平", "头顶齐平/螺母全埋");
    private readonly Button _apply = new() { Text = "应用" };
    private readonly Button _cancel = new() { Text = "取消" };
    private readonly Button _read = Tool("读取", "放弃草稿，将已保存参数读取到主面板");
    private readonly Button _delete = Tool("删除", "删除整个参数化组件（需二次确认，不删除宿主）");
    private FastenerComponentData _component;
    private bool _loading;
    private bool _deleteArmed;
    private bool _processing;
    private string _lastReportedStatus = string.Empty;

    public ViewportQuickEditorWindow(RhinoDoc doc, FastenerComponentData component)
    {
        _doc = doc;
        _component = component;
        Title = "参数化紧固件上下文编辑";
        WindowStyle = WindowStyle.None;
        ShowInTaskbar = false;
        Topmost = true;
        Resizable = false;
        Padding = new Padding(1);
        FastenerUiTheme.SetRole(this, FastenerThemeRole.CardBorder);
        _title.Wrap = WrapMode.None;
        _title.Width = TextContentWidth;
        _summary.Wrap = WrapMode.None;
        _summary.Width = TextContentWidth;
        _health.Wrap = WrapMode.None;
        _health.Width = TextContentWidth;
        _status.Wrap = WrapMode.None;
        _status.Width = TextContentWidth;
        _status.Height = StatusHeight;
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
        foreach (var button in new[]
        {
            _commonLength, _zero, _flush, _cancel, _read, _delete
        })
            FastenerUiTheme.ApplySecondary(button);
        FastenerUiTheme.ApplyPrimary(_apply, true);
        FastenerUiTheme.SetRole(_delete, FastenerThemeRole.DestructiveAction);
        ApplyCompactMetrics();
        WireEvents();
        LoadComponent(component);
        _previewTimer.Elapsed += (_, _) => { _previewTimer.Stop(); RebuildSessionDraft(); };
        _deleteTimer.Elapsed += (_, _) => ResetDeleteConfirmation();
        KeyDown += WindowKeyDown;
        Closed += (_, _) =>
        {
            _previewTimer.Stop();
            _deleteTimer.Stop();
            _preview.Set(null);
            _doc.Views.Redraw();
        };
        FastenerUiTheme.WatchWindow(this);
    }

    public Guid ComponentId => _component.ComponentId;
    public uint DocumentSerialNumber => _doc.RuntimeSerialNumber;
    public event EventHandler? DismissedByUser;

    public void LoadComponent(FastenerComponentData component)
    {
        _component = component;
        if (!ContextualEditSessionService.TryGetOrCreate(_doc, component.ComponentId, out var session, out var message)
            || session is null)
        {
            SetStatus(false, Short(message), message);
            return;
        }
        LoadControls(session.Draft.Component);
        _preview.Set(session.Draft.Prepared);
        SetSessionStatus(session);
        RebuildContent();
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
        var viewportBounds = new Rectangle(screen.Left, screen.Top, screen.Width, screen.Height);
        if (client.X < 0 || client.Y < 0 || client.X > screen.Width || client.Y > screen.Height)
            return false;
        var anchor = new Point(
            screen.Left + (int)Math.Round(client.X),
            screen.Top + (int)Math.Round(client.Y));
        var windowWidth = Math.Max(CompactWidth, Width);
        var windowHeight = Math.Max(ClientSize.Height, Height);
        var candidates = new[]
        {
            new Point(anchor.X + OffsetX, anchor.Y + OffsetY),
            new Point(anchor.X - windowWidth - OffsetX, anchor.Y + OffsetY),
            new Point(anchor.X + OffsetX, anchor.Y - windowHeight - OffsetY),
            new Point(anchor.X - windowWidth - OffsetX, anchor.Y - windowHeight - OffsetY)
        };
        var exclusion = TryGetComponentScreenBounds(view, out var componentBounds)
            ? componentBounds
            : new Rectangle(anchor.X - 18, anchor.Y - 18, 36, 36);
        var best = candidates
            .Select((candidate, index) => new
            {
                Candidate = candidate,
                Score = PositionScore(candidate, windowWidth, windowHeight, viewportBounds, exclusion, index)
            })
            .OrderBy(item => item.Score)
            .First().Candidate;
        Location = new Point(
            Math.Clamp(best.X, screen.Left + 6, Math.Max(screen.Left + 6, screen.Right - windowWidth - 6)),
            Math.Clamp(best.Y, screen.Top + 6, Math.Max(screen.Top + 6, screen.Bottom - windowHeight - 6)));
        return true;
    }

    public void CloseProgrammatically() { _preview.Set(null); Close(); }

    private void LoadControls(FastenerComponentData component)
    {
        _loading = true;
        _component = component;
        _embed.MinValue = FastenerKindTraits.SupportsHeadGap(component.Kind) ? -500 : 0;
        _size.SelectedKey = component.Size;
        _length.Value = component.Length;
        _embed.Value = component.HeadEmbedDepth;
        _outerDiameter.Value = component.InsertOuterDiameter;
        _depthCompensation.Value = component.InsertDepthCompensation;
        _assembly.SelectedKey = component.AssemblyMode.ToString();
        _lockingNut.Checked = component.HexNutStyle == HexNutStyle.NylonInsertLocking;
        _flush.Text = component.Kind == FastenerKind.HexNut ? "全埋" : "齐平";
        _loading = false;
        UpdateIdentity(component);
    }

    private Control BuildContent()
    {
        var root = new DynamicLayout { Spacing = new Size(FastenerUiMetrics.SpaceSmall, FastenerUiMetrics.SpaceTight) };
        root.AddRow(_title);
        root.AddRow(_summary);
        root.AddRow(_health);
        AddFields(root);
        root.AddRow(_status);
        root.AddRow(new TableLayout
        {
            Spacing = new Size(4, 0),
            Rows =
            {
                new TableRow(
                    new TableCell(_read, true),
                    new TableCell(_cancel, true),
                    new TableCell(_apply, true),
                    new TableCell(_delete, true))
            }
        });
        return root;
    }

    private void AddFields(DynamicLayout root)
    {
        if (FastenerKindTraits.IsScrew(_component.Kind))
        {
            root.AddRow(new TableLayout { Spacing = new Size(4, 0), Rows = { new TableRow(InlineField("规格", _size, 58), InlineField("长度", _length, 62), _commonLength) } });
            root.AddRow(new TableLayout { Spacing = new Size(4, 0), Rows = { new TableRow(new TableCell(InlineField("偏移", _embed, 72), true), _zero, _flush) } });
            root.AddRow(InlineField("装配", _assembly));
        }
        else if (_component.Kind == FastenerKind.HexNut)
        {
            root.AddRow(new TableLayout { Spacing = new Size(5, 0), Rows = { new TableRow(InlineField("规格", _size, 62), new TableCell(_lockingNut, true)) } });
            root.AddRow(new TableLayout { Spacing = new Size(4, 0), Rows = { new TableRow(new TableCell(InlineField("嵌入", _embed, 72), true), _zero, _flush) } });
        }
        else
        {
            root.AddRow(new TableLayout { Spacing = new Size(4, 0), Rows = { new TableRow(InlineField("规格", _size, 58), new TableCell(InlineField("长度", _length, 64), true)) } });
            root.AddRow(new TableLayout { Spacing = new Size(4, 0), Rows = { new TableRow(new TableCell(InlineField("外径", _outerDiameter, 64), true), new TableCell(InlineField("深补", _depthCompensation, 64), true)) } });
        }
    }

    private void WireEvents()
    {
        _size.SelectedIndexChanged += (_, _) => HandleSizeChanged();
        _length.ValueChanged += (_, _) => { UpdateIdentity(); SchedulePreview(); };
        _embed.ValueChanged += (_, _) => SchedulePreview();
        _outerDiameter.ValueChanged += (_, _) => SchedulePreview();
        _depthCompensation.ValueChanged += (_, _) => SchedulePreview();
        _assembly.SelectedIndexChanged += (_, _) => SchedulePreview();
        _lockingNut.CheckedChanged += (_, _) => LockingNutChanged();
        _commonLength.Click += (_, _) => ShowLengthMenu();
        _zero.Click += (_, _) => _embed.Value = 0;
        _flush.Click += (_, _) => _embed.Value = FlushDepth(_size.SelectedKey ?? _component.Size);
        _apply.Click += (_, _) => ApplyUpdate();
        _cancel.Click += (_, _) => Dismiss(true);
        _read.Click += (_, _) => ReadSavedComponent();
        _delete.Click += (_, _) => DeleteComponent();
    }

    private void SchedulePreview()
    {
        if (_loading) return;
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void RebuildSessionDraft()
    {
        var draft = BuildDraft();
        if (draft is null) { SetStatus(false, "参数不完整"); return; }
        ContextualEditSessionService.TrySetDraft(_doc, _component.ComponentId, draft, out var session, out var message);
        if (session is null)
        {
            _preview.Set(null);
            SetStatus(false, Short(message), message);
            return;
        }
        _component = session.Draft.Component;
        _preview.Set(session.Draft.Prepared);
        SetSessionStatus(session);
        UpdateIdentity(session.Draft.Component);
        _doc.Views.Redraw();
    }

    private FastenerComponentData? BuildDraft()
    {
        if (string.IsNullOrWhiteSpace(_size.SelectedKey)
            || !ContextualEditSessionService.TryGetOrCreate(_doc, _component.ComponentId, out var session, out _)
            || session is null)
            return null;
        var source = session.Draft.Component;
        var mode = Enum.TryParse<ScrewAssemblyMode>(_assembly.SelectedKey, out var parsed) ? parsed : source.AssemblyMode;
        var draft = source with
        {
            HexNutStyle = _lockingNut.Checked == true ? HexNutStyle.NylonInsertLocking : HexNutStyle.Standard,
            Size = _size.SelectedKey,
            Length = _length.Value,
            HeadEmbedDepth = _embed.Value,
            CounterboreBridgeEnabled = source.Kind == FastenerKind.SocketCap && _embed.Value > 0 && source.CounterboreBridgeEnabled,
            InsertOuterDiameter = _outerDiameter.Value,
            InsertDepthCompensation = _depthCompensation.Value
        };
        if (mode == source.AssemblyMode) return draft;
        var template = FastenerTemplateData.FromComponent(draft) with { AssemblyMode = mode };
        var display = GlobalDisplaySettingsService.Current;
        return template.ToUpdateTemplate(display.FastenerOpacityPercent, display.CutterOpacityPercent).ApplyTo(source);
    }

    private void ApplyUpdate()
    {
        if (_processing)
            return;
        SetProcessing(true);
        try
        {
            RebuildSessionDraft();
            if (!ContextualEditSessionService.TryCommit(_doc, _component.ComponentId, out var saved, out var message) || saved is null)
            {
                SetStatus(false, Short(message), message);
                RhinoMM.Plugin.Services.FastenerCommandText.WriteLine($"上下文更新失败：{message}");
                return;
            }
            FastenerTemplateLibraryService.RecordSuccessfulOperation(FastenerTemplateData.FromComponent(saved), FastenerOperationKind.Update, out _);
            RhinoMM.Plugin.Services.FastenerCommandText.WriteLine(message);
            ViewportQuickEditorService.SuppressForComponent(saved.ComponentId);
        }
        finally
        {
            SetProcessing(false);
        }
    }

    private void ReadSavedComponent()
    {
        ContextualEditSessionService.Cancel(_doc, _component.ComponentId);
        if (!ComponentRepository.TryReadComponent(_doc, _component.ComponentId, out var saved))
        {
            const string message = "组件已缺失或数据损坏，请先运行维护中心。";
            SetStatus(false, "组件无法读取", message);
            return;
        }
        Panels.OpenPanel(typeof(RhinoMMPanel).GUID);
        ComponentEditorSession.Activate(_doc, saved, false, ComponentActivationIntent.LoadIntoEditor);
        RhinoMM.Plugin.Services.FastenerCommandText.WriteLine($"已将 {FastenerLabels.Kind(saved)} {saved.Size} 读取到创建模板。 ");
        ViewportQuickEditorService.SuppressForComponent(saved.ComponentId);
    }

    private void DeleteComponent()
    {
        if (!_deleteArmed)
        {
            _deleteArmed = true;
            _delete.Text = "确认删除";
            _delete.ToolTip = "再次点击删除完整参数化组件；宿主不会删除。";
            FastenerUiTheme.SetRole(_delete, FastenerThemeRole.DestructiveConfirm);
            _deleteTimer.Stop();
            _deleteTimer.Start();
            return;
        }
        var control = ComponentRepository.FindControlPoint(_doc, _component.ComponentId);
        if (control is null) return;
        var undo = _doc.BeginUndoRecord("参数化紧固件：删除组件");
        try { _doc.Objects.Delete(control.Id, true); }
        finally { if (undo != 0) _doc.EndUndoRecord(undo); }
        ContextualEditSessionService.Cancel(_doc, _component.ComponentId);
        DismissedByUser?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void ResetDeleteConfirmation()
    {
        _deleteTimer.Stop();
        _deleteArmed = false;
        _delete.Text = "删除";
        _delete.ToolTip = "删除整个参数化组件（需二次确认，不删除宿主）";
        FastenerUiTheme.SetRole(_delete, FastenerThemeRole.DestructiveAction);
    }

    private void Dismiss(bool cancelDraft)
    {
        if (cancelDraft) ContextualEditSessionService.Cancel(_doc, _component.ComponentId);
        DismissedByUser?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void SetSessionStatus(ContextualEditSession session)
    {
        if (!session.Draft.IsValid) SetStatus(false, Short(session.Draft.Message), session.Draft.Message);
        else if (session.Draft.Warnings.Count > 0)
            SetStatus(true, Short(string.Join(" ", session.Draft.Warnings)), string.Join(" ", session.Draft.Warnings), true);
        else SetStatus(true, string.Empty);
    }

    private void SetStatus(bool valid, string text, string? detail = null, bool warning = false)
    {
        var full = detail ?? text;
        _status.Text = FitSingleLine(_status, text, TextContentWidth);
        _status.ToolTip = full;
        _status.Visible = !string.IsNullOrWhiteSpace(text);
        _apply.Enabled = valid;
        FastenerUiTheme.SetRole(_status, warning ? FastenerThemeRole.StatusWarning : FastenerThemeRole.StatusError);
        if (_status.Visible
            && !string.Equals(full, _lastReportedStatus, StringComparison.Ordinal))
        {
            _lastReportedStatus = full;
            RhinoMM.Plugin.Services.FastenerCommandText.WriteLine($"参数化紧固件快捷编辑：{full}");
        }
        else if (!_status.Visible)
        {
            _lastReportedStatus = string.Empty;
        }
        RebuildContent();
    }

    private void RebuildContent()
    {
        var card = FastenerUiTheme.CreateCard(BuildContent(), FastenerUiMetrics.SpaceCompact);
        Content = card;
        var preferred = card.GetPreferredSize(new SizeF(CompactWidth, 1000));
        var preferredHeight = Math.Max(112, (int)Math.Ceiling(preferred.Height));
        ClientSize = new Size(CompactWidth, preferredHeight);
        FastenerUiTheme.ApplyTree(this);
        FastenerUiLocalization.ApplyTree(this);
        TryReposition();
    }

    private void HandleSizeChanged()
    {
        if (_loading || string.IsNullOrWhiteSpace(_size.SelectedKey)) return;
        if (_lockingNut.Checked == true && _size.SelectedKey == "M1.6") _size.SelectedKey = "M2";
        var oldFlush = FlushDepth(_component.Size);
        if (Math.Abs(_embed.Value - oldFlush) <= 1e-6) _embed.Value = FlushDepth(_size.SelectedKey);
        UpdateIdentity();
        SchedulePreview();
    }

    private void LockingNutChanged()
    {
        if (_loading) return;
        if (_lockingNut.Checked == true && _size.SelectedKey == "M1.6") _size.SelectedKey = "M2";
        UpdateIdentity();
        SchedulePreview();
    }

    private double FlushDepth(string size)
    {
        var spec = string.Equals(size, _component.Size, StringComparison.OrdinalIgnoreCase)
            ? FastenerSpecResolver.Resolve(_component, RhinoMMPlugIn.Catalog)
            : RhinoMMPlugIn.Catalog.Get(size);
        return _component.Kind == FastenerKind.HexNut
            ? HexNutDimensions.Resolve(_lockingNut.Checked == true ? HexNutStyle.NylonInsertLocking : HexNutStyle.Standard, spec).TotalHeight
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

    private void UpdateIdentity(FastenerComponentData? component = null)
    {
        var value = component ?? _component;
        var size = _size.SelectedKey ?? value.Size;
        var length = component?.Length ?? _length.Value;
        var shortKind = value.Kind == FastenerKind.HexNut
            ? _lockingNut.Checked == true
                ? FastenerText.Get("Fastener.LockNut.Short")
                : FastenerText.Get("Fastener.HexNut.Short")
            : FastenerLabels.ShortKind(value.Kind);
        var title = value.Kind == FastenerKind.HexNut ? $"{shortKind} {size}" : $"{shortKind} {size}X{CompactNumber(length)}";
        var needsRelink = ComponentHostResolver.NeedsRelink(value);
        var summary = $"{AssemblyName(value.AssemblyMode)} · {value.Bindings.Select(item => item.TargetObjectId).Distinct().Count()}个宿主";
        var health = needsRelink ? "待重新绑定" : "组件正常";
        _title.Text = FitSingleLine(_title, title, TextContentWidth);
        _summary.Text = FitSingleLine(_summary, summary, TextContentWidth);
        _health.Text = health;
        FastenerUiTheme.SetRole(_health, needsRelink
            ? FastenerThemeRole.StatusWarning
            : FastenerThemeRole.StatusSuccess);
        _title.ToolTip = $"{title} · {FastenerSelectionSummaryFormatter.Compact(value)}";
        _summary.ToolTip = summary;
        _health.ToolTip = needsRelink
            ? "组件宿主关系需要重新绑定后才能安全更新或导出。"
            : "控制点、宿主和参数状态正常。";
        FastenerUiLocalization.ApplyTree(this);
    }

    private void WindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Keys.Enter) { e.Handled = true; ApplyUpdate(); }
        else if (e.Key == Keys.Escape) { e.Handled = true; Dismiss(true); }
    }

    private void ApplyCompactMetrics()
    {
        var font = new Font(SystemFont.Default, 9);
        _title.Font = new Font(SystemFont.Bold, 10);
        foreach (var control in new Control[]
        {
            _size, _length, _embed, _outerDiameter, _depthCompensation, _assembly,
            _lockingNut, _commonLength, _zero, _flush, _apply, _cancel, _read,
            _delete
        })
        {
            if (control is CommonControl common) common.Font = font;
            control.Height = ControlHeight;
        }
        foreach (var button in new[] { _read, _cancel, _apply, _delete })
        {
            button.Width = -1;
            button.Height = FastenerUiMetrics.QuickButtonHeight;
        }
    }

    private void SetProcessing(bool processing)
    {
        _processing = processing;
        _apply.Text = processing ? "处理中" : "应用";
        _read.Enabled = !processing;
        _cancel.Enabled = !processing;
        _delete.Enabled = !processing;
        _apply.Enabled = !processing && ContextualEditSessionService.TryGet(
            _doc,
            _component.ComponentId,
            out var session) && session?.Draft.IsValid == true;
    }

    private bool TryGetComponentScreenBounds(RhinoView view, out Rectangle bounds)
    {
        var ids = new[] { _component.ProxyObjectId }
            .Concat(_component.Bindings.Select(binding => binding.CutterObjectId))
            .Where(id => id != Guid.Empty)
            .Distinct();
        var points = new List<Point>();
        foreach (var id in ids)
        {
            var geometry = _doc.Objects.FindId(id)?.Geometry;
            if (geometry is null)
                continue;
            var box = geometry.GetBoundingBox(true);
            foreach (var corner in box.GetCorners())
            {
                var client = view.ActiveViewport.WorldToClient(corner);
                if (client.IsValid)
                    points.Add(new Point(
                        view.ScreenRectangle.Left + (int)Math.Round(client.X),
                        view.ScreenRectangle.Top + (int)Math.Round(client.Y)));
            }
        }
        if (points.Count == 0)
        {
            bounds = Rectangle.Empty;
            return false;
        }
        var left = points.Min(point => point.X);
        var top = points.Min(point => point.Y);
        var right = points.Max(point => point.X);
        var bottom = points.Max(point => point.Y);
        bounds = new Rectangle(left - 8, top - 8, Math.Max(1, right - left + 16), Math.Max(1, bottom - top + 16));
        return true;
    }

    private static long PositionScore(
        Point candidate,
        int width,
        int height,
        Rectangle viewport,
        Rectangle exclusion,
        int preference)
    {
        var window = new Rectangle(candidate.X, candidate.Y, width, height);
        var overflow = Math.Max(0, viewport.Left - window.Left)
            + Math.Max(0, window.Right - viewport.Right)
            + Math.Max(0, viewport.Top - window.Top)
            + Math.Max(0, window.Bottom - viewport.Bottom);
        var overlapWidth = Math.Max(0, Math.Min(window.Right, exclusion.Right) - Math.Max(window.Left, exclusion.Left));
        var overlapHeight = Math.Max(0, Math.Min(window.Bottom, exclusion.Bottom) - Math.Max(window.Top, exclusion.Top));
        var overlap = overlapWidth * overlapHeight;
        return overflow * 1_000_000L + overlap * 100L + preference;
    }

    private static NumericStepper Number(double increment, double min = 0, double max = 1000) => new()
    {
        MinValue = min, MaxValue = max, DecimalPlaces = 2, Increment = increment
    };
    private static Button Tool(string text, string tooltip) => new() { Text = text, ToolTip = tooltip };
    private static Control InlineField(string label, Control control, int? width = null)
    {
        if (width.HasValue) control.Width = width.Value;
        return new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 3,
            VerticalContentAlignment = VerticalAlignment.Center,
            Items = { CompactLabel(label), new StackLayoutItem(control, !width.HasValue) }
        };
    }
    private static Label CompactLabel(string text)
    {
        var label = FastenerUiTheme.SecondaryLabel(text);
        label.Font = new Font(SystemFont.Default, 9);
        label.VerticalAlignment = VerticalAlignment.Center;
        return label;
    }
    private static string AssemblyName(ScrewAssemblyMode mode) => mode switch
    {
        ScrewAssemblyMode.EngagementOnly => "只咬合",
        ScrewAssemblyMode.NutFastened => "螺母固定",
        _ => "螺纹咬合"
    };
    private static string CompactNumber(double value) => Math.Abs(value - Math.Round(value)) < 1e-9
        ? Math.Round(value).ToString("0", CultureInfo.InvariantCulture)
        : value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Short(string value)
    {
        var single = value.Replace(Environment.NewLine, " ").Trim();
        return single.Length <= 42 ? single : single[..39] + "…";
    }

    private static string FitSingleLine(Label label, string value, float availableWidth)
    {
        var single = value.Replace("\r", " ").Replace("\n", " ").Trim();
        if (string.IsNullOrEmpty(single) || label.Font.MeasureString(single).Width <= availableWidth)
            return single;
        const string ellipsis = "…";
        var low = 0;
        var high = single.Length;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (label.Font.MeasureString(single[..middle] + ellipsis).Width <= availableWidth)
                low = middle;
            else
                high = middle - 1;
        }
        return low == 0 ? ellipsis : single[..low] + ellipsis;
    }
}
