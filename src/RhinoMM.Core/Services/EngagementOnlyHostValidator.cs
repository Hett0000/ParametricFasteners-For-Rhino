using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public sealed record EngagementOnlyHostResult(
    bool IsValid,
    SmartHostAssignment? Assignment,
    double MaximumLength,
    string Message)
{
    public static EngagementOnlyHostResult Invalid(double maximumLength, string message) =>
        new(false, null, maximumLength, message);
}

public static class EngagementOnlyHostValidator
{
    public static EngagementOnlyHostResult Validate(
        Guid placementHostId,
        IEnumerable<SmartHostInterval> source,
        double headEmbedDepth,
        double fastenerLength,
        double tolerance)
    {
        var intervals = source
            .Where(item => item.ObjectId != Guid.Empty && item.Exit - item.Entry > tolerance)
            .GroupBy(item => item.ObjectId)
            .Select(group => group.OrderBy(item => item.Entry).First())
            .ToArray();
        var placement = intervals.FirstOrDefault(item => item.ObjectId == placementHostId);
        if (placement is null)
            return EngagementOnlyHostResult.Invalid(0, "放置面宿主未与螺杆轴线形成有效交集。");
        if (placement.Entry > tolerance)
            return EngagementOnlyHostResult.Invalid(0, "“只咬合”宿主必须从放置面开始连续容纳螺杆。");

        var requiredReach = headEmbedDepth + fastenerLength;
        var conflictingHost = intervals.FirstOrDefault(item =>
            item.ObjectId != placementHostId
            && item.Entry < requiredReach - tolerance
            && item.Exit > tolerance);
        if (conflictingHost is not null)
        {
            return EngagementOnlyHostResult.Invalid(
                Math.Max(0, placement.Exit - headEmbedDepth),
                "“只咬合”模式检测到螺杆范围内存在第二个实体；请调整位置或关闭该模式。");
        }

        var maximumLength = Math.Max(0, placement.Exit - headEmbedDepth);
        if (fastenerLength > maximumLength + tolerance)
        {
            return EngagementOnlyHostResult.Invalid(
                maximumLength,
                $"只咬合宿主可用长度 {maximumLength:0.###} mm，当前螺杆 {fastenerLength:0.###} mm；"
                + $"请将长度缩短至 {maximumLength:0.###} mm 以内。");
        }

        return new EngagementOnlyHostResult(
            true,
            new SmartHostAssignment(
                placement.ObjectId,
                ShaftFitRole.ThreadEngagement,
                placement.Entry,
                placement.Exit),
            maximumLength,
            string.Empty);
    }
}
