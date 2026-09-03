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
public sealed class RhinoMMPanel : Panel, IPanel, ILocalizableView
{
    private static WeakReference<RhinoMMPanel>? _currentPanel;
    private static readonly double[] CommonLengths = [8, 10, 12, 15, 20, 25, 30, 35, 40, 45, 50, 60];
    private static readonly DepthMode[] EngagementDepthModes =
    [
        DepthMode.ThroughTarget,
        DepthMode.FastenerLengthPlusOneDiameter,
        DepthMode.FastenerLengthPlusCustom
    ];

    private readonly CardSelector _kind = new();
    private readonly CheckBox _nylonLockingNut = new()
    {
        Text = "尼龙防松螺母",
        ToolTip = "勾选后使用尼龙防松螺母尺寸；M3–M12 为 GB/T 889.1-2015 兼容尺寸，M2、M2.5 为 DIN 985 工程扩展预设。"
    };
    private readonly CardSelector _size = new();
    private readonly CardSelector _lengthCards = new();
    private readonly NumericStepper _length = new() { MinValue = 0, MaxValue = 500, DecimalPlaces = 2, Increment = 0.5 };
    private readonly NumericStepper _headEmbed = new()
    {
        MinValue = -500,
        MaxValue = 500,
        DecimalPlaces = 2,
        Increment = 0.1,
        ToolTip = "正值表示嵌入，0 表示头底贴面，负值表示螺丝头离开宿主表面的距离。"
    };
    private readonly NumericStepper _insertOuterDiameter = new() { MinValue = 0, MaxValue = 500, DecimalPlaces = 2, Increment = 0.1 };
    private readonly NumericStepper _insertDiameterCompensation = new() { MinValue = -20, MaxValue = 20, DecimalPlaces = 2, Increment = 0.05 };
    private readonly NumericStepper _insertDepthCompensation = new() { MinValue = 0, MaxValue = 1000, DecimalPlaces = 2, Increment = 0.1, Value = 1 };
    private readonly Label _insertFinalDiameter = new()
    {
        TextColor = FastenerUiTheme.SecondaryText,
        Wrap = WrapMode.None
    };
    private readonly Panel _insertSummaryHost = new() { MinimumSize = new Size(0, 0) };
    private readonly Label _nutStandardDimensions = new()
    {
        TextColor = FastenerUiTheme.SecondaryText,
        Wrap = WrapMode.None
    };
    private readonly Label _insertChamferNote = new()
    {
        Text = "入口 45°×0.5 mm ⓘ",
        TextColor = FastenerUiTheme.SecondaryText,
        Wrap = WrapMode.None,
        ToolTip = "入口导角固定为 45°×0.5 mm；最终切割深度 = 热熔螺母长度 + 深度补偿。补偿超过宿主背面时将自然贯穿。"
    };
    private readonly Panel _insertNoteHost = new() { MinimumSize = new Size(0, 0) };
    private readonly Button _zeroHeadButton = new() { Text = "0", Height = FastenerUiTheme.ControlHeight, Width = 36 };
    private readonly Button _flushHeadButton = new() { Text = "齐平", Height = FastenerUiTheme.ControlHeight, Width = 52 };
    private readonly StackLayout _headEmbedControl;
    private readonly NumericStepper _printerCorrection = new() { MinValue = -2, MaxValue = 2, DecimalPlaces = 3, Increment = 0.05 };
    private readonly NumericStepper _bite = new() { MinValue = 0, MaxValue = 5, DecimalPlaces = 3, Increment = 0.05 };
    private readonly Label _holeDiameterSummary = new()
    {
        TextColor = FastenerUiTheme.SecondaryText,
        Wrap = WrapMode.None
    };
    private readonly CardSelector _holeContext = new();
    private readonly Panel _holeContextHost = new() { Width = 148 };
    private readonly DropDown _presetDepth = new();
    private readonly NumericStepper _presetBlindDepth = new()
    {
        MinValue = -1000,
        MaxValue = 1000,
        DecimalPlaces = 2,
        Increment = 0.5,
        ToolTip = "孔底 = 嵌入深度 + 螺杆长度 + 追加深度；兼容旧浅盲孔时允许负值。"
    };
    private readonly CheckBox _presetClearancePreview = new() { Text = "预览" };
    private readonly CheckBox _presetClearanceBoolean = new() { Text = "布尔" };
    private readonly CheckBox _presetEngagementPreview = new() { Text = "预览" };
    private readonly CheckBox _presetEngagementBoolean = new() { Text = "布尔" };
    private readonly CheckBox _counterboreBridgeEnabled = new()
    {
        Text = "悬垂沉孔架桥"
    };
    private readonly NumericStepper _counterboreBridgeLayerHeight = new()
    {
        MinValue = 0.05,
        MaxValue = 1.0,
        DecimalPlaces = 2,
        Increment = 0.02,
        Value = 0.2
    };
    private readonly Label _counterboreBridgeSummary = new()
    {
        Text = "每层 0.20 mm · 总高 0.40 mm",
        Wrap = WrapMode.Word
    };
    private readonly CheckBox _engagementEntryChamferEnabled = new()
    {
        Text = "咬合孔入口倒角",
        ToolTip = "仅用于螺纹咬合模式；沿螺丝轴生成45°直线锥口。"
    };
    private readonly NumericStepper _engagementEntryChamferSize = new()
    {
        MinValue = 0.05,
        MaxValue = 5.0,
        DecimalPlaces = 2,
        Increment = 0.05,
        Value = 0.5,
        ToolTip = "轴向45°模式表示最深入口处的最小倒角；表面等距模式表示宿主表面和孔壁各退让 C。"
    };
    private readonly NumericStepper _engagementOnlyAlignmentDepth = new()
    {
        MinValue = 0,
        MaxValue = 1000,
        DecimalPlaces = 2,
        Increment = 0.5,
        Value = 3,
        ToolTip = "从完整孔口圆周的最深入口起算；0 表示关闭顶部对位孔。"
    };
    private readonly NumericStepper _engagementOnlyAlignmentCompensation = new()
    {
        MinValue = 0,
        MaxValue = 5,
        DecimalPlaces = 2,
        Increment = 0.05,
        Value = 0.2,
        ToolTip = "对位孔直径 = 螺纹公称直径 + 此正补偿 + 宿主附加修正。"
    };
    private readonly Label _engagementOnlyAlignmentSummary = new()
    {
        TextColor = FastenerUiTheme.SecondaryText,
        Wrap = WrapMode.Word
    };
    private readonly CardSelector _engagementEntryChamferMode = new();
    private readonly Label _engagementEntryChamferSummary = new()
    {
        Text = "45°直线倒角 · C0.5",
        Wrap = WrapMode.None
    };
    private readonly CardSelector _assemblyMode = new();
    private readonly NumericStepper _nutTipProtrusion = new()
    {
        MinValue = 0,
        MaxValue = 1000,
        DecimalPlaces = 2,
        Increment = 0.5,
        Value = 2
    };
    private readonly NumericStepper _nutPocketCompensation = new()
    {
        MinValue = -20,
        MaxValue = 20,
        DecimalPlaces = 2,
        Increment = 0.05,
        Value = 0.2
    };
    private readonly CheckBox _pairedNutLocking = new()
    {
        Text = "尼龙防松螺母",
        ToolTip = "关闭时使用普通六角螺母；开启后使用尼龙防松螺母尺寸。"
    };
    private readonly CheckBox _presetNutPreview = new() { Text = "预览" };
    private readonly CheckBox _presetNutBoolean = new() { Text = "布尔" };
    private readonly Label _pairedNutSummary = new()
    {
        TextColor = FastenerUiTheme.SecondaryText,
        Wrap = WrapMode.Word
    };
    private readonly Label _summary = new()
    {
        Text = "模板 · 未设置",
        Font = new Font(SystemFont.Bold, FastenerUiMetrics.SummaryFontSize),
        TextColor = FastenerUiTheme.PrimaryText,
        Wrap = WrapMode.None,
        Height = 20
    };
    private readonly Label _selectionSummary = new()
    {
        Text = "目标 · 未选择更新目标",
        Wrap = WrapMode.None,
        Height = 18,
        TextColor = FastenerUiTheme.SecondaryText
    };
    private readonly Label _status = new()
    {
        Text = string.Empty,
        Wrap = WrapMode.None,
        Height = 18,
        TextColor = FastenerUiTheme.SecondaryText,
        Visible = false
    };
    private readonly Button _favoriteTemplateButton = new()
    {
        Text = "☆",
        Width = 34,
        Height = FastenerUiTheme.ControlHeight,
        ToolTip = "收藏当前面板模板"
    };
    private readonly Button _templateMenuButton = new()
    {
        Text = "模板 ▾",
        Width = 70,
        Height = FastenerUiTheme.ControlHeight,
        ToolTip = "最近使用与收藏模板"
    };
    private readonly CheckBox _quickEditorToggle = new()
    {
        Text = "快速小窗",
        ToolTip = "单选控制点后自动显示紧凑快速编辑器"
    };
    private readonly Panel _templateStripHost = new() { MinimumSize = new Size(0, 0) };
    private readonly DynamicLayout _parameterLayout = new() { Spacing = new Size(FastenerUiMetrics.SpaceCompact, FastenerUiMetrics.SpaceCompact) };
    private readonly DynamicLayout _assemblyLayout = new() { Spacing = new Size(FastenerUiMetrics.SpaceSmall, FastenerUiMetrics.SpaceSmall) };
    private readonly DynamicLayout _holeHeaderLayout = new() { Spacing = new Size(FastenerUiMetrics.SpaceSmall, FastenerUiMetrics.SpaceSmall) };
    private readonly DynamicLayout _holeParameterLayout = new() { Spacing = new Size(FastenerUiMetrics.SpaceCompact, FastenerUiMetrics.SpaceCompact) };
    private readonly DynamicLayout _presetOptionsLayout = new() { Spacing = new Size(FastenerUiMetrics.SpaceSmall, FastenerUiMetrics.SpaceSmall) };
    private readonly Label _dimensionTitle = FastenerUiTheme.SectionTitle("基础尺寸");
    private readonly Label _assemblyTitle = FastenerUiTheme.SectionTitle("装配方式");
    private readonly Label _holeTitle = FastenerUiTheme.SectionTitle("孔与切割");
    private readonly Scrollable _scrollable;
    private readonly Button _applyButton = new() { Height = FastenerUiTheme.ActionButtonHeight, Enabled = true };
    private readonly Button _placeButton = new() { Height = FastenerUiTheme.ActionButtonHeight };
    private readonly Button _moreButton = new() { Height = FastenerUiTheme.ActionButtonHeight };
    private readonly Panel _actionsHost = new();
    private readonly Panel _statusHost = new() { Visible = false };
    private readonly List<Button> _actionButtons = [];
    private readonly Dictionary<Button, PanelActionIcon> _actionButtonIcons = [];
    private readonly Dictionary<Button, FastenerUiActionDescriptor> _actionDescriptors = [];
    private readonly UITimer _selectionTimer = new() { Interval = 0.06 };
    private readonly UITimer _successStatusTimer = new() { Interval = 1.2 };
    private FastenerComponentData? _loadedComponent;
    private IReadOnlyList<FastenerComponentData> _loadedComponents = [];
    private SelectedComponentSummary? _selectedSummary;
    private bool? _clearancePreviewOverride;
    private bool? _clearanceBooleanOverride;
    private bool? _engagementPreviewOverride;
    private bool? _engagementBooleanOverride;
    private bool? _installationPreviewOverride;
    private bool? _installationBooleanOverride;
    private bool? _nutPocketPreviewOverride;
    private bool? _nutPocketBooleanOverride;
    private bool _loadingControls;
    private bool _geometryDirty;
    private bool _applyInProgress;
    private HoleEditingContext _holeEditingContext = HoleEditingContext.PlacementPreset;
    private ResponsiveLayoutProfile? _layoutProfile;
    private StatusKind _statusKind = StatusKind.Info;
    private FastenerThemePalette? _appliedTheme;

