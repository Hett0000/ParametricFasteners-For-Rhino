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
    private static readonly double[] CommonLengths = [8, 10, 12, 15, 20, 25, 30, 35, 40, 45, 50, 60];
    private static readonly DepthMode[] EngagementDepthModes =
    [
        DepthMode.ThroughTarget,
        DepthMode.FastenerLengthPlusOneDiameter,
        DepthMode.FastenerLengthPlusCustom
    ];

    private readonly CardSelector _kind = new();
    private readonly CardSelector _nutStyle = new();
    private readonly CardSelector _size = new();
    private readonly CardSelector _lengthCards = new();
    private readonly NumericStepper _length = new() { MinValue = 0, MaxValue = 500, DecimalPlaces = 2, Increment = 0.5 };
    private readonly NumericStepper _headEmbed = new() { MinValue = 0, MaxValue = 500, DecimalPlaces = 2, Increment = 0.1 };
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
        Wrap = WrapMode.Word
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
    private readonly CheckBox _presetClearanceBoolean = new() { Text = "导出布尔" };
    private readonly CheckBox _presetEngagementPreview = new() { Text = "预览" };
    private readonly CheckBox _presetEngagementBoolean = new() { Text = "导出布尔" };
    private readonly CheckBox _counterboreBridgeEnabled = new()
    {
        Text = "启用双层交叉桥"
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
    private readonly CheckBox _engagementOnly = new()
    {
        Text = "只咬合",
        ToolTip = "仅将放置面实体作为咬合宿主；螺杆必须完整容纳在该宿主内。"
    };
    private readonly Label _summary = new()
    {
        Text = "未读取组件",
        Font = new Font(SystemFont.Bold, 12),
        TextColor = FastenerUiTheme.PrimaryText,
        Wrap = WrapMode.Word,
        Height = 22
    };
    private readonly Label _status = new()
    {
        Text = "选择组件后点击“读取组件”。",
        Wrap = WrapMode.Word,
        Height = 18,
        TextColor = FastenerUiTheme.SecondaryText
    };
    private readonly DynamicLayout _parameterLayout = new() { Spacing = new Size(6, 6) };
    private readonly DynamicLayout _holeHeaderLayout = new() { Spacing = new Size(4, 4) };
    private readonly DynamicLayout _holeParameterLayout = new() { Spacing = new Size(6, 6) };
    private readonly DynamicLayout _presetOptionsLayout = new() { Spacing = new Size(4, 4) };
    private readonly Label _holeTitle = FastenerUiTheme.SectionTitle("孔与切割");
    private readonly Scrollable _scrollable;
    private readonly Button _applyButton = new() { Height = FastenerUiTheme.ActionButtonHeight, Enabled = true };
    private readonly Button _placeButton = new() { Height = FastenerUiTheme.ActionButtonHeight };
    private readonly Panel _actionsHost = new();
    private readonly List<Button> _actionButtons = [];
    private readonly Dictionary<Button, PanelActionIcon> _actionButtonIcons = [];
    private readonly UITimer _selectionTimer = new() { Interval = 0.06 };
    private FastenerComponentData? _loadedComponent;
    private IReadOnlyList<FastenerComponentData> _loadedComponents = [];
    private SelectedComponentSummary? _selectedSummary;
    private bool? _clearancePreviewOverride;
    private bool? _clearanceBooleanOverride;
    private bool? _engagementPreviewOverride;
    private bool? _engagementBooleanOverride;
    private bool? _installationPreviewOverride;
    private bool? _installationBooleanOverride;
    private bool _loadingControls;
    private bool _geometryDirty;
    private HoleEditingContext _holeEditingContext = HoleEditingContext.PlacementPreset;
    private ResponsiveLayoutProfile? _layoutProfile;
    private StatusKind _statusKind = StatusKind.Info;
    private FastenerThemePalette? _appliedTheme;

    public RhinoMMPanel()
    {
        Style = Panels.EtoPanelStyleName;
        FastenerUiTheme.RefreshPalette();
        FastenerUiTheme.SetRole(this, FastenerThemeRole.Canvas);
        FastenerUiTheme.SetRole(_insertFinalDiameter, FastenerThemeRole.SecondaryText);
        FastenerUiTheme.SetRole(_nutStandardDimensions, FastenerThemeRole.SecondaryText);
        FastenerUiTheme.SetRole(_insertChamferNote, FastenerThemeRole.SecondaryText);
        FastenerUiTheme.SetRole(_counterboreBridgeSummary, FastenerThemeRole.SecondaryText);
        FastenerUiTheme.SetRole(_holeDiameterSummary, FastenerThemeRole.SecondaryText);
        FastenerUiTheme.SetRole(_summary, FastenerThemeRole.PrimaryText);
        FastenerUiTheme.SetRole(_status, FastenerThemeRole.SecondaryText);
        FastenerUiTheme.ApplySecondary(_zeroHeadButton);
        FastenerUiTheme.ApplySecondary(_flushHeadButton);
        _insertSummaryHost.Content = _insertFinalDiameter;
        _insertNoteHost.Content = _insertChamferNote;
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
        _nutStyle.Add(
            HexNutStyle.Standard.ToString(),
            "普通",
            "普通六角螺母");
        _nutStyle.Add(
            HexNutStyle.NylonInsertLocking.ToString(),
            "尼龙防松",
            "尼龙防松螺母；M3–M12 为 GB/T 889.1-2015 兼容尺寸，M2、M2.5 为 DIN 985 工程扩展预设。");
        _nutStyle.SetColumns(2);
        foreach (var spec in RhinoMMPlugIn.Catalog.Sizes)
            _size.Add(spec.Designation, spec.Designation);
        foreach (var value in CommonLengths)
            _lengthCards.Add(value.ToString(System.Globalization.CultureInfo.InvariantCulture), value.ToString());
        _holeContext.Add(HoleEditingContext.CurrentComponent.ToString(), "当前组件");
        _holeContext.Add(HoleEditingContext.PlacementPreset.ToString(), "放置预设");
        _holeContext.SetColumns(2);
        _holeContext.SelectedKey = HoleEditingContext.PlacementPreset.ToString();
        _holeContextHost.Content = _holeContext;
        foreach (var mode in EngagementDepthModes)
            _presetDepth.Items.Add(new ListItem { Key = mode.ToString(), Text = FastenerLabels.Depth(mode) });
        LoadPlacementPresetControls();
        var dimensionCard = FastenerUiTheme.CreateCard(new StackLayout
        {
            Orientation = Orientation.Vertical,
            Spacing = FastenerUiTheme.SpaceSmall,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Items = { FastenerUiTheme.SectionTitle("紧固件尺寸"), _parameterLayout }
        }, 6);
        var holeCard = FastenerUiTheme.CreateCard(new StackLayout
        {
            Orientation = Orientation.Vertical,
            Spacing = FastenerUiTheme.SpaceSmall,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Items = { _holeHeaderLayout, _holeParameterLayout, _presetOptionsLayout }
        }, 6);
        var scrollingContent = new DynamicLayout
        {
            Padding = new Padding(0, 2, 0, FastenerUiTheme.SpaceSmall),
            Spacing = new Size(0, FastenerUiTheme.SpaceSmall)
        };
        scrollingContent.AddRow(dimensionCard);
        scrollingContent.AddRow(holeCard);

        _scrollable = new Scrollable
        {
            Border = BorderType.None,
            ExpandContentWidth = true,
            ExpandContentHeight = false,
            Content = scrollingContent
        };
        FastenerUiTheme.SetRole(_scrollable, FastenerThemeRole.Canvas);

        var actions = BuildActions();
        var summaryCard = FastenerUiTheme.CreateCard(new StackLayout
        {
            Orientation = Orientation.Vertical,
            Spacing = 3,
            Items = { _summary, _status }
        }, 6);
        var rootLayout = new TableLayout
        {
            Padding = new Padding(6, 4),
            Spacing = new Size(0, FastenerUiTheme.SpaceSmall),
            Rows =
            {
                new TableRow(summaryCard),
                new TableRow(_scrollable) { ScaleHeight = true },
                new TableRow(actions)
            }
        };
        FastenerUiTheme.SetRole(rootLayout, FastenerThemeRole.Canvas);
        Content = rootLayout;

        SizeChanged += (_, _) => RebuildResponsiveLayout();
        _scrollable.SizeChanged += (_, _) => RebuildResponsiveLayout();
        RhinoApp.AppSettingsChanged += RhinoAppSettingsChanged;
        RhinoDoc.SelectObjects += DocumentSelectionChanged;
        ComponentEditorSession.ActiveSelectionChanged += SessionSelectionChanged;
        WireEvents();
        LoadControls();
        RebuildResponsiveLayout(force: true);
        UpdateHoleContextPresentation();
        RefreshSelectedTargets();
        UpdateSummary();
        ApplyTheme();
    }

    public void PanelShown(uint documentSerialNumber, ShowPanelReason reason)
    {
        ApplyTheme();
        var doc = RhinoDoc.ActiveDoc;
        if (doc is null)
            return;
        if (ComponentEditorSession.TryGetActiveSet(
                doc,
                out var activeComponents,
                out var intent)
            && intent == ComponentActivationIntent.LoadIntoEditor)
            ActivateComponents(activeComponents);
        RefreshSelectedTargets();
    }

    public void PanelHidden(uint documentSerialNumber, ShowPanelReason reason) { }

    public void PanelClosing(uint documentSerialNumber, bool onCloseDocument)
    {
        RhinoApp.AppSettingsChanged -= RhinoAppSettingsChanged;
        RhinoDoc.SelectObjects -= DocumentSelectionChanged;
        ComponentEditorSession.ActiveSelectionChanged -= SessionSelectionChanged;
    }

    private void RhinoAppSettingsChanged(object? sender, EventArgs e) =>
        Application.Instance.AsyncInvoke(ApplyTheme);

    private void ApplyTheme()
    {
        FastenerUiTheme.RefreshPalette();
        if (_appliedTheme == FastenerUiTheme.Palette)
            return;
        _appliedTheme = FastenerUiTheme.Palette;
        FastenerUiTheme.ApplyTree(this);
        _kind.RefreshTheme();
        _nutStyle.RefreshTheme();
        _size.RefreshTheme();
        _lengthCards.RefreshTheme();
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
            RefreshSelectedTargets();
        };
        _kind.SelectedKeyChanged += (_, _) =>
        {
            UpdateHexNutSizeAvailability();
            ApplyEmbedDefaultForKindChange();
            LoadHeatSetDefaultsForNewKind();
            UpdateHeadEmbedControls();
            RebuildResponsiveLayout(force: true);
            UpdateInsertSummary();
            UpdateHoleDiameterSummary();
            ScheduleUpdate();
        };
        _nutStyle.SelectedKeyChanged += (_, _) => HexNutStyleChanged();
        _size.SelectedKeyChanged += (_, _) =>
        {
            PreserveHexNutEmbedModeAcrossSizeChange();
            UpdateInsertSummary();
            UpdateHoleDiameterSummary();
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
        _counterboreBridgeEnabled.CheckedChanged += (_, _) =>
            CounterboreBridgeChanged(rebuildLayout: true);
        _counterboreBridgeLayerHeight.ValueChanged += (_, _) =>
            CounterboreBridgeChanged(rebuildLayout: false);
        _engagementOnly.CheckedChanged += (_, _) => EngagementOnlyChanged();
        _zeroHeadButton.Click += (_, _) => _headEmbed.Value = 0;
        _flushHeadButton.Click += (_, _) => SetFlushHeadDepth();
    }

    private Control BuildActions()
    {
        var load = MakeActionButton(
            PanelActionIcon.Read,
            "读取组件：读取选中控制点的参数",
            (_, _) => LoadSelection());
        ConfigureActionButton(
            _placeButton,
            PanelActionIcon.Place,
            "放置 / 绑定：创建并绑定新的紧固件",
            (_, _) => Run("_-ParametricFastenersPlace"));
        ConfigureActionButton(
            _applyButton,
            PanelActionIcon.Apply,
            "应用更新：更新选中的控制点组件",
            (_, _) => ApplyLoaded());
        var refresh = MakeActionButton(
            PanelActionIcon.Refresh,
            "刷新 / 清理：修复位置并清理无效组件",
            (_, _) => RefreshDocument());
        var toRhino = MakeActionButton(
            PanelActionIcon.Rhino,
            "放入 Rhino｜左击：仅布尔宿主｜右击：布尔宿主 + 紧固件实体",
            (_, _) => RunExport("_-ParametricFastenersExportToRhino"));
        toRhino.MouseDown += (_, e) =>
        {
            if (!e.Buttons.HasFlag(MouseButtons.Alternate))
                return;
            e.Handled = true;
            Application.Instance.AsyncInvoke(() =>
                RunExport("_-ParametricFastenersExportToRhinoWithFasteners"));
        };
        var step = MakeActionButton(
            PanelActionIcon.Step,
            "导出 STEP：布尔计算后导出 STEP",
            (_, _) => RunExport("_-ParametricFastenersExportStep"));
        var statistics = MakeActionButton(
            PanelActionIcon.Statistics,
            "紧固件统计：统计数量并可保存 Excel",
            (_, _) => Run("_-ParametricFastenersStatistics"));
        var more = new Button();
        ConfigureActionButton(
            more,
            PanelActionIcon.More,
            "更多：全局显示、导出 STL、转换模型和校验文档",
            (_, _) => ShowMoreMenu(more));
        _actionButtons.AddRange([
            load, _placeButton, _applyButton, refresh,
            toRhino, step, statistics, more
        ]);
        RebuildActionLayout();
        UpdatePrimaryActionStyle();
        return FastenerUiTheme.CreateCard(_actionsHost, FastenerUiTheme.SpaceSmall);
    }

    private void RebuildActionLayout()
    {
        var cells = new List<TableCell>();
        for (var index = 0; index < _actionButtons.Count; index++)
        {
            if (index == 4)
                cells.Add(new TableCell(new Panel { Width = 4 }));
            cells.Add(new TableCell(_actionButtons[index], true));
        }
        var grid = new TableLayout { Spacing = new Size(2, 0) };
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

        var succeeded = ComponentRefreshService.CleanMissingControlPoints(doc, out var result, out var message);
        if (!succeeded)
        {
            SetStatus(message, StatusKind.Error);
            return;
        }

        var repairedSelection = ComponentRepository.ReadSelectedControlPoints(doc);
        if (repairedSelection.Count > 0)
        {
            ComponentEditorSession.ActivateMany(
                doc,
                repairedSelection,
                false,
                ComponentActivationIntent.SynchronizeOnly);
        }
        else
        {
            var remaining = _loadedComponents
                .Select(component => ComponentRepository.TryReadComponent(doc, component.ComponentId, out var current)
                    ? current
                    : null)
                .Where(component => component is not null)
                .Cast<FastenerComponentData>()
                .ToArray();
            if (remaining.Length > 0)
                ComponentEditorSession.ActivateMany(
                    doc,
                    remaining,
                    false,
                    ComponentActivationIntent.SynchronizeOnly);
            else
                ComponentEditorSession.Forget(doc);
        }

        RefreshSelectedTargets();
        SetStatus(message, result.FailedComponents > 0 ? StatusKind.Warning : StatusKind.Success);
    }

    private void ShowMoreMenu(Control owner)
    {
        var display = new ButtonMenuItem { Text = "全局显示…" };
        display.Click += (_, _) => GlobalDisplaySettingsDialog.Show(RhinoDoc.ActiveDoc);
        var stl = new ButtonMenuItem { Text = "导出 STL…" };
        stl.Click += (_, _) => RunExport("_-ParametricFastenersExportStl");
        var adopt = new ButtonMenuItem { Text = "转换选中模型" };
        adopt.Click += (_, _) => Run("_-ParametricFastenersAdopt");
        var validate = new ButtonMenuItem { Text = "校验文档" };
        validate.Click += (_, _) => Run("_-ParametricFastenersValidate");
        var menu = new ContextMenu
        {
            Items =
            {
                display,
                new SeparatorMenuItem(),
                stl,
                new SeparatorMenuItem(),
                adopt,
                validate
            }
        };
        menu.Show(owner);
    }

    private void RebuildResponsiveLayout(bool force = false)
    {
        var profile = ResponsiveLayoutProfile.ForWidth(AvailableContentWidth());
        if (!force && _layoutProfile == profile)
            return;
        _layoutProfile = profile;
        RebuildActionLayout();
        _kind.SetColumns(profile.KindColumns);
        _size.SetColumns(profile.SizeColumns);
        _lengthCards.SetColumns(profile.LengthColumns);
        ApplyCompactFieldWidths(profile);
        _parameterLayout.Clear();
        _holeHeaderLayout.Clear();
        _holeParameterLayout.Clear();
        _presetOptionsLayout.Clear();
        _parameterLayout.AddRow(FieldStack("类型", _kind));
        var selectedKind = SelectedKind();
        if (selectedKind == FastenerKind.HexNut)
        {
            _parameterLayout.AddRow(FieldStack("螺母样式", _nutStyle));
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
            _parameterLayout.AddRow(FieldStack("常用长度 mm", _lengthCards));
            if (profile.PairDimensionFields)
                _parameterLayout.AddRow(CompactRow(
                    FieldStack("自定义长度 mm", _length),
                    FieldStack("嵌入深度 mm", _headEmbedControl)));
            else
            {
                _parameterLayout.AddRow(CompactRow(FieldStack("自定义长度 mm", _length)));
                _parameterLayout.AddRow(CompactRow(FieldStack("嵌入深度 mm", _headEmbedControl)));
            }
        }

        _holeHeaderLayout.AddRow(CompactRow(_holeTitle, _holeContextHost));
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
            _holeParameterLayout.AddRow(CompactRow(
                FieldStack("孔径修正 mm", _printerCorrection),
                FieldStack("咬合缩减 mm", _bite),
                _holeDiameterSummary));
        }
        else if (profile.HoleFieldColumns == 2)
        {
            _holeParameterLayout.AddRow(CompactRow(
                FieldStack("孔径修正 mm", _printerCorrection),
                FieldStack("咬合缩减 mm", _bite)));
            _holeParameterLayout.AddRow(CompactRow(_holeDiameterSummary));
        }
        else
        {
            _parameterLayout.AddRow(FieldStack("规格", _size));
            _holeParameterLayout.AddRow(CompactRow(FieldStack("孔径修正 mm", _printerCorrection)));
            _holeParameterLayout.AddRow(CompactRow(FieldStack("咬合缩减 mm", _bite)));
            _holeParameterLayout.AddRow(CompactRow(_holeDiameterSummary));
        }

        AddPlacementPresetRows(profile.HoleFieldColumns);
        _parameterLayout.Create();
        _holeHeaderLayout.Create();
        _holeParameterLayout.Create();
        _presetOptionsLayout.Create();
        UpdateHoleContextPresentation();

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
            _presetOptionsLayout.AddRow(CompactRow(
                FieldStack("安装槽/孔模块", ToggleRow(
                    _presetClearancePreview,
                    _presetClearanceBoolean))));
            return;
        }
        var isEngagementOnly = _engagementOnly.Checked == true;
        var clearanceOptions = FieldStack("通孔模块", ToggleRow(_presetClearancePreview, _presetClearanceBoolean));
        var depth = FieldStack("咬合孔深度", _presetDepth);
        var engagementOptions = FieldStack("咬合孔模块", ToggleRow(_presetEngagementPreview, _presetEngagementBoolean));
        var engagementOnly = FieldStack("加强筋模式", _engagementOnly);
        if (isEngagementOnly)
        {
            _presetOptionsLayout.AddRow(CompactRow(depth, engagementOnly));
            _presetOptionsLayout.AddRow(CompactRow(engagementOptions));
        }
        else
        {
            _presetOptionsLayout.AddRow(CompactRow(clearanceOptions, engagementOptions));
            _presetOptionsLayout.AddRow(CompactRow(depth, engagementOnly));
        }

        if (_presetDepth.SelectedKey == DepthMode.FastenerLengthPlusCustom.ToString())
        {
            _presetOptionsLayout.AddRow(CompactRow(
                FieldStack("追加深度 mm", _presetBlindDepth)));
        }

        if (SelectedKind() == FastenerKind.SocketCap)
        {
            _presetOptionsLayout.AddRow(CompactRow(
                FieldStack("悬垂沉孔架桥", _counterboreBridgeEnabled)));
            if (_counterboreBridgeEnabled.Checked == true)
            {
                _presetOptionsLayout.AddRow(CompactRow(
                    FieldStack("架桥层高 mm", _counterboreBridgeLayerHeight),
                    _counterboreBridgeSummary));
            }
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
        _counterboreBridgeLayerHeight.Width = profile.NumericFieldWidth;
        var insertInfoWidth = Math.Max(0, AvailableContentWidth() - 32);
        _insertSummaryHost.Width = insertInfoWidth;
        _insertNoteHost.Width = insertInfoWidth;
        _insertFinalDiameter.Width = insertInfoWidth;
        _insertChamferNote.Width = insertInfoWidth;
    }

    private static StackLayout ToggleRow(CheckBox preview, CheckBox booleanEnabled) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = FastenerUiTheme.SpaceSmall,
        Items = { preview, booleanEnabled }
    };

    private static StackLayout CompactRow(params Control[] controls)
    {
        var row = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
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
        Spacing = 2,
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        Items = { FastenerUiTheme.PrimaryLabel(label), control }
    };

    private Button MakeActionButton(
        PanelActionIcon icon,
        string toolTip,
        EventHandler<EventArgs> handler)
    {
        var button = new Button();
        ConfigureActionButton(button, icon, toolTip, handler);
        return button;
    }

    private void ConfigureActionButton(
        Button button,
        PanelActionIcon icon,
        string toolTip,
        EventHandler<EventArgs> handler)
    {
        button.Text = string.Empty;
        button.ToolTip = toolTip;
        button.Height = FastenerUiTheme.ActionButtonHeight;
        button.MinimumSize = new Size(0, FastenerUiTheme.ActionButtonHeight);
        button.Click += handler;
        _actionButtonIcons[button] = icon;
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
        Application.Instance.AsyncInvoke(() =>
        {
            _selectionTimer.Stop();
            _selectionTimer.Start();
        });
    }

    private void RefreshSelectedTargets()
    {
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
        var text = _selectedSummary?.ShortText ?? "未选择更新目标";
        _status.Text = text;
        _status.ToolTip = _selectedSummary?.FullText ?? text;
        _statusKind = _selectedSummary?.HasBlockingIssues == true
            ? StatusKind.Warning
            : StatusKind.Info;
        ApplyStatusTheme();
    }

    private void SessionSelectionChanged(object? sender, ComponentSelectionChangedEventArgs e)
    {
        if (RhinoDoc.ActiveDoc?.RuntimeSerialNumber != e.Document.RuntimeSerialNumber)
            return;
        if (e.Intent == ComponentActivationIntent.LoadIntoEditor)
            ActivateComponents(e.Components);
        else
            RefreshLoadedComponentCache(e.Document, e.Components);
        RefreshSelectedTargets();
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
        _nutStyle.Select(state.HexNutStyle.ToString(), false);
        _size.Select(state.Size, false);
        UpdateHexNutSizeAvailability();
        _length.Value = state.Length;
        UpdateLengthCardSelection();
        _headEmbed.Value = state.HeadEmbedDepth;
        _counterboreBridgeEnabled.Checked = state.CounterboreBridgeEnabled;
        _counterboreBridgeLayerHeight.Value = state.CounterboreBridgeLayerHeight;
        _engagementOnly.Checked = state.EngagementOnly;
        UpdateCounterboreBridgeSummary();
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
        UpdateHeadEmbedControls();
        UpdateInsertSummary();
        _loadingControls = false;
        RebuildResponsiveLayout(force: true);
    }

    private void SaveControls()
    {
        var state = EditorState.Current;
        if (Enum.TryParse<FastenerKind>(_kind.SelectedKey, out var kind))
            state.Kind = kind;
        state.HexNutStyle = state.Kind == FastenerKind.HexNut
            && Enum.TryParse<HexNutStyle>(_nutStyle.SelectedKey, out var nutStyle)
                ? nutStyle
                : HexNutStyle.Standard;
        if (!string.IsNullOrWhiteSpace(_size.SelectedKey))
            state.Size = _size.SelectedKey;
        state.Length = _length.Value;
        state.HeadEmbedDepth = FastenerKindTraits.SupportsEmbedDepth(state.Kind) ? _headEmbed.Value : 0;
        state.CounterboreBridgeEnabled = state.Kind == FastenerKind.SocketCap
            && _counterboreBridgeEnabled.Checked == true;
        state.CounterboreBridgeLayerHeight = _counterboreBridgeLayerHeight.Value;
        state.EngagementOnly = !FastenerKindTraits.IsNut(state.Kind)
            && _engagementOnly.Checked == true;
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
            InstallationBooleanEnabled = _installationBooleanOverride
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
        _engagementOnly.Checked = preset.EngagementOnly;
        EditorState.Current.EngagementOnly = preset.EngagementOnly;
        UpdateCounterboreBridgeSummary();
        SetCustomDepthVisibility(
            preset.EngagementDepthMode == DepthMode.FastenerLengthPlusCustom);
        _presetClearancePreview.Checked = preset.ClearancePreviewVisible;
        _presetClearanceBoolean.Checked = preset.ClearanceBooleanEnabled;
        _presetEngagementPreview.Checked = preset.EngagementPreviewVisible;
        _presetEngagementBoolean.Checked = preset.EngagementBooleanEnabled;
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
            var spec = RhinoMMPlugIn.Catalog.Get(_size.SelectedKey);
            if (SelectedKind() == FastenerKind.HexNut)
            {
                var nutDimensions = HexNutDimensions.Resolve(SelectedHexNutStyle(), spec);
                var finalAcrossFlats = nutDimensions.AcrossFlats + _printerCorrection.Value;
                var extension = nutDimensions.IsEngineeringExtension
                    ? " · 工程预设，打印前核对实物"
                    : string.Empty;
                _nutStandardDimensions.Text =
                    $"{nutDimensions.Standard} · 对边 {nutDimensions.AcrossFlats:0.###} · 总高 {nutDimensions.TotalHeight:0.###} · 槽 {finalAcrossFlats:0.###} · 嵌入 {_headEmbed.Value:0.###} mm{extension}";
                _nutStandardDimensions.ToolTip = nutDimensions.IsEngineeringExtension
                    ? "M2、M2.5 不属于 GB/T 889.1-2015 尺寸范围；此处采用 DIN 985 工程扩展预设，切割前请核对实际采购件。"
                    : "尼龙防松螺母尺寸兼容 GB/T 889.1-2015。";
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
            _placeButton.Enabled = heatSetValid;
            if (!heatSetValid)
                _placeButton.ToolTip = "放置 / 绑定：请先输入有效的热熔螺母长度、外径、孔径补偿和深度补偿";
            else
                _placeButton.ToolTip = "放置 / 绑定：创建并绑定新的紧固件";
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
        Enum.TryParse<HexNutStyle>(_nutStyle.SelectedKey, out var style)
            ? style
            : EditorState.Current.HexNutStyle;

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
            CounterboreBridgeEnabled = _counterboreBridgeEnabled.Checked == true,
            CounterboreBridgeLayerHeight = _counterboreBridgeLayerHeight.Value,
            ClearancePreviewVisible = _presetClearancePreview.Checked == true,
            ClearanceBooleanEnabled = _presetClearanceBoolean.Checked == true,
            EngagementPreviewVisible = _presetEngagementPreview.Checked == true,
            EngagementBooleanEnabled = _presetEngagementBoolean.Checked == true,
            EngagementOnly = _engagementOnly.Checked == true
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
            var nominal = RhinoMMPlugIn.Catalog.Get(_size.SelectedKey).NominalDiameter;
            var clearance = nominal + _printerCorrection.Value;
            var engagement = nominal - _bite.Value;
            _holeDiameterSummary.Text = $"通孔 Ø{clearance:0.###} · 咬合 Ø{engagement:0.###}";
            _holeDiameterSummary.ToolTip =
                $"通孔 = 公称直径 {nominal:0.###} + 孔径修正 {_printerCorrection.Value:0.###} = {clearance:0.###} mm\n"
                + $"咬合 = 公称直径 {nominal:0.###} − 咬合缩减 {_bite.Value:0.###} = {engagement:0.###} mm";
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
            _printerCorrection.Value = state.PrinterCorrection;
            _bite.Value = state.BiteReduction;
            _presetDepth.SelectedKey = state.EngagementDepthMode.ToString();
            _presetBlindDepth.Value = state.EngagementBlindDepth;
            _counterboreBridgeEnabled.Checked = state.CounterboreBridgeEnabled;
            _counterboreBridgeLayerHeight.Value = state.CounterboreBridgeLayerHeight;
            UpdateCounterboreBridgeSummary();
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
        _holeContextHost.Visible = hasComponents;
        _holeContext.SetEnabled(HoleEditingContext.CurrentComponent.ToString(), hasComponents);
        _presetOptionsLayout.Visible = true;
        var selected = _selectedSummary;
        _applyButton.Enabled = selected is { HasTargets: true, HasBlockingIssues: false };
        _applyButton.ToolTip = selected?.HasBlockingIssues == true
            ? "应用更新：选中组件需要先运行“刷新 / 清理”"
            : selected is { HasTargets: true }
                ? $"应用更新：将面板模板应用到 {selected.Components.Count} 个控制点组件"
                : "应用更新：请先选中一个或多个控制点";
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

    private void EngagementOnlyChanged()
    {
        if (_loadingControls)
            return;
        SaveControls();
        if (_holeEditingContext == HoleEditingContext.PlacementPreset)
            SavePlacementPresetControls();
        else
            MarkGeometryDirty();
        NotifySmartPlacementDraftChanged();
        UpdateSummary();
        RebuildResponsiveLayout(force: true);
    }

    private void UpdateCounterboreBridgeSummary()
    {
        var layerHeight = _counterboreBridgeLayerHeight.Value;
        _counterboreBridgeSummary.Text =
            $"双层 · 总高 {layerHeight * 2:0.00} mm";
        _counterboreBridgeSummary.ToolTip =
            "第一层形成两条切线桥，第二层形成垂直桥，随后恢复圆形螺杆孔。";
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
            ? effectiveRole == ShaftFitRole.ThreadEngagement
                ? _presetEngagementPreview.Checked == true
                : _presetClearancePreview.Checked == true
            : effectiveRole == ShaftFitRole.ThreadEngagement
                ? _presetEngagementBoolean.Checked == true
                : _presetClearanceBoolean.Checked == true;
        SetModuleOverride(effectiveRole, isPreview, value);
        if (_holeEditingContext == HoleEditingContext.PlacementPreset)
        {
            SaveActivePlacementPresetControls();
            ShowSelectionSummary();
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
    }

    private void LoadCurrentModuleVisibility()
    {
        var bindings = _loadedComponents.SelectMany(component => component.Bindings).ToArray();
        var clearance = bindings.FirstOrDefault(binding => binding.Role == ShaftFitRole.Clearance);
        var engagement = bindings.FirstOrDefault(binding => binding.Role == ShaftFitRole.ThreadEngagement);
        var installation = bindings.FirstOrDefault(binding => binding.Role == ShaftFitRole.InstallationPocket);
        _presetClearancePreview.Checked =
            (clearance ?? installation)?.IsPreviewVisible ?? true;
        _presetClearanceBoolean.Checked =
            (clearance ?? installation)?.IsBooleanEnabled ?? true;
        _presetEngagementPreview.Checked = engagement?.IsPreviewVisible ?? true;
        _presetEngagementBoolean.Checked = engagement?.IsBooleanEnabled ?? true;
    }

    private void ApplyTopLevelVisibilityToLoadedComponents(
        ShaftFitRole role,
        bool isPreview,
        bool value)
    {
        if (_loadedComponents.Count == 0 || RhinoDoc.ActiveDoc is not { } doc)
        {
            ShowSelectionSummary();
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
        if (RhinoDoc.ActiveDoc is not { } doc)
        {
            SetStatus("当前没有可更新的 Rhino 文档。", StatusKind.Warning);
            return false;
        }
        RefreshSelectedTargets();
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
        if (selectedComponents.Count > 1)
        {
            var targetIsNut = FastenerKindTraits.IsNut(EditorState.Current.Kind);
            if (selectedComponents.Any(component => FastenerKindTraits.IsNut(component.Kind) != targetIsNut))
            {
                SetStatus("批量更新不能在螺丝与螺母类别之间转换；请重新放置对应类型。", StatusKind.Warning);
                return false;
            }
            if (targetIsNut && selectedComponents.Any(component =>
                    component.Bindings.Count != 1
                    || component.Bindings[0].Role != ShaftFitRole.InstallationPocket))
            {
                SetStatus("批量更新螺母要求每个组件都具有一个有效的安装宿主。", StatusKind.Warning);
                return false;
            }
        }

        var keepLoadedComponentDetails = selectedComponents.Count == 1
            && _loadedComponents.Count == 1
            && selectedComponents[0].ComponentId == _loadedComponents[0].ComponentId;
        var template = CaptureUpdateTemplate();
        var drafts = selectedComponents
            .Select(component => template.ApplyTo(component))
            .ToArray();
        if (FastenerComponentService.CreateOrReplaceMany(doc, drafts, out var saved, out var message))
        {
            doc.Objects.UnselectAll(false);
            foreach (var component in saved)
            {
                var controlPoint = ComponentRepository.FindControlPoint(doc, component.ComponentId);
                if (controlPoint is not null)
                    doc.Objects.Select(controlPoint.Id, false);
            }
            ComponentEditorSession.ActivateMany(
                doc,
                saved,
                false,
                ComponentActivationIntent.SynchronizeOnly);
            if (keepLoadedComponentDetails)
            {
                _loadedComponents = saved;
                _loadedComponent = saved[0];
            }
            _geometryDirty = false;
            doc.Views.Redraw();
            RefreshSelectedTargets();
            SetStatus(message, message.Contains("警告") || message.Contains("贯穿") ? StatusKind.Warning : StatusKind.Success);
            return true;
        }
        SetStatus(message, StatusKind.Error);
        return false;
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
                $"模板 · {state.Size} · {FastenerLabels.NutStyle(state.HexNutStyle)} · 嵌入{state.HeadEmbedDepth:0.##}",
            FastenerKind.HeatSetInsert =>
                $"模板 · {state.Size} · {kind} · L{state.Length:0.##} · Ø{state.InsertOuterDiameter:0.##}",
            _ =>
                $"模板 · {state.Size} · {kind} · L{state.Length:0.##}"
                + $"{(state.EngagementOnly ? " · 只咬合" : string.Empty)}"
                + $" · {CompactDepthLabel(state.EngagementDepthMode)}"
        };
        _summary.ToolTip = _summary.Text;
        ShowSelectionSummary();
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
        _headEmbed.Enabled = supportsEmbedDepth;
        _zeroHeadButton.Enabled = supportsEmbedDepth;
        _flushHeadButton.Enabled = supportsEmbedDepth;
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
            var previousSpec = RhinoMMPlugIn.Catalog.Get(EditorState.Current.Size);
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
        var spec = RhinoMMPlugIn.Catalog.Get(_size.SelectedKey);
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
            var spec = RhinoMMPlugIn.Catalog.Get(_size.SelectedKey);
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
        var locking = SelectedKind() == FastenerKind.HexNut
            && SelectedHexNutStyle() == HexNutStyle.NylonInsertLocking;
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
            RhinoMMPlugIn.Catalog.Get(_size.SelectedKey)).TotalHeight;
    }

    private void SetStatus(string message, StatusKind kind)
    {
        _status.Text = message;
        _status.ToolTip = message;
        _statusKind = kind;
        ApplyStatusTheme();
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
