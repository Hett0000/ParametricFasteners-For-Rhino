using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.UI;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.UI;

[System.Runtime.InteropServices.Guid("7310FEC5-7DCA-4637-B778-AB3C0FBA9DA7")]
public sealed class RhinoMMPanel : Panel, IPanel
{
    private readonly DropDown _kind = new();
    private readonly DropDown _size = new();
    private readonly NumericStepper _length = new() { MinValue = 0.5, MaxValue = 500, DecimalPlaces = 2, Increment = 0.5 };
    private readonly NumericStepper _printerCorrection = new() { MinValue = -2, MaxValue = 2, DecimalPlaces = 3, Increment = 0.05 };
    private readonly DropDown _clearanceFit = new();
    private readonly NumericStepper _bite = new() { MinValue = 0, MaxValue = 5, DecimalPlaces = 3, Increment = 0.05 };
    private readonly Slider _fastenerOpacity = new() { MinValue = 0, MaxValue = 100, Value = 70 };
    private readonly Slider _cutterOpacity = new() { MinValue = 0, MaxValue = 100, Value = 35 };
    private readonly Label _fastenerOpacityValue = new() { Text = "70%", Width = 42 };
    private readonly Label _cutterOpacityValue = new() { Text = "35%", Width = 42 };
    private readonly StackLayout _moduleList = new() { Orientation = Orientation.Vertical, Spacing = 4 };
    private readonly CheckBox _autoUpdate = new() { Text = "修改参数后自动重建", Checked = true };
    private readonly Label _status = new() { Text = "选择现有组件可读取并修改。" };
    private readonly UITimer _updateTimer = new() { Interval = 0.5 };
    private readonly UITimer _displayTimer = new() { Interval = 0.1 };
    private FastenerComponentData? _loadedComponent;
    private bool _loadingControls;

    public RhinoMMPanel()
    {
        foreach (var value in Enum.GetValues<FastenerKind>())
            _kind.Items.Add(new ListItem { Key = value.ToString(), Text = FastenerLabels.Kind(value) });
        foreach (var spec in RhinoMMPlugIn.Catalog.Sizes) _size.Items.Add(spec.Designation);
        foreach (var value in Enum.GetValues<ClearanceFitClass>())
            _clearanceFit.Items.Add(new ListItem { Key = value.ToString(), Text = FastenerLabels.ClearanceFit(value) });

        var load = MakeButton("读取选中组件", (_, _) => LoadSelection());
        var place = MakeButton("放置 / 绑定孔", (_, _) => Run("_-RhinoMMPlaceHole"));
        var apply = MakeButton("立即应用更新", (_, _) => ApplyLoaded());
        var adopt = MakeButton("转换选中模型", (_, _) => { SaveControls(); Run("_-RhinoMMAdoptFastener"); });
        var export = MakeButton("布尔并导出 STL / STEP", (_, _) => Run("_-RhinoMMExportPrint"));
        var validate = MakeButton("校验文档", (_, _) => Run("_-RhinoMMValidate"));

        Content = new Scrollable
        {
            Content = new DynamicLayout
            {
                Padding = 12,
                Spacing = new Size(6, 8),
                Rows =
                {
                    new Label { Text = "参数化紧固件", Font = new Font(SystemFont.Bold, 16) },
                    new Label { Text = "FDM 紧固件与可延迟布尔孔" },
                    Row("类型", _kind),
                    Row("规格", _size),
                    Row("长度 mm", _length),
                    Row("打印孔径修正 mm", _printerCorrection),
                    Row("通孔配合", _clearanceFit),
                    Row("咬合缩减 mm", _bite),
                    Row("紧固件不透明度", OpacityControl(_fastenerOpacity, _fastenerOpacityValue)),
                    Row("切割模块不透明度", OpacityControl(_cutterOpacity, _cutterOpacityValue)),
                    _autoUpdate,
                    new Label { Text = "────────────────────" },
                    new Label { Text = "补偿切割模块", Font = new Font(SystemFont.Bold, 12) },
                    _moduleList,
                    new Label { Text = "关闭仅隐藏预览，导出时仍参与布尔。", TextColor = Colors.Gray },
                    new Label { Text = "────────────────────" },
                    load,
                    place,
                    apply,
                    adopt,
                    validate,
                    export,
                    new Label { Text = "────────────────────" },
                    _status,
                    new Label
                    {
                        Text = "通孔 = 标准间隙孔 + 打印修正；咬合孔 = 公称直径 − 咬合缩减 + 打印修正。咬合缩减须通过试片校准。",
                        Wrap = WrapMode.Word
                    }
                }
            }
        };
        LoadControls();
        LoadModules();
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

    public void PanelShown(uint documentSerialNumber, ShowPanelReason reason) => LoadSelection(silent: true);
    public void PanelHidden(uint documentSerialNumber, ShowPanelReason reason) { }
    public void PanelClosing(uint documentSerialNumber, bool onCloseDocument) { }

    private static StackLayout Row(string label, Control control) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 8,
        Items =
        {
            new StackLayoutItem(new Label { Text = label, Width = 135, VerticalAlignment = VerticalAlignment.Center }),
            new StackLayoutItem(control, true)
        }
    };

