using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Plugin.Geometry;

namespace RhinoMM.Plugin.Services;

/// <summary>
/// Builds a standard distance-distance chamfer on the actual planar entrance
/// edge. The returned Breps are the complete host-specific removal volume,
/// including the original shaft hole.
/// </summary>
internal static class EngagementSurfaceEqualChamferService
{
    public static bool TryCreateCutters(
        GeometryBase geometry,
        Brep shaftCutter,
        PlacementFrame placement,
        double shaftRadius,
        double entryDepth,
        double chamferSize,
        double tolerance,
        double angleTolerance,
        out IReadOnlyList<Brep> cutters,
        out string message)
    {
        cutters = [];
        message = string.Empty;
        var host = geometry switch
        {
            Brep brep => brep.DuplicateBrep(),
            Extrusion extrusion => extrusion.ToBrep(),
            _ => null
        };
        if (host is null || !host.IsValid || !host.IsSolid)
        {
            message = "表面等距倒角只能用于有效的封闭 Brep 或挤出实体。";
            return false;
        }

        var drilledResults = Brep.CreateBooleanDifference(
            [host],
            [shaftCutter],
            tolerance,
            false);
        var drilledCandidates = drilledResults?
            .Where(item => item.IsValid && item.IsSolid)
            .ToArray() ?? [];
        if (drilledCandidates.Length != 1)
        {
            message = "主咬合孔切割未得到唯一封闭宿主，无法继续生成表面等距倒角。";
            return false;
        }
        var drilled = drilledCandidates[0];
        if (!TryFindPlanarEntranceLoop(
                drilled,
                placement,
                shaftRadius,
                entryDepth,
                tolerance,
                out var edgeIndices,
                out message))
            return false;

        var radii = Enumerable.Repeat(chamferSize, edgeIndices.Count).ToArray();
        var chamferedResults = Brep.CreateFilletEdges(
            drilled,
            edgeIndices,
            radii,
            radii,
            BlendType.Chamfer,
            RailType.DistanceFromEdge,
            false,
            tolerance,
            angleTolerance);
        var chamferedCandidates = chamferedResults?
            .Where(item => item.IsValid && item.IsSolid)
            .ToArray() ?? [];
        if (chamferedCandidates.Length != 1)
        {
            message = "宿主表面或孔壁没有足够空间容纳等距倒角，未得到唯一封闭结果。";
            return false;
        }
        var chamfered = chamferedCandidates[0];

        var removed = Brep.CreateBooleanDifference(
            [host],
            [chamfered],
            tolerance,
            false)?
            .Where(item => item.IsValid && item.IsSolid)
            .ToArray() ?? [];
        if (removed.Length == 0)
        {
            message = "无法从等距倒角结果反求完整切割体。";
            return false;
        }

        // Reapply the derived removal volume once. This prevents a preview
        // cutter that cannot reproduce the final host from reaching document
        // objects or any export path.
        var reproduced = Brep.CreateBooleanDifference(
            [host],
            removed,
            tolerance,
            false)?
            .Where(item => item.IsValid && item.IsSolid)
            .ToArray() ?? [];
        if (reproduced.Length != 1
            || !VolumesMatch(reproduced[0], chamfered, tolerance))
        {
            message = "表面等距倒角切割体无法稳定复现宿主结果，已停止以避免预览与导出不一致。";
            return false;
        }

        cutters = removed;
        return true;
    }

