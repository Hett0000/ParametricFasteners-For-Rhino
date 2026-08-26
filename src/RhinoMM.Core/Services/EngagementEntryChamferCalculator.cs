namespace RhinoMM.Core.Services;

public sealed record EngagementEntryChamferProfile(
    double Start,
    double End,
    double InnerRadius,
    double OuterRadius,
    double EntryMinimum,
    double EntryMaximum,
    double ExitMinimum);

public sealed record EngagementPlanarChamferSolution(
    double EntryMinimum,
    double EntryMaximum,
    double MouthMinimum,
    double MouthMaximumRadius,
    double SmallEnd,
    double SurfaceOffset,
    double SlopeX,
    double SlopeY,
    double ShaftRadius)
{
    public double MouthRadiusAt(double angle)
    {
        var denominator = 1 + SlopeX * Math.Cos(angle) + SlopeY * Math.Sin(angle);
        return (ShaftRadius + SmallEnd - SurfaceOffset) / denominator;
    }
}

public static class EngagementEntryChamferCalculator
{
    public static bool TryCreate(
        double finalDiameter,
        double chamferSize,
        double entryMinimum,
        double entryMaximum,
        double exitMinimum,
        double mouthMinimum,
        double outsidePadding,
        double tolerance,
        out EngagementEntryChamferProfile? profile,
        out string message)
    {
        profile = null;
        message = string.Empty;
        if (!double.IsFinite(finalDiameter) || finalDiameter <= tolerance)
        {
            message = "咬合孔最终直径无效。";
            return false;
        }
        if (!double.IsFinite(chamferSize) || chamferSize is < 0.05 or > 5.0
            || chamferSize <= tolerance)
        {
            message = $"倒角 C{chamferSize:0.###} mm 必须在 0.05–5.00 mm 内且大于文档绝对公差。";
            return false;
        }
        if (!double.IsFinite(entryMinimum)
            || !double.IsFinite(entryMaximum)
            || !double.IsFinite(exitMinimum)
            || !double.IsFinite(mouthMinimum)
            || entryMaximum < entryMinimum - tolerance)
        {
            message = "咬合宿主入口包络无效。";
            return false;
        }

        var padding = Math.Max(outsidePadding, tolerance * 2);
        var start = mouthMinimum - padding;
        var end = entryMaximum + chamferSize;
        if (end >= exitMinimum - tolerance)
        {
            var required = end - entryMinimum;
            var available = exitMinimum - entryMinimum;
            message = $"咬合宿主入口后厚度不足以容纳 C{chamferSize:0.###} 倒角；"
                + $"至少需要 {required:0.###} mm，当前约 {available:0.###} mm。";
            return false;
        }

        var innerRadius = finalDiameter * 0.5;
        // The cone slope is exactly 1:1. At the deepest entry plane its
        // radius is therefore innerRadius + C, even on an oblique surface.
        var outerRadius = innerRadius + end - start;
        profile = new EngagementEntryChamferProfile(
            start,
            end,
            innerRadius,
            outerRadius,
            entryMinimum,
            entryMaximum,
            exitMinimum);
        return true;
    }

    public static bool TrySolvePlanarEntry(
        double shaftRadius,
        double chamferSize,
        double surfaceOffset,
        double slopeX,
        double slopeY,
        double tolerance,
        out EngagementPlanarChamferSolution? solution,
        out string message)
    {
        solution = null;
        message = string.Empty;
        if (!double.IsFinite(shaftRadius) || shaftRadius <= tolerance
            || !double.IsFinite(chamferSize) || chamferSize <= tolerance
            || !double.IsFinite(surfaceOffset)
            || !double.IsFinite(slopeX)
            || !double.IsFinite(slopeY))
        {
            message = "倾斜入口平面参数无效。";
            return false;
        }

        var slope = Math.Sqrt(slopeX * slopeX + slopeY * slopeY);
        if (slope >= 1 - 1e-8)
        {
            message = "入口面与螺丝轴的夹角达到或超过45°，轴向45°倒角无法形成有限闭合切口。";
            return false;
        }

        var entryMinimum = surfaceOffset - slope * shaftRadius;
        var entryMaximum = surfaceOffset + slope * shaftRadius;
        var smallEnd = entryMaximum + chamferSize;
        var numerator = shaftRadius + smallEnd - surfaceOffset;
        var mouthMaximumRadius = numerator / (1 - slope);
        var mouthMinimum = surfaceOffset - slope * mouthMaximumRadius;
        if (!double.IsFinite(mouthMaximumRadius)
            || !double.IsFinite(mouthMinimum)
            || mouthMaximumRadius <= shaftRadius)
        {
            message = "无法计算倾斜入口的闭合45°倒角轮廓。";
            return false;
        }

        solution = new EngagementPlanarChamferSolution(
            entryMinimum,
            entryMaximum,
            mouthMinimum,
            mouthMaximumRadius,
            smallEnd,
            surfaceOffset,
            slopeX,
            slopeY,
            shaftRadius);
        return true;
    }
}
