using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Plugin.Geometry;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Services;

internal static class ComponentHostResolver
{
    public static bool NeedsRelink(FastenerComponentData component) =>
        component.Bindings.Any(binding => binding.TargetObjectId == Guid.Empty);

    public static bool IsValidBinding(
        RhinoDoc doc,
        FastenerComponentData component,
        HoleTargetBinding binding,
        Guid targetId)
    {
        var target = doc.Objects.FindId(targetId);
        if (!IsOrdinaryHost(target)
            || !FastenerGeometryFactory.TryGetTargetInterval(
                target!.Geometry,
                component.Placement,
                doc.ModelAbsoluteTolerance,
                out var interval,
                out var usedFallback))
            return false;
        if (usedFallback)
            return false;
        return !binding.IncludeHeadSeat
            || interval.Min <= doc.ModelAbsoluteTolerance
               && interval.Max >= -doc.ModelAbsoluteTolerance;
    }

    public static RhinoObject? FindUnique(
        RhinoDoc doc,
        FastenerComponentData component,
        HoleTargetBinding binding,
        IEnumerable<Guid> candidateIds,
        GeometryBase? cutterGeometry,
        IReadOnlySet<Guid>? excludedIds = null)
    {
        var candidates = candidateIds
            .Distinct()
            .Where(id => excludedIds is null || !excludedIds.Contains(id))
            .Select(doc.Objects.FindId)
            .Where(IsOrdinaryHost)
            .Cast<RhinoObject>()
            .Select(candidate => new
            {
                Candidate = candidate,
                Score = Score(doc, component, binding, candidate, cutterGeometry)
            })
            .Where(item => item.Score >= 0)
            .OrderByDescending(item => item.Score)
            .ToArray();
        if (candidates.Length == 0)
            return null;
        if (candidates.Length == 1)
            return candidates[0].Candidate;
        var epsilon = Math.Max(Math.Pow(doc.ModelAbsoluteTolerance, 3), 1e-9);
        return candidates[0].Score - candidates[1].Score > epsilon
            ? candidates[0].Candidate
            : null;
    }

    public static IReadOnlyList<Guid> AllOrdinaryHostIds(RhinoDoc doc) => doc.Objects
        .Where(IsOrdinaryHost)
        .Select(obj => obj.Id)
        .ToArray();

    private static double Score(
        RhinoDoc doc,
        FastenerComponentData component,
        HoleTargetBinding binding,
        RhinoObject candidate,
        GeometryBase? cutterGeometry)
    {
        if (!FastenerGeometryFactory.TryGetTargetInterval(
                candidate.Geometry,
                component.Placement,
                doc.ModelAbsoluteTolerance,
                out var interval,
                out var usedFallback))
            return -1;
        if (usedFallback)
            return -1;
        if (binding.IncludeHeadSeat
            && (interval.Min > doc.ModelAbsoluteTolerance
                || interval.Max < -doc.ModelAbsoluteTolerance))
            return -1;

        var centerScore = 1.0 / (1.0 + Math.Abs((interval.Min + interval.Max) * 0.5));
        if (cutterGeometry is null)
            return centerScore;
        var overlap = BoundingBoxOverlapVolume(
            candidate.Geometry.GetBoundingBox(true),
            cutterGeometry.GetBoundingBox(true));
        if (overlap <= Math.Pow(doc.ModelAbsoluteTolerance, 3))
            return -1;
        return overlap * 1000.0 + centerScore;
    }

    private static double BoundingBoxOverlapVolume(BoundingBox left, BoundingBox right)
    {
        var x = Math.Max(0, Math.Min(left.Max.X, right.Max.X) - Math.Max(left.Min.X, right.Min.X));
        var y = Math.Max(0, Math.Min(left.Max.Y, right.Max.Y) - Math.Max(left.Min.Y, right.Min.Y));
        var z = Math.Max(0, Math.Min(left.Max.Z, right.Max.Z) - Math.Max(left.Min.Z, right.Min.Z));
        return x * y * z;
    }

    internal static bool IsOrdinaryHost(RhinoObject? obj) => obj is not null
        && obj.Geometry is Brep or Extrusion
        && string.IsNullOrWhiteSpace(
            obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey));
}
