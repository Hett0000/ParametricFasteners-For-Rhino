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
        if (draft.AssemblyMode == ScrewAssemblyMode.NutFastened)
            return TryReconcileNutFastened(doc, draft, out reconciled, out changes, out message);
        if (draft.AssemblyMode == ScrewAssemblyMode.EngagementOnly || draft.EngagementOnly)
            return TryReconcileEngagementOnly(doc, draft, out reconciled, out changes, out message);
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
            var intervals = FindIntervals(
                hosts,
                draft.Placement,
                reach,
                tolerance,
                preserveFullExit: draft.SmartRecognitionMode == SmartPlacementRecognitionMode.Automatic);
            if (draft.ConfirmedEngagementHostId != Guid.Empty
                && intervals.All(item => item.ObjectId != draft.ConfirmedEngagementHostId)
                && doc.Objects.FindId(draft.ConfirmedEngagementHostId) is { } confirmedTarget
                && SmartHostIntervalService.TryGet(
                    confirmedTarget.Geometry,
                    draft.Placement,
                    tolerance,
                    out var confirmedInterval,
                    out _)
                && confirmedInterval.Max > tolerance)
            {
                intervals = intervals
                    .Append(new SmartHostInterval(
                        draft.ConfirmedEngagementHostId,
                        Math.Max(0, confirmedInterval.Min),
                        confirmedInterval.Max))
                    .ToArray();
            }
            var classification = draft.ConfirmedEngagementHostId != Guid.Empty
                ? ClassifyConfirmed(
                    intervals,
                    headSeatTargetId,
                    draft.ConfirmedEngagementHostId,
                    tolerance,
                    draft.HeadEmbedDepth,
                    draft.Length)
                : SmartHostClassifier.Classify(
                    intervals,
                    draft.SmartRecognitionMode,
                    tolerance,
                    draft.HoleDiameterFormula == HoleDiameterFormula.NominalIndependent
                        ? headSeatTargetId
                        : Guid.Empty,
                    draft.HeadEmbedDepth,
                    draft.Length);
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

    private static bool TryReconcileNutFastened(
        RhinoDoc doc,
        FastenerComponentData draft,
        out FastenerComponentData reconciled,
        out SmartBindingChangeSummary changes,
        out string message)
    {
        reconciled = draft;
        changes = new SmartBindingChangeSummary(0, 0, 0);
        message = string.Empty;
        if (!FastenerKindTraits.IsScrew(draft.Kind))
        {
            message = "“螺母固定”模式仅支持螺丝。";
            return false;
        }
        var placementHostId = draft.Bindings
            .FirstOrDefault(item => item.IncludeHeadSeat)?.TargetObjectId
            ?? draft.Bindings.FirstOrDefault(item => item.Role == ShaftFitRole.Clearance)?.TargetObjectId
            ?? Guid.Empty;
        if (placementHostId == Guid.Empty)
        {
            message = "螺母固定组件缺少放置面宿主。";
            return false;
        }

        try
        {
            var tolerance = Math.Max(doc.ModelAbsoluteTolerance, 1e-6);
            var spec = FastenerSpecResolver.Resolve(draft, RhinoMMPlugIn.Catalog);
            var reach = Math.Max(tolerance, draft.HeadEmbedDepth + draft.Length);
            var existingTargetIds = draft.Bindings
                .Select(item => item.TargetObjectId)
                .Where(id => id != Guid.Empty)
                .ToHashSet();
            var candidates = CaptureHosts(doc, existingTargetIds);
            if (!draft.AutoRecognizeHosts)
                candidates = candidates.Where(host => existingTargetIds.Contains(host.ObjectId)).ToArray();
            var intervals = FindIntervals(
                candidates,
                draft.Placement,
                reach,
                tolerance,
                preserveFullExit: true);
            var resolution = NutFastenedHostResolver.Resolve(
                intervals,
                placementHostId,
                draft,
                spec,
                tolerance);
            if (!resolution.IsValid)
            {
                message = resolution.Message;
                return false;
            }

            var profile = draft.SmartBindingProfile ?? ProfileFromBindings(draft.Bindings);
            var rebuilt = new List<HoleTargetBinding>();
            foreach (var assignment in resolution.ClearanceAssignments)
            {
                var existing = draft.Bindings.FirstOrDefault(item =>
                    item.TargetObjectId == assignment.ObjectId
                    && item.Role == ShaftFitRole.Clearance);
                rebuilt.Add(existing is not null
                    ? existing with
                    {
                        IncludeHeadSeat = assignment.ObjectId == placementHostId,
                        DepthMode = DepthMode.ThroughTarget,
                        CutterObjectId = Guid.Empty
                    }
                    : new HoleTargetBinding
                    {
                        TargetObjectId = assignment.ObjectId,
                        Role = ShaftFitRole.Clearance,
                        ClearanceFit = profile.ClearanceFit,
                        DepthMode = DepthMode.ThroughTarget,
                        IncludeHeadSeat = assignment.ObjectId == placementHostId,
                        IsPreviewVisible = profile.ClearancePreviewVisible,
                        IsBooleanEnabled = profile.ClearanceBooleanEnabled
                    });
            }
            var existingPocket = draft.Bindings.FirstOrDefault(item =>
                item.Role == ShaftFitRole.NutPocket);
            rebuilt.Add(existingPocket is not null
                ? existingPocket with
                {
                    TargetObjectId = resolution.NutPocketTargetId,
                    DepthMode = DepthMode.ThroughTarget,
                    IncludeHeadSeat = false,
                    CutterObjectId = Guid.Empty
                }
                : new HoleTargetBinding
                {
                    TargetObjectId = resolution.NutPocketTargetId,
                    Role = ShaftFitRole.NutPocket,
                    DepthMode = DepthMode.ThroughTarget,
                    IsPreviewVisible = profile.NutPocketPreviewVisible,
                    IsBooleanEnabled = profile.NutPocketBooleanEnabled
                });

            var oldKeys = draft.Bindings
                .Select(item => (item.TargetObjectId, item.Role))
                .ToHashSet();
            var newKeys = rebuilt
                .Select(item => (item.TargetObjectId, item.Role))
                .ToHashSet();
            changes = new SmartBindingChangeSummary(
                newKeys.Count(key => !oldKeys.Contains(key)),
                oldKeys.Count(key => !newKeys.Contains(key)),
                0);
            reconciled = draft with
            {
                AssemblyMode = ScrewAssemblyMode.NutFastened,
                EngagementOnly = false,
                Bindings = rebuilt
            };
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    private static bool TryReconcileEngagementOnly(
        RhinoDoc doc,
        FastenerComponentData draft,
        out FastenerComponentData reconciled,
        out SmartBindingChangeSummary changes,
        out string message)
    {
        reconciled = draft;
        changes = new SmartBindingChangeSummary(0, 0, 0);
        message = string.Empty;
        if (FastenerKindTraits.IsNut(draft.Kind))
        {
            message = "“只咬合”模式仅支持螺丝。";
            return false;
        }

        var placementHostId = draft.Bindings
            .FirstOrDefault(item => item.IncludeHeadSeat)?.TargetObjectId
            ?? draft.Bindings.FirstOrDefault()?.TargetObjectId
            ?? Guid.Empty;
        if (placementHostId == Guid.Empty)
        {
            message = "“只咬合”组件缺少放置面宿主。";
            return false;
        }

        var tolerance = Math.Max(doc.ModelAbsoluteTolerance, 1e-6);
        var reach = Math.Max(tolerance, draft.HeadEmbedDepth + draft.Length);
        var existingTargetIds = draft.Bindings
            .Select(item => item.TargetObjectId)
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        var hosts = CaptureHosts(doc, existingTargetIds);
        var intervals = FindIntervals(
            hosts,
            draft.Placement,
            reach,
            tolerance,
            preserveFullExit: true);
        var validation = EngagementOnlyHostValidator.Validate(
            placementHostId,
            intervals,
            draft.HeadEmbedDepth,
            draft.Length,
            tolerance);
        if (!validation.IsValid || validation.Assignment is null)
        {
            message = validation.Message;
            return false;
        }

        var profile = draft.SmartBindingProfile ?? ProfileFromBindings(draft.Bindings);
        var result = SmartBindingReconciler.Reconcile(
            draft.Bindings,
            [validation.Assignment],
            placementHostId,
            profile);
        reconciled = draft with
        {
            Bindings = result.Bindings,
            EngagementOnly = true
        };
        changes = result.Changes;
        return true;
    }

    private static SmartBindingProfile ProfileFromBindings(
        IReadOnlyList<HoleTargetBinding> bindings)
    {
        var engagement = bindings.FirstOrDefault(item =>
            item.Role == ShaftFitRole.ThreadEngagement);
        return new SmartBindingProfile(
            ClearanceFitClass.Normal,
            engagement?.BiteReduction ?? 0.35,
            engagement?.DepthMode ?? DepthMode.FastenerLengthPlusOneDiameter,
            engagement?.BlindDepth ?? 0,
            true,
            true,
            engagement?.IsPreviewVisible ?? true,
            engagement?.IsBooleanEnabled ?? true,
            bindings.FirstOrDefault(item => item.Role == ShaftFitRole.NutPocket)?.IsPreviewVisible ?? true,
            bindings.FirstOrDefault(item => item.Role == ShaftFitRole.NutPocket)?.IsBooleanEnabled ?? true);
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
        double tolerance,
        bool preserveFullExit = false)
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
            var clippedExit = Math.Min(reach, interval.Max);
            if (clippedExit - entry <= tolerance)
                continue;
            intervals.Add(new SmartHostInterval(
                host.ObjectId,
                entry,
                preserveFullExit ? interval.Max : clippedExit));
        }
        return intervals;
    }

    internal static SmartHostClassification ClassifyConfirmed(
        IEnumerable<SmartHostInterval> source,
        Guid placementHostId,
        Guid confirmedHostId,
        double tolerance,
        double headEmbedDepth,
        double fastenerLength)
    {
        var intervals = source
            .Where(item => item.ObjectId != Guid.Empty && item.Exit - item.Entry > tolerance)
            .GroupBy(item => item.ObjectId)
            .Select(group => group.OrderBy(item => item.Entry).First())
            .OrderBy(item => item.Entry)
            .ThenBy(item => item.Exit)
            .ToArray();
        if (intervals.Length == 0 || intervals[0].ObjectId != placementHostId)
            return SmartHostClassification.Invalid("已确认组件的放置面宿主不再是轴向第一宿主；请重新绑定。");
        var confirmedIndex = Array.FindIndex(intervals, item => item.ObjectId == confirmedHostId);
        if (confirmedIndex <= 0)
            return SmartHostClassification.Invalid("已确认的咬合宿主已丢失或不在放置宿主之后；请解除确认并重新识别。");
        var reach = headEmbedDepth + fastenerLength;
        if (reach < intervals[confirmedIndex].Entry - tolerance)
        {
            var minimum = Math.Max(0, intervals[confirmedIndex].Entry - headEmbedDepth);
            return SmartHostClassification.Invalid(
                $"当前长度无法到达已确认咬合宿主；至少需要 {minimum:0.###} mm。");
        }
        for (var index = 1; index <= confirmedIndex; index++)
        {
            if (intervals[index].Entry < intervals[index - 1].Exit - tolerance)
                return SmartHostClassification.Invalid("已确认咬合宿主与前方宿主重叠，无法可靠绑定。");
        }
        var assignments = intervals.Take(confirmedIndex + 1)
            .Select((item, index) => new SmartHostAssignment(
                item.ObjectId,
                index == confirmedIndex ? ShaftFitRole.ThreadEngagement : ShaftFitRole.Clearance,
                item.Entry,
                item.Exit))
            .ToArray();
        return new SmartHostClassification(true, assignments, string.Empty);
    }
}
