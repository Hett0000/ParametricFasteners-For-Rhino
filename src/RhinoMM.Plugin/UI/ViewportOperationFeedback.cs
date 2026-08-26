using Eto.Forms;
using Rhino;
using Rhino.Display;
using Rhino.Geometry;
using RhinoMM.Core.Domain;

namespace RhinoMM.Plugin.UI;

internal static class ViewportOperationFeedback
{
    private sealed class FeedbackConduit : DisplayConduit
    {
        public IReadOnlyList<Point3d> Points { get; set; } = [];
        public BoundingBox Box { get; set; } = BoundingBox.Empty;

        protected override void PostDrawObjects(DrawEventArgs e)
        {
            var color = System.Drawing.Color.FromArgb(30, 132, 230);
            if (Points.Count <= 20)
            {
                foreach (var point in Points)
                    e.Display.DrawPoint(point, PointStyle.ControlPoint, 7, color);
            }
            else if (Box.IsValid)
            {
                e.Display.DrawBox(Box, color, 2);
            }
        }
    }

    private static readonly FeedbackConduit Conduit = new();
    private static UITimer? _timer;

    public static void Show(RhinoDoc doc, IEnumerable<FastenerComponentData> components)
    {
        var points = components.Select(component => new Point3d(
            component.Placement.OriginX,
            component.Placement.OriginY,
            component.Placement.OriginZ)).Distinct().ToArray();
        if (points.Length == 0)
            return;
        var box = new BoundingBox(points);
        var margin = Math.Max(doc.ModelAbsoluteTolerance * 100, 2.0);
        box.Inflate(margin);
        Conduit.Points = points;
        Conduit.Box = box;
        Conduit.Enabled = true;
        doc.Views.Redraw();
        _timer?.Stop();
        _timer = new UITimer { Interval = 0.8 };
        _timer.Elapsed += (_, _) =>
        {
            _timer?.Stop();
            Conduit.Enabled = false;
            doc.Views.Redraw();
        };
        _timer.Start();
    }
}
