using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.UI;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Geometry;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.UI;

[System.Runtime.InteropServices.Guid("7310FEC5-7DCA-4637-B778-AB3C0FBA9DA7")]
public sealed class RhinoMMPanel : Panel, IPanel
{
    private const int WideLayoutBreakpoint = 360;

    private readonly DropDown _kind = new();
    private readonly DropDown _size = new();
    private readonly NumericStepper _length = new() { MinValue = 0.5, MaxValue = 500, DecimalPlaces = 2, Increment = 0.5 };
    private readonly NumericStepper _printerCorrection = new() { MinValue = -2, MaxValue = 2, DecimalPlaces = 3, Increment = 0.05 };
    private readonly DropDown _clearanceFit = new();
    private readonly NumericStepper _bite = new() { MinValue = 0, MaxValue = 5, DecimalPlaces = 3, Increment = 0.05 };
    private readonly Slider _fastenerOpacity = new() { MinValue = 0, MaxValue = 100, Value = 70 };
    private readonly Slider _cutterOpacity = new() { MinValue = 0, MaxValue = 100, Value = 35 };
    private readonly Label _fastenerOpacityValue = new() { Text = "70%", Width = 38 };
    private readonly Label _cutterOpacityValue = new() { Text = "35%", Width = 38 };
    private readonly StackLayout _moduleList = new() { Orientation = Orientation.Vertical, Spacing = 6 };
    private readonly CheckBox _autoUpdate = new() { Text = "参数修改后自动重建", Checked = true };
    private readonly Label _summary = new()
    {
        Text = "未读取组件",
        Font = new Font(SystemFont.Bold, 12),
        Wrap = WrapMode.Word
    };
    private readonly Label _status = new()
    {
        Text = "选择组件后点击“读取组件”。",
        Wrap = WrapMode.Word,
        Height = 36,
        TextColor = Colors.Gray
    };
    private readonly Panel _contentHost = new();
    private readonly Scrollable _scrollable;
    private readonly Expander _displayExpander;
    private readonly Expander _helpExpander;
    private readonly UITimer _updateTimer = new() { Interval = 0.5 };
    private readonly UITimer _displayTimer = new() { Interval = 0.1 };
    private FastenerComponentData? _loadedComponent;
    private bool _loadingControls;
    private bool? _wideLayout;

    public RhinoMMPanel()
    {
        foreach (var value in Enum.GetValues<FastenerKind>())
            _kind.Items.Add(new ListItem { Key = value.ToString(), Text = FastenerLabels.Kind(value) });
        foreach (var spec in RhinoMMPlugIn.Catalog.Sizes)
            _size.Items.Add(spec.Designation);
        foreach (var value in Enum.GetValues<ClearanceFitClass>())
            _clearanceFit.Items.Add(new ListItem { Key = value.ToString(), Text = FastenerLabels.ClearanceFit(value) });

        _displayExpander = new Expander
        {
            Header = SectionHeader("显示设置"),
            Expanded = false,
            Content = new StackLayout
            {
                Orientation = Orientation.Vertical,
                Spacing = 6,
                Items =
                {
                    FieldStack("紧固件不透明度", OpacityControl(_fastenerOpacity, _fastenerOpacityValue)),
                    FieldStack("切割模块不透明度", OpacityControl(_cutterOpacity, _cutterOpacityValue))
                }
            }
        };
        _helpExpander = new Expander
        {
            Header = SectionHeader("使用说明"),
            Expanded = false,
            Content = new Label
            {
                Text = "通孔使用标准间隙与打印修正；咬合孔使用公称直径减去咬合缩减。咬合缩减请通过打印试片校准。线框模式不显示材质透明度。",
                Wrap = WrapMode.Word
            }
        };

        _scrollable = new Scrollable
        {
            Border = BorderType.None,
            ExpandContentWidth = true,
            ExpandContentHeight = false,
            Content = _contentHost
        };

        var actions = BuildActions();
        Content = new TableLayout
        {
            Padding = new Padding(10, 8),
            Spacing = new Size(0, 6),
            Rows =
            {
                new TableRow(new StackLayout
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 2,
                    Items = { _summary, _status }
                }),
                new TableRow(_scrollable) { ScaleHeight = true },
                new TableRow(actions)
            }
        };

