using Rhino.Display;
using RhinoMM.Plugin.Services;
using DrawingColor = System.Drawing.Color;

namespace RhinoMM.Plugin.UI;

internal sealed class QuickEditPreviewConduit : DisplayConduit
{
    private PreparedFastenerGeometry? _prepared;

    public void Set(PreparedFastenerGeometry? prepared)
    {
        _prepared = prepared;
        Enabled = prepared is not null;
    }

    protected override void PostDrawObjects(DrawEventArgs e)
    {
        if (_prepared is null)
            return;
        var display = GlobalDisplaySettingsService.Current;
        var fastenerColor = DrawingColor.FromArgb(54, 139, 220);
        var cutterColor = DrawingColor.FromArgb(226, 139, 48);
        var fastenerMaterial = new DisplayMaterial(
            fastenerColor,
            1 - Math.Clamp(display.FastenerOpacityPercent, 0, 100) / 100.0);
        var cutterMaterial = new DisplayMaterial(
            cutterColor,
            1 - Math.Clamp(display.CutterOpacityPercent, 0, 100) / 100.0);
        foreach (var proxy in _prepared.Proxies)
        {
            e.Display.DrawBrepShaded(proxy, fastenerMaterial);
            e.Display.DrawBrepWires(proxy, fastenerColor, 2);
        }
        foreach (var cutter in _prepared.Cutters)
        {
            foreach (var shaft in cutter.Shafts)
            {
                e.Display.DrawBrepShaded(shaft, cutterMaterial);
                e.Display.DrawBrepWires(shaft, cutterColor, 1);
            }
            foreach (var head in cutter.Heads)
            {
                e.Display.DrawBrepShaded(head, cutterMaterial);
                e.Display.DrawBrepWires(head, cutterColor, 1);
            }
        }
    }
}
