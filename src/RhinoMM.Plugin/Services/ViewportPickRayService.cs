using Rhino;
using Rhino.Display;
using Rhino.Geometry;

namespace RhinoMM.Plugin.Services;

/// <summary>
/// Creates a near-to-far world ray from the viewport-client coordinates
/// delivered by GetPointMouseEventArgs. WindowPoint is already local to the
/// RhinoViewport; converting it to desktop screen coordinates offsets the ray
/// in docked, tiled and high-DPI viewports.
/// </summary>
internal static class ViewportPickRayService
{
    private const double ProjectionTolerancePixels = 3.5;
    private static DateTime _lastDiagnosticUtc = DateTime.MinValue;

    public static bool TryCreate(
        RhinoViewport viewport,
        System.Drawing.Point clientPoint,
        out Line ray,
        out string error)
    {
        ray = Line.Unset;
        error = string.Empty;

        ray = viewport.ClientToWorld(clientPoint);
        if (!ray.IsValid
            || ray.Length <= RhinoMath.ZeroTolerance)
        {
            WriteDiagnostic(viewport, clientPoint, double.NaN, "ClientToWorld returned an invalid line");
            error = "无法从当前视口的鼠标位置建立拾取射线。";
            return false;
        }

        var cameraDirection = viewport.CameraDirection;
        if (cameraDirection.Unitize()
            && Vector3d.Multiply(ray.Direction, cameraDirection) < 0)
        {
            ray = new Line(ray.To, ray.From);
        }

        var fromClient = viewport.WorldToClient(ray.From);
        var toClient = viewport.WorldToClient(ray.To);
        var fromError = ProjectionError(clientPoint, fromClient);
        var toError = ProjectionError(clientPoint, toClient);
        var projectionError = Math.Max(fromError, toError);
        if (!double.IsFinite(projectionError)
            || projectionError > ProjectionTolerancePixels)
        {
            WriteDiagnostic(viewport, clientPoint, projectionError,
                $"from={fromError:0.###}px, to={toError:0.###}px");
            ray = Line.Unset;
            error = "当前视口的鼠标拾取坐标无效；请重新激活该视口后重试。";
            return false;
        }

        return true;
    }

    private static double ProjectionError(System.Drawing.Point expected, Point2d actual)
    {
        if (!actual.IsValid)
            return double.PositiveInfinity;
        var dx = expected.X - actual.X;
        var dy = expected.Y - actual.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static void WriteDiagnostic(
        RhinoViewport viewport,
        System.Drawing.Point clientPoint,
        double projectionError,
        string detail)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastDiagnosticUtc).TotalSeconds < 1)
            return;
        _lastDiagnosticUtc = now;
        var bounds = viewport.Bounds;
        var projection = viewport.IsParallelProjection ? "parallel" : "perspective";
        var errorText = double.IsFinite(projectionError)
            ? $"{projectionError:0.###} px"
            : "无效";
        RhinoMM.Plugin.Services.FastenerCommandText.WriteLine(
            $"参数化紧固件拾取诊断：视口={viewport.Name}，投影={projection}，" +
            $"本地坐标=({clientPoint.X},{clientPoint.Y})，视口范围={bounds.Width}×{bounds.Height}，" +
            $"反投影误差={errorText}，详情={detail}");
    }
}