        SizeChanged += (_, _) => RebuildResponsiveLayout();
        ComponentEditorSession.ActiveComponentChanged += SessionComponentChanged;
        WireEvents();
        LoadControls();
        LoadModules();
        RebuildResponsiveLayout(force: true);
    }

    public void PanelShown(uint documentSerialNumber, ShowPanelReason reason)
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc is null)
            return;
        if (ComponentRepository.TryReadSelection(doc, out var selected))
            ComponentEditorSession.Activate(doc, selected, true);
        else if (ComponentEditorSession.TryGetActive(doc, out var active))
            ActivateComponent(active);
    }

    public void PanelHidden(uint documentSerialNumber, ShowPanelReason reason) { }

    public void PanelClosing(uint documentSerialNumber, bool onCloseDocument)
    {
        ComponentEditorSession.ActiveComponentChanged -= SessionComponentChanged;
    }

    private void WireEvents()
    {
        _updateTimer.Elapsed += (_, _) =>
        {
            _updateTimer.Stop();
            ApplyLoaded();
        };
        _displayTimer.Elapsed += (_, _) =>
        {
            _displayTimer.Stop();
            ApplyDisplaySettings();
        };
        _kind.SelectedIndexChanged += (_, _) => ScheduleUpdate();
        _size.SelectedIndexChanged += (_, _) => ScheduleUpdate();
        _length.ValueChanged += (_, _) => ScheduleUpdate();
        _printerCorrection.ValueChanged += (_, _) => ScheduleUpdate();
        _clearanceFit.SelectedIndexChanged += (_, _) => ScheduleUpdate();
        _bite.ValueChanged += (_, _) => ScheduleUpdate();
        _fastenerOpacity.ValueChanged += (_, _) => ScheduleDisplayUpdate();
        _cutterOpacity.ValueChanged += (_, _) => ScheduleDisplayUpdate();
    }

    private Control BuildActions()
    {
        var load = MakeButton("读取组件", (_, _) => LoadSelection());
        var place = MakeButton("放置 / 绑定", (_, _) => Run("_-ParametricFastenersPlace"));
        var apply = MakeButton("应用更新", (_, _) => ApplyLoaded());
        var export = MakeButton("布尔导出", (_, _) => Run("_-ParametricFastenersExport"));
        var more = new Button { Text = "更多…", Height = 28 };
        more.Click += (_, _) => ShowMoreMenu(more);
        var grid = new DynamicLayout { Spacing = new Size(6, 5) };
        grid.AddRow(load, place);
        grid.AddRow(apply, export);
        grid.AddRow(more);
        return grid;
    }

    private void ShowMoreMenu(Control owner)
    {
        var adopt = new ButtonMenuItem { Text = "转换选中模型" };
        adopt.Click += (_, _) => Run("_-ParametricFastenersAdopt");
        var validate = new ButtonMenuItem { Text = "校验文档" };
        validate.Click += (_, _) => Run("_-ParametricFastenersValidate");
        var menu = new ContextMenu { Items = { adopt, validate } };
        menu.Show(owner);
    }

    private void RebuildResponsiveLayout(bool force = false)
    {
        var wide = ClientSize.Width >= WideLayoutBreakpoint;
        if (!force && _wideLayout == wide)
            return;
        _wideLayout = wide;
        _contentHost.Content = null;

        var layout = new DynamicLayout
        {
            Padding = new Padding(0, 4, 0, 8),
            Spacing = new Size(6, 6)
        };
        layout.AddRow(SectionHeader("基础参数"));
        AddField(layout, wide, "类型", _kind);
        AddField(layout, wide, "规格", _size);
        AddField(layout, wide, "长度 mm", _length);
        AddField(layout, wide, "孔径修正 mm", _printerCorrection);
        AddField(layout, wide, "通孔配合", _clearanceFit);
        AddField(layout, wide, "咬合缩减 mm", _bite);
        layout.AddRow(_autoUpdate);
        layout.AddRow(_displayExpander);
        layout.AddRow(SectionHeader("补偿切割模块"));
        layout.AddRow(_moduleList);
        layout.AddRow(_helpExpander);
        _contentHost.Content = layout;
    }

    private static void AddField(DynamicLayout layout, bool wide, string label, Control control)
    {
        if (wide)
            layout.AddRow(new Label { Text = label, VerticalAlignment = VerticalAlignment.Center }, control);
        else
        {
            layout.AddRow(new Label { Text = label });
            layout.AddRow(control);
        }
    }

    private static Label SectionHeader(string text) => new()
    {
        Text = text,
        Font = new Font(SystemFont.Bold, 10)
    };

    private static StackLayout FieldStack(string label, Control control) => new()
    {
        Orientation = Orientation.Vertical,
        Spacing = 2,
        Items = { new Label { Text = label }, control }
    };

    private static Button MakeButton(string text, EventHandler<EventArgs> handler)
    {
        var button = new Button { Text = text, Height = 28 };
        button.Click += handler;
        return button;
    }

    private static StackLayout OpacityControl(Slider slider, Label value) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 4,
        Items =
        {
            new StackLayoutItem(slider, true),
            new StackLayoutItem(value)
        }
    };

    private void SessionComponentChanged(object? sender, ComponentChangedEventArgs e)
    {
        if (RhinoDoc.ActiveDoc?.RuntimeSerialNumber != e.Document.RuntimeSerialNumber)
            return;
        ActivateComponent(e.Component);
    }

    private void ActivateComponent(FastenerComponentData component)
    {
        _loadedComponent = component;
        EditorState.Current.Load(component);
        LoadControls();
        LoadModules();
        UpdateSummary();
        SetStatus($"已读取 {component.Bindings.Count} 个宿主。", StatusKind.Success);
    }

    private void LoadSelection()
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc is not null && ComponentEditorSession.TryActivateSelection(doc, true, out _))
            return;
        SetStatus("未选中参数化紧固件或切割模块。", StatusKind.Warning);
    }

    private bool EnsureLoaded(RhinoDoc doc)
    {
        if (ComponentRepository.TryReadSelection(doc, out var selected))
        {
            ComponentEditorSession.Activate(doc, selected, true);
            return true;
        }
        if (_loadedComponent is not null
            && ComponentRepository.FindComponentObjects(doc, _loadedComponent.ComponentId).Any())
        {
            return true;
        }
        if (ComponentEditorSession.TryGetActive(doc, out var active))
        {
            _loadedComponent = active;
            return true;
        }
        return false;
    }

    private void LoadControls()
    {
        _loadingControls = true;
        var state = EditorState.Current;
        _kind.SelectedKey = state.Kind.ToString();
        _size.SelectedKey = state.Size;
        _length.Value = state.Length;
        _printerCorrection.Value = state.PrinterCorrection;
        _clearanceFit.SelectedKey = state.ClearanceFit.ToString();
        _bite.Value = state.BiteReduction;
        _fastenerOpacity.Value = (int)Math.Round(state.FastenerOpacityPercent);
        _cutterOpacity.Value = (int)Math.Round(state.CutterOpacityPercent);
        UpdateOpacityLabels();
        _loadingControls = false;
    }

    private void SaveControls()
    {
        var state = EditorState.Current;
        if (Enum.TryParse<FastenerKind>(_kind.SelectedKey, out var kind))
            state.Kind = kind;
        if (!string.IsNullOrWhiteSpace(_size.SelectedKey))
            state.Size = _size.SelectedKey;
        state.Length = _length.Value;
        state.PrinterCorrection = _printerCorrection.Value;
        if (Enum.TryParse<ClearanceFitClass>(_clearanceFit.SelectedKey, out var fit))
            state.ClearanceFit = fit;
        state.BiteReduction = _bite.Value;
        state.FastenerOpacityPercent = _fastenerOpacity.Value;
        state.CutterOpacityPercent = _cutterOpacity.Value;
    }

    private void Run(string command)
    {
        SaveControls();
        RhinoApp.RunScript(command, false);
    }

    private void ScheduleUpdate()
    {
        if (_loadingControls || _loadedComponent is null || _autoUpdate.Checked != true)
            return;
        SaveControls();
        _updateTimer.Stop();
        _updateTimer.Start();
        SetStatus("参数已修改，准备重建…", StatusKind.Info);
    }

    private void ScheduleDisplayUpdate()
    {
        UpdateOpacityLabels();
        if (_loadingControls || _loadedComponent is null)
            return;
        SaveControls();
        _displayTimer.Stop();
        _displayTimer.Start();
    }

    private void UpdateOpacityLabels()
    {
        _fastenerOpacityValue.Text = $"{_fastenerOpacity.Value}%";
        _cutterOpacityValue.Text = $"{_cutterOpacity.Value}%";
    }

    private void LoadModules()
    {
        _loadingControls = true;
        _moduleList.Items.Clear();
        if (_loadedComponent is null || _loadedComponent.Bindings.Count == 0)
        {
            _moduleList.Items.Add(new Label
            {
                Text = "当前组件没有绑定切割模块。",
                TextColor = Colors.Gray,
                Wrap = WrapMode.Word
            });
            _loadingControls = false;
            return;
        }

        foreach (var binding in _loadedComponent.Bindings)
            _moduleList.Items.Add(BuildModuleCard(binding));
        _loadingControls = false;
    }

    private Control BuildModuleCard(HoleTargetBinding binding)
    {
        var doc = RhinoDoc.ActiveDoc;
        var spec = RhinoMMPlugIn.Catalog.Get(_loadedComponent!.Size);
        var target = doc?.Objects.FindId(binding.TargetObjectId);
        var fullName = target?.Attributes.Name;
        if (string.IsNullOrWhiteSpace(fullName))
            fullName = $"实体 {binding.TargetObjectId.ToString("N")[..8]}";
        var shortName = fullName.Length > 30 ? $"{fullName[..27]}…" : fullName;
        var diameter = HoleDiameterCalculator.Calculate(spec, binding, _loadedComponent.PrintProfile).FinalDiameter;
        var title = new Label
        {
            Text = $"{shortName} · {FastenerLabels.Role(binding.Role)} · Ø{diameter:0.###}",
            ToolTip = fullName,
            Wrap = WrapMode.Word,
            Height = 34
        };

        var depthLabel = new Label
        {
            Text = DepthDescription(binding, target?.Geometry),
            TextColor = Colors.Gray,
            Wrap = WrapMode.Word
        };
        var preview = new CheckBox { Text = "显示预览", Checked = binding.IsPreviewVisible };
        var booleanEnabled = new CheckBox { Text = "参与导出布尔", Checked = binding.IsBooleanEnabled };
        preview.CheckedChanged += (_, _) => SetBinding(
            binding.BindingId,
            item => item with { IsPreviewVisible = preview.Checked == true },
            rebuildGeometry: false);
        booleanEnabled.CheckedChanged += (_, _) => SetBinding(
            binding.BindingId,
            item => item with { IsBooleanEnabled = booleanEnabled.Checked == true },
            rebuildGeometry: false);

        var card = new DynamicLayout
        {
            Padding = new Padding(6),
            Spacing = new Size(4, 3)
        };
        card.AddRow(title);
        if (binding.Role == ShaftFitRole.ThreadEngagement)
        {
            var depth = new DropDown();
            foreach (var mode in Enum.GetValues<DepthMode>())
                depth.Items.Add(new ListItem { Key = mode.ToString(), Text = FastenerLabels.Depth(mode) });
            depth.SelectedKey = binding.DepthMode.ToString();
            var customDepth = new NumericStepper
            {
                MinValue = 0.1,
                MaxValue = 1000,
                DecimalPlaces = 2,
                Increment = 0.5,
                Value = binding.BlindDepth > 0 ? binding.BlindDepth : _loadedComponent.Length
            };
            customDepth.Visible = binding.DepthMode == DepthMode.Blind;
            depth.SelectedIndexChanged += (_, _) =>
            {
                if (_loadingControls || !Enum.TryParse<DepthMode>(depth.SelectedKey, out var mode))
                    return;
                customDepth.Visible = mode == DepthMode.Blind;
                SetBinding(binding.BindingId, item => item with
                {
                    DepthMode = mode,
                    BlindDepth = mode == DepthMode.Blind ? customDepth.Value : item.BlindDepth
                }, rebuildGeometry: true);
            };
            customDepth.ValueChanged += (_, _) =>
            {
                if (_loadingControls || depth.SelectedKey != DepthMode.Blind.ToString())
                    return;
                SetBinding(binding.BindingId, item => item with { BlindDepth = customDepth.Value }, rebuildGeometry: true);
            };
            card.AddRow(depth, customDepth);
        }
        card.AddRow(depthLabel);
        card.AddRow(preview, booleanEnabled);
        return new Panel { Content = card };
    }

    private string DepthDescription(HoleTargetBinding binding, Rhino.Geometry.GeometryBase? geometry)
    {
        if (_loadedComponent is null)
            return FastenerLabels.Depth(binding.DepthMode);
        if (binding.DepthMode == DepthMode.ThroughTarget)
            return "贯穿当前宿主";
        var spec = RhinoMMPlugIn.Catalog.Get(_loadedComponent.Size);
        var limit = FastenerGeometryFactory.DepthLimit(_loadedComponent, spec, binding);
        var result = $"孔底 {limit:0.###} mm";
        var doc = RhinoDoc.ActiveDoc;
        if (doc is not null && geometry is not null
            && FastenerGeometryFactory.TryGetTargetInterval(
                geometry, _loadedComponent.Placement, doc.ModelAbsoluteTolerance, out var interval, out _))
        {
            result += limit >= interval.Max - doc.ModelAbsoluteTolerance ? " · 将贯穿" : " · 盲孔";
        }
        return result;
    }

    private void SetBinding(Guid bindingId, Func<HoleTargetBinding, HoleTargetBinding> update, bool rebuildGeometry)
    {
        if (_loadingControls || _loadedComponent is null)
            return;
        _loadedComponent = _loadedComponent with
        {
            Bindings = _loadedComponent.Bindings
                .Select(binding => binding.BindingId == bindingId ? update(binding) : binding)
                .ToArray(),
            UpdatedAt = DateTimeOffset.UtcNow
        };
        if (rebuildGeometry)
            ApplyLoaded();
        else
            ApplyDisplaySettings();
    }

    private void ApplyDisplaySettings()
    {
        if (_loadedComponent is null || RhinoDoc.ActiveDoc is not { } doc)
            return;
        SaveControls();
        var updated = _loadedComponent with
        {
            FastenerOpacityPercent = EditorState.Current.FastenerOpacityPercent,
            CutterOpacityPercent = EditorState.Current.CutterOpacityPercent,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        if (ComponentPresentationService.ApplyDisplaySettings(doc, updated, out var message))
        {
            _loadedComponent = updated;
            ComponentEditorSession.Activate(doc, updated, true);
            SetStatus(message, StatusKind.Success);
        }
        else
            SetStatus(message, StatusKind.Error);
    }

    private void ApplyLoaded()
    {
        if (RhinoDoc.ActiveDoc is not { } doc || !EnsureLoaded(doc) || _loadedComponent is null)
        {
            SetStatus("请先选择并读取一个组件。", StatusKind.Warning);
            return;
        }
        SaveControls();
        var draft = EditorState.Current.CreateDraft(_loadedComponent.Placement, _loadedComponent.Bindings) with
        {
            ComponentId = _loadedComponent.ComponentId,
            AdoptedSourceObjectId = _loadedComponent.AdoptedSourceObjectId
        };
        if (FastenerComponentService.CreateOrReplace(doc, draft, out var saved, out var message))
        {
            ComponentEditorSession.Activate(doc, saved, true);
            SetStatus(message, message.Contains("警告") || message.Contains("贯穿") ? StatusKind.Warning : StatusKind.Success);
        }
        else
            SetStatus(message, StatusKind.Error);
    }

    private void UpdateSummary()
    {
        _summary.Text = _loadedComponent is null
            ? "未读取组件"
            : $"{_loadedComponent.Size} · {FastenerLabels.Kind(_loadedComponent.Kind)} · L{_loadedComponent.Length:0.##} · {_loadedComponent.Bindings.Count}个宿主";
    }

    private void SetStatus(string message, StatusKind kind)
    {
        _status.Text = message;
        _status.TextColor = kind switch
        {
            StatusKind.Success => Color.FromArgb(36, 124, 68),
            StatusKind.Warning => Color.FromArgb(184, 105, 0),
            StatusKind.Error => Color.FromArgb(190, 45, 45),
            _ => Colors.Gray
        };
    }

    private enum StatusKind
    {
        Info,
        Success,
        Warning,
        Error
    }
}
