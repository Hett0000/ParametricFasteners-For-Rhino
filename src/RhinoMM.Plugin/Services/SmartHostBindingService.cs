using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Geometry;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Services;

internal static class SmartHostBindingService
{
    public static bool TryReconcile(
        RhinoDoc doc,
        FastenerComponentData draft,
        out FastenerComponentData reconciled,
        out SmartBindingChangeSummary changes,
        out string message)
    {
        reconciled = draft;
        changes = new SmartBindingChangeSummary(0, 0, 0);
        message = string.Empty;
        if (!draft.AutoRecognizeHosts)
            return true;
        if (FastenerKindTraits.IsNut(draft.Kind))
        {
            message = "螺母类组件不支持螺杆轴向宿主重识别。";
            return false;
        }
        if (draft.SmartBindingProfile is not { } profile)
        {
            message = "智能组件缺少摆放时的通孔/咬合模板，无法安全重识别宿主。";
            return false;
        }

        var headSeatTargetId = draft.Bindings
            .FirstOrDefault(item => item.IncludeHeadSeat)?.TargetObjectId ?? Guid.Empty;
        if (headSeatTargetId == Guid.Empty)
        {
            message = "智能组件缺少放置面宿主，无法重新识别绑定。";
            return false;
        }

        try
        {
            var tolerance = Math.Max(doc.ModelAbsoluteTolerance, 1e-6);
            var reach = Math.Max(tolerance, draft.HeadEmbedDepth + draft.Length);
            var existingTargetIds = draft.Bindings
                .Select(item => item.TargetObjectId)
                .Where(id => id != Guid.Empty)
                .ToHashSet();
            var hosts = CaptureHosts(doc, existingTargetIds);
            var intervals = FindIntervals(hosts, draft.Placement, reach, tolerance);
            var classification = SmartHostClassifier.Classify(
                intervals,
                draft.SmartRecognitionMode,
                tolerance);
            if (!classification.IsValid)
            {
                message = classification.Message;
                return false;
            }

            var result = SmartBindingReconciler.Reconcile(
                draft.Bindings,
                classification.Assignments,
                headSeatTargetId,
                profile);
            reconciled = draft with { Bindings = result.Bindings };
            changes = result.Changes;
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    public static IReadOnlyList<SmartPlacementHost> CaptureHosts(
        RhinoDoc doc,
        IReadOnlySet<Guid>? includeExistingTargetIds = null) =>
        doc.Objects
            .Where(obj =>
                ComponentHostResolver.IsOrdinaryHost(obj)
                && (includeExistingTargetIds?.Contains(obj.Id) == true
                    || obj.IsNormal
                    && obj.Visible
                    && !obj.IsHidden
                    && !obj.IsLocked))
            .Select(obj => new
            {
                Object = obj,
                Brep = obj.Geometry switch
                {
                    Brep brep => brep,
                    Extrusion extrusion => extrusion.ToBrep(),
                    _ => null
                }
            })
            .Where(item => item.Brep?.IsSolid == true)
            .Select(item => new SmartPlacementHost(
                item.Object.Id,
                item.Object,
                item.Brep!,
                item.Brep!.GetBoundingBox(true)))
            .ToArray();

    public static IReadOnlyList<SmartHostInterval> FindIntervals(
        IEnumerable<SmartPlacementHost> hosts,
        PlacementFrame placement,
        double reach,
        double tolerance)
    {
        var plane = FastenerGeometryFactory.ToPlane(placement);
        var padding = Math.Max(tolerance * 10, 0.2);
        var line = new Line(
            plane.Origin - plane.ZAxis * padding,
            plane.Origin + plane.ZAxis * (reach + padding));
        var intervals = new List<SmartHostInterval>();
        foreach (var host in hosts)
        {
            if (!Intersection.LineBox(line, host.BoundingBox, tolerance, out _))
                continue;
            if (!FastenerGeometryFactory.TryGetTargetInterval(
                    host.Brep,
                    placement,
                    tolerance,
                    out var interval,
                    out var usedFallback)
                || usedFallback)
                continue;

            var entry = Math.Max(0, interval.Min);
            var exit = Math.Min(reach, interval.Max);
            if (exit - entry <= tolerance)
                continue;
            intervals.Add(new SmartHostInterval(host.ObjectId, entry, exit));
        }
        return intervals;
    }
}
