namespace RhinoMM.Plugin.UI;

internal enum FastenerUiActionId
{
    Place,
    ApplyUpdate,
    Maintenance,
    ExportRhino,
    ExportStep,
    Statistics,
    AssemblyInspector,
    More
}

internal enum FastenerUiActionGroup
{
    Edit,
    Output,
    Inspect
}

internal sealed record FastenerUiActionDescriptor(
    FastenerUiActionId Id,
    FastenerUiActionGroup Group,
    PanelActionIcon Icon,
    string Name,
    string ToolTip,
    string PrimaryCommand,
    string? AlternateCommand = null);

/// <summary>
/// The single semantic source for the panel action strip and Rhino toolbar.
/// Commands may still route to a panel-owned workflow, but order, grouping,
/// naming and left/right semantics are defined only here.
/// </summary>
internal static class FastenerUiActionCoordinator
{
    public static IReadOnlyList<FastenerUiActionDescriptor> Actions { get; } =
    [
        new(
            FastenerUiActionId.Place,
            FastenerUiActionGroup.Edit,
            PanelActionIcon.Place,
            "放置 / 绑定",
            "左击：智能放置｜右击：点集批量放置",
            "_-ParametricFastenersPlace",
            "_-ParametricFastenersBatchPlace"),
        new(
            FastenerUiActionId.ApplyUpdate,
            FastenerUiActionGroup.Edit,
            PanelActionIcon.Apply,
            "应用更新",
            "将当前面板模板应用到 Rhino 当前选择",
            "_-ParametricFastenersApplyUpdate"),
        new(
            FastenerUiActionId.Maintenance,
            FastenerUiActionGroup.Edit,
            PanelActionIcon.Refresh,
            "刷新 / 维护",
            "打开维护中心：快速刷新、重新绑定和清理残留",
            "_-ParametricFastenersRefresh"),
        new(
            FastenerUiActionId.ExportRhino,
            FastenerUiActionGroup.Output,
            PanelActionIcon.Rhino,
            "放入 Rhino",
            "左击：仅布尔宿主｜右击：布尔宿主 + 紧固件实体",
            "_-ParametricFastenersExportToRhino",
            "_-ParametricFastenersExportToRhinoWithFasteners"),
        new(
            FastenerUiActionId.ExportStep,
            FastenerUiActionGroup.Output,
            PanelActionIcon.Step,
            "导出 STEP",
            "布尔计算后导出 STEP",
            "_-ParametricFastenersExportStep"),
        new(
            FastenerUiActionId.Statistics,
            FastenerUiActionGroup.Output,
            PanelActionIcon.Statistics,
            "紧固件统计",
            "统计数量并可保存 Excel",
            "_-ParametricFastenersStatistics"),
        new(
            FastenerUiActionId.AssemblyInspector,
            FastenerUiActionGroup.Inspect,
            PanelActionIcon.Inspector,
            "装配检查器",
            "检查装配可靠性、布尔结果与长度建议",
            "_-ParametricFastenersAssemblyInspector"),
        new(
            FastenerUiActionId.More,
            FastenerUiActionGroup.Inspect,
            PanelActionIcon.More,
            "更多",
            "读取、模板、输出、全局显示、版本信息等工具",
            "_-ParametricFastenersMore")
    ];

    public static FastenerUiActionDescriptor Get(FastenerUiActionId id) =>
        Actions.First(action => action.Id == id);
}
