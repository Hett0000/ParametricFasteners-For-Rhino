using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class HeadGeometryCalculator
{
    public readonly record struct CountersunkSeatProfile(
        double LargeRadius,
        double SmallRadius,
        double Height);

    public static double GetHeadHeight(FastenerKind kind, FastenerSizeSpec spec) => kind switch
    {
        FastenerKind.SocketCap => spec.Head.SocketHeight,
        FastenerKind.Countersunk => CountersunkHeight(spec),
        FastenerKind.HexBolt => spec.Head.HexHeight,
        _ => 0
    };

    public static double GetLegacyCountersunkConeHeight(FastenerSizeSpec spec)
    {
        var halfAngle = ValidateHalfAngle(spec.Head.CountersunkAngle);
        return spec.Head.CountersunkDiameter / 2 / Math.Tan(halfAngle);
    }

    public static CountersunkSeatProfile GetCountersunkSeatProfile(
        FastenerSizeSpec spec,
        double radialClearance,
        double axialPadding)
    {
        if (radialClearance < 0 || axialPadding < 0)
            throw new ArgumentOutOfRangeException(nameof(radialClearance), "沉头余量不能小于 0。");
        var halfAngle = ValidateHalfAngle(spec.Head.CountersunkAngle);
        var slope = Math.Tan(halfAngle);
        var headHeight = CountersunkHeight(spec);
        var largeRadius = spec.Head.CountersunkDiameter / 2
            + radialClearance
            + axialPadding * slope;
        var smallRadius = spec.NominalDiameter / 2
            + radialClearance
            - axialPadding * slope;
        if (smallRadius <= 0)
            throw new InvalidOperationException("沉头切割体的小端半径必须大于 0。");
        return new CountersunkSeatProfile(
            largeRadius,
            smallRadius,
            headHeight + axialPadding * 2);
    }

    private static double CountersunkHeight(FastenerSizeSpec spec)
    {
        var halfAngle = ValidateHalfAngle(spec.Head.CountersunkAngle);
        var radialHeight = (spec.Head.CountersunkDiameter - spec.NominalDiameter) / 2;
        if (radialHeight <= 0)
            throw new InvalidOperationException("沉头直径必须大于螺杆公称直径。");
        return radialHeight / Math.Tan(halfAngle);
    }

    private static double ValidateHalfAngle(double countersunkAngle)
    {
        var halfAngle = countersunkAngle * Math.PI / 360.0;
        if (halfAngle <= 0 || halfAngle >= Math.PI / 2)
            throw new InvalidOperationException("沉头角度必须在 0–180° 之间。");
        return halfAngle;
    }
}