    private static bool TryFindPlanarEntranceLoop(
        Brep drilled,
        PlacementFrame placement,
        double shaftRadius,
        double entryDepth,
        double tolerance,
        out IReadOnlyList<int> edgeIndices,
        out string message)
    {
        edgeIndices = [];
        message = string.Empty;
        var frame = FastenerGeometryFactory.ToPlane(placement);
        var radialTolerance = Math.Max(tolerance * 20, Math.Max(shaftRadius * 1e-4, 1e-5));
        var depthTolerance = Math.Max(tolerance * 20, 0.01);
        var candidates = new List<(int FaceIndex, double Score, int[] Edges)>();

        for (var faceIndex = 0; faceIndex < drilled.Faces.Count; faceIndex++)
        {
            var face = drilled.Faces[faceIndex];
            if (!face.TryGetPlane(out var plane, tolerance * 10)
                || !TryAxisPlaneDepth(frame, plane, out var planeDepth))
                continue;
            var loopEdges = face.AdjacentEdges()
                .Where(index => IsShaftBoundaryEdge(
                    drilled.Edges[index],
                    frame,
                    shaftRadius,
                    radialTolerance))
                .Distinct()
                .ToArray();
            if (loopEdges.Length == 0)
                continue;
            var joined = Curve.JoinCurves(
                loopEdges.Select(index => drilled.Edges[index].DuplicateCurve()),
                tolerance * 2);
            if (joined.Length != 1 || !joined[0].IsClosed)
                continue;
            candidates.Add((faceIndex, Math.Abs(planeDepth - entryDepth), loopEdges));
        }

        if (candidates.Count == 0)
        {
            message = "咬合孔入口不是单一连续平面，表面等距倒角不可用；请改用轴向45°。";
            return false;
        }
        var ordered = candidates.OrderBy(item => item.Score).ToArray();
        if (ordered[0].Score > depthTolerance)
        {
            message = "无法在咬合孔首次入口处识别完整平面边环；请改用轴向45°。";
            return false;
        }
        if (ordered.Length > 1
            && Math.Abs(ordered[1].Score - ordered[0].Score) <= depthTolerance)
        {
            message = "咬合孔入口存在多个平面边环，无法唯一确定等距倒角位置。";
            return false;
        }

        // Every edge must use the same planar face. An adjacent coplanar face
        // is intentionally not merged: surface-equal mode is defined only for
        // one continuous entrance face.
        var selected = ordered[0];
        if (selected.Edges.Any(index =>
                !drilled.Edges[index].AdjacentFaces().Contains(selected.FaceIndex)))
        {
            message = "孔口边环跨越多个入口面，表面等距倒角不可用。";
            return false;
        }
        edgeIndices = selected.Edges;
        return true;
    }

    private static bool IsShaftBoundaryEdge(
        BrepEdge edge,
        Plane frame,
        double shaftRadius,
        double tolerance)
    {
        if (edge.Valence != EdgeAdjacency.Interior)
            return false;
        for (var index = 0; index <= 12; index++)
        {
            var point = edge.PointAtNormalizedLength(index / 12.0);
            var delta = point - frame.Origin;
            var x = Vector3d.Multiply(delta, frame.XAxis);
            var y = Vector3d.Multiply(delta, frame.YAxis);
            var radius = Math.Sqrt(x * x + y * y);
            if (Math.Abs(radius - shaftRadius) > tolerance)
                return false;
        }
        return true;
    }

    private static bool TryAxisPlaneDepth(Plane frame, Plane surface, out double depth)
    {
        depth = 0;
        var normal = surface.Normal;
        if (!normal.Unitize())
            return false;
        var denominator = Vector3d.Multiply(normal, frame.ZAxis);
        if (Math.Abs(denominator) <= 1e-10)
            return false;
        depth = Vector3d.Multiply(surface.Origin - frame.Origin, normal) / denominator;
        return double.IsFinite(depth);
    }

    private static bool VolumesMatch(Brep left, Brep right, double tolerance)
    {
        var leftVolume = VolumeMassProperties.Compute(left)?.Volume;
        var rightVolume = VolumeMassProperties.Compute(right)?.Volume;
        if (leftVolume is null || rightVolume is null)
            return false;
        var scale = Math.Max(1, Math.Max(Math.Abs(leftVolume.Value), Math.Abs(rightVolume.Value)));
        return Math.Abs(leftVolume.Value - rightVolume.Value) <= Math.Max(tolerance * tolerance * tolerance * 10, scale * 1e-7);
    }
}
