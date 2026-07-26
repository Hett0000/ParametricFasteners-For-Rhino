using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public sealed record SmartHostInterval(
    Guid ObjectId,
    double Entry,
    double Exit);

public sealed record SmartHostAssignment(
    Guid ObjectId,
    ShaftFitRole Role,
    double Entry,
    double Exit);

public sealed record SmartHostClassification(
    bool IsValid,
    IReadOnlyList<SmartHostAssignment> Assignments,
    string Message)
{
    public static SmartHostClassification Invalid(string message) =>
        new(false, Array.Empty<SmartHostAssignment>(), message);
}

public static class SmartHostClassifier
{
    public static SmartHostClassification Classify(
        IEnumerable<SmartHostInterval> source,
        SmartPlacementRecognitionMode mode,
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
            return SmartHostClassification.Invalid("螺杆有效长度内没有检测到可绑定的封闭实体。");

        for (var index = 1; index < intervals.Length; index++)
        {
            var previous = intervals[index - 1];
            var current = intervals[index];
            if (current.Entry < previous.Exit - tolerance)
            {
                return SmartHostClassification.Invalid(
                    "检测到相互重叠的宿主，无法可靠判断穿过体与咬合体；请使用经典放置。");
            }

            if (Math.Abs(current.Entry - previous.Entry) <= tolerance)
            {
                return SmartHostClassification.Invalid(
                    "多个宿主的进入深度相同，自动识别存在歧义；请使用经典放置。");
            }
        }

        var assignments = intervals
            .Select((item, index) => new SmartHostAssignment(
                item.ObjectId,
                mode switch
                {
                    SmartPlacementRecognitionMode.AllClearance => ShaftFitRole.Clearance,
                    SmartPlacementRecognitionMode.AllEngagement => ShaftFitRole.ThreadEngagement,
                    _ => index == intervals.Length - 1
                        ? ShaftFitRole.ThreadEngagement
                        : ShaftFitRole.Clearance
                },
                item.Entry,
                item.Exit))
            .ToArray();

        return new SmartHostClassification(true, assignments, string.Empty);
    }
}
