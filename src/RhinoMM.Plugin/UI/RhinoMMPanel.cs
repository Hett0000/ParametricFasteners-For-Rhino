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
        DepthMode.FastenerLengthPlusTwoDiameters,
        DepthMode.Blind
    ];

    private readonly CardSelector _kind = new();
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
    private readonly DropDown _clearanceFit = new();
    private readonly NumericStepper _bite = new() { MinValue = 0, MaxValue = 5, DecimalPlaces = 3, Increment = 0.05 };
    private readonly CardSelector _holeContext = new();
    private readonly Panel _holeContextHost = new() { Width = 180 };
    private readonly DropDown _presetDepth = new();
    private readonly NumericStepper _presetBlindDepth = new() { MinValue = 0.1, MaxValue = 1000, DecimalPlaces = 2, Increment = 0.5 };
    private readonly CheckBox _presetClearancePreview = new() { Text = "预览" };
    private readonly CheckBox _presetClearanceBoolean = new() { Text = "导出布尔" };
    private readonly CheckBox _presetEngagementPreview = new() { Text = "预览" };
    private readonly CheckBox _presetEngagementBoolean = new() { Text = "导出布尔" };
    private readonly Slider _fastenerOpacity = new() { MinValue = 0, MaxValue = 100, Value = 70 };
    private readonly Slider _cutterOpacity = new() { MinValue = 0, MaxValue = 100, Value = 35 };
    private readonly Label _fastenerOpacityValue = new() { Text = "70%", Width = 38 };
    private readonly Label _cutterOpacityValue = new() { Text = "35%", Width = 38 };
    private readonly StackLayout _moduleList = new() { Orientation = Orientation.Vertical, Spacing = 6 };
    private readonly Label _summary = new()
    {
        Text = "未读取组件",
        Font = new Font(SystemFont.Bold, 12),
        TextColor = FastenerUiTheme.PrimaryText,
        Wrap = WrapMode.None,
        Height = 22
    };
    private readonly Label _status = new()
    {
        Text = "选择组件后点击“读取组件”。",
        Wrap = WrapMode.None,
        Height = 18,
        TextColor = FastenerUiTheme.SecondaryText
    };
    private readonly DynamicLayout _parameterLayout = new() { Spacing = new Size(6, 6) };
    private readonly DynamicLayout _holeHeaderLayout = new() { Spacing = new Size(4, 4) };
    private readonly DynamicLayout _holeParameterLayout = new() { Spacing = new Size(6, 6) };
    private readonly DynamicLayout _presetOptionsLayout = new() { Spacing = new Size(4, 4) };
    private readonly Label _holeTitle = FastenerUiTheme.SectionTitle("孔与切割");
    private readonly Label _moduleHeader = SectionHeader("宿主切割模块（0）");
    private readonly Panel _moduleCard;
    private readonly Scrollable _scrollable;
    private readonly Expander _displayExpander;
    private readonly Expander _helpExpander;
    private readonly Button _applyButton = new() { Height = FastenerUiTheme.ActionButtonHeight, Enabled = true };
    private readonly Button _placeButton = new() { Height = FastenerUiTheme.ActionButtonHeight };
    private readonly Panel _actionsHost = new();
    private readonly List<Button> _actionButtons = [];
    private readonly Dictionary<Button, PanelActionIcon> _actionButtonIcons = [];
    private readonly UITimer _displayTimer = new() { Interval = 0.1 };
    private FastenerComponentData? _loadedComponent;
    private IReadOnlyList<FastenerComponentData> _loadedComponents = [];
    private IReadOnlyList<HoleTargetBinding>? _draftBindings;
    private BatchModuleTemplate? _batchModuleTemplate;
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
        FastenerUiTheme.SetRole(_summary, FastenerThemeRole.PrimaryText);
        FastenerUiTheme.SetRole(_status, FastenerThemeRole.SecondaryText);
        FastenerUiTheme.SetRole(_fastenerOpacityValue, FastenerThemeRole.PrimaryText);
        FastenerUiTheme.SetRole(_cutterOpacityValue, FastenerThemeRole.PrimaryText);
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
        foreach (var spec in RhinoMMPlugIn.Catalog.Sizes)
            _size.Add(spec.Designation, spec.Designation);
        foreach (var value in CommonLengths)
            _lengthCards.Add(value.ToString(System.Globalization.CultureInfo.InvariantCulture), value.ToString());
        foreach (var value in Enum.GetValues<ClearanceFitClass>())
            _clearanceFit.Items.Add(new ListItem { Key = value.ToString(), Text = FastenerLabels.ClearanceFit(value) });
        _holeContext.Add(HoleEditingContext.CurrentComponent.ToString(), "当前组件");
        _holeContext.Add(HoleEditingContext.PlacementPreset.ToString(), "放置预设");
        _holeContext.SetColumns(2);
        _holeContext.SelectedKey = HoleEditingContext.PlacementPreset.ToString();
        _holeContextHost.Content = _holeContext;
        foreach (var mode in EngagementDepthModes)
            _presetDepth.Items.Add(new ListItem { Key = mode.ToString(), Text = FastenerLabels.Depth(mode) });
        LoadPlacementPresetControls();
        var globalDisplay = GlobalDisplaySettingsService.Current;
        _fastenerOpacity.Value = (int)Math.Round(globalDisplay.FastenerOpacityPercent);
        _cutterOpacity.Value = (int)Math.Round(globalDisplay.CutterOpacityPercent);
        UpdateOpacityLabels();

        _displayExpander = new Expander
        {
            Header = FastenerUiTheme.SectionTitle("全局显示"),
            Expanded = false,
            Content = new StackLayout
            {
                Orientation = Orientation.Vertical,
                Spacing = FastenerUiTheme.SpaceSmall,
                Items =
                {
                    FieldStack("紧固件不透明度", OpacityControl(_fastenerOpacity, _fastenerOpacityValue)),
                    FieldStack("切割模块不透明度", OpacityControl(_cutterOpacity, _cutterOpacityValue)),
                    FastenerUiTheme.Register(new Label
                    {
                        Text = "应用于当前文档全部组件，并作为后续新建组件默认值。",
                        Wrap = WrapMode.Word
                    }, FastenerThemeRole.SecondaryText)
                }
            }
        };
        _helpExpander = new Expander
        {
            Header = FastenerUiTheme.SectionTitle("使用说明"),
            Expanded = false,
            Content = FastenerUiTheme.Register(new Label
            {
                Text = "通孔使用标准间隙与打印修正；咬合孔使用公称直径减去咬合缩减。咬合缩减请通过打印试片校准。线框模式不显示材质透明度。",
                Wrap = WrapMode.Word
            }, FastenerThemeRole.SecondaryText)
        };

        var dimensionCard = FastenerUiTheme.CreateCard(new StackLayout
        {
            Orientation = Orientation.Vertical,
            Spacing = FastenerUiTheme.SpaceSmall,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Items = { FastenerUiTheme.SectionTitle("紧固件尺寸"), _parameterLayout }
        });
        var holeCard = FastenerUiTheme.CreateCard(new StackLayout
        {
            Orientation = Orientation.Vertical,
            Spacing = FastenerUiTheme.SpaceSmall,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Items = { _holeHeaderLayout, _holeParameterLayout, _presetOptionsLayout }
        });
        _moduleCard = FastenerUiTheme.CreateCard(new StackLayout
        {
            Orientation = Orientation.Vertical,
            Spacing = FastenerUiTheme.SpaceSmall,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Items = { _moduleHeader, _moduleList }
        });
        var scrollingContent = new DynamicLayout
        {
            Padding = new Padding(0, 2, 0, FastenerUiTheme.SpaceSmall),
            Spacing = new Size(0, FastenerUiTheme.SpaceSmall)
        };
        scrollingContent.AddRow(dimensionCard);
        scrollingContent.AddRow(holeCard);
        scrollingContent.AddRow(FastenerUiTheme.CreateCard(_displayExpander));
        scrollingContent.AddRow(_moduleCard);
        scrollingContent.AddRow(FastenerUiTheme.CreateCard(_helpExpander));

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
        ComponentEditorSession.ActiveSelectionChanged += SessionSelectionChanged;
        WireEvents();
        LoadControls();
        RebuildResponsiveLayout(force: true);
        LoadModules();
        UpdateHoleContextPresentation();
        UpdateSummary();
        ApplyTheme();
    }

    public void PanelShown(uint documentSerialNumber, ShowPanelReason reason)
    {
        ApplyTheme();
        var doc = RhinoDoc.ActiveDoc;
        if (doc is null)
            return;
        if (ComponentEditorSession.TryActivateSelectionSet(doc, true, out _))
            return;
        if (ComponentEditorSession.TryGetActiveSet(doc, out var active))
            ActivateComponents(active);
    }

    public void PanelHidden(uint documentSerialNumber, ShowPanelReason reason) { }

    public void PanelClosing(uint documentSerialNumber, bool onCloseDocument)
    {
        RhinoApp.AppSettingsChanged -= RhinoAppSettingsChanged;
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
        _size.RefreshTheme();
        _lengthCards.RefreshTheme();
        _holeContext.RefreshTheme();
        foreach (var button in _actionButtons)
        {
            var primary = button == _placeButton
                ? _loadedComponents.Count == 0 || _holeEditingContext == HoleEditingContext.PlacementPreset
                : button == _applyButton
                  && _loadedComponents.Count > 0
                  && _holeEditingContext == HoleEditingContext.CurrentComponent
                  && _applyButton.Enabled;
            ApplyActionButtonStyle(button, primary);
        }
        ApplyStatusTheme();
        Invalidate();
    }

    private void WireEvents()
    {
        _displayTimer.Elapsed += (_, _) =>
        {
            _displayTimer.Stop();
            ApplyGlobalDisplaySettings();
        };
        _kind.SelectedKeyChanged += (_, _) =>
        {
            ApplyEmbedDefaultForKindChange();
            LoadHeatSetDefaultsForNewKind();
            UpdateHeadEmbedControls();
            RebuildResponsiveLayout(force: true);
            UpdateInsertSummary();
            ScheduleUpdate();
        };
        _size.SelectedKeyChanged += (_, _) =>
        {
            PreserveHexNutEmbedModeAcrossSizeChange();
            UpdateInsertSummary();
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
        _clearanceFit.SelectedIndexChanged += (_, _) => HoleCommonParameterChanged();
        _bite.ValueChanged += (_, _) => HoleCommonParameterChanged();
        _holeContext.SelectedKeyChanged += (_, _) => HoleContextChanged();
        _presetDepth.SelectedIndexChanged += (_, _) =>
        {
            _presetBlindDepth.Visible = _presetDepth.SelectedKey == DepthMode.Blind.ToString();
            SavePlacementPresetControls();
        };
        _presetBlindDepth.ValueChanged += (_, _) => SavePlacementPresetControls();
        _presetClearancePreview.CheckedChanged += (_, _) => SaveActivePlacementPresetControls();
        _presetClearanceBoolean.CheckedChanged += (_, _) => SaveActivePlacementPresetControls();
        _presetEngagementPreview.CheckedChanged += (_, _) => SavePlacementPresetControls();
        _presetEngagementBoolean.CheckedChanged += (_, _) => SavePlacementPresetControls();
        _fastenerOpacity.ValueChanged += (_, _) => ScheduleGlobalDisplayUpdate();
        _cutterOpacity.ValueChanged += (_, _) => ScheduleGlobalDisplayUpdate();
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
            "更多：导出 STL、转换模型和校验文档",
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
        var editing = _loadedComponents.Count > 0
            && _holeEditingContext == HoleEditingContext.CurrentComponent;
        ApplyActionButtonStyle(_placeButton, !editing);
        ApplyActionButtonStyle(_applyButton, editing && _applyButton.Enabled);
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

        if (ComponentEditorSession.TryActivateSelectionSet(doc, false, out _))
        {
            // The selection activation event reloads the panel with repaired object IDs and placement data.
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
                ComponentEditorSession.ActivateMany(doc, remaining, false);
            else
                ComponentEditorSession.Forget(doc);
        }

        SetStatus(message, result.FailedComponents > 0 ? StatusKind.Warning : StatusKind.Success);
    }

    private void ShowMoreMenu(Control owner)
    {
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
        _parameterLayout.AddRow(FieldStack("规格", _size));
        var selectedKind = SelectedKind();
        if (selectedKind == FastenerKind.HexNut)
        {
            _parameterLayout.AddRow(_nutStandardDimensions);
            _parameterLayout.AddRow(CompactRow(
                FieldStack("嵌入深度 mm", _headEmbedControl)));
        }
        else if (selectedKind == FastenerKind.HeatSetInsert)
        {
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
                FieldStack("通孔配合", _clearanceFit),
                FieldStack("咬合缩减 mm", _bite)));
        }
        else if (profile.HoleFieldColumns == 2)
        {
            _holeParameterLayout.AddRow(CompactRow(
                FieldStack("孔径修正 mm", _printerCorrection),
                FieldStack("通孔配合", _clearanceFit)));
            _holeParameterLayout.AddRow(CompactRow(FieldStack("咬合缩减 mm", _bite)));
        }
        else
        {
            _holeParameterLayout.AddRow(CompactRow(FieldStack("孔径修正 mm", _printerCorrection)));
            _holeParameterLayout.AddRow(CompactRow(FieldStack("通孔配合", _clearanceFit)));
            _holeParameterLayout.AddRow(CompactRow(FieldStack("咬合缩减 mm", _bite)));
        }

        AddPlacementPresetRows(profile.HoleFieldColumns);
        _parameterLayout.Create();
        _holeHeaderLayout.Create();
        _holeParameterLayout.Create();
        _presetOptionsLayout.Create();
        UpdateHoleContextPresentation();

        if (!force)
            LoadModules();
    }

    private static Label SectionHeader(string text) => FastenerUiTheme.SectionTitle(text);

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
        var clearanceOptions = FieldStack("通孔模块", ToggleRow(_presetClearancePreview, _presetClearanceBoolean));
        var depth = FieldStack("咬合孔深度", DepthPresetRow());
        var engagementOptions = FieldStack("咬合孔模块", ToggleRow(_presetEngagementPreview, _presetEngagementBoolean));
        if (columns >= 3)
        {
            _presetOptionsLayout.AddRow(CompactRow(clearanceOptions, engagementOptions));
            _presetOptionsLayout.AddRow(CompactRow(depth));
        }
        else if (columns == 2)
        {
            _presetOptionsLayout.AddRow(CompactRow(clearanceOptions, engagementOptions));
            _presetOptionsLayout.AddRow(CompactRow(depth));
        }
        else
        {
            _presetOptionsLayout.AddRow(CompactRow(clearanceOptions));
            _presetOptionsLayout.AddRow(CompactRow(engagementOptions));
            _presetOptionsLayout.AddRow(CompactRow(depth));
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
        _clearanceFit.Width = profile.SelectFieldWidth;
        _bite.Width = profile.NumericFieldWidth;
        _presetDepth.Width = profile.DepthFieldWidth;
        _presetBlindDepth.Width = profile.NumericFieldWidth;
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

    private StackLayout DepthPresetRow() => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = FastenerUiTheme.SpaceSmall,
        Items =
        {
            new StackLayoutItem(_presetDepth),
            new StackLayoutItem(_presetBlindDepth)
        }
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
        ApplyActionButtonStyle(button, false);
    }

    private void ApplyActionButtonStyle(Button button, bool primary)
    {
        if (!_actionButtonIcons.TryGetValue(button, out var icon))
            return;
        FastenerUiTheme.SetRole(
            button,
            primary ? FastenerThemeRole.PrimaryAction : FastenerThemeRole.SecondaryAction);
        button.Image = PanelIconProvider.Get(icon, primary, FastenerUiTheme.IsDark);
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

    private void SessionSelectionChanged(object? sender, ComponentSelectionChangedEventArgs e)
    {
        if (RhinoDoc.ActiveDoc?.RuntimeSerialNumber != e.Document.RuntimeSerialNumber)
            return;
        ActivateComponents(e.Components);
    }

    private void ActivateComponent(FastenerComponentData component)
        => ActivateComponents([component]);

    private void ActivateComponents(IReadOnlyList<FastenerComponentData> components)
    {
        if (components.Count == 0)
        {
            _loadedComponents = [];
            _loadedComponent = null;
            _draftBindings = null;
            _batchModuleTemplate = null;
            _geometryDirty = false;
            SetHoleContext(HoleEditingContext.PlacementPreset);
            LoadModules();
            UpdateSummary();
            SetStatus("已切换为新建组件；当前参数保留。", StatusKind.Info);
            return;
        }
        _loadedComponents = components.ToArray();
        _loadedComponent = components[0];
        _draftBindings = _loadedComponent.Bindings;
        _batchModuleTemplate = components.Count > 1
            ? BatchModuleTemplate.FromComponents(components)
            : null;
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
        SetHoleContext(HoleEditingContext.CurrentComponent);
        LoadControls();
        LoadModules();
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
        if (doc is not null && ComponentEditorSession.TryActivateSelectionSet(doc, true, out _))
            return;
        SetStatus("请选中一个或多个参数化紧固件控制点。", StatusKind.Warning);
    }

    private void LoadControls()
    {
        _loadingControls = true;
        var state = EditorState.Current;
        _kind.SelectedKey = state.Kind.ToString();
        _size.SelectedKey = state.Size;
        _length.Value = state.Length;
        UpdateLengthCardSelection();
        _headEmbed.Value = state.HeadEmbedDepth;
        _insertOuterDiameter.Value = state.InsertOuterDiameter;
        _insertDiameterCompensation.Value = state.InsertDiameterCompensation;
        _insertDepthCompensation.Value = state.InsertDepthCompensation;
        if (_holeEditingContext == HoleEditingContext.CurrentComponent)
        {
            _printerCorrection.Value = state.PrinterCorrection;
            _clearanceFit.SelectedKey = state.ClearanceFit.ToString();
            _bite.Value = state.BiteReduction;
        }
        else
        {
            LoadPlacementPresetValues();
        }
        var globalDisplay = GlobalDisplaySettingsService.Current;
        _fastenerOpacity.Value = (int)Math.Round(globalDisplay.FastenerOpacityPercent);
        _cutterOpacity.Value = (int)Math.Round(globalDisplay.CutterOpacityPercent);
        UpdateOpacityLabels();
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
        if (!string.IsNullOrWhiteSpace(_size.SelectedKey))
            state.Size = _size.SelectedKey;
        state.Length = _length.Value;
        state.HeadEmbedDepth = FastenerKindTraits.SupportsEmbedDepth(state.Kind) ? _headEmbed.Value : 0;
        state.InsertOuterDiameter = state.Kind == FastenerKind.HeatSetInsert
            ? _insertOuterDiameter.Value
            : 0;
        state.InsertDiameterCompensation = state.Kind == FastenerKind.HeatSetInsert
            ? _insertDiameterCompensation.Value
            : 0;
        state.InsertDepthCompensation = state.Kind == FastenerKind.HeatSetInsert
            ? _insertDepthCompensation.Value
            : 0;
        if (_holeEditingContext == HoleEditingContext.CurrentComponent)
        {
            state.PrinterCorrection = _printerCorrection.Value;
            if (Enum.TryParse<ClearanceFitClass>(_clearanceFit.SelectedKey, out var fit))
                state.ClearanceFit = fit;
            state.BiteReduction = _bite.Value;
        }
        state.FastenerOpacityPercent = GlobalDisplaySettingsService.Current.FastenerOpacityPercent;
        state.CutterOpacityPercent = GlobalDisplaySettingsService.Current.CutterOpacityPercent;
    }

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
        _clearanceFit.SelectedKey = preset.ClearanceFit.ToString();
        _bite.Value = preset.BiteReduction;
        _presetDepth.SelectedKey = preset.EngagementDepthMode.ToString();
        _presetBlindDepth.Value = preset.EngagementBlindDepth;
        _presetBlindDepth.Visible = preset.EngagementDepthMode == DepthMode.Blind;
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
            var finalAcrossFlats = spec.Head.NutAcrossFlats + _printerCorrection.Value;
            _nutStandardDimensions.Text =
                $"对边 {spec.Head.NutAcrossFlats:0.###} · 厚 {spec.Head.NutThickness:0.###} · 槽 {finalAcrossFlats:0.###} · 嵌入 {_headEmbed.Value:0.###} mm";
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
            _insertFinalDiameter.Text = string.Empty;
        }
    }

    private FastenerKind SelectedKind() =>
        Enum.TryParse<FastenerKind>(_kind.SelectedKey, out var kind)
            ? kind
            : EditorState.Current.Kind;

    private void SavePlacementPresetControls()
    {
        if (_loadingControls || RhinoMMPlugIn.Instance is null)
            return;
        var current = PlacementPresetService.Current;
        var fit = Enum.TryParse<ClearanceFitClass>(_clearanceFit.SelectedKey, out var parsedFit)
            ? parsedFit
            : current.ClearanceFit;
        var depth = Enum.TryParse<DepthMode>(_presetDepth.SelectedKey, out var parsedDepth)
            ? parsedDepth
            : current.EngagementDepthMode;
        var preset = current with
        {
            PrinterCorrection = _printerCorrection.Value,
            ClearanceFit = fit,
            BiteReduction = _bite.Value,
            EngagementDepthMode = depth,
            EngagementBlindDepth = _presetBlindDepth.Value,
            ClearancePreviewVisible = _presetClearancePreview.Checked == true,
            ClearanceBooleanEnabled = _presetClearanceBoolean.Checked == true,
            EngagementPreviewVisible = _presetEngagementPreview.Checked == true,
            EngagementBooleanEnabled = _presetEngagementBoolean.Checked == true
        };
        if (!PlacementPresetService.Save(RhinoMMPlugIn.Instance.Settings, preset, out var message))
        {
            SetStatus(message, StatusKind.Warning);
            return;
        }
        NotifySmartPlacementDraftChanged();
    }

    private void HoleCommonParameterChanged()
    {
        if (_loadingControls)
            return;
        UpdateInsertSummary();
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
        _loadingControls = true;
        _holeContext.Select(context.ToString(), false);
        if (context == HoleEditingContext.PlacementPreset)
            LoadPlacementPresetValues();
        else
        {
            var state = EditorState.Current;
            _printerCorrection.Value = state.PrinterCorrection;
            _clearanceFit.SelectedKey = state.ClearanceFit.ToString();
            _bite.Value = state.BiteReduction;
        }
        _loadingControls = false;
        UpdateHoleContextPresentation();
    }

    private void UpdateHoleContextPresentation()
    {
        var hasComponents = _loadedComponents.Count > 0;
        var needsRelink = _loadedComponents.Any(ComponentHostResolver.NeedsRelink);
        var editing = hasComponents
            && !needsRelink
            && _holeEditingContext == HoleEditingContext.CurrentComponent;
        _holeContextHost.Visible = hasComponents;
        _holeContext.SetEnabled(HoleEditingContext.CurrentComponent.ToString(), hasComponents);
        _presetOptionsLayout.Visible = _holeEditingContext == HoleEditingContext.PlacementPreset;
        _moduleCard.Visible = editing;
        _applyButton.Enabled = editing;
        _applyButton.ToolTip = editing
            ? _loadedComponents.Count > 1
                ? $"批量更新：统一更新选中的 {_loadedComponents.Count} 个组件"
                : "应用更新：更新选中的控制点组件"
            : needsRelink
                ? "应用更新：组件需要先重新绑定宿主"
                : _holeEditingContext == HoleEditingContext.PlacementPreset
                ? "应用更新：放置预设不会修改已有组件"
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

    private static void NotifySmartPlacementDraftChanged()
    {
        SmartPlacementDraftChangeService.NotifyChanged();
        RhinoDoc.ActiveDoc?.Views.Redraw();
    }

    private void ScheduleGlobalDisplayUpdate()
    {
        UpdateOpacityLabels();
        if (_loadingControls || RhinoMMPlugIn.Instance is null)
            return;
        var settings = new GlobalDisplaySettings(
            _fastenerOpacity.Value,
            _cutterOpacity.Value);
        if (!GlobalDisplaySettingsService.Save(
                RhinoMMPlugIn.Instance.Settings,
                settings,
                out var message))
        {
            SetStatus(message, StatusKind.Warning);
        }
        _displayTimer.Stop();
        _displayTimer.Start();
    }

    private void UpdateOpacityLabels()
    {
        _fastenerOpacityValue.Text = $"{_fastenerOpacity.Value}%";
        _cutterOpacityValue.Text = $"{_cutterOpacity.Value}%";
    }

    private void ApplyGlobalDisplaySettings()
    {
        if (RhinoDoc.ActiveDoc is not { } doc)
            return;
        var allComponents = ComponentRepository.ReadAllControlPoints(doc, out var ignoredComponentCount);
        if (allComponents.Count == 0)
        {
            SetStatus(
                ignoredComponentCount > 0
                    ? "已保存全局显示默认值；文档残留组件请先运行“刷新 / 清理”。"
                    : "已保存全局显示默认值；新建组件将使用该设置。",
                ignoredComponentCount > 0 ? StatusKind.Warning : StatusKind.Success);
            return;
        }
        var display = GlobalDisplaySettingsService.Current;
        var drafts = allComponents.Select(component => component with
        {
            FastenerOpacityPercent = display.FastenerOpacityPercent,
            CutterOpacityPercent = display.CutterOpacityPercent,
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

        ComponentEditorSession.UpdateCachedComponents(doc, saved);
        var savedById = saved.ToDictionary(component => component.ComponentId);
        _loadedComponents = _loadedComponents
            .Select(component => savedById.GetValueOrDefault(component.ComponentId, component))
            .ToArray();
        if (_loadedComponent is not null
            && savedById.TryGetValue(_loadedComponent.ComponentId, out var current))
            _loadedComponent = current;
        SetStatus(
            ignoredComponentCount > 0
                ? $"{message} 另有 {ignoredComponentCount} 个残留组件未处理。"
                : message,
            ignoredComponentCount > 0 ? StatusKind.Warning : StatusKind.Success);
    }

    private void LoadModules()
    {
        _loadingControls = true;
        _moduleList.Items.Clear();
        var moduleCount = _loadedComponents.Count > 1
            ? _loadedComponents.Sum(component => component.Bindings.Count)
            : (_draftBindings ?? _loadedComponent?.Bindings ?? []).Count;
        _moduleHeader.Text = $"宿主切割模块（{moduleCount}）";
        if (_loadedComponents.Count > 1)
        {
            _batchModuleTemplate ??= BatchModuleTemplate.FromComponents(_loadedComponents);
            _moduleList.Items.Add(BuildBatchModuleTemplateControls(_batchModuleTemplate));
            _loadingControls = false;
            return;
        }
        if (_loadedComponent is null || _loadedComponent.Bindings.Count == 0)
        {
            _moduleList.Items.Add(FastenerUiTheme.Register(new Label
            {
                Text = "暂无绑定模块。",
                Wrap = WrapMode.Word,
                Height = 22
            }, FastenerThemeRole.SecondaryText));
            _loadingControls = false;
            return;
        }

        foreach (var binding in _draftBindings ?? _loadedComponent.Bindings)
            _moduleList.Items.Add(BuildModuleCard(binding));
        _loadingControls = false;
    }

    private Control BuildBatchModuleTemplateControls(BatchModuleTemplate template)
    {
        var layout = new DynamicLayout { Spacing = new Size(4, 4) };
        if (template.HasClearance)
        {
            var preview = new CheckBox { Text = "预览", Checked = template.ClearancePreviewVisible };
            var booleanEnabled = new CheckBox { Text = "导出布尔", Checked = template.ClearanceBooleanEnabled };
            preview.CheckedChanged += (_, _) => UpdateBatchModuleTemplate(current => current with
            {
                ClearancePreviewVisible = preview.Checked == true
            }, rebuildGeometry: false);
            booleanEnabled.CheckedChanged += (_, _) => UpdateBatchModuleTemplate(current => current with
            {
                ClearanceBooleanEnabled = booleanEnabled.Checked == true
            }, rebuildGeometry: false);
            layout.AddRow(
                FastenerUiTheme.Register(
                    new Label { Text = "通孔模板", Font = SystemFonts.Bold(), VerticalAlignment = VerticalAlignment.Center },
                    FastenerThemeRole.PrimaryText),
                preview,
                booleanEnabled);
        }

        if (template.HasEngagement)
        {
            var depth = new DropDown();
            foreach (var mode in EngagementDepthModes)
                depth.Items.Add(new ListItem { Key = mode.ToString(), Text = FastenerLabels.Depth(mode) });
            depth.SelectedKey = template.EngagementDepthMode.ToString();
            var customDepth = new NumericStepper
            {
                MinValue = 0.1,
                MaxValue = 1000,
                DecimalPlaces = 2,
                Increment = 0.5,
                Value = template.EngagementBlindDepth > 0
                    ? template.EngagementBlindDepth
                    : EditorState.Current.Length,
                Visible = template.EngagementDepthMode == DepthMode.Blind
            };
            var preview = new CheckBox { Text = "预览", Checked = template.EngagementPreviewVisible };
            var booleanEnabled = new CheckBox { Text = "导出布尔", Checked = template.EngagementBooleanEnabled };
            depth.Width = _layoutProfile?.DepthFieldWidth ?? 156;
            customDepth.Width = _layoutProfile?.NumericFieldWidth ?? 88;
            depth.SelectedIndexChanged += (_, _) =>
            {
                if (_loadingControls || !Enum.TryParse<DepthMode>(depth.SelectedKey, out var mode))
                    return;
                customDepth.Visible = mode == DepthMode.Blind;
                UpdateBatchModuleTemplate(current => current with
                {
                    EngagementDepthMode = mode,
                    EngagementBlindDepth = mode == DepthMode.Blind
                        ? customDepth.Value
                        : current.EngagementBlindDepth
                });
            };
            customDepth.ValueChanged += (_, _) => UpdateBatchModuleTemplate(current => current with
            {
                EngagementBlindDepth = customDepth.Value
            });
            preview.CheckedChanged += (_, _) => UpdateBatchModuleTemplate(current => current with
            {
                EngagementPreviewVisible = preview.Checked == true
            }, rebuildGeometry: false);
            booleanEnabled.CheckedChanged += (_, _) => UpdateBatchModuleTemplate(current => current with
            {
                EngagementBooleanEnabled = booleanEnabled.Checked == true
            }, rebuildGeometry: false);
            var roleLabel = FastenerUiTheme.Register(new Label
            {
                Text = "咬合模板",
                Font = SystemFonts.Bold(),
                VerticalAlignment = VerticalAlignment.Center
            }, FastenerThemeRole.PrimaryText);
            layout.AddRow(CompactRow(roleLabel, depth, customDepth));
            layout.AddRow(CompactRow(preview, booleanEnabled));
        }

        if (template.HasInstallation)
        {
            var preview = new CheckBox { Text = "预览", Checked = template.InstallationPreviewVisible };
            var booleanEnabled = new CheckBox { Text = "导出布尔", Checked = template.InstallationBooleanEnabled };
            preview.CheckedChanged += (_, _) => UpdateBatchModuleTemplate(current => current with
            {
                InstallationPreviewVisible = preview.Checked == true
            }, rebuildGeometry: false);
            booleanEnabled.CheckedChanged += (_, _) => UpdateBatchModuleTemplate(current => current with
            {
                InstallationBooleanEnabled = booleanEnabled.Checked == true
            }, rebuildGeometry: false);
            layout.AddRow(CompactRow(
                FastenerUiTheme.Register(
                    new Label { Text = "螺母安装模板", Font = SystemFonts.Bold(), VerticalAlignment = VerticalAlignment.Center },
                    FastenerThemeRole.PrimaryText),
                preview,
                booleanEnabled));
        }

        if (!template.HasClearance && !template.HasEngagement && !template.HasInstallation)
            layout.AddRow(FastenerUiTheme.SecondaryLabel("选中组件没有可同步的切割模块。"));
        return layout;
    }

    private void UpdateBatchModuleTemplate(
        Func<BatchModuleTemplate, BatchModuleTemplate> update,
        bool rebuildGeometry = true)
    {
        if (_loadingControls || _batchModuleTemplate is null)
            return;
        _batchModuleTemplate = update(_batchModuleTemplate);
        if (rebuildGeometry)
            MarkGeometryDirty();
        else
            ApplyBatchDisplayBindings();
    }

    private void ApplyBatchDisplayBindings()
    {
        if (_batchModuleTemplate is null)
            return;
        var template = _batchModuleTemplate;
        _loadedComponents = _loadedComponents.Select(component => component with
        {
            Bindings = component.Bindings.Select(binding => binding.Role switch
            {
                ShaftFitRole.Clearance when template.HasClearance => binding with
                {
                    IsPreviewVisible = template.ClearancePreviewVisible,
                    IsBooleanEnabled = template.ClearanceBooleanEnabled
                },
                ShaftFitRole.ThreadEngagement when template.HasEngagement => binding with
                {
                    IsPreviewVisible = template.EngagementPreviewVisible,
                    IsBooleanEnabled = template.EngagementBooleanEnabled
                },
                ShaftFitRole.InstallationPocket when template.HasInstallation => binding with
                {
                    IsPreviewVisible = template.InstallationPreviewVisible,
                    IsBooleanEnabled = template.InstallationBooleanEnabled
                },
                _ => binding
            }).ToArray(),
            UpdatedAt = DateTimeOffset.UtcNow
        }).ToArray();
        _loadedComponent = _loadedComponents.FirstOrDefault();
        ApplyDisplaySettings();
    }

    private Control BuildModuleCard(HoleTargetBinding binding)
    {
        var doc = RhinoDoc.ActiveDoc;
        var previewComponent = CurrentDraftComponent();
        var spec = RhinoMMPlugIn.Catalog.Get(previewComponent.Size);
        var target = doc?.Objects.FindId(binding.TargetObjectId);
        var fullName = target?.Attributes.Name;
        if (string.IsNullOrWhiteSpace(fullName))
            fullName = binding.TargetObjectId == Guid.Empty
                ? "待重新绑定宿主"
                : $"实体 {binding.TargetObjectId.ToString("N")[..8]}";
        var shortName = fullName.Length > 22 ? $"{fullName[..19]}…" : fullName;
        var moduleDescription = binding.Role == ShaftFitRole.InstallationPocket
            ? previewComponent.Kind == FastenerKind.HexNut
                ? $"六角安装槽 · 对边 {spec.Head.NutAcrossFlats + previewComponent.PrintProfile.HoleDiameterCorrection + binding.BindingOverride:0.###}"
                : $"热熔安装孔 · Ø{previewComponent.InsertOuterDiameter + previewComponent.InsertDiameterCompensation:0.###}"
            : $"{FastenerLabels.Role(binding.Role)} · Ø{HoleDiameterCalculator.Calculate(spec, binding, previewComponent.PrintProfile).FinalDiameter:0.###}";
        var title = FastenerUiTheme.Register(new Label
        {
            Text = $"{shortName} · {moduleDescription}",
            ToolTip = fullName,
            Wrap = WrapMode.Word,
            Height = 24
        }, FastenerThemeRole.PrimaryText);
        var preview = new CheckBox { Text = "预览", ToolTip = "显示切割模块预览", Checked = binding.IsPreviewVisible };
        var booleanEnabled = new CheckBox { Text = "导出布尔", ToolTip = "参与导出布尔切割", Checked = binding.IsBooleanEnabled };
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
            foreach (var mode in EngagementDepthModes)
                depth.Items.Add(new ListItem { Key = mode.ToString(), Text = FastenerLabels.Depth(mode) });
            depth.SelectedKey = binding.DepthMode.ToString();
            var customDepth = new NumericStepper
            {
                MinValue = 0.1,
                MaxValue = 1000,
                DecimalPlaces = 2,
                Increment = 0.5,
                Value = binding.BlindDepth > 0 ? binding.BlindDepth : previewComponent.Length
            };
            customDepth.Visible = binding.DepthMode == DepthMode.Blind;
            depth.Width = _layoutProfile?.DepthFieldWidth ?? 156;
            customDepth.Width = _layoutProfile?.NumericFieldWidth ?? 88;
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
            var depthLayout = new DynamicLayout { Spacing = new Size(4, 2) };
            depthLayout.AddRow(CompactRow(depth, customDepth));
            depthLayout.AddRow(FastenerUiTheme.Register(new Label
            {
                Text = DepthDescription(binding, target?.Geometry),
                Wrap = WrapMode.Word
            }, FastenerThemeRole.SecondaryText));
            card.AddRow(depthLayout);
        }
        else
        {
            card.AddRow(FastenerUiTheme.Register(new Label
            {
                Text = DepthDescription(binding, target?.Geometry),
                Wrap = WrapMode.Word
            }, FastenerThemeRole.SecondaryText));
        }
        card.AddRow(CompactRow(preview, booleanEnabled));
        return FastenerUiTheme.CreateCard(card, 0);
    }

    private string DepthDescription(HoleTargetBinding binding, Rhino.Geometry.GeometryBase? geometry)
    {
        if (_loadedComponent is null)
            return FastenerLabels.Depth(binding.DepthMode);
        if (binding.DepthMode == DepthMode.ThroughTarget
            && binding.Role != ShaftFitRole.Clearance)
            return "贯穿当前宿主";
        var previewComponent = CurrentDraftComponent();
        var spec = RhinoMMPlugIn.Catalog.Get(previewComponent.Size);
        if (binding.Role == ShaftFitRole.InstallationPocket)
        {
            var depth = InstallationPocketCalculator.CuttingDepth(previewComponent, spec);
            return previewComponent.Kind == FastenerKind.HexNut
                ? depth <= 0
                    ? "嵌入深度 0 mm · 不切割宿主"
                    : $"六角槽深度 {depth:0.###} mm"
                : HeatSetDepthDescription(previewComponent, geometry, depth);
        }
        var limit = FastenerGeometryFactory.DepthLimit(previewComponent, spec, binding);
        var result = $"孔底 {limit:0.###} mm";
        var doc = RhinoDoc.ActiveDoc;
        if (doc is not null && geometry is not null
            && FastenerGeometryFactory.TryGetTargetInterval(
                geometry, previewComponent.Placement, doc.ModelAbsoluteTolerance, out var interval, out _))
        {
            if (binding.Role == ShaftFitRole.Clearance)
            {
                return limit >= interval.Max - doc.ModelAbsoluteTolerance
                    ? "随螺杆长度 · 已到达宿主背面，将贯穿"
                    : $"随螺杆长度 · 孔底 {limit:0.###} mm · 盲孔";
            }
            result += limit >= interval.Max - doc.ModelAbsoluteTolerance
                ? " · 计算深度超过宿主厚度，将贯穿"
                : " · 盲孔";
        }
        return result;
    }

    private static string HeatSetDepthDescription(
        FastenerComponentData component,
        Rhino.Geometry.GeometryBase? geometry,
        double cuttingDepth)
    {
        var result =
            $"切割深度 {cuttingDepth:0.###} mm（螺母长度 {component.Length:0.###} + 补偿 {component.InsertDepthCompensation:0.###}）"
            + $" · 入口 45°×{InstallationPocketCalculator.HeatSetChamferDepth(component):0.###} mm";
        var doc = RhinoDoc.ActiveDoc;
        if (doc is not null && geometry is not null
            && FastenerGeometryFactory.TryGetTargetInterval(
                geometry, component.Placement, doc.ModelAbsoluteTolerance, out var interval, out _)
            && cuttingDepth >= interval.Max - doc.ModelAbsoluteTolerance)
            result += component.InsertDepthCompensation > doc.ModelAbsoluteTolerance
                ? " · 补偿深度超过宿主厚度，将贯穿"
                : " · 安装孔深度到达宿主背面，将贯穿";
        return result;
    }

    private FastenerComponentData CurrentDraftComponent()
    {
        if (_loadedComponent is null)
            throw new InvalidOperationException("No parametric fastener is active.");
        return EditorState.Current.CreateDraft(
            _loadedComponent.Placement,
            _draftBindings ?? _loadedComponent.Bindings) with
        {
            ComponentId = _loadedComponent.ComponentId,
            AdoptedSourceObjectId = _loadedComponent.AdoptedSourceObjectId,
            ControlPointObjectId = _loadedComponent.ControlPointObjectId
        };
    }

    private void SetBinding(Guid bindingId, Func<HoleTargetBinding, HoleTargetBinding> update, bool rebuildGeometry)
    {
        if (_loadingControls || _loadedComponent is null)
            return;
        var currentDraft = _draftBindings ?? _loadedComponent.Bindings;
        _draftBindings = currentDraft
            .Select(binding => binding.BindingId == bindingId ? update(binding) : binding)
            .ToArray();
        if (rebuildGeometry)
        {
            MarkGeometryDirty();
            return;
        }

        _loadedComponent = _loadedComponent with
        {
            Bindings = _loadedComponent.Bindings
                .Select(binding => binding.BindingId == bindingId ? update(binding) : binding)
                .ToArray(),
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _loadedComponents = _loadedComponents
            .Select(component => component.ComponentId == _loadedComponent.ComponentId
                ? _loadedComponent
                : component)
            .ToArray();
        ApplyDisplaySettings();
    }

    private void ApplyDisplaySettings()
    {
        if (_loadedComponents.Count == 0 || RhinoDoc.ActiveDoc is not { } doc)
            return;
        SaveControls();
        var updatedComponents = new List<FastenerComponentData>();
        foreach (var component in _loadedComponents)
        {
            var updated = component with
            {
                FastenerOpacityPercent = EditorState.Current.FastenerOpacityPercent,
                CutterOpacityPercent = EditorState.Current.CutterOpacityPercent,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            if (!ComponentPresentationService.ApplyDisplaySettings(doc, updated, out var message))
            {
                _geometryDirty = true;
                SetStatus(message, StatusKind.Error);
                return;
            }
            updatedComponents.Add(updated);
            ComponentEditorSession.UpdateCache(doc, updated);
        }
        _loadedComponents = updatedComponents;
        _loadedComponent = updatedComponents[0];
        SetStatus(
            updatedComponents.Count > 1
                ? $"已同步 {updatedComponents.Count} 个组件的透明度。"
                : "显示设置已更新。",
            StatusKind.Success);
    }

    private bool ApplyLoaded()
    {
        if (_holeEditingContext != HoleEditingContext.CurrentComponent)
        {
            SetStatus("当前正在编辑放置预设；不会修改已有组件。", StatusKind.Warning);
            return false;
        }
        if (RhinoDoc.ActiveDoc is not { } doc)
        {
            SetStatus("当前没有可更新的 Rhino 文档。", StatusKind.Warning);
            return false;
        }
        if (_loadedComponents.Any(ComponentHostResolver.NeedsRelink))
        {
            SetStatus("当前组件需要先重新绑定宿主；请选择对应宿主并运行“刷新 / 清理”。", StatusKind.Warning);
            return false;
        }

        SaveControls();
        var selectedComponents = ComponentRepository.ReadSelectedControlPoints(doc);
        if (selectedComponents.Count == 0)
        {
            SetStatus("请先选中一个或多个紧固件控制点；未更新历史组件。", StatusKind.Warning);
            return false;
        }
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

        var preservePanelBindings = selectedComponents.Count == 1
            && _loadedComponents.Count == 1
            && selectedComponents[0].ComponentId == _loadedComponents[0].ComponentId;
        var drafts = selectedComponents
            .Select(component =>
            {
                var bindings = preservePanelBindings
                    ? _draftBindings ?? component.Bindings
                    : selectedComponents.Count > 1 && _batchModuleTemplate is not null
                        ? _batchModuleTemplate.Apply(component.Bindings)
                        : component.Bindings;
                var draft = EditorState.Current.CreateUpdateDraft(component, bindings);
                return selectedComponents.Count > 1 && _batchModuleTemplate is not null
                    ? _batchModuleTemplate.ApplySmartProfile(draft)
                    : draft;
            })
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
            ComponentEditorSession.ActivateMany(doc, saved, false);
            doc.Views.Redraw();
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
        var drafts = _loadedComponents
            .Select(component =>
            {
                var bindings = _loadedComponents.Count == 1
                    ? _draftBindings ?? component.Bindings
                    : _batchModuleTemplate?.Apply(component.Bindings) ?? component.Bindings;
                var draft = EditorState.Current.CreateUpdateDraft(component, bindings);
                return _loadedComponents.Count > 1 && _batchModuleTemplate is not null
                    ? _batchModuleTemplate.ApplySmartProfile(draft)
                    : draft;
            })
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

        ComponentEditorSession.ActivateMany(doc, saved, false);
        doc.Views.Redraw();
        SetStatus(message, message.Contains("警告") || message.Contains("贯穿")
            ? StatusKind.Warning
            : StatusKind.Success);
        return true;
    }

    private static string KindCardText(FastenerKind kind) => kind switch
    {
        FastenerKind.SocketCap => "圆柱头",
        FastenerKind.Countersunk => "沉头",
        FastenerKind.HexBolt => "六角头",
        FastenerKind.HexNut => "六角螺母",
        FastenerKind.HeatSetInsert => "热熔螺母",
        _ => FastenerLabels.Kind(kind)
    };

    private void UpdateSummary()
    {
        var state = EditorState.Current;
        if (_loadedComponents.Count > 1)
        {
            var length = FastenerKindTraits.UsesLengthInStatistics(state.Kind)
                ? $" · L{state.Length:0.##}"
                : string.Empty;
            _summary.Text = $"批量 · {_loadedComponents.Count} 个组件 · {state.Size} · {FastenerLabels.Kind(state.Kind)}{length}";
            _summary.ToolTip = _summary.Text;
            return;
        }
        var prefix = _loadedComponent is null ? "新建" : "单选";
        var hosts = _loadedComponent is null ? string.Empty : $" · {_loadedComponent.Bindings.Count}个宿主";
        var summaryLength = FastenerKindTraits.UsesLengthInStatistics(state.Kind)
            ? $" · L{state.Length:0.##}"
            : string.Empty;
        _summary.Text = $"{prefix} · {state.Size} · {FastenerLabels.Kind(state.Kind)}{summaryLength}{hosts}";
        _summary.ToolTip = _summary.Text;
        if (_loadedComponent is null && !_geometryDirty)
            SetStatus("可直接设置参数并点击“放置 / 绑定”。", StatusKind.Info);
    }

    private void MarkGeometryDirty()
    {
        if (_loadingControls)
            return;
        _geometryDirty = true;
        SetStatus(
            _loadedComponent is null
                ? "参数已修改；放置时将创建新组件。"
                : "参数已修改但尚未应用；可更新当前组件或放置新组件。",
            StatusKind.Info);
    }

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
        _headEmbed.Value = RhinoMMPlugIn.Catalog.Get(_size.SelectedKey).Head.NutThickness;
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
            var previousThickness = RhinoMMPlugIn.Catalog
                .Get(EditorState.Current.Size)
                .Head.NutThickness;
            if (Math.Abs(_headEmbed.Value - previousThickness) <= 0.01)
            {
                _headEmbed.Value = RhinoMMPlugIn.Catalog
                    .Get(_size.SelectedKey)
                    .Head.NutThickness;
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
            ? spec.Head.NutThickness
            : HeadGeometryCalculator.GetHeadHeight(kind, spec);
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