    public RhinoMMPanel()
    {
        _currentPanel = new WeakReference<RhinoMMPanel>(this);
        _successStatusTimer.Elapsed += (_, _) =>
        {
            _successStatusTimer.Stop();
            if (_statusKind == StatusKind.Success)
            {
                _status.Text = string.Empty;
                _status.ToolTip = null;
                _status.Visible = false;
                _statusHost.Visible = false;
            }
        };
        Style = Panels.EtoPanelStyleName;
        FastenerUiTheme.RefreshPalette();
        FastenerUiTheme.SetRole(this, FastenerThemeRole.Canvas);
        FastenerUiTheme.SetRole(_insertFinalDiameter, FastenerThemeRole.SecondaryText);
        FastenerUiTheme.SetRole(_nutStandardDimensions, FastenerThemeRole.SecondaryText);
        FastenerUiTheme.SetRole(_insertChamferNote, FastenerThemeRole.SecondaryText);
        FastenerUiTheme.SetRole(_counterboreBridgeSummary, FastenerThemeRole.SecondaryText);
        FastenerUiTheme.SetRole(_pairedNutSummary, FastenerThemeRole.SecondaryText);
        FastenerUiTheme.SetRole(_holeDiameterSummary, FastenerThemeRole.SecondaryText);
        FastenerUiTheme.SetRole(_summary, FastenerThemeRole.PrimaryText);
        FastenerUiTheme.SetRole(_selectionSummary, FastenerThemeRole.SecondaryText);
        FastenerUiTheme.SetRole(_status, FastenerThemeRole.SecondaryText);
        FastenerUiTheme.SetRole(_quickEditorToggle, FastenerThemeRole.PrimaryText);
        FastenerUiTheme.ApplySecondary(_zeroHeadButton);
        FastenerUiTheme.ApplySecondary(_flushHeadButton);
        FastenerUiTheme.ApplySecondary(_favoriteTemplateButton);
        FastenerUiTheme.ApplySecondary(_templateMenuButton);
        _insertSummaryHost.Content = _insertFinalDiameter;
        _insertNoteHost.Content = _insertChamferNote;
        _quickEditorToggle.Checked = ViewportQuickEditorService.AutoShowEnabled;
        _headEmbedControl = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = FastenerUiTheme.SpaceSmall,
            Items =
            {
                new StackLayoutItem(_headEmbed, true),
                new StackLayoutItem(_zeroHeadButton),
                new StackLayoutItem(_flushHeadButton)
            }
        };
        foreach (var value in Enum.GetValues<FastenerKind>())
            _kind.Add(value.ToString(), KindCardText(value), FastenerLabels.Kind(value));
        foreach (var spec in RhinoMMPlugIn.Catalog.Sizes)
            _size.Add(spec.Designation, spec.Designation);
        foreach (var value in CommonLengths)
            _lengthCards.Add(value.ToString(System.Globalization.CultureInfo.InvariantCulture), value.ToString());
        _assemblyMode.Add(ScrewAssemblyMode.ThreadEngagement.ToString(), "螺纹咬合", "通孔 + 最终咬合宿主");
        _assemblyMode.Add(ScrewAssemblyMode.EngagementOnly.ToString(), "只咬合", "单一宿主加强筋模式");
        _assemblyMode.Add(ScrewAssemblyMode.NutFastened.ToString(), "螺母固定", "全部正补偿通孔 + 末端螺母槽");
        _assemblyMode.SetColumns(3);
        _assemblyMode.Select(ScrewAssemblyMode.ThreadEngagement.ToString(), false);
        _engagementEntryChamferMode.Add(
            EngagementEntryChamferMode.AxialFortyFive.ToString(),
            "轴向45°",
            "沿螺丝轴生成45°锥形导入口，支持平面和连续曲面入口。");
        _engagementEntryChamferMode.Add(
            EngagementEntryChamferMode.SurfaceEqualDistance.ToString(),
            "表面等距",
            "宿主表面和孔壁分别退让相同 C 值；仅支持单一连续平面入口。");
        _engagementEntryChamferMode.SetColumns(2);
        _engagementEntryChamferMode.Select(
            EngagementEntryChamferMode.AxialFortyFive.ToString(),
            false);
        _holeContext.Add(HoleEditingContext.CurrentComponent.ToString(), "当前组件");
        _holeContext.Add(HoleEditingContext.PlacementPreset.ToString(), "放置预设");
        _holeContext.SetColumns(2);
        _holeContext.SelectedKey = HoleEditingContext.PlacementPreset.ToString();
        _holeContextHost.Content = _holeContext;
        foreach (var mode in EngagementDepthModes)
            _presetDepth.Items.Add(new ListItem { Key = mode.ToString(), Text = FastenerLabels.Depth(mode) });
        LoadPlacementPresetControls();
        var parameterSurface = FastenerUiTheme.CreateCard(new StackLayout
        {
            Orientation = Orientation.Vertical,
            Spacing = FastenerUiMetrics.SpaceCompact,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Items =
            {
                _dimensionTitle,
                _parameterLayout,
                CreateSectionDivider(),
                _assemblyTitle,
                _assemblyLayout,
                CreateSectionDivider(),
                _holeHeaderLayout,
                _holeParameterLayout,
                _presetOptionsLayout
            }
        }, FastenerUiMetrics.SpaceCompact);
        var scrollingContent = new DynamicLayout
        {
            Padding = new Padding(0, 2, 0, FastenerUiMetrics.SpaceSmall),
            Spacing = new Size(0, FastenerUiTheme.SpaceSmall)
        };
        scrollingContent.AddRow(parameterSurface);

        _scrollable = new Scrollable
        {
            Border = BorderType.None,
            ExpandContentWidth = true,
            ExpandContentHeight = false,
            Content = scrollingContent
        };
        FastenerUiTheme.SetRole(_scrollable, FastenerThemeRole.Canvas);

        var actions = BuildActions();
        RebuildTemplateStripLayout(ResponsiveLayoutProfile.CompactBreakpoint, force: true);
        var summaryCard = FastenerUiTheme.CreateCard(new StackLayout
        {
            Orientation = Orientation.Vertical,
            Spacing = FastenerUiMetrics.SpaceTight,
            Items = { _summary, _selectionSummary, _templateStripHost }
        }, FastenerUiMetrics.SpaceCompact);
        _statusHost.Content = FastenerUiTheme.CreateCard(_status, FastenerUiMetrics.SpaceSmall);
        var rootLayout = new TableLayout
        {
            Padding = new Padding(FastenerUiMetrics.SpaceCompact, FastenerUiMetrics.SpaceSmall),
            Spacing = new Size(0, FastenerUiTheme.SpaceSmall),
            Rows =
            {
                new TableRow(summaryCard),
                new TableRow(_scrollable) { ScaleHeight = true },
                new TableRow(_statusHost),
                new TableRow(actions)
            }
        };
        FastenerUiTheme.SetRole(rootLayout, FastenerThemeRole.Canvas);
        Content = rootLayout;