    private static Button MakeButton(string text, EventHandler<EventArgs> handler)
    {
        var button = new Button { Text = text, Height = 30 };
        button.Click += handler;
        return button;
    }

    private static StackLayout OpacityControl(Slider slider, Label value) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 6,
        Items =
        {
            new StackLayoutItem(slider, true),
            new StackLayoutItem(value)
        }
    };

    private void LoadSelection(bool silent = false)
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc is not null && ComponentRepository.TryReadSelection(doc, out var component))
        {
            _loadedComponent = component;
            EditorState.Current.Load(component);
            LoadControls();
            LoadModules();
            _status.Text = $"已读取 {component.Size} / {FastenerLabels.Kind(component.Kind)} / {component.Bindings.Count} 个绑定体";
        }
        else if (!silent)
        {
            _status.Text = "未选中参数化紧固件或切割模块。";
        }
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
        if (Enum.TryParse<FastenerKind>(_kind.SelectedKey, out var kind)) state.Kind = kind;
        if (!string.IsNullOrWhiteSpace(_size.SelectedKey)) state.Size = _size.SelectedKey;
        state.Length = _length.Value;
        state.PrinterCorrection = _printerCorrection.Value;
        if (Enum.TryParse<ClearanceFitClass>(_clearanceFit.SelectedKey, out var fit)) state.ClearanceFit = fit;
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
        _status.Text = "参数已修改，准备重建…";
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
        _moduleList.Items.Clear();
        if (_loadedComponent is null || _loadedComponent.Bindings.Count == 0)
        {
            _moduleList.Items.Add(new Label { Text = "当前组件没有绑定切割模块。", TextColor = Colors.Gray });
            return;
        }

        var doc = RhinoDoc.ActiveDoc;
        var spec = RhinoMMPlugIn.Catalog.Get(_loadedComponent.Size);
        foreach (var binding in _loadedComponent.Bindings)
        {
            var targetName = doc?.Objects.FindId(binding.TargetObjectId)?.Attributes.Name;
            if (string.IsNullOrWhiteSpace(targetName))
                targetName = $"实体 {binding.TargetObjectId.ToString("N")[..8]}";
            var diameter = HoleDiameterCalculator.Calculate(spec, binding, _loadedComponent.PrintProfile).FinalDiameter;
            var checkBox = new CheckBox
            {
                Checked = binding.IsPreviewVisible,
                Text = $"{targetName} · {FastenerLabels.Role(binding.Role)} · Ø{diameter:0.###} mm"
            };
            var bindingId = binding.BindingId;
            checkBox.CheckedChanged += (_, _) => SetModuleVisibility(bindingId, checkBox.Checked == true);
            _moduleList.Items.Add(checkBox);
        }
    }

    private void SetModuleVisibility(Guid bindingId, bool visible)
    {
        if (_loadingControls || _loadedComponent is null)
            return;
        _loadedComponent = _loadedComponent with
        {
            Bindings = _loadedComponent.Bindings
                .Select(binding => binding.BindingId == bindingId ? binding with { IsPreviewVisible = visible } : binding)
                .ToArray(),
            UpdatedAt = DateTimeOffset.UtcNow
        };
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
            EditorState.Current.Load(updated);
        }
        _status.Text = message;
    }

    private void ApplyLoaded()
    {
        if (_loadedComponent is null || RhinoDoc.ActiveDoc is not { } doc)
        {
            _status.Text = "请先选择组件并点击“读取选中组件”。";
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
            _loadedComponent = saved;
            EditorState.Current.Load(saved);
            LoadModules();
        }
        _status.Text = message;
    }
}
