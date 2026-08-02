using Rhino;
using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Services;

internal sealed record SelectedComponentSummary(
    IReadOnlyList<FastenerComponentData> Components,
    int SelectedControlPoints,
    int InvalidComponents,
    int NeedsRepair,
    string ShortText,
    string FullText)
{
    public bool HasTargets => Components.Count > 0;
    public bool HasBlockingIssues => InvalidComponents > 0 || NeedsRepair > 0;

    public static SelectedComponentSummary Capture(RhinoDoc doc)
    {
        var selectedPoints = doc.Objects.GetSelectedObjects(false, false)
            .Where(obj => obj.Attributes.GetUserString(ComponentRepository.RoleKey) == "ControlPoint"
                && obj.Geometry is Point)
            .ToArray();
        var components = new List<FastenerComponentData>();
        var componentIds = new HashSet<Guid>();
        var invalid = 0;
        foreach (var point in selectedPoints)
        {
            if (!ComponentRepository.TryRead(point, out var component)
                || !componentIds.Add(component.ComponentId))
            {
                invalid++;
                continue;
            }
            components.Add(component);
        }

        var needsRepair = components.Count(component =>
            ComponentHostResolver.NeedsRelink(component)
            || component.Bindings.Any(binding =>
                binding.TargetObjectId == Guid.Empty
                || doc.Objects.FindId(binding.TargetObjectId) is null));
        var shortText = BuildShortText(components, selectedPoints.Length, invalid, needsRepair);
        return new SelectedComponentSummary(
            components,
            selectedPoints.Length,
            invalid,
            needsRepair,
            shortText,
            BuildFullText(components, selectedPoints.Length, invalid, needsRepair));
    }

    private static string BuildShortText(
        IReadOnlyList<FastenerComponentData> components,
        int selectedCount,
        int invalid,
        int needsRepair)
    {
        if (selectedCount == 0)
            return "未选择更新目标";
        if (invalid > 0 || needsRepair > 0)
            return $"已选 {selectedCount} 个 · 有效{components.Count - needsRepair} · 需刷新{invalid + needsRepair}";
        if (components.Count == 1)
            return FastenerSelectionSummaryFormatter.Compact(components[0]);

        var groups = components
            .GroupBy(component => component.Size)
            .OrderBy(group => NominalDiameter(group.Key))
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();
        var shown = groups.Take(2)
            .Select(group => $"{group.Key}×{group.Count()}")
            .ToList();
        if (groups.Length > 2)
            shown.Add($"另 {groups.Length - 2} 种");
        var hosts = components.Sum(component => component.Bindings.Count);
        return $"已选 {components.Count} 个 · {string.Join(" / ", shown)} · 共{hosts}个宿主";
    }

    private static string BuildFullText(
        IReadOnlyList<FastenerComponentData> components,
        int selectedCount,
        int invalid,
        int needsRepair)
    {
        if (selectedCount == 0)
            return "当前没有选中参数化紧固件控制点。";
        if (components.Count == 1 && invalid == 0 && needsRepair == 0)
            return FastenerSelectionSummaryFormatter.Full(components[0]);
        var groups = components
            .GroupBy(component => new
            {
                component.Kind,
                component.HexNutStyle,
                component.Size,
                Length = FastenerKindTraits.UsesLengthInStatistics(component.Kind)
                    ? component.Length
                    : 0
            })
            .OrderBy(group => group.Key.Kind)
            .ThenBy(group => NominalDiameter(group.Key.Size))
            .ThenBy(group => group.Key.Length)
            .Select(group =>
            {
                var length = FastenerKindTraits.UsesLengthInStatistics(group.Key.Kind)
                    ? $" L{group.Key.Length:0.##}"
                    : string.Empty;
                var type = group.Key.Kind == FastenerKind.HexNut
                    ? FastenerLabels.NutStyle(group.Key.HexNutStyle)
                    : FastenerLabels.Kind(group.Key.Kind);
                return $"{type} {group.Key.Size}{length} × {group.Count()}";
            });
        var result = string.Join("；", groups);
        if (invalid > 0 || needsRepair > 0)
            result += $"。无效 {invalid}，需重新绑定或刷新 {needsRepair}。";
        return result;
    }

    private static double NominalDiameter(string size) =>
        double.TryParse(
            size.TrimStart('M', 'm'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : double.MaxValue;
}
