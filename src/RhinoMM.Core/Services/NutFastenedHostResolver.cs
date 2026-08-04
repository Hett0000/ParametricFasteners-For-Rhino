using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public sealed record NutFastenedHostResolution(
    bool IsValid,
    IReadOnlyList<SmartHostAssignment> ClearanceAssignments,
    Guid NutPocketTargetId,
    string Message)
{
    public static NutFastenedHostResolution Invalid(string message) =>
        new(false, [], Guid.Empty, message);
}

public static class NutFastenedHostResolver
{
    public static NutFastenedHostResolution Resolve(
        IEnumerable<SmartHostInterval> source,
        Guid placementHostId,
        FastenerComponentData component,
        FastenerSizeSpec spec,
        double tolerance)
    {
        var intervals = source
            .Where(item => item.ObjectId != Guid.Empty && item.Exit - item.Entry > tolerance)
            .GroupBy(item => item.ObjectId)
            .Select(group => group.OrderBy(item => item.Entry).First())
            .OrderBy(item => item.Entry)
            .ThenBy(item => item.Exit)
            .ToArray();
        if (intervals.Length == 0)
            return NutFastenedHostResolution.Invalid("螺杆范围内没有检测到可生成正补偿孔的宿主。");
        if (placementHostId == Guid.Empty
            || intervals[0].ObjectId != placementHostId
            || intervals[0].Entry > tolerance)
            return NutFastenedHostResolution.Invalid(
                "点击面所属实体不是螺杆遇到的第一宿主；请调整位置或使用经典放置。");

        for (var index = 1; index < intervals.Length; index++)
        {
            if (intervals[index].Entry < intervals[index - 1].Exit - tolerance)
                return NutFastenedHostResolution.Invalid(
                    "检测到相互重叠的宿主，无法唯一确定配套螺母所在物体。");
        }

        PairedNutAxialRange nutRange;
        try
        {
            nutRange = PairedNutAssemblyCalculator.AxialRange(component, spec);
        }
        catch (Exception ex)
        {
            return NutFastenedHostResolution.Invalid(ex.Message);
        }
        if (nutRange.InnerFace < component.HeadEmbedDepth - tolerance)
            return NutFastenedHostResolution.Invalid(
                "当前螺杆长度无法同时容纳配套螺母和末端露出量；请增加螺杆长度或减小露出量。");

        var candidates = intervals
            .Where(item =>
                item.Exit >= nutRange.InnerFace - tolerance
                && item.Entry <= nutRange.OuterFace + tolerance)
            .OrderByDescending(item => item.Entry)
            .ThenByDescending(item => item.Exit)
            .ToArray();
        if (candidates.Length == 0)
            return NutFastenedHostResolution.Invalid(
                $"螺母范围 {nutRange.InnerFace:0.###}–{nutRange.OuterFace:0.###} mm 未与任何宿主相交；请调整螺杆长度或末端露出量。");
        if (candidates.Length > 1
            && Math.Abs(candidates[0].Entry - candidates[1].Entry) <= tolerance)
            return NutFastenedHostResolution.Invalid("多个宿主同时位于配套螺母位置，无法唯一确定螺母槽宿主。");

        var assignments = intervals
            .Select(item => new SmartHostAssignment(
                item.ObjectId,
                ShaftFitRole.Clearance,
                item.Entry,
                item.Exit))
            .ToArray();
        return new NutFastenedHostResolution(
            true,
            assignments,
            candidates[0].ObjectId,
            string.Empty);
    }
}
