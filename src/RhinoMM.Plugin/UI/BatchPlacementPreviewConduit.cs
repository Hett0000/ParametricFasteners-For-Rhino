using System.Drawing;
using Rhino.Display;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.UI;

internal sealed class BatchPlacementPreviewConduit : DisplayConduit
{
    private IReadOnlyList<BatchPlacementPreflightItem> _items = [];
    private int _selectedNumber;

    public void Update(IReadOnlyList<BatchPlacementPreflightItem> items, int selectedNumber = 0)
    {
        _items = items;
        _selectedNumber = selectedNumber;
    }

    protected override void DrawForeground(DrawEventArgs e)
    {
        base.DrawForeground(e);
        foreach (var item in _items)
        {
            var color = item.Status switch
            {
                BatchPlacementStatus.Valid => Color.FromArgb(48, 172, 95),
                BatchPlacementStatus.Warning => Color.FromArgb(230, 160, 38),
                _ => Color.FromArgb(220, 74, 70)
            };
            var selected = item.Candidate.Number == _selectedNumber;
            e.Display.DrawPoint(
                item.Candidate.Point,
                selected ? PointStyle.ControlPoint : PointStyle.ActivePoint,
                selected ? 9 : 6,
                color);
            if (selected)
                e.Display.DrawDot(item.Candidate.Point, item.Candidate.Number.ToString(), color, Color.White);
        }
    }
}