        SizeChanged += (_, _) => RebuildResponsiveLayout();
        _scrollable.SizeChanged += (_, _) => RebuildResponsiveLayout();
        RhinoApp.AppSettingsChanged += RhinoAppSettingsChanged;
        RhinoDoc.SelectObjects += DocumentSelectionChanged;
        RhinoDoc.DeselectAllObjects += DocumentSelectionCleared;
        ComponentEditorSession.ActiveSelectionChanged += SessionSelectionChanged;
        FastenerTemplateLibraryService.Changed += TemplateLibraryChanged;
        FastenerLocalizationService.Register(this);
        WireEvents();
        LoadControls();
        RebuildResponsiveLayout(force: true);
        UpdateHoleContextPresentation();
        SynchronizeSelectedTargets();
        UpdateSummary();
        ApplyTheme();
    }

    public void PanelShown(uint documentSerialNumber, ShowPanelReason reason)
    {
        ApplyTheme();
        _quickEditorToggle.Checked = ViewportQuickEditorService.AutoShowEnabled;
        var doc = RhinoDoc.ActiveDoc;
        if (doc is null)
            return;
        if (ComponentEditorSession.TryGetActiveSet(
                doc,
                out var activeComponents,
                out var intent)
            && intent == ComponentActivationIntent.LoadIntoEditor)
            ActivateComponents(activeComponents);
        SynchronizeSelectedTargets();
    }

    public void PanelHidden(uint documentSerialNumber, ShowPanelReason reason) { }

    public void PanelClosing(uint documentSerialNumber, bool onCloseDocument)
    {
        _selectionTimer.Stop();
        _successStatusTimer.Stop();
        RhinoApp.AppSettingsChanged -= RhinoAppSettingsChanged;
        RhinoDoc.SelectObjects -= DocumentSelectionChanged;
        RhinoDoc.DeselectAllObjects -= DocumentSelectionCleared;
        ComponentEditorSession.ActiveSelectionChanged -= SessionSelectionChanged;
        FastenerTemplateLibraryService.Changed -= TemplateLibraryChanged;
        FastenerLocalizationService.Unregister(this);
    }

    private void RhinoAppSettingsChanged(object? sender, EventArgs e) =>
        Application.Instance.AsyncInvoke(ApplyTheme);

    private void ApplyTheme()
    {
        FastenerUiTheme.RefreshPalette();
        FastenerUiLocalization.ApplyTree(this);
        RebuildTemplateStripLayout(AvailableContentWidth(), force: true);
        if (_appliedTheme == FastenerUiTheme.Palette)
            return;
        _appliedTheme = FastenerUiTheme.Palette;
        FastenerUiTheme.ApplyTree(this);
        _kind.RefreshTheme();
        _size.RefreshTheme();
        _lengthCards.RefreshTheme();
        _assemblyMode.RefreshTheme();
        _engagementEntryChamferMode.RefreshTheme();
        _holeContext.RefreshTheme();
        foreach (var button in _actionButtons)
            ApplyActionButtonStyle(button);
        ApplyStatusTheme();
        Invalidate();
    }

    private void WireEvents()
    {
        _selectionTimer.Elapsed += (_, _) =>
        {
            _selectionTimer.Stop();
            SynchronizeSelectedTargets();
        };
        _kind.SelectedKeyChanged += (_, _) =>
        {
            var selectedKind = SelectedKind();
            var preserveFullEmbed = selectedKind == FastenerKind.HexNut
                && EditorState.Current.Kind == FastenerKind.HexNut
                && IsCurrentHexNutFullEmbed();
            UpdateHexNutSizeAvailability();
            ApplyEmbedDefaultForKindChange();
            if (preserveFullEmbed)
                _headEmbed.Value = CurrentHexNutHeight();
            LoadHeatSetDefaultsForNewKind();
            UpdateHeadEmbedControls();
            RebuildResponsiveLayout(force: true);
            UpdateInsertSummary();
            UpdateHoleDiameterSummary();
            UpdateEngagementOnlyAlignmentSummary();
            UpdatePairedNutSummary();
            ScheduleUpdate();
        };
        _nylonLockingNut.CheckedChanged += (_, _) => HexNutStyleChanged();
        _size.SelectedKeyChanged += (_, _) =>
        {
            if (!_loadingControls
                && EditorState.Current.CustomDefinitionSnapshot is not null
                && !string.Equals(_size.SelectedKey, EditorState.Current.CustomDefinitionSnapshot.ThreadDesignation, StringComparison.OrdinalIgnoreCase))
            {
                EditorState.Current.CustomDefinitionId = null;
                EditorState.Current.CustomDefinitionName = string.Empty;
                EditorState.Current.CustomDefinitionSnapshot = null;
            }
            PreserveHexNutEmbedModeAcrossSizeChange();
            UpdateInsertSummary();
            UpdateHoleDiameterSummary();
            UpdateEngagementOnlyAlignmentSummary();
            UpdatePairedNutSummary();
            ScheduleUpdate();
        };
        _lengthCards.SelectedKeyChanged += (_, _) => SelectCommonLength();
        _length.ValueChanged += (_, _) =>
        {
            UpdateLengthCardSelection();
            SaveHeatSetPresetControls();
            ScheduleUpdate();
        };
        _headEmbed.ValueChanged += (_, _) =>
        {
            if (!_loadingControls
                && _headEmbed.Value <= 0
                && _counterboreBridgeEnabled.Checked == true)
                _counterboreBridgeEnabled.Checked = false;
            _counterboreBridgeEnabled.Enabled = SelectedKind() == FastenerKind.SocketCap
                && _headEmbed.Value > 0;
            UpdateInsertSummary();
            ScheduleUpdate();
        };
        _insertOuterDiameter.ValueChanged += (_, _) =>
        {
            UpdateInsertSummary();
            SaveHeatSetPresetControls();
            ScheduleUpdate();
        };
        _insertDiameterCompensation.ValueChanged += (_, _) =>
        {
            UpdateInsertSummary();
            SaveHeatSetPresetControls();
            ScheduleUpdate();
        };
        _insertDepthCompensation.ValueChanged += (_, _) =>
        {
            UpdateInsertSummary();
            SaveHeatSetPresetControls();
            ScheduleUpdate();
        };
        _printerCorrection.ValueChanged += (_, _) => HoleCommonParameterChanged();
        _bite.ValueChanged += (_, _) => HoleCommonParameterChanged();
        _holeContext.SelectedKeyChanged += (_, _) => HoleContextChanged();
        _presetDepth.SelectedIndexChanged += (_, _) => EngagementDepthChanged(rebuildLayout: true);
        _presetBlindDepth.ValueChanged += (_, _) => EngagementDepthChanged(rebuildLayout: false);
        _presetClearancePreview.CheckedChanged += (_, _) =>
            TopLevelModuleToggleChanged(ShaftFitRole.Clearance, true);
        _presetClearanceBoolean.CheckedChanged += (_, _) =>
            TopLevelModuleToggleChanged(ShaftFitRole.Clearance, false);
        _presetEngagementPreview.CheckedChanged += (_, _) =>
            TopLevelModuleToggleChanged(ShaftFitRole.ThreadEngagement, true);
        _presetEngagementBoolean.CheckedChanged += (_, _) =>
            TopLevelModuleToggleChanged(ShaftFitRole.ThreadEngagement, false);
        _presetNutPreview.CheckedChanged += (_, _) =>
            TopLevelModuleToggleChanged(ShaftFitRole.NutPocket, true);
        _presetNutBoolean.CheckedChanged += (_, _) =>
            TopLevelModuleToggleChanged(ShaftFitRole.NutPocket, false);
        _counterboreBridgeEnabled.CheckedChanged += (_, _) =>
            CounterboreBridgeChanged(rebuildLayout: true);
        _counterboreBridgeLayerHeight.ValueChanged += (_, _) =>
            CounterboreBridgeChanged(rebuildLayout: false);
        _engagementEntryChamferEnabled.CheckedChanged += (_, _) =>
            EngagementEntryChamferChanged(rebuildLayout: true);
        _engagementEntryChamferMode.SelectedKeyChanged += (_, _) =>
            EngagementEntryChamferChanged(rebuildLayout: false);
        _engagementEntryChamferSize.ValueChanged += (_, _) =>
            EngagementEntryChamferChanged(rebuildLayout: false);
        _engagementOnlyAlignmentDepth.ValueChanged += (_, _) =>
            EngagementOnlyAlignmentChanged();
        _engagementOnlyAlignmentCompensation.ValueChanged += (_, _) =>
            EngagementOnlyAlignmentChanged();
        _assemblyMode.SelectedKeyChanged += (_, _) => AssemblyModeChanged();
        _pairedNutLocking.CheckedChanged += (_, _) => PairedNutParameterChanged(rebuildLayout: false);
        _nutTipProtrusion.ValueChanged += (_, _) => PairedNutParameterChanged(rebuildLayout: false);
        _nutPocketCompensation.ValueChanged += (_, _) => PairedNutParameterChanged(rebuildLayout: false);
        _zeroHeadButton.Click += (_, _) => _headEmbed.Value = 0;
        _flushHeadButton.Click += (_, _) => SetFlushHeadDepth();
        _favoriteTemplateButton.Click += (_, _) => FavoriteCurrentTemplate();
        _templateMenuButton.Click += (_, _) => ShowTemplateMenu(_templateMenuButton);
        _quickEditorToggle.CheckedChanged += (_, _) =>
            ViewportQuickEditorService.SetAutoShowEnabled(_quickEditorToggle.Checked == true);
    }

    private void TemplateLibraryChanged(object? sender, EventArgs e) =>
        Application.Instance.AsyncInvoke(() => UpdateTemplateStrip());

    private Control BuildActions()
    {
        var placeAction = FastenerUiActionCoordinator.Get(FastenerUiActionId.Place);
        ConfigureActionButton(
            _placeButton,
            placeAction,
            (_, _) => Run(placeAction.PrimaryCommand));
        _placeButton.MouseDown += (_, e) =>
        {
            if (!e.Buttons.HasFlag(MouseButtons.Alternate))
                return;
            e.Handled = true;
            Application.Instance.AsyncInvoke(() => Run(placeAction.AlternateCommand!));
        };
        var applyAction = FastenerUiActionCoordinator.Get(FastenerUiActionId.ApplyUpdate);
        ConfigureActionButton(
            _applyButton,
            applyAction,
            (_, _) => ApplyLoaded());
        var maintenanceAction = FastenerUiActionCoordinator.Get(FastenerUiActionId.Maintenance);
        var refresh = MakeActionButton(
            maintenanceAction,
            (_, _) => RefreshDocument());
        var rhinoAction = FastenerUiActionCoordinator.Get(FastenerUiActionId.ExportRhino);
        var toRhino = MakeActionButton(
            rhinoAction,
            (_, _) => RunExport(rhinoAction.PrimaryCommand));
        toRhino.MouseDown += (_, e) =>
        {
            if (!e.Buttons.HasFlag(MouseButtons.Alternate))
                return;
            e.Handled = true;
            Application.Instance.AsyncInvoke(() =>
                RunExport(rhinoAction.AlternateCommand!));
        };
        var stepAction = FastenerUiActionCoordinator.Get(FastenerUiActionId.ExportStep);
        var step = MakeActionButton(
            stepAction,
            (_, _) => RunExport(stepAction.PrimaryCommand));
        var statisticsAction = FastenerUiActionCoordinator.Get(FastenerUiActionId.Statistics);
        var statistics = MakeActionButton(
            statisticsAction,
            (_, _) => Run(statisticsAction.PrimaryCommand));
        var inspectorAction = FastenerUiActionCoordinator.Get(FastenerUiActionId.AssemblyInspector);
        var inspector = MakeActionButton(
            inspectorAction,
            (_, _) => Run(inspectorAction.PrimaryCommand));
        var moreAction = FastenerUiActionCoordinator.Get(FastenerUiActionId.More);
        ConfigureActionButton(
            _moreButton,
            moreAction,
            (_, _) => ShowMoreMenu(_moreButton));
        _actionButtons.AddRange([
            _placeButton, _applyButton, refresh,
            toRhino, step, statistics,
            inspector, _moreButton
        ]);
        RebuildActionLayout();
        UpdatePrimaryActionStyle();
        return FastenerUiTheme.CreateCard(_actionsHost, FastenerUiTheme.SpaceSmall);
    }

    private void RebuildActionLayout()
    {
        var cells = new List<TableCell>();
        FastenerUiActionGroup? previousGroup = null;
        for (var index = 0; index < _actionButtons.Count; index++)
        {
            var button = _actionButtons[index];
            if (_actionDescriptors.TryGetValue(button, out var descriptor)
                && previousGroup is not null
                && descriptor.Group != previousGroup)
            {
                var separator = FastenerUiTheme.Register(new Panel { Width = 1 }, FastenerThemeRole.CardBorder);
                cells.Add(new TableCell(new Panel
                {
                    Width = FastenerUiMetrics.SpaceSmall,
                    Padding = new Padding(1, FastenerUiMetrics.SpaceSmall),
                    Content = separator
                }));
            }
            cells.Add(new TableCell(button, true));
            if (descriptor is not null)
                previousGroup = descriptor.Group;
        }
        var grid = new TableLayout { Spacing = new Size(1, 0) };
        grid.Rows.Add(new TableRow(cells));
        _actionsHost.Content = grid;
    }

    private void UpdatePrimaryActionStyle()
    {
        foreach (var button in _actionButtons)
            ApplyActionButtonStyle(button);
    }

    private void RefreshDocument()
    {
        if (RhinoDoc.ActiveDoc is not { } doc)
        {
            SetStatus("当前没有可刷新的 Rhino 文档。", StatusKind.Warning);
            return;
        }

        MaintenanceCenterDialog.Show(doc);
        SynchronizeSelectedTargets();
    }

    internal static void OpenMoreMenu()
    {
        Panels.OpenPanel(typeof(RhinoMMPanel).GUID);
        Application.Instance.AsyncInvoke(() =>
        {
            if (_currentPanel is not null
                && _currentPanel.TryGetTarget(out var panel))
                panel.ShowMoreMenu(panel._moreButton);
            else
                RhinoMM.Plugin.Services.FastenerCommandText.WriteLine("无法打开参数化紧固件的“更多”菜单。");
        });
    }

    internal static bool ApplyCurrentTemplateToSelection(out string message)
    {
        if (_currentPanel is null || !_currentPanel.TryGetTarget(out var panel))
        {
            message = "主面板尚未打开，无法读取当前创建模板。";
            return false;
        }
        var success = panel.ApplyLoaded();
        message = success ? "已将主面板模板应用到当前选择。" : "主面板模板更新未完成；请查看面板状态或 Rhino 命令行。";
        return success;
    }

    internal static void LoadExternalTemplate(FastenerTemplateData data, string name)
    {
        Panels.OpenPanel(typeof(RhinoMMPanel).GUID);
        Application.Instance.AsyncInvoke(() =>
        {
            if (_currentPanel is not null && _currentPanel.TryGetTarget(out var panel))
                panel.ApplyTemplateData(data, name);
        });
    }

    private void ShowMoreMenu(Control owner)
    {
        FastenerActionPalette.Show(owner,
        [
            new FastenerPaletteActionDescriptor("编辑", "读取选中组件参数", "读取 模板 参数", LoadSelection),
            new("编辑", "参数手柄", "长度 嵌入 旋转 露出", () => Run("_-ParametricFastenersEditHandles")),
            new("编辑", "重复上一放置", "重复 放置", () => Run("_-ParametricFastenersRepeatPlace")),
            new("编辑", "重复上一更新", "重复 更新", () => Run("_-ParametricFastenersRepeatUpdate")),
            new("编辑", "长度按宿主自适应更新", "批量 长度 宿主 自适应", () => Run("_-ParametricFastenersAdaptiveUpdate")),
            new("检查", "组件导航器与问题中心", "导航 问题 健康", () => Run("_-ParametricFastenersNavigator")),
            new("检查", "装配检查器", "长度 建议 碰撞 壁厚", () => Run("_-ParametricFastenersAssemblyInspector")),
            new("检查", "智能装配建议", "测量 长度 装配方式 自适应", () => Run("_-ParametricFastenersAssemblySuggestions")),
            new("检查", "维护中心", "刷新 重绑 清理 修复", () => Run("_-ParametricFastenersRefresh")),
            new("检查", "校验文档", "校验 验证", () => Run("_-ParametricFastenersValidate")),
            new("输出", "输出中心", "3DM STEP STL Excel CSV", () => Run("_-ParametricFastenersOutputCenter")),
            new("输出", "导出 STL", "打印 网格", () => RunExport("_-ParametricFastenersExportStl")),
            new("管理", "模板管理", "最近 收藏 导入 导出", FastenerTemplateManagerDialog.Show),
            new("管理", "装配方案管理", "收藏 方案 自适应 输出", FastenerTemplateManagerDialog.Show),
            new("管理", "自定义标准件库 Beta", "规格 标准件 JSON CSV", () => Run("_-ParametricFastenersCustomLibrary")),
            new("管理", "转换选中模型", "迁移 旧模型", () => Run("_-ParametricFastenersAdopt")),
            new("设置", "全局显示", "透明度 本体 切割", () => GlobalDisplaySettingsDialog.Show(RhinoDoc.ActiveDoc)),
            new("设置", LanguageChoice(FastenerLanguageMode.Auto, "语言 · 自动（跟随 Rhino）"), "语言 中文 English language", () => SetLanguage(FastenerLanguageMode.Auto)),
            new("设置", LanguageChoice(FastenerLanguageMode.SimplifiedChinese, "语言 · 简体中文"), "语言 中文 Chinese", () => SetLanguage(FastenerLanguageMode.SimplifiedChinese)),
            new("设置", LanguageChoice(FastenerLanguageMode.English, "语言 · English"), "语言 英文 English", () => SetLanguage(FastenerLanguageMode.English)),
            new("设置", $"关于参数化紧固件 · v{FastenerVersionInfo.PluginVersion}", "版本 安装 路径", FastenerAboutDialog.Show)
        ]);
    }

    private static string LanguageChoice(FastenerLanguageMode mode, string label) =>
        FastenerLocalizationService.Mode == mode ? $"● {label}" : label;

    private void SetLanguage(FastenerLanguageMode mode)
    {
        FastenerLocalizationService.SetMode(mode, out var message);
        SetStatus(message, StatusKind.Success);
    }

    public void ApplyLocalization()
    {
        RebuildResponsiveLayout(force: true);
        UpdateSummary();
        UpdateTemplateStrip();
        FastenerUiLocalization.ApplyTree(this);
        RebuildTemplateStripLayout(AvailableContentWidth(), force: true);
        Invalidate();
    }

    private void ShowTemplateMenu(Control owner)
    {
        var menu = new ContextMenu();
        if (FastenerTemplateLibraryService.Current.Recent.Count > 0)
        {
            menu.Items.Add(new ButtonMenuItem { Text = "最近使用", Enabled = false });
            foreach (var entry in FastenerTemplateLibraryService.Current.Recent)
            {
                var item = new ButtonMenuItem { Text = entry.Name };
                item.Click += (_, _) =>
                {
                    FastenerTemplateLibraryService.ActivateScheme(entry.Scheme);
                    ApplyTemplateData(entry.Data, entry.Name);
                };
                menu.Items.Add(item);
            }
        }
        if (FastenerTemplateLibraryService.Current.Favorites.Count > 0)
        {
            if (menu.Items.Count > 0)
                menu.Items.Add(new SeparatorMenuItem());
            menu.Items.Add(new ButtonMenuItem { Text = "收藏", Enabled = false });
            foreach (var entry in FastenerTemplateLibraryService.Current.Favorites)
            {
                var item = new ButtonMenuItem { Text = $"★ {entry.Name}" };
                item.Click += (_, _) =>
                {
                    FastenerTemplateLibraryService.ActivateScheme(entry.Scheme);
                    ApplyTemplateData(entry.Data, entry.Name);
                };
                menu.Items.Add(item);
            }
        }
        if (menu.Items.Count > 0)
            menu.Items.Add(new SeparatorMenuItem());
        var manage = new ButtonMenuItem { Text = "管理模板…" };
        manage.Click += (_, _) => FastenerTemplateManagerDialog.Show();
        menu.Items.Add(manage);
        menu.Show(owner);
    }

    private void FavoriteCurrentTemplate()
    {
        SaveControls();
        var data = FastenerTemplateData.FromUpdateTemplate(CaptureUpdateTemplate());
        if (!FastenerTemplateLibraryService.AddFavorite(null, data, out var entry, out var message))
        {
            SetStatus(message, StatusKind.Warning);
            return;
        }
        SetStatus($"已收藏模板“{entry.Name}”。", StatusKind.Success);
        UpdateTemplateStrip();
    }

    private void ApplyTemplateData(FastenerTemplateData data, string? name = null)
    {
        data = data.Normalize();
        if (!FastenerTemplateLibraryService.TryValidate(data, out var validationMessage))
        {
            SetStatus(validationMessage, StatusKind.Error);
            return;
        }
        var clearancePreview = data.ClearancePreviewVisible ?? true;
        var clearanceBoolean = data.ClearanceBooleanEnabled ?? true;
        var engagementPreview = data.EngagementPreviewVisible ?? true;
        var engagementBoolean = data.EngagementBooleanEnabled ?? true;
        var nutPreview = data.NutPocketPreviewVisible ?? true;
        var nutBoolean = data.NutPocketBooleanEnabled ?? true;
        var preset = new PlacementCutterPreset(
            data.PrinterCorrection,
            ClearanceFitClass.Normal,
            data.BiteReduction,
            data.CounterboreBridgeEnabled,
            data.CounterboreBridgeLayerHeight,
            data.EngagementDepthMode,
            data.EngagementBlindDepth,
            clearancePreview,
            clearanceBoolean,
            engagementPreview,
            engagementBoolean,
            data.AssemblyMode == ScrewAssemblyMode.EngagementOnly,
            data.AssemblyMode,
            data.PairedNutStyle,
            data.NutTipProtrusion,
            data.NutPocketCompensation,
            nutPreview,
            nutBoolean,
            data.EngagementEntryChamferEnabled,
            data.EngagementEntryChamferSize,
            data.EngagementEntryChamferMode,
            data.EngagementOnlyAlignmentDepth,
            data.EngagementOnlyAlignmentDiameterCompensation);
        if (RhinoMMPlugIn.Instance is { } plugin)
        {
            PlacementPresetService.Save(plugin.Settings, preset, out _);
            if (data.Kind == FastenerKind.HeatSetInsert)
            {
                HeatSetInsertPresetService.Save(
                    plugin.Settings,
                    new HeatSetInsertPreset(
                        data.Length,
                        data.InsertOuterDiameter,
                        data.InsertDiameterCompensation,
                        data.InsertDepthCompensation,
                        data.InstallationPreviewVisible ?? true,
                        data.InstallationBooleanEnabled ?? true),
                    out _);
            }
        }
        EditorState.Current.LoadTemplate(data);
        _clearancePreviewOverride = data.ClearancePreviewVisible;
        _clearanceBooleanOverride = data.ClearanceBooleanEnabled;
        _engagementPreviewOverride = data.EngagementPreviewVisible;
        _engagementBooleanOverride = data.EngagementBooleanEnabled;
        _installationPreviewOverride = data.InstallationPreviewVisible;
        _installationBooleanOverride = data.InstallationBooleanEnabled;
        _nutPocketPreviewOverride = data.NutPocketPreviewVisible;
        _nutPocketBooleanOverride = data.NutPocketBooleanEnabled;
        _holeEditingContext = HoleEditingContext.PlacementPreset;
        _holeContext.Select(HoleEditingContext.PlacementPreset.ToString(), false);
        LoadControls();
        UpdateSummary();
        UpdateTemplateStrip();
        NotifySmartPlacementDraftChanged();
        SetStatus($"已载入模板“{name ?? FastenerTemplateFormatter.Compact(data)}”；场景选择未改变。", StatusKind.Success);
    }

    private void UpdateTemplateStrip()
    {
        try
        {
            var data = FastenerTemplateData.FromUpdateTemplate(CaptureUpdateTemplate());
            var favorite = FastenerTemplateLibraryService.Current.Favorites.FirstOrDefault(item =>
                item.Data.Signature() == data.Signature());
            _favoriteTemplateButton.Text = favorite is null ? "☆" : "★";
            _favoriteTemplateButton.ToolTip = favorite is null
                ? "收藏当前面板模板"
                : $"已收藏为“{favorite.Name}”";
        }
        catch
        {
            _favoriteTemplateButton.Text = "☆";
            _favoriteTemplateButton.ToolTip = "收藏当前面板模板";
        }
        RebuildTemplateStripLayout(AvailableContentWidth(), force: true);
    }

    private void RebuildResponsiveLayout(bool force = false)
    {
        var availableWidth = AvailableContentWidth();
        RebuildTemplateStripLayout(availableWidth, force);
        var profile = ResponsiveLayoutProfile.ForWidth(availableWidth);
        if (!force && _layoutProfile == profile)
            return;
        _layoutProfile = profile;
        RebuildActionLayout();
        _kind.SetColumns(profile.KindColumns);
        _size.SetColumns(profile.SizeColumns);
        _lengthCards.SetColumns(profile.LengthColumns);
        ApplyCompactFieldWidths(profile);
        _parameterLayout.Clear();
        _assemblyLayout.Clear();
        _holeHeaderLayout.Clear();
        _holeParameterLayout.Clear();
        _presetOptionsLayout.Clear();
        _parameterLayout.AddRow(FieldStack("类型", _kind));
        var selectedKind = SelectedKind();
        var isScrew = FastenerKindTraits.IsScrew(selectedKind);
        _assemblyTitle.Visible = true;
        _assemblyLayout.Visible = true;
        if (selectedKind == FastenerKind.HexNut)
        {
            _parameterLayout.AddRow(CompactRow(_nylonLockingNut));
            _parameterLayout.AddRow(FieldStack("规格", _size));
            _parameterLayout.AddRow(_nutStandardDimensions);
            _parameterLayout.AddRow(CompactRow(
                FieldStack("嵌入深度 mm", _headEmbedControl)));
        }
        else if (selectedKind == FastenerKind.HeatSetInsert)
        {
            _parameterLayout.AddRow(FieldStack("规格", _size));
            if (profile.PairDimensionFields)
            {
                _parameterLayout.AddRow(CompactRow(
                    FieldStack("长度 mm", _length),
                    FieldStack("外径 mm", _insertOuterDiameter)));
                _parameterLayout.AddRow(CompactRow(
                    FieldStack("孔径补偿 mm", _insertDiameterCompensation),
                    FieldStack("深度补偿 mm", _insertDepthCompensation)));
            }
            else
            {
                _parameterLayout.AddRow(CompactRow(FieldStack("长度 mm", _length)));
                _parameterLayout.AddRow(CompactRow(FieldStack("外径 mm", _insertOuterDiameter)));
                _parameterLayout.AddRow(CompactRow(FieldStack("孔径补偿 mm", _insertDiameterCompensation)));
                _parameterLayout.AddRow(CompactRow(FieldStack("深度补偿 mm", _insertDepthCompensation)));
            }
            _parameterLayout.AddRow(_insertSummaryHost);
            _parameterLayout.AddRow(_insertNoteHost);
        }
        else
        {
            _parameterLayout.AddRow(FieldStack("规格", _size));
            _parameterLayout.AddRow(FieldStack("常用长度 mm", _lengthCards));
            if (profile.PairDimensionFields)
                _parameterLayout.AddRow(CompactRow(
                    FieldStack("自定义长度 mm", _length),
                    FieldStack("嵌入/离面 mm", _headEmbedControl)));
            else
            {
                _parameterLayout.AddRow(CompactRow(FieldStack("自定义长度 mm", _length)));
                _parameterLayout.AddRow(CompactRow(FieldStack("嵌入/离面 mm", _headEmbedControl)));
            }
        }

        if (isScrew)
        {
            _assemblyLayout.AddRow(_assemblyMode);
            AddAssemblyParameterRows();
        }
        else if (selectedKind == FastenerKind.HexNut)
        {
            _assemblyLayout.AddRow(FastenerUiTheme.SecondaryLabel("单宿主 · 六角安装槽"));
        }
        else
        {
            _assemblyLayout.AddRow(FastenerUiTheme.SecondaryLabel("单宿主 · 热熔安装孔"));
        }

        // 0.37 exposes one unambiguous template surface. Component selection
        // only supplies update targets; it never switches the visible hole editor.
        _holeHeaderLayout.AddRow(_holeTitle);
        if (selectedKind == FastenerKind.HexNut)
        {
            _holeParameterLayout.AddRow(CompactRow(
                FieldStack("槽宽补偿 mm", _printerCorrection)));
        }
        else if (selectedKind == FastenerKind.HeatSetInsert)
        {
            var insertHoleNote = FastenerUiTheme.SecondaryLabel("安装孔由上方参数计算 ⓘ");
            insertHoleNote.ToolTip = "最终孔径 = 外径 + 孔径补偿；最终切割深度 = 热熔螺母长度 + 深度补偿。";
            _holeParameterLayout.AddRow(CompactRow(insertHoleNote));
        }
        else if (profile.HoleFieldColumns == 3)
        {
            var fields = SelectedAssemblyMode() == ScrewAssemblyMode.NutFastened
                ? new Control[] { FieldStack("孔径修正 mm", _printerCorrection), _holeDiameterSummary }
                : [FieldStack("孔径修正 mm", _printerCorrection), FieldStack("咬合缩减 mm", _bite), _holeDiameterSummary];
            _holeParameterLayout.AddRow(CompactRow(fields));
        }
        else if (profile.HoleFieldColumns == 2)
        {
            _holeParameterLayout.AddRow(
                SelectedAssemblyMode() == ScrewAssemblyMode.NutFastened
                    ? CompactRow(FieldStack("孔径修正 mm", _printerCorrection))
                    : CompactRow(
                        FieldStack("孔径修正 mm", _printerCorrection),
                        FieldStack("咬合缩减 mm", _bite)));
            _holeParameterLayout.AddRow(CompactRow(_holeDiameterSummary));
        }
        else
        {
            _holeParameterLayout.AddRow(CompactRow(FieldStack("孔径修正 mm", _printerCorrection)));
            if (SelectedAssemblyMode() != ScrewAssemblyMode.NutFastened)
                _holeParameterLayout.AddRow(CompactRow(FieldStack("咬合缩减 mm", _bite)));
            _holeParameterLayout.AddRow(CompactRow(_holeDiameterSummary));
        }

        AddPlacementPresetRows(profile.HoleFieldColumns);
        _parameterLayout.Create();
        _assemblyLayout.Create();
        _holeHeaderLayout.Create();
        _holeParameterLayout.Create();
        _presetOptionsLayout.Create();
        UpdateHoleContextPresentation();

    }

    private void RebuildTemplateStripLayout(int availableWidth, bool force = false)
    {
        var quickWidth = PreferredTextControlWidth(_quickEditorToggle, 74, 28);
        var naturalMenuWidth = PreferredTextControlWidth(_templateMenuButton, 70, 20);
        var spacing = FastenerUiMetrics.SpaceTight;
        var innerWidth = Math.Max(0, availableWidth - (FastenerUiMetrics.SpaceCompact * 2));
        var maximumMenuWidth = Math.Max(
            54,
            innerWidth - _favoriteTemplateButton.Width - quickWidth - (spacing * 3));
        var menuWidth = Math.Min(naturalMenuWidth, maximumMenuWidth);
        _quickEditorToggle.Width = quickWidth;
        _templateMenuButton.Width = menuWidth;

        if (!force && _templateStripHost.Content is not null)
            return;

        _templateStripHost.Content = null;
        _templateStripHost.Content = new TableLayout
        {
            Spacing = new Size(spacing, 0),
            Rows =
            {
                new TableRow(
                    _favoriteTemplateButton,
                    _templateMenuButton,
                    new TableCell(new Panel { MinimumSize = new Size(0, 0) }, true),
                    _quickEditorToggle)
            }
        };
    }

    private static int PreferredTextControlWidth(Control control, int minimumWidth, int horizontalChrome)
    {
        var measured = control switch
        {
            CheckBox checkBox => checkBox.Font.MeasureString(checkBox.Text ?? string.Empty).Width,
            Button button => button.Font.MeasureString(button.Text ?? string.Empty).Width,
            _ => 0f
        };
        return Math.Max(minimumWidth, (int)Math.Ceiling(measured) + horizontalChrome);
    }

    private int AvailableContentWidth()
    {
        if (_scrollable.ClientSize.Width > 0)
            return _scrollable.ClientSize.Width;
        return Math.Max(0, ClientSize.Width - 12);
    }

    private void AddPlacementPresetRows(int columns)
    {
        if (FastenerKindTraits.IsNut(SelectedKind()))
        {
            _presetOptionsLayout.AddRow(ModuleRoleRow(
                SelectedKind() == FastenerKind.HexNut ? "安装槽" : "安装孔",
                _presetClearancePreview,
                _presetClearanceBoolean));
            return;
        }
        var assemblyMode = SelectedAssemblyMode();
        var depth = FieldStack("咬合孔深度", _presetDepth);
        if (assemblyMode == ScrewAssemblyMode.EngagementOnly)
        {
            _presetOptionsLayout.AddRow(CompactRow(depth));
            _presetOptionsLayout.AddRow(ModuleRoleRow(
                "咬合孔", _presetEngagementPreview, _presetEngagementBoolean));
        }
        else if (assemblyMode == ScrewAssemblyMode.NutFastened)
        {
            _presetOptionsLayout.AddRow(ModuleRoleRow(
                "通孔", _presetClearancePreview, _presetClearanceBoolean));
            _presetOptionsLayout.AddRow(ModuleRoleRow(
                "螺母槽", _presetNutPreview, _presetNutBoolean));
        }
        else
        {
            _presetOptionsLayout.AddRow(CompactRow(depth));
            _presetOptionsLayout.AddRow(ModuleRoleRow(
                "通孔", _presetClearancePreview, _presetClearanceBoolean));
            _presetOptionsLayout.AddRow(ModuleRoleRow(
                "咬合孔", _presetEngagementPreview, _presetEngagementBoolean));
        }

        if (assemblyMode != ScrewAssemblyMode.NutFastened
            && _presetDepth.SelectedKey == DepthMode.FastenerLengthPlusCustom.ToString())
        {
            _presetOptionsLayout.AddRow(CompactRow(
                FieldStack("追加深度 mm", _presetBlindDepth)));
        }

        if (assemblyMode == ScrewAssemblyMode.ThreadEngagement)
        {
            var enabled = _engagementEntryChamferEnabled.Checked == true;
            _engagementEntryChamferMode.Enabled = enabled;
            _engagementEntryChamferSize.Enabled = enabled;
            _presetOptionsLayout.AddRow(CompactRow(
                _engagementEntryChamferEnabled,
                _engagementEntryChamferMode,
                FieldStack("C mm", _engagementEntryChamferSize)));
            _presetOptionsLayout.AddRow(CompactRow(_engagementEntryChamferSummary));
        }

        if (SelectedKind() == FastenerKind.SocketCap)
        {
            var enabled = _counterboreBridgeEnabled.Checked == true
                && _headEmbed.Value > 0;
            _counterboreBridgeLayerHeight.Enabled = enabled;
            _presetOptionsLayout.AddRow(CompactRow(
                _counterboreBridgeEnabled,
                FieldStack("层高 mm", _counterboreBridgeLayerHeight),
                _counterboreBridgeSummary));
        }
    }

    private void AddAssemblyParameterRows()
    {
        switch (SelectedAssemblyMode())
        {
            case ScrewAssemblyMode.EngagementOnly:
                _engagementOnlyAlignmentCompensation.Enabled =
                    _engagementOnlyAlignmentDepth.Value > 0;
                _assemblyLayout.AddRow(CompactRow(
                    FieldStack("对位深度 mm", _engagementOnlyAlignmentDepth),
                    FieldStack("正补偿 mm", _engagementOnlyAlignmentCompensation)));
                _assemblyLayout.AddRow(CompactRow(_engagementOnlyAlignmentSummary));
                break;
            case ScrewAssemblyMode.NutFastened:
                _assemblyLayout.AddRow(CompactRow(
                    FieldStack("末端露出 mm", _nutTipProtrusion),
                    FieldStack("槽补偿 mm", _nutPocketCompensation)));
                _assemblyLayout.AddRow(CompactRow(_pairedNutLocking, _pairedNutSummary));
                break;
            default:
                break;
        }
    }

    private void ApplyCompactFieldWidths(ResponsiveLayoutProfile profile)
    {
        _length.Width = profile.DimensionFieldWidth;
        _headEmbed.Width = profile.DimensionFieldWidth;
        _insertOuterDiameter.Width = profile.NumericFieldWidth;
        _insertDiameterCompensation.Width = profile.NumericFieldWidth;
        _insertDepthCompensation.Width = profile.NumericFieldWidth;
        _printerCorrection.Width = profile.NumericFieldWidth;
        _bite.Width = profile.NumericFieldWidth;
        _presetDepth.Width = profile.DepthFieldWidth;
        _presetBlindDepth.Width = profile.NumericFieldWidth;
        _engagementEntryChamferSize.Width = profile.NumericFieldWidth;
        _counterboreBridgeLayerHeight.Width = profile.NumericFieldWidth;
        _nutTipProtrusion.Width = profile.NumericFieldWidth;
        _nutPocketCompensation.Width = profile.NumericFieldWidth;
        var insertInfoWidth = Math.Max(0, AvailableContentWidth() - 32);
        _insertSummaryHost.Width = insertInfoWidth;
        _insertNoteHost.Width = insertInfoWidth;
        _insertFinalDiameter.Width = insertInfoWidth;
        _insertChamferNote.Width = insertInfoWidth;
        _pairedNutSummary.Width = insertInfoWidth;
    }

    private static TableLayout ModuleRoleRow(
        string role,
        CheckBox preview,
        CheckBox booleanEnabled)
    {
        var label = FastenerUiTheme.PrimaryLabel(role);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.Width = 62;
        return new TableLayout
        {
            Spacing = new Size(FastenerUiMetrics.SpaceCompact, 0),
            Rows =
            {
                new TableRow(
                    label,
                    preview,
                    booleanEnabled,
                    new TableCell(null, true))
            }
        };
    }

    private static Control CreateSectionDivider() =>
        FastenerUiTheme.Register(new Panel
        {
            Height = 1,
            MinimumSize = new Size(0, 1)
        }, FastenerThemeRole.CardBorder);

    private static StackLayout CompactRow(params Control[] controls)
    {
        var row = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = FastenerUiMetrics.SpaceCompact,
            VerticalContentAlignment = VerticalAlignment.Top
        };
        foreach (var control in controls)
            row.Items.Add(new StackLayoutItem(control));
        row.Items.Add(new StackLayoutItem(new Panel(), true));
        return row;
    }

    private static StackLayout FieldStack(string label, Control control) => new()
    {
        Orientation = Orientation.Vertical,
        Spacing = FastenerUiMetrics.SpaceTight,
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        Items = { FastenerUiTheme.PrimaryLabel(label), control }
    };

    private Button MakeActionButton(
        FastenerUiActionDescriptor descriptor,
        EventHandler<EventArgs> handler)
    {
        var button = new Button();
        ConfigureActionButton(button, descriptor, handler);
        return button;
    }

    private void ConfigureActionButton(
        Button button,
        FastenerUiActionDescriptor descriptor,
        EventHandler<EventArgs> handler)
    {
        button.Text = string.Empty;
        button.ToolTip = $"{FastenerText.Translate(descriptor.Name)}｜{FastenerText.Translate(descriptor.ToolTip)}";
        button.Height = FastenerUiTheme.ActionButtonHeight;
        button.MinimumSize = new Size(0, FastenerUiTheme.ActionButtonHeight);
        button.Click += handler;
        _actionButtonIcons[button] = descriptor.Icon;
        _actionDescriptors[button] = descriptor;
        ApplyActionButtonStyle(button);
    }

    private void ApplyActionButtonStyle(Button button)
    {
        if (!_actionButtonIcons.TryGetValue(button, out var icon))
            return;
        FastenerUiTheme.SetRole(button, FastenerThemeRole.SecondaryAction);
        button.Image = PanelIconProvider.Get(icon, FastenerUiTheme.IsDark);
    }

    private void DocumentSelectionChanged(
        object? sender,
        Rhino.DocObjects.RhinoObjectSelectionEventArgs e)
    {
        if (RhinoDoc.ActiveDoc?.RuntimeSerialNumber != e.Document.RuntimeSerialNumber)
            return;
        ScheduleSelectedTargetSynchronization();
    }

    private void DocumentSelectionCleared(
        object? sender,
        Rhino.DocObjects.RhinoDeselectAllObjectsEventArgs e)
    {
        if (RhinoDoc.ActiveDoc?.RuntimeSerialNumber != e.Document.RuntimeSerialNumber)
            return;
        ScheduleSelectedTargetSynchronization();
    }

    private void ScheduleSelectedTargetSynchronization() =>
        Application.Instance.AsyncInvoke(() =>
        {
            _selectionTimer.Stop();
            _selectionTimer.Start();
        });

    private void SynchronizeSelectedTargets()
    {
        _selectionTimer.Stop();
        if (RhinoDoc.ActiveDoc is not { } doc)
        {
            _selectedSummary = null;
            ShowSelectionSummary();
            UpdateHoleContextPresentation();
            return;
        }
        _selectedSummary = SelectedComponentSummary.Capture(doc);
        ShowSelectionSummary();
        UpdateHoleContextPresentation();
        UpdateHoleDiameterSummary();
    }

    private void ShowSelectionSummary()
    {
        var text = _selectedSummary is null
            ? "目标 · 未选择更新目标"
            : $"目标 · {_selectedSummary.ShortText}";
        _selectionSummary.Text = text;
        _selectionSummary.ToolTip = _selectedSummary?.FullText ?? text;
        FastenerUiTheme.SetRole(
            _selectionSummary,
            _selectedSummary?.HasBlockingIssues == true
                ? FastenerThemeRole.StatusWarning
                : FastenerThemeRole.SecondaryText);
    }

    private void SessionSelectionChanged(object? sender, ComponentSelectionChangedEventArgs e)
    {
        if (RhinoDoc.ActiveDoc?.RuntimeSerialNumber != e.Document.RuntimeSerialNumber)
            return;
        if (e.Intent == ComponentActivationIntent.LoadIntoEditor)
            ActivateComponents(e.Components);
        else
            RefreshLoadedComponentCache(e.Document, e.Components);
        SynchronizeSelectedTargets();
    }

    private void RefreshLoadedComponentCache(
        RhinoDoc doc,
        IReadOnlyList<FastenerComponentData> components)
    {
        if (_loadedComponents.Count == 0)
            return;
        var updates = components.ToDictionary(component => component.ComponentId);
        _loadedComponents = _loadedComponents
            .Select(component =>
                updates.TryGetValue(component.ComponentId, out var updated)
                    ? updated
                    : ComponentRepository.TryReadComponent(doc, component.ComponentId, out var current)
                        ? current
                        : component)
            .ToArray();
        _loadedComponent = _loadedComponents.FirstOrDefault();
    }

    private void ActivateComponent(FastenerComponentData component)
        => ActivateComponents([component]);

    private void ActivateComponents(IReadOnlyList<FastenerComponentData> components)
    {
        if (components.Count == 0)
        {
            _loadedComponents = [];
            _loadedComponent = null;
            _geometryDirty = false;
            SetHoleContext(HoleEditingContext.PlacementPreset);
            UpdateSummary();
            SetStatus("已切换为新建组件；当前参数保留。", StatusKind.Info);
            return;
        }
        _loadingControls = true;
        _loadedComponents = components.ToArray();
        _loadedComponent = components[0];
        ResetModuleOverrides();
        _geometryDirty = false;
        EditorState.Current.Load(_loadedComponent);
        if (components.Count > 1)
        {
            var allBindings = components.SelectMany(component => component.Bindings).ToArray();
            var clearance = allBindings.FirstOrDefault(binding => binding.Role == ShaftFitRole.Clearance);
            if (clearance is not null)
                EditorState.Current.ClearanceFit = clearance.ClearanceFit;
            var engagement = allBindings.FirstOrDefault(binding => binding.Role == ShaftFitRole.ThreadEngagement);
            if (engagement is not null)
                EditorState.Current.BiteReduction = engagement.BiteReduction;
        }
        _holeEditingContext = HoleEditingContext.CurrentComponent;
        _holeContext.Select(HoleEditingContext.CurrentComponent.ToString(), false);
        LoadControls();
        // Reading is the only operation that replaces the create/update template.
        // Once loaded, keep a single visible template context for both future
        // placement and selection-driven updates.
        _holeEditingContext = HoleEditingContext.PlacementPreset;
        _holeContext.Select(HoleEditingContext.PlacementPreset.ToString(), false);
        SaveControls();
        SaveActivePlacementPresetControls();
        UpdateSummary();
        if (components.Any(ComponentHostResolver.NeedsRelink))
            SetStatus("组件副本尚未绑定宿主；请选择对应宿主后运行“刷新 / 清理”。", StatusKind.Warning);
        else
            SetStatus(
                components.Count > 1
                    ? $"已读取 {components.Count} 个控制点；基础参数将批量应用。"
                    : $"已读取 {_loadedComponent.Bindings.Count} 个宿主。",
                StatusKind.Success);
    }

    private void LoadSelection()
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc is not null && ComponentEditorSession.TryActivateSelectionSet(doc, false, out _))
            return;
        SetStatus("请选中一个或多个参数化紧固件控制点。", StatusKind.Warning);
    }

    private void LoadControls()
    {
        _loadingControls = true;
        var state = EditorState.Current;
        _kind.Select(state.Kind.ToString(), false);
        _nylonLockingNut.Checked = state.Kind == FastenerKind.HexNut
            && state.HexNutStyle == HexNutStyle.NylonInsertLocking;
        _size.Select(state.Size, false);
        _length.Value = state.Length;
        UpdateLengthCardSelection();
        _headEmbed.MinValue = FastenerKindTraits.SupportsHeadGap(state.Kind) ? -500 : 0;
        _headEmbed.Value = state.HeadEmbedDepth;
        _counterboreBridgeEnabled.Checked = state.CounterboreBridgeEnabled;
        _counterboreBridgeLayerHeight.Value = state.CounterboreBridgeLayerHeight;
        _engagementEntryChamferEnabled.Checked = state.EngagementEntryChamferEnabled;
        _engagementEntryChamferMode.Select(
            state.EngagementEntryChamferMode.ToString(),
            false);
        _engagementEntryChamferSize.Value = state.EngagementEntryChamferSize;
        _engagementOnlyAlignmentDepth.Value = state.EngagementOnlyAlignmentDepth;
        _engagementOnlyAlignmentCompensation.Value =
            state.EngagementOnlyAlignmentDiameterCompensation;
        _assemblyMode.Select(state.AssemblyMode.ToString(), false);
        _pairedNutLocking.Checked = state.PairedNutStyle == HexNutStyle.NylonInsertLocking;
        _nutTipProtrusion.Value = state.NutTipProtrusion;
        _nutPocketCompensation.Value = state.NutPocketCompensation;
        UpdateCounterboreBridgeSummary();
        UpdateEngagementEntryChamferSummary();
        UpdateEngagementOnlyAlignmentSummary();
        _insertOuterDiameter.Value = state.InsertOuterDiameter;
        _insertDiameterCompensation.Value = state.InsertDiameterCompensation;
        _insertDepthCompensation.Value = state.InsertDepthCompensation;
        if (_holeEditingContext == HoleEditingContext.CurrentComponent)
        {
            _printerCorrection.Value = state.PrinterCorrection;
            _bite.Value = state.BiteReduction;
            _presetDepth.SelectedKey = state.EngagementDepthMode.ToString();
            _presetBlindDepth.Value = state.EngagementBlindDepth;
            SetCustomDepthVisibility(
                state.EngagementDepthMode == DepthMode.FastenerLengthPlusCustom);
            LoadCurrentModuleVisibility();
        }
        else
        {
            LoadPlacementPresetValues();
        }
        UpdateHexNutSizeAvailability();
        UpdateHeadEmbedControls();
        UpdateInsertSummary();
        UpdatePairedNutSummary();
        _loadingControls = false;
        RebuildResponsiveLayout(force: true);
    }

    private void SaveControls()
    {
        var state = EditorState.Current;
        state.Kind = SelectedKind();
        state.HexNutStyle = state.Kind == FastenerKind.HexNut
            ? SelectedHexNutStyle()
                : HexNutStyle.Standard;
        if (!string.IsNullOrWhiteSpace(_size.SelectedKey))
            state.Size = _size.SelectedKey;
        state.Length = _length.Value;
        state.HeadEmbedDepth = FastenerKindTraits.SupportsEmbedDepth(state.Kind) ? _headEmbed.Value : 0;
        state.CounterboreBridgeEnabled = state.Kind == FastenerKind.SocketCap
            && state.HeadEmbedDepth > 0
            && _counterboreBridgeEnabled.Checked == true;
        state.CounterboreBridgeLayerHeight = _counterboreBridgeLayerHeight.Value;
        state.AssemblyMode = FastenerKindTraits.IsScrew(state.Kind)
            ? SelectedAssemblyMode()
            : ScrewAssemblyMode.ThreadEngagement;
        state.EngagementEntryChamferEnabled = FastenerKindTraits.IsScrew(state.Kind)
            && state.AssemblyMode == ScrewAssemblyMode.ThreadEngagement
            && _engagementEntryChamferEnabled.Checked == true;
        state.EngagementEntryChamferMode = SelectedEngagementEntryChamferMode();
        state.EngagementEntryChamferSize = _engagementEntryChamferSize.Value;
        state.EngagementOnlyAlignmentDepth = _engagementOnlyAlignmentDepth.Value;
        state.EngagementOnlyAlignmentDiameterCompensation =
            _engagementOnlyAlignmentCompensation.Value;
        state.EngagementOnly = state.AssemblyMode == ScrewAssemblyMode.EngagementOnly;
        state.PairedNutStyle = state.AssemblyMode == ScrewAssemblyMode.NutFastened
            && _pairedNutLocking.Checked == true
                ? HexNutStyle.NylonInsertLocking
                : HexNutStyle.Standard;
        state.NutTipProtrusion = _nutTipProtrusion.Value;
        state.NutPocketCompensation = _nutPocketCompensation.Value;
        state.InsertOuterDiameter = state.Kind == FastenerKind.HeatSetInsert
            ? _insertOuterDiameter.Value
            : 0;
        state.InsertDiameterCompensation = state.Kind == FastenerKind.HeatSetInsert
            ? _insertDiameterCompensation.Value
            : 0;
        state.InsertDepthCompensation = state.Kind == FastenerKind.HeatSetInsert
            ? _insertDepthCompensation.Value
            : 0;
        state.PrinterCorrection = _printerCorrection.Value;
        state.ClearanceFit = ClearanceFitClass.Normal;
        state.BiteReduction = _bite.Value;
        if (Enum.TryParse<DepthMode>(_presetDepth.SelectedKey, out var depthMode))
            state.EngagementDepthMode = depthMode;
        state.EngagementBlindDepth = _presetBlindDepth.Value;
        state.FastenerOpacityPercent = GlobalDisplaySettingsService.Current.FastenerOpacityPercent;
        state.CutterOpacityPercent = GlobalDisplaySettingsService.Current.CutterOpacityPercent;
    }

    private FastenerUpdateTemplate CaptureUpdateTemplate() =>
        EditorState.Current.CaptureUpdateTemplate() with
        {
            ClearancePreviewVisible = _clearancePreviewOverride,
            ClearanceBooleanEnabled = _clearanceBooleanOverride,
            EngagementPreviewVisible = _engagementPreviewOverride,
            EngagementBooleanEnabled = _engagementBooleanOverride,
            InstallationPreviewVisible = _installationPreviewOverride,
            InstallationBooleanEnabled = _installationBooleanOverride,
            NutPocketPreviewVisible = _nutPocketPreviewOverride,
            NutPocketBooleanEnabled = _nutPocketBooleanOverride
        };

    private void LoadPlacementPresetControls()
    {
        _loadingControls = true;
        LoadPlacementPresetValues();
        _loadingControls = false;
    }

    private void LoadPlacementPresetValues()
    {
        var preset = PlacementPresetService.Current;
        _printerCorrection.Value = preset.PrinterCorrection;
        _bite.Value = preset.BiteReduction;
        _presetDepth.SelectedKey = preset.EngagementDepthMode.ToString();
        _presetBlindDepth.Value = preset.EngagementBlindDepth;
        _counterboreBridgeEnabled.Checked = preset.CounterboreBridgeEnabled;
        _counterboreBridgeLayerHeight.Value = preset.CounterboreBridgeLayerHeight;
        _engagementEntryChamferEnabled.Checked = preset.EngagementEntryChamferEnabled;
        _engagementEntryChamferMode.Select(
            preset.EngagementEntryChamferMode.ToString(),
            false);
        _engagementEntryChamferSize.Value = preset.EngagementEntryChamferSize;
        _engagementOnlyAlignmentDepth.Value = preset.EngagementOnlyAlignmentDepth;
        _engagementOnlyAlignmentCompensation.Value =
            preset.EngagementOnlyAlignmentDiameterCompensation;
        _assemblyMode.Select(preset.AssemblyMode.ToString(), false);
        _pairedNutLocking.Checked = preset.PairedNutStyle == HexNutStyle.NylonInsertLocking;
        _nutTipProtrusion.Value = preset.NutTipProtrusion;
        _nutPocketCompensation.Value = preset.NutPocketCompensation;
        EditorState.Current.AssemblyMode = preset.AssemblyMode;
        EditorState.Current.EngagementOnly = preset.AssemblyMode == ScrewAssemblyMode.EngagementOnly;
        EditorState.Current.PairedNutStyle = preset.PairedNutStyle;
        EditorState.Current.NutTipProtrusion = preset.NutTipProtrusion;
        EditorState.Current.NutPocketCompensation = preset.NutPocketCompensation;
        EditorState.Current.EngagementOnlyAlignmentDepth =
            preset.EngagementOnlyAlignmentDepth;
        EditorState.Current.EngagementOnlyAlignmentDiameterCompensation =
            preset.EngagementOnlyAlignmentDiameterCompensation;
        UpdateCounterboreBridgeSummary();
        UpdateEngagementEntryChamferSummary();
        UpdateEngagementOnlyAlignmentSummary();
        SetCustomDepthVisibility(
            preset.EngagementDepthMode == DepthMode.FastenerLengthPlusCustom);
        _presetClearancePreview.Checked = preset.ClearancePreviewVisible;
        _presetClearanceBoolean.Checked = preset.ClearanceBooleanEnabled;
        _presetEngagementPreview.Checked = preset.EngagementPreviewVisible;
        _presetEngagementBoolean.Checked = preset.EngagementBooleanEnabled;
        _presetNutPreview.Checked = preset.NutPocketPreviewVisible;
        _presetNutBoolean.Checked = preset.NutPocketBooleanEnabled;
        UpdatePairedNutSummary();
        if (SelectedKind() == FastenerKind.HeatSetInsert)
        {
            var heatSet = HeatSetInsertPresetService.Current;
            _length.Value = heatSet.Length;
            _insertOuterDiameter.Value = heatSet.OuterDiameter;
            _insertDiameterCompensation.Value = heatSet.DiameterCompensation;
            _insertDepthCompensation.Value = heatSet.DepthCompensation;
            _presetClearancePreview.Checked = heatSet.PreviewVisible;
            _presetClearanceBoolean.Checked = heatSet.BooleanEnabled;
            UpdateInsertSummary();
        }
    }

    private void SaveActivePlacementPresetControls()
    {
        if (SelectedKind() == FastenerKind.HeatSetInsert)
            SaveHeatSetPresetControls();
        else
            SavePlacementPresetControls();
    }

    private void SaveHeatSetPresetControls()
    {
        if (_loadingControls
            || RhinoMMPlugIn.Instance is null
            || SelectedKind() != FastenerKind.HeatSetInsert
            || _holeEditingContext != HoleEditingContext.PlacementPreset)
            return;
        var preset = new HeatSetInsertPreset(
            _length.Value,
            _insertOuterDiameter.Value,
            _insertDiameterCompensation.Value,
            _insertDepthCompensation.Value,
            _presetClearancePreview.Checked == true,
            _presetClearanceBoolean.Checked == true);
        if (!HeatSetInsertPresetService.Save(RhinoMMPlugIn.Instance.Settings, preset, out var message))
        {
            SetStatus(message, StatusKind.Warning);
            return;
        }
        NotifySmartPlacementDraftChanged();
    }

    private void LoadHeatSetDefaultsForNewKind()
    {
        if (_loadingControls || SelectedKind() != FastenerKind.HeatSetInsert)
            return;
        var preset = HeatSetInsertPresetService.Current;
        _length.Value = preset.Length;
        _insertOuterDiameter.Value = preset.OuterDiameter;
        _insertDiameterCompensation.Value = preset.DiameterCompensation;
        _insertDepthCompensation.Value = preset.DepthCompensation;
        _presetClearancePreview.Checked = preset.PreviewVisible;
        _presetClearanceBoolean.Checked = preset.BooleanEnabled;
    }

    private void UpdateInsertSummary()
    {
        if (string.IsNullOrWhiteSpace(_size.SelectedKey))
            return;
        try
        {
            var spec = CurrentSpec(_size.SelectedKey);
            if (SelectedKind() == FastenerKind.HexNut)
            {
                var nutDimensions = HexNutDimensions.Resolve(SelectedHexNutStyle(), spec);
                var finalAcrossFlats = nutDimensions.AcrossFlats + _printerCorrection.Value;
                var standardLine = nutDimensions.IsEngineeringExtension
                    ? "DIN 985 · 工程预设"
                    : nutDimensions.Standard;
                _nutStandardDimensions.Text =
                    $"{standardLine}\n"
                    + $"对边 {nutDimensions.AcrossFlats:0.###} · 高 {nutDimensions.TotalHeight:0.###} · "
                    + $"槽 {finalAcrossFlats:0.###} · 嵌入 {_headEmbed.Value:0.###} mm";
                _nutStandardDimensions.ToolTip = nutDimensions.IsEngineeringExtension
                    ? $"{nutDimensions.Standard}；M2、M2.5 不属于 GB/T 889.1-2015 尺寸范围，切割前请核对实际采购件。"
                    : $"{nutDimensions.Standard}；对边 {nutDimensions.AcrossFlats:0.###} mm，总高 {nutDimensions.TotalHeight:0.###} mm，最终槽宽 {finalAcrossFlats:0.###} mm，嵌入 {_headEmbed.Value:0.###} mm。";
            }
            else
            {
                _nutStandardDimensions.Text = string.Empty;
                _nutStandardDimensions.ToolTip = string.Empty;
            }
            var finalDiameter = _insertOuterDiameter.Value + _insertDiameterCompensation.Value;
            var finalDepth = _length.Value + _insertDepthCompensation.Value;
            _insertFinalDiameter.Text = $"孔 Ø{finalDiameter:0.###} · 深 {finalDepth:0.###} mm";
            _insertFinalDiameter.ToolTip =
                $"最终孔径 = 外径 {_insertOuterDiameter.Value:0.###} + 孔径补偿 {_insertDiameterCompensation.Value:0.###} = {finalDiameter:0.###} mm；"
                + $"最终切割深度 = 螺母长度 {_length.Value:0.###} + 深度补偿 {_insertDepthCompensation.Value:0.###} = {finalDepth:0.###} mm。";
            var heatSetValid = SelectedKind() != FastenerKind.HeatSetInsert
                || (_length.Value > 0
                    && _insertOuterDiameter.Value > 0
                    && _insertDepthCompensation.Value >= 0
                    && finalDiameter > spec.NominalDiameter);
            _placeButton.Enabled = true;
            if (!heatSetValid)
                _placeButton.ToolTip = "放置 / 绑定｜当前热熔参数无效；点击后将显示需要修正的参数";
            else
                _placeButton.ToolTip = "放置 / 绑定｜左击：智能放置｜右击：点集批量放置";
        }
        catch
        {
            _nutStandardDimensions.Text = string.Empty;
            _nutStandardDimensions.ToolTip = string.Empty;
            _insertFinalDiameter.Text = string.Empty;
        }
    }

    private FastenerKind SelectedKind() =>
        Enum.TryParse<FastenerKind>(_kind.SelectedKey, out var kind)
            ? kind
            : EditorState.Current.Kind;

    private HexNutStyle SelectedHexNutStyle() =>
        SelectedKind() == FastenerKind.HexNut && _nylonLockingNut.Checked == true
            ? HexNutStyle.NylonInsertLocking
            : HexNutStyle.Standard;

    private ScrewAssemblyMode SelectedAssemblyMode() =>
        Enum.TryParse<ScrewAssemblyMode>(_assemblyMode.SelectedKey, out var mode)
            ? mode
            : EditorState.Current.AssemblyMode;

    private EngagementEntryChamferMode SelectedEngagementEntryChamferMode() =>
        Enum.TryParse<EngagementEntryChamferMode>(
            _engagementEntryChamferMode.SelectedKey,
            out var mode)
                ? mode
                : EditorState.Current.EngagementEntryChamferMode;

    private void SavePlacementPresetControls()
    {
        if (_loadingControls
            || RhinoMMPlugIn.Instance is null
            || _holeEditingContext != HoleEditingContext.PlacementPreset)
            return;
        var current = PlacementPresetService.Current;
        var depth = Enum.TryParse<DepthMode>(_presetDepth.SelectedKey, out var parsedDepth)
            ? parsedDepth
            : current.EngagementDepthMode;
        var preset = current with
        {
            PrinterCorrection = _printerCorrection.Value,
            ClearanceFit = ClearanceFitClass.Normal,
            BiteReduction = _bite.Value,
            EngagementDepthMode = depth,
            EngagementBlindDepth = _presetBlindDepth.Value,
            CounterboreBridgeEnabled = _headEmbed.Value > 0
                && _counterboreBridgeEnabled.Checked == true,
            CounterboreBridgeLayerHeight = _counterboreBridgeLayerHeight.Value,
            ClearancePreviewVisible = _presetClearancePreview.Checked == true,
            ClearanceBooleanEnabled = _presetClearanceBoolean.Checked == true,
            EngagementPreviewVisible = _presetEngagementPreview.Checked == true,
            EngagementBooleanEnabled = _presetEngagementBoolean.Checked == true,
            EngagementOnly = SelectedAssemblyMode() == ScrewAssemblyMode.EngagementOnly,
            AssemblyMode = SelectedAssemblyMode(),
            PairedNutStyle = _pairedNutLocking.Checked == true
                ? HexNutStyle.NylonInsertLocking
                : HexNutStyle.Standard,
            NutTipProtrusion = _nutTipProtrusion.Value,
            NutPocketCompensation = _nutPocketCompensation.Value,
            NutPocketPreviewVisible = _presetNutPreview.Checked == true,
            NutPocketBooleanEnabled = _presetNutBoolean.Checked == true,
            EngagementEntryChamferEnabled = SelectedAssemblyMode() == ScrewAssemblyMode.ThreadEngagement
                && _engagementEntryChamferEnabled.Checked == true,
            EngagementEntryChamferSize = _engagementEntryChamferSize.Value,
            EngagementEntryChamferMode = SelectedEngagementEntryChamferMode(),
            EngagementOnlyAlignmentDepth = _engagementOnlyAlignmentDepth.Value,
            EngagementOnlyAlignmentDiameterCompensation =
                _engagementOnlyAlignmentCompensation.Value
        };
        if (!PlacementPresetService.Save(RhinoMMPlugIn.Instance.Settings, preset, out var message))
        {
            SetStatus(message, StatusKind.Warning);
            return;
        }
        NotifySmartPlacementDraftChanged();
    }

    private void UpdateHoleDiameterSummary()
    {
        if (FastenerKindTraits.IsNut(SelectedKind())
            || string.IsNullOrWhiteSpace(_size.SelectedKey))
        {
            _holeDiameterSummary.Text = string.Empty;
            _holeDiameterSummary.ToolTip = string.Empty;
            return;
        }

        try
        {
            var nominal = CurrentSpec(_size.SelectedKey).NominalDiameter;
            var clearance = nominal + _printerCorrection.Value;
            var engagement = nominal - _bite.Value;
            if (SelectedAssemblyMode() == ScrewAssemblyMode.NutFastened)
            {
                _holeDiameterSummary.Text = $"全部通孔 Ø{clearance:0.###}";
                _holeDiameterSummary.ToolTip =
                    $"所有宿主圆孔 = 公称直径 {nominal:0.###} + 孔径修正 {_printerCorrection.Value:0.###} = {clearance:0.###} mm";
            }
            else
            {
                _holeDiameterSummary.Text = $"通孔 Ø{clearance:0.###} · 咬合 Ø{engagement:0.###}";
                _holeDiameterSummary.ToolTip =
                    $"通孔 = 公称直径 {nominal:0.###} + 孔径修正 {_printerCorrection.Value:0.###} = {clearance:0.###} mm\n"
                    + $"咬合 = 公称直径 {nominal:0.###} − 咬合缩减 {_bite.Value:0.###} = {engagement:0.###} mm";
            }
        }
        catch
        {
            _holeDiameterSummary.Text = string.Empty;
            _holeDiameterSummary.ToolTip = string.Empty;
        }
    }

    private void HoleCommonParameterChanged()
    {
        if (_loadingControls)
            return;
        UpdateInsertSummary();
        UpdateHoleDiameterSummary();
        UpdateEngagementOnlyAlignmentSummary();
        SaveControls();
        if (_holeEditingContext == HoleEditingContext.PlacementPreset)
        {
            SavePlacementPresetControls();
            UpdateSummary();
            return;
        }
        ScheduleUpdate();
    }

    private void HoleContextChanged()
    {
        if (_loadingControls)
            return;
        if (!Enum.TryParse<HoleEditingContext>(_holeContext.SelectedKey, out var context))
            return;
        if (context == HoleEditingContext.CurrentComponent && _loadedComponents.Count == 0)
            context = HoleEditingContext.PlacementPreset;
        SetHoleContext(context);
    }

    private void SetHoleContext(HoleEditingContext context)
    {
        if (context == HoleEditingContext.CurrentComponent && _loadedComponents.Count == 0)
            context = HoleEditingContext.PlacementPreset;
        _holeEditingContext = context;
        ResetModuleOverrides();
        _loadingControls = true;
        _holeContext.Select(context.ToString(), false);
        if (context == HoleEditingContext.PlacementPreset)
            LoadPlacementPresetValues();
        else
        {
            var state = EditorState.Current;
            _assemblyMode.Select(state.AssemblyMode.ToString(), false);
            _pairedNutLocking.Checked = state.PairedNutStyle == HexNutStyle.NylonInsertLocking;
            _nutTipProtrusion.Value = state.NutTipProtrusion;
            _nutPocketCompensation.Value = state.NutPocketCompensation;
            _printerCorrection.Value = state.PrinterCorrection;
            _bite.Value = state.BiteReduction;
            _presetDepth.SelectedKey = state.EngagementDepthMode.ToString();
            _presetBlindDepth.Value = state.EngagementBlindDepth;
            _counterboreBridgeEnabled.Checked = state.CounterboreBridgeEnabled;
            _counterboreBridgeLayerHeight.Value = state.CounterboreBridgeLayerHeight;
            _engagementEntryChamferEnabled.Checked = state.EngagementEntryChamferEnabled;
            _engagementEntryChamferMode.Select(
                state.EngagementEntryChamferMode.ToString(),
                false);
            _engagementEntryChamferSize.Value = state.EngagementEntryChamferSize;
            _engagementOnlyAlignmentDepth.Value = state.EngagementOnlyAlignmentDepth;
            _engagementOnlyAlignmentCompensation.Value =
                state.EngagementOnlyAlignmentDiameterCompensation;
            UpdateCounterboreBridgeSummary();
            UpdateEngagementEntryChamferSummary();
            UpdateEngagementOnlyAlignmentSummary();
            SetCustomDepthVisibility(
                state.EngagementDepthMode == DepthMode.FastenerLengthPlusCustom);
            LoadCurrentModuleVisibility();
        }
        _loadingControls = false;
        SaveControls();
        UpdateHoleContextPresentation();
        UpdateSummary();
    }

    private void UpdateHoleContextPresentation()
    {
        var hasComponents = _loadedComponents.Count > 0;
        _holeContextHost.Visible = false;
        _holeContext.SetEnabled(HoleEditingContext.CurrentComponent.ToString(), hasComponents);
        _presetOptionsLayout.Visible = true;
        var selected = _selectedSummary;
        // The update entry remains available in every idle selection state.
        // ApplyLoaded performs a fresh, transactional validation when invoked.
        _applyButton.Enabled = true;
        _applyButton.ToolTip = selected?.HasBlockingIssues == true
            ? "应用更新：点击后将检查损坏或待重绑组件并给出维护建议"
            : selected is { HasTargets: true }
                ? $"应用更新：将面板模板应用到 {selected.Components.Count} 个控制点组件"
                : "应用更新：点击后将提示选择一个或多个控制点";
        UpdatePrimaryActionStyle();
    }

    private void SelectCommonLength()
    {
        if (_loadingControls
            || !double.TryParse(
                _lengthCards.SelectedKey,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value))
            return;
        _length.Value = value;
    }

    private void UpdateLengthCardSelection()
    {
        var match = CommonLengths.FirstOrDefault(value => Math.Abs(value - _length.Value) <= 0.001);
        _lengthCards.Select(
            match > 0 ? match.ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
            false);
    }

    private void Run(string command)
    {
        SaveControls();
        UpdateSummary();
        if (RhinoApp.RunScript(command, false))
            SetStatus("命令已启动，请按 Rhino 命令行提示操作。", StatusKind.Info);
        else
            SetStatus("无法启动命令，请查看 Rhino 命令行。", StatusKind.Error);
    }

    private void RunExport(string command)
    {
        SaveControls();
        if (!ApplyDraftForExport())
        {
            SetStatus("参数应用失败，已取消导出。", StatusKind.Error);
            return;
        }
        Run(command);
    }

    private void ScheduleUpdate()
    {
        if (_loadingControls)
            return;
        SaveControls();
        UpdateSummary();
        MarkGeometryDirty();
        NotifySmartPlacementDraftChanged();
    }

    private void EngagementDepthChanged(bool rebuildLayout)
    {
        if (_loadingControls)
            return;
        SetCustomDepthVisibility(
            _presetDepth.SelectedKey == DepthMode.FastenerLengthPlusCustom.ToString());
        SaveControls();
        if (_holeEditingContext == HoleEditingContext.PlacementPreset)
        {
            SavePlacementPresetControls();
        }
        else
        {
            MarkGeometryDirty();
        }
        NotifySmartPlacementDraftChanged();
        UpdateSummary();
        if (rebuildLayout)
            RebuildResponsiveLayout(force: true);
    }

    private void CounterboreBridgeChanged(bool rebuildLayout)
    {
        if (_loadingControls)
            return;
        UpdateCounterboreBridgeSummary();
        SaveControls();
        if (_holeEditingContext == HoleEditingContext.PlacementPreset)
            SavePlacementPresetControls();
        else
            MarkGeometryDirty();
        NotifySmartPlacementDraftChanged();
        UpdateSummary();
        if (rebuildLayout)
            RebuildResponsiveLayout(force: true);
    }

    private void EngagementEntryChamferChanged(bool rebuildLayout)
    {
        if (_loadingControls)
            return;
        UpdateEngagementEntryChamferSummary();
        SaveControls();
        if (_holeEditingContext == HoleEditingContext.PlacementPreset)
            SavePlacementPresetControls();
        else
            MarkGeometryDirty();
        NotifySmartPlacementDraftChanged();
        UpdateSummary();
        if (rebuildLayout)
            RebuildResponsiveLayout(force: true);
    }

    private void EngagementOnlyAlignmentChanged()
    {
        if (_loadingControls)
            return;
        _engagementOnlyAlignmentCompensation.Enabled = _engagementOnlyAlignmentDepth.Value > 0;
        UpdateEngagementOnlyAlignmentSummary();
        SaveControls();
        if (_holeEditingContext == HoleEditingContext.PlacementPreset)
            SavePlacementPresetControls();
        else
            MarkGeometryDirty();
        NotifySmartPlacementDraftChanged();
        UpdateSummary();
    }

    private void AssemblyModeChanged()
    {
        if (_loadingControls)
            return;
        SaveControls();
        if (_holeEditingContext == HoleEditingContext.PlacementPreset)
            SavePlacementPresetControls();
        else
            MarkGeometryDirty();
        NotifySmartPlacementDraftChanged();
        UpdateHexNutSizeAvailability();
        UpdateHoleDiameterSummary();
        UpdateEngagementOnlyAlignmentSummary();
        UpdatePairedNutSummary();
        UpdateSummary();
        RebuildResponsiveLayout(force: true);
    }

    private void PairedNutParameterChanged(bool rebuildLayout)
    {
        if (_loadingControls)
            return;
        if (_pairedNutLocking.Checked == true && _size.SelectedKey == "M1.6")
            _size.Select("M2", true);
        SaveControls();
        UpdateHexNutSizeAvailability();
        UpdatePairedNutSummary();
        if (_holeEditingContext == HoleEditingContext.PlacementPreset)
            SavePlacementPresetControls();
        else
            MarkGeometryDirty();
        NotifySmartPlacementDraftChanged();
        UpdateSummary();
        if (rebuildLayout)
            RebuildResponsiveLayout(force: true);
    }

    private void UpdatePairedNutSummary()
    {
        if (SelectedAssemblyMode() != ScrewAssemblyMode.NutFastened
            || string.IsNullOrWhiteSpace(_size.SelectedKey))
        {
            _pairedNutSummary.Text = string.Empty;
            _pairedNutSummary.ToolTip = string.Empty;
            return;
        }
        try
        {
            var spec = CurrentSpec(_size.SelectedKey);
            var style = _pairedNutLocking.Checked == true
                ? HexNutStyle.NylonInsertLocking
                : HexNutStyle.Standard;
            var dimensions = HexNutDimensions.Resolve(style, spec);
            var slot = dimensions.AcrossFlats + _nutPocketCompensation.Value;
            _pairedNutSummary.Text =
                $"{FastenerLabels.NutStyle(style)} · 对边 {dimensions.AcrossFlats:0.###} · 槽 {slot:0.###} mm";
            _pairedNutSummary.ToolTip =
                $"{dimensions.Standard}；螺杆尖端露出螺母外侧 {_nutTipProtrusion.Value:0.###} mm；"
                + $"六角槽对边 = {dimensions.AcrossFlats:0.###} + {_nutPocketCompensation.Value:0.###} = {slot:0.###} mm。";
        }
        catch
        {
            _pairedNutSummary.Text = "当前规格不支持所选配套螺母";
            _pairedNutSummary.ToolTip = _pairedNutSummary.Text;
        }
    }

    private void UpdateCounterboreBridgeSummary()
    {
        var layerHeight = _counterboreBridgeLayerHeight.Value;
        _counterboreBridgeSummary.Text =
            $"双层 · 总高 {layerHeight * 2:0.00} mm";
        _counterboreBridgeSummary.ToolTip =
            "第一层形成两条切线桥，第二层形成垂直桥，随后恢复圆形螺杆孔。";
    }

    private void UpdateEngagementEntryChamferSummary()
    {
        var size = _engagementEntryChamferSize.Value;
        if (SelectedEngagementEntryChamferMode()
            == EngagementEntryChamferMode.SurfaceEqualDistance)
        {
            _engagementEntryChamferSummary.Text = $"表面等距 · C{size:0.##}";
            _engagementEntryChamferSummary.ToolTip =
                "宿主表面与孔壁分别退让相同 C 值；仅支持单一连续平面入口。";
            return;
        }

        _engagementEntryChamferSummary.Text = $"轴向45° · C{size:0.##}";
        _engagementEntryChamferSummary.ToolTip =
            "沿螺丝轴向生成45°锥形导入口；支持平面和连续曲面入口。";
    }

    private void UpdateEngagementOnlyAlignmentSummary()
    {
        if (_engagementOnlyAlignmentDepth.Value <= 0)
        {
            _engagementOnlyAlignmentSummary.Text = "顶部对位关闭";
            _engagementOnlyAlignmentSummary.ToolTip = "深度为 0，不生成正补偿对位段和60°圆台。";
            return;
        }
        if (string.IsNullOrWhiteSpace(_size.SelectedKey))
        {
            _engagementOnlyAlignmentSummary.Text = string.Empty;
            return;
        }
        try
        {
            var spec = CurrentSpec(_size.SelectedKey);
            var guide = spec.NominalDiameter + _engagementOnlyAlignmentCompensation.Value;
            var engagement = spec.NominalDiameter - _bite.Value;
            _engagementOnlyAlignmentSummary.Text =
                $"对位 Ø{guide:0.###}×{_engagementOnlyAlignmentDepth.Value:0.##} · 60° · 咬合 Ø{engagement:0.###}";
            _engagementOnlyAlignmentSummary.ToolTip =
                "对位段从完整孔口圆周的最深入口起算；圆台夹角60°，随后恢复咬合孔直径。";
        }
        catch
        {
            _engagementOnlyAlignmentSummary.Text = "无法计算当前规格的对位尺寸";
        }
    }

    private void SetCustomDepthVisibility(bool visible)
    {
        _presetBlindDepth.Visible = visible;
    }

    private void TopLevelModuleToggleChanged(ShaftFitRole role, bool isPreview)
    {
        if (_loadingControls)
            return;
        var effectiveRole = FastenerKindTraits.IsNut(SelectedKind())
            ? ShaftFitRole.InstallationPocket
            : role;
        var value = isPreview
            ? effectiveRole switch
            {
                ShaftFitRole.ThreadEngagement => _presetEngagementPreview.Checked == true,
                ShaftFitRole.NutPocket => _presetNutPreview.Checked == true,
                _ => _presetClearancePreview.Checked == true
            }
            : effectiveRole switch
            {
                ShaftFitRole.ThreadEngagement => _presetEngagementBoolean.Checked == true,
                ShaftFitRole.NutPocket => _presetNutBoolean.Checked == true,
                _ => _presetClearanceBoolean.Checked == true
            };
        SetModuleOverride(effectiveRole, isPreview, value);
        if (_holeEditingContext == HoleEditingContext.PlacementPreset)
        {
            SaveActivePlacementPresetControls();
            SynchronizeSelectedTargets();
            return;
        }
        ApplyTopLevelVisibilityToLoadedComponents(effectiveRole, isPreview, value);
    }

    private void SetModuleOverride(ShaftFitRole role, bool isPreview, bool value)
    {
        switch (role, isPreview)
        {
            case (ShaftFitRole.Clearance, true):
                _clearancePreviewOverride = value;
                break;
            case (ShaftFitRole.Clearance, false):
                _clearanceBooleanOverride = value;
                break;
            case (ShaftFitRole.ThreadEngagement, true):
                _engagementPreviewOverride = value;
                break;
            case (ShaftFitRole.ThreadEngagement, false):
                _engagementBooleanOverride = value;
                break;
            case (ShaftFitRole.InstallationPocket, true):
                _installationPreviewOverride = value;
                break;
            case (ShaftFitRole.InstallationPocket, false):
                _installationBooleanOverride = value;
                break;
            case (ShaftFitRole.NutPocket, true):
                _nutPocketPreviewOverride = value;
                break;
            case (ShaftFitRole.NutPocket, false):
                _nutPocketBooleanOverride = value;
                break;
        }
    }

    private void ResetModuleOverrides()
    {
        _clearancePreviewOverride = null;
        _clearanceBooleanOverride = null;
        _engagementPreviewOverride = null;
        _engagementBooleanOverride = null;
        _installationPreviewOverride = null;
        _installationBooleanOverride = null;
        _nutPocketPreviewOverride = null;
        _nutPocketBooleanOverride = null;
    }

    private void LoadCurrentModuleVisibility()
    {
        var bindings = _loadedComponents.SelectMany(component => component.Bindings).ToArray();
        var clearance = bindings.FirstOrDefault(binding => binding.Role == ShaftFitRole.Clearance);
        var engagement = bindings.FirstOrDefault(binding => binding.Role == ShaftFitRole.ThreadEngagement);
        var installation = bindings.FirstOrDefault(binding => binding.Role == ShaftFitRole.InstallationPocket);
        var nutPocket = bindings.FirstOrDefault(binding => binding.Role == ShaftFitRole.NutPocket);
        _presetClearancePreview.Checked =
            (clearance ?? installation)?.IsPreviewVisible ?? true;
        _presetClearanceBoolean.Checked =
            (clearance ?? installation)?.IsBooleanEnabled ?? true;
        _presetEngagementPreview.Checked = engagement?.IsPreviewVisible ?? true;
        _presetEngagementBoolean.Checked = engagement?.IsBooleanEnabled ?? true;
        _presetNutPreview.Checked = nutPocket?.IsPreviewVisible ?? true;
        _presetNutBoolean.Checked = nutPocket?.IsBooleanEnabled ?? true;
    }

    private void ApplyTopLevelVisibilityToLoadedComponents(
        ShaftFitRole role,
        bool isPreview,
        bool value)
    {
        if (_loadedComponents.Count == 0 || RhinoDoc.ActiveDoc is not { } doc)
        {
            SynchronizeSelectedTargets();
            return;
        }
        var drafts = _loadedComponents.Select(component => component with
        {
            Bindings = component.Bindings.Select(binding =>
                binding.Role != role
                    ? binding
                    : isPreview
                        ? binding with { IsPreviewVisible = value }
                        : binding with { IsBooleanEnabled = value }).ToArray(),
            SmartBindingProfile = UpdateSmartModuleSwitch(
                component.SmartBindingProfile,
                role,
                isPreview,
                value),
            UpdatedAt = DateTimeOffset.UtcNow
        }).ToArray();
        if (!ComponentPresentationService.ApplyDisplaySettings(
                doc,
                drafts,
                out var saved,
                out var message))
        {
            SetStatus(message, StatusKind.Error);
            return;
        }
        _loadedComponents = saved;
        _loadedComponent = saved.FirstOrDefault();
        ComponentEditorSession.UpdateCachedComponents(doc, saved);
        SynchronizeSelectedTargets();
        SetStatus(message, StatusKind.Success);
    }

    private static SmartBindingProfile? UpdateSmartModuleSwitch(
        SmartBindingProfile? profile,
        ShaftFitRole role,
        bool isPreview,
        bool value)
    {
        if (profile is null)
            return null;
        return (role, isPreview) switch
        {
            (ShaftFitRole.Clearance, true) =>
                profile with { ClearancePreviewVisible = value },
            (ShaftFitRole.Clearance, false) =>
                profile with { ClearanceBooleanEnabled = value },
            (ShaftFitRole.ThreadEngagement, true) =>
                profile with { EngagementPreviewVisible = value },
            (ShaftFitRole.ThreadEngagement, false) =>
                profile with { EngagementBooleanEnabled = value },
            (ShaftFitRole.NutPocket, true) =>
                profile with { NutPocketPreviewVisible = value },
            (ShaftFitRole.NutPocket, false) =>
                profile with { NutPocketBooleanEnabled = value },
            _ => profile
        };
    }

    private static void NotifySmartPlacementDraftChanged()
    {
        SmartPlacementDraftChangeService.NotifyChanged();
        RhinoDoc.ActiveDoc?.Views.Redraw();
    }

    private bool ApplyLoaded()
    {
        if (_applyInProgress)
        {
            SetStatus("正在应用更新，请稍候。", StatusKind.Info);
            return false;
        }
        if (RhinoDoc.ActiveDoc is not { } doc)
        {
            SetStatus("当前没有可更新的 Rhino 文档。", StatusKind.Warning);
            return false;
        }
        _applyInProgress = true;
        try
        {
            // Always capture the real Rhino selection at click time. The cached
            // summary is presentation-only and must never choose update targets.
            _selectedSummary = SelectedComponentSummary.Capture(doc);
            ShowSelectionSummary();
            UpdateHoleContextPresentation();
            if (_selectedSummary is not { HasTargets: true })
            {
                SetStatus("请先选中一个或多个紧固件控制点；面板模板未修改。", StatusKind.Warning);
                return false;
            }
            if (_selectedSummary.HasBlockingIssues)
            {
                SetStatus("选中的组件存在损坏或待重新绑定项；请先运行“刷新 / 清理”。", StatusKind.Warning);
                return false;
            }

            SaveControls();
            var selectedComponents = _selectedSummary.Components;
            var keepLoadedComponentDetails = selectedComponents.Count == 1
                && _loadedComponents.Count == 1
                && selectedComponents[0].ComponentId == _loadedComponents[0].ComponentId;
            var template = CaptureUpdateTemplate();
            if (ComponentUpdateCoordinator.TryApplyTemplate(
                    doc,
                    selectedComponents,
                    template,
                    out var saved,
                    out var message))
            {
                if (keepLoadedComponentDetails)
                {
                    _loadedComponents = saved;
                    _loadedComponent = saved[0];
                }
                _geometryDirty = false;
                FastenerTemplateLibraryService.RecordSuccessfulOperation(
                    FastenerTemplateData.FromUpdateTemplate(template),
                    FastenerOperationKind.Update,
                    out _);
                SynchronizeSelectedTargets();
                SetStatus(message, message.Contains("警告") || message.Contains("贯穿") ? StatusKind.Warning : StatusKind.Success);
                return true;
            }
            SetStatus(message, StatusKind.Error);
            return false;
        }
        finally
        {
            _applyInProgress = false;
            // Selection events raised by UnselectAll/Select can arrive after the
            // transaction. Re-read once more after the current UI event finishes.
            Application.Instance.AsyncInvoke(SynchronizeSelectedTargets);
        }
    }

    private bool ApplyDraftForExport()
    {
        if (_loadedComponents.Count == 0)
            return true;
        if (_holeEditingContext == HoleEditingContext.PlacementPreset)
            return true;
        if (RhinoDoc.ActiveDoc is not { } doc)
        {
            SetStatus("当前没有可更新的 Rhino 文档。", StatusKind.Warning);
            return false;
        }

        SaveControls();
        var template = CaptureUpdateTemplate();
        var drafts = _loadedComponents
            .Select(component => template.ApplyTo(component))
            .ToArray();
        var needsApply = _geometryDirty || drafts
            .Zip(_loadedComponents, (draft, component) => !FastenerGeometryParameters.Match(draft, component))
            .Any(changed => changed);
        if (!needsApply)
            return true;
        if (!FastenerComponentService.CreateOrReplaceMany(doc, drafts, out var saved, out var message))
        {
            SetStatus(message, StatusKind.Error);
            return false;
        }

        ComponentEditorSession.ActivateMany(
            doc,
            saved,
            false,
            ComponentActivationIntent.SynchronizeOnly);
        _loadedComponents = saved;
        _loadedComponent = saved.FirstOrDefault();
        _geometryDirty = false;
        doc.Views.Redraw();
        SetStatus(message, message.Contains("警告") || message.Contains("贯穿")
            ? StatusKind.Warning
            : StatusKind.Success);
        return true;
    }

    private static string KindCardText(FastenerKind kind) => kind switch
    {
        FastenerKind.SocketCap => "杯头",
        FastenerKind.Countersunk => "沉头",
        FastenerKind.HexBolt => "六角头",
        FastenerKind.HexNut => "六角螺母",
        FastenerKind.HeatSetInsert => "热熔螺母",
        _ => FastenerLabels.Kind(kind)
    };

    private void UpdateSummary()
    {
        var state = EditorState.Current;
        var kind = KindCardText(state.Kind);
        _summary.Text = state.Kind switch
        {
            FastenerKind.HexNut =>
                $"模板 · {FastenerLabels.NutStyle(state.HexNutStyle)} {state.Size} · 嵌入{state.HeadEmbedDepth:0.##}",
            FastenerKind.HeatSetInsert =>
                $"模板 · {kind} {state.Size}×{state.Length:0.##} · Ø{state.InsertOuterDiameter:0.##}",
            _ =>
                $"模板 · {kind} {state.Size}×{state.Length:0.##}"
                + (state.HeadEmbedDepth < 0
                    ? $" · 离面{Math.Abs(state.HeadEmbedDepth):0.##}"
                    : string.Empty)
                + (state.AssemblyMode switch
                {
                    ScrewAssemblyMode.EngagementOnly => " · 只咬合 · " + CompactDepthLabel(state.EngagementDepthMode),
                    ScrewAssemblyMode.NutFastened => $" · 螺母固定 · 露出{state.NutTipProtrusion:0.##}",
                    _ => " · " + CompactDepthLabel(state.EngagementDepthMode)
                })
        };
        _summary.ToolTip = _summary.Text;
        UpdateTemplateStrip();
        // Parameter edits must not depend on a selection event having already
        // refreshed the cached summary. Re-read the actual Rhino selection so
        // box-selected control points can be updated immediately.
        SynchronizeSelectedTargets();
        FastenerUiLocalization.ApplyTree(this);
    }

    private void MarkGeometryDirty()
    {
        if (_loadingControls)
            return;
        _geometryDirty = true;
        ShowSelectionSummary();
    }

    private static string CompactDepthLabel(DepthMode mode) => mode switch
    {
        DepthMode.ThroughTarget => "贯穿",
        DepthMode.FastenerLengthPlusOneDiameter => "L+1D",
        DepthMode.FastenerLengthPlusCustom => $"L+{EditorState.Current.EngagementBlindDepth:0.##}",
        DepthMode.FastenerLengthPlusTwoDiameters => "L+2D",
        DepthMode.Blind => "自定义深度",
        _ => FastenerLabels.Depth(mode)
    };

    private void UpdateHeadEmbedControls()
    {
        var kind = SelectedKind();
        var supportsEmbedDepth = FastenerKindTraits.SupportsEmbedDepth(kind);
        _headEmbed.MinValue = FastenerKindTraits.SupportsHeadGap(kind) ? -500 : 0;
        if (!FastenerKindTraits.SupportsHeadGap(kind) && _headEmbed.Value < 0)
            _headEmbed.Value = 0;
        _headEmbed.Enabled = supportsEmbedDepth;
        _zeroHeadButton.Enabled = supportsEmbedDepth;
        _flushHeadButton.Enabled = supportsEmbedDepth;
        _counterboreBridgeEnabled.Enabled = kind == FastenerKind.SocketCap
            && _headEmbed.Value > 0;
        _flushHeadButton.Text = kind == FastenerKind.HexNut ? "全埋" : "齐平";
        _flushHeadButton.ToolTip = kind == FastenerKind.HexNut
            ? "将嵌入深度设置为当前规格的标准螺母厚度"
            : "将螺丝头顶面设置为与宿主表面齐平";
        if (!supportsEmbedDepth && _headEmbed.Value != 0)
            _headEmbed.Value = 0;
    }

    private void ApplyEmbedDefaultForKindChange()
    {
        if (_loadingControls
            || SelectedKind() != FastenerKind.HexNut
            || EditorState.Current.Kind == FastenerKind.HexNut
            || string.IsNullOrWhiteSpace(_size.SelectedKey))
            return;
        _headEmbed.Value = CurrentHexNutHeight();
    }

    private void PreserveHexNutEmbedModeAcrossSizeChange()
    {
        if (_loadingControls
            || SelectedKind() != FastenerKind.HexNut
            || string.IsNullOrWhiteSpace(_size.SelectedKey)
            || string.IsNullOrWhiteSpace(EditorState.Current.Size)
            || EditorState.Current.Size == _size.SelectedKey)
            return;
        try
        {
            var previousSpec = CurrentSpec(EditorState.Current.Size);
            var previousThickness = HexNutDimensions.Resolve(
                EditorState.Current.HexNutStyle,
                previousSpec).TotalHeight;
            if (Math.Abs(_headEmbed.Value - previousThickness) <= 0.01)
            {
                _headEmbed.Value = CurrentHexNutHeight();
            }
        }
        catch
        {
            // Keep a custom absolute depth when either size cannot be resolved.
        }
    }

    private void SetFlushHeadDepth()
    {
        if (!Enum.TryParse<FastenerKind>(_kind.SelectedKey, out var kind)
            || !FastenerKindTraits.SupportsEmbedDepth(kind)
            || string.IsNullOrWhiteSpace(_size.SelectedKey))
            return;
        var spec = CurrentSpec(_size.SelectedKey);
        _headEmbed.Value = kind == FastenerKind.HexNut
            ? HexNutDimensions.Resolve(SelectedHexNutStyle(), spec).TotalHeight
            : HeadGeometryCalculator.GetHeadHeight(kind, spec);
    }

    private void HexNutStyleChanged()
    {
        if (_loadingControls || SelectedKind() != FastenerKind.HexNut)
            return;

        var preserveFullEmbed = IsCurrentHexNutFullEmbed();
        UpdateHexNutSizeAvailability();
        if (preserveFullEmbed)
            _headEmbed.Value = CurrentHexNutHeight();
        UpdateInsertSummary();
        ScheduleUpdate();
    }

    private bool IsCurrentHexNutFullEmbed()
    {
        if (string.IsNullOrWhiteSpace(_size.SelectedKey))
            return false;
        try
        {
            var spec = CurrentSpec(_size.SelectedKey);
            var previousHeight = HexNutDimensions.Resolve(
                EditorState.Current.HexNutStyle,
                spec).TotalHeight;
            return Math.Abs(_headEmbed.Value - previousHeight) <= 0.01;
        }
        catch
        {
            return false;
        }
    }

    private void UpdateHexNutSizeAvailability()
    {
        var locking = (SelectedKind() == FastenerKind.HexNut
                && SelectedHexNutStyle() == HexNutStyle.NylonInsertLocking)
            || (FastenerKindTraits.IsScrew(SelectedKind())
                && SelectedAssemblyMode() == ScrewAssemblyMode.NutFastened
                && _pairedNutLocking.Checked == true);
        foreach (var spec in RhinoMMPlugIn.Catalog.Sizes)
        {
            _size.SetEnabled(spec.Designation, !locking || HexNutDimensions.Supports(
                HexNutStyle.NylonInsertLocking,
                spec.Designation));
            _size.SetToolTip(
                spec.Designation,
                locking && spec.Designation is "M2" or "M2.5"
                    ? $"{spec.Designation} · DIN 985 工程扩展预设，非 GB/T 889.1；打印前请核对实物。"
                    : spec.Designation);
        }
        if (locking && !HexNutDimensions.Supports(
                HexNutStyle.NylonInsertLocking,
                _size.SelectedKey ?? string.Empty))
        {
            _size.Select("M2", false);
        }
    }

    private double CurrentHexNutHeight()
    {
        if (string.IsNullOrWhiteSpace(_size.SelectedKey))
            return 0;
        return HexNutDimensions.Resolve(
            SelectedHexNutStyle(),
            CurrentSpec(_size.SelectedKey)).TotalHeight;
    }

    private static FastenerSizeSpec CurrentSpec(string size)
    {
        var state = EditorState.Current;
        return state.CustomDefinitionSnapshot is { } custom
            && string.Equals(custom.ThreadDesignation, size, StringComparison.OrdinalIgnoreCase)
                ? custom.SizeSpec
                : RhinoMMPlugIn.Catalog.Get(size);
    }

    private void SetStatus(string message, StatusKind kind)
    {
        _successStatusTimer.Stop();
        _status.Text = message;
        _status.ToolTip = message;
        _status.Visible = !string.IsNullOrWhiteSpace(message);
        _statusHost.Visible = _status.Visible;
        _statusKind = kind;
        ApplyStatusTheme();
        if (kind == StatusKind.Success)
            _successStatusTimer.Start();
    }

    private void ApplyStatusTheme()
    {
        FastenerUiTheme.SetRole(_status, _statusKind switch
        {
            StatusKind.Success => FastenerThemeRole.StatusSuccess,
            StatusKind.Warning => FastenerThemeRole.StatusWarning,
            StatusKind.Error => FastenerThemeRole.StatusError,
            _ => FastenerThemeRole.SecondaryText
        });
    }

    private enum StatusKind
    {
        Info,
        Success,
        Warning,
        Error
    }
}
