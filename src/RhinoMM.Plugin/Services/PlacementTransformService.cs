using Rhino.Geometry;
using RhinoMM.Core.Domain;

namespace RhinoMM.Plugin.Services;

/// <summary>
/// Transforms a saved placement frame without letting Rhino recompute its
/// Z axis from a reflected X/Y pair.  A mirror reverses handedness, while a
/// fastener still needs its shaft axis to follow the actually transformed Z
/// direction.  The returned frame is therefore canonicalized to a right-handed
/// frame using transformed Z and X.
/// </summary>
internal static class PlacementTransformService
{
    private const double Epsilon = 1e-9;

    public static bool TryTransform(
        PlacementFrame source,
        Transform transform,
        out PlacementFrame transformed,
        out bool isRigid)
    {
        var origin = new Point3d(source.OriginX, source.OriginY, source.OriginZ);
        var xAxis = new Vector3d(source.XAxisX, source.XAxisY, source.XAxisZ);
        var yAxis = new Vector3d(source.YAxisX, source.YAxisY, source.YAxisZ);
        var zAxis = new Vector3d(source.ZAxisX, source.ZAxisY, source.ZAxisZ);

        origin.Transform(transform);
        xAxis.Transform(transform);
        yAxis.Transform(transform);
        zAxis.Transform(transform);

        isRigid = IsOrthonormal(xAxis, yAxis, zAxis);
        if (!origin.IsValid || !xAxis.IsValid || !yAxis.IsValid || !zAxis.IsValid)
        {
            transformed = source;
            return false;
        }

        if (!zAxis.Unitize())
        {
            transformed = source;
            return false;
        }

        // Keep the transformed local X direction, but remove any numerical
        // component parallel to Z before rebuilding a right-handed Y axis.
        xAxis -= Vector3d.Multiply(xAxis, zAxis) * zAxis;
        if (!xAxis.Unitize())
        {
            xAxis = Vector3d.CrossProduct(yAxis, zAxis);
            if (!xAxis.Unitize())
            {
                transformed = source;
                return false;
            }
        }

        var canonicalY = Vector3d.CrossProduct(zAxis, xAxis);
        if (!canonicalY.Unitize())
        {
            transformed = source;
            return false;
        }

        transformed = new PlacementFrame(
            origin.X, origin.Y, origin.Z,
            xAxis.X, xAxis.Y, xAxis.Z,
            canonicalY.X, canonicalY.Y, canonicalY.Z,
            zAxis.X, zAxis.Y, zAxis.Z);
        return true;
    }

    private static bool IsOrthonormal(Vector3d xAxis, Vector3d yAxis, Vector3d zAxis) =>
        Math.Abs(xAxis.Length - 1.0) <= Epsilon
        && Math.Abs(yAxis.Length - 1.0) <= Epsilon
        && Math.Abs(zAxis.Length - 1.0) <= Epsilon
        && Math.Abs(Vector3d.Multiply(xAxis, yAxis)) <= Epsilon
        && Math.Abs(Vector3d.Multiply(xAxis, zAxis)) <= Epsilon
        && Math.Abs(Vector3d.Multiply(yAxis, zAxis)) <= Epsilon;
}
