using RhinoMM.Core.Domain;

namespace RhinoMM.Plugin.Services;

internal static class FastenerPlacementEditing
{
    public static PlacementFrame RotateAroundAxis(PlacementFrame frame, double radians)
    {
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        return frame with
        {
            XAxisX = frame.XAxisX * cosine + frame.YAxisX * sine,
            XAxisY = frame.XAxisY * cosine + frame.YAxisY * sine,
            XAxisZ = frame.XAxisZ * cosine + frame.YAxisZ * sine,
            YAxisX = -frame.XAxisX * sine + frame.YAxisX * cosine,
            YAxisY = -frame.XAxisY * sine + frame.YAxisY * cosine,
            YAxisZ = -frame.XAxisZ * sine + frame.YAxisZ * cosine
        };
    }
}
