using System.Reflection;
using Eto.Drawing;

namespace RhinoMM.Plugin.UI;

internal enum PanelActionIcon
{
    Read,
    Place,
    Apply,
    Refresh,
    Rhino,
    Step,
    Statistics,
    More
}

internal static class PanelIconProvider
{
    private const int LogicalSize = 24;
    private const string ResourcePrefix = "RhinoMM.Plugin.UI.Icons.Generated";
    private static readonly Dictionary<(PanelActionIcon Icon, bool Inverse), Icon> Cache = [];
    private static readonly (float Scale, int Pixels)[] Frames = [(1f, 24), (1.5f, 36), (2f, 48)];

    public static Icon Get(PanelActionIcon icon, bool inverse)
    {
        if (Cache.TryGetValue((icon, inverse), out var cached))
            return cached;

        var stem = icon switch
        {
            PanelActionIcon.Read => "read",
            PanelActionIcon.Place => "place",
            PanelActionIcon.Apply => "apply",
            PanelActionIcon.Refresh => "refresh",
            PanelActionIcon.Rhino => "rhino",
            PanelActionIcon.Step => "step",
            PanelActionIcon.Statistics => "statistics",
            PanelActionIcon.More => "more",
            _ => throw new ArgumentOutOfRangeException(nameof(icon), icon, null)
        };
        var frames = Frames
            .Select(frame => new IconFrame(
                frame.Scale,
                LoadBitmap(inverse
                    ? $"{stem}-inverse-{frame.Pixels}.png"
                    : $"{stem}-{frame.Pixels}.png")))
            .ToArray();
        var result = new Icon(frames).WithSize(LogicalSize, LogicalSize);
        Cache[(icon, inverse)] = result;
        return result;
    }

    private static Bitmap LoadBitmap(string fileName)
    {
        var resourceName = $"{ResourcePrefix}.{fileName}";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded panel icon: {resourceName}");
        return new Bitmap(stream);
    }
}
