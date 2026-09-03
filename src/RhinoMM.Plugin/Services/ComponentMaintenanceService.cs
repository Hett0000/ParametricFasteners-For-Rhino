using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Plugin.Geometry;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.UI;

namespace RhinoMM.Plugin.Services;

internal sealed record RelinkCandidate(Guid ObjectId, string Name, Interval Interval, double Score, string Reason);
internal sealed record RelinkRequest(FastenerComponentData Component, HoleTargetBinding Binding, IReadOnlyList<RelinkCandidate> Candidates);

internal static class ComponentMaintenanceService
{
    public static bool QuickRefresh(RhinoDoc doc, out string message)
        => ComponentDocumentHealthService.RepairDeterministic(doc, out message);

    public static bool CleanupResiduals(RhinoDoc doc, out string message)
    {
        var groups = doc.Objects.Where(obj => Guid.TryParse(obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey), out _))
            .GroupBy(obj => Guid.Parse(obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey)!)).ToArray();
        var residual = groups.Where(group => !group.Any(obj => obj.Geometry is Point
            && obj.Attributes.GetUserString(ComponentRepository.RoleKey) == "ControlPoint")).ToArray();
        if (residual.Length == 0)
        {
            message = "没有缺少控制点的插件残留。";
            return true;
        }
        using var healthSuppression = ComponentDocumentHealthService.SuppressDuringMutation(doc);
        var undo = doc.BeginUndoRecord("参数化紧固件：清理残留");
        try
        {
            foreach (var group in residual)
            {
                using var suppression = ComponentLifecycleService.Suppress(doc, group.Key);
                ComponentPresentationService.RemoveGroup(doc, group.Key);
                foreach (var obj in group.ToArray())
                    doc.Objects.Delete(obj, true);
            }
            message = $"已清理 {residual.Length} 个缺少控制点的残留组件。";
            FastenerDocumentIndexService.Invalidate(doc);
            doc.Views.Redraw();
            return true;
        }
        finally
        {
            if (undo != 0)
                doc.EndUndoRecord(undo);
        }
    }

    public static IReadOnlyList<RelinkRequest> BuildRelinkRequests(
        RhinoDoc doc,
        FastenerUiOperationContext? operation = null)
    {
        var selected = ComponentRepository.ReadSelectedControlPoints(doc);
        var source = selected.Count > 0 ? selected : ComponentRepository.ReadAllControlPoints(doc, out _);
        var hostIds = ComponentHostResolver.AllOrdinaryHostIds(doc);
        var requests = new List<RelinkRequest>();
        foreach (var component in source)
        foreach (var binding in component.Bindings.Where(binding => binding.TargetObjectId == Guid.Empty
                     || doc.Objects.FindId(binding.TargetObjectId) is null))
        {
            if (operation is not null && !operation.Yield(
                    "计算重绑候选",
                    requests.Count,
                    Math.Max(1, source.Sum(item => item.Bindings.Count)),
                    component.ComponentId.ToString("N")[..8]))
                return requests;
            var cutter = ComponentRepository.FindComponentObjects(doc, component.ComponentId).FirstOrDefault(obj =>
                string.Equals(obj.Attributes.GetUserString(ComponentRepository.BindingIdKey), binding.BindingId.ToString("D"), StringComparison.OrdinalIgnoreCase)
                && obj.Attributes.GetUserString(ComponentRepository.RoleKey) is "Cutter" or "HeadCutter");
            var candidates = hostIds.Select(doc.Objects.FindId).Where(ComponentHostResolver.IsOrdinaryHost).Cast<RhinoObject>()
                .Select(host => Candidate(doc, component, binding, host, cutter?.Geometry))
                .Where(item => item is not null).Cast<RelinkCandidate>()
                .OrderByDescending(item => item.Score).Take(12).ToArray();
            requests.Add(new RelinkRequest(component, binding, candidates));
        }
        return requests;
    }

    public static bool CommitRelinks(RhinoDoc doc, IReadOnlyDictionary<Guid, Guid> bindingTargets, out string message)
    {
        var components = ComponentRepository.ReadAllControlPoints(doc, out _)
            .Where(component => component.Bindings.Any(binding => bindingTargets.ContainsKey(binding.BindingId))).ToArray();
        var drafts = components.Select(component => component with
        {
            Bindings = component.Bindings.Select(binding => bindingTargets.TryGetValue(binding.BindingId, out var target)
                ? binding with { TargetObjectId = target, CutterObjectId = Guid.Empty }
                : binding).ToArray(),
            UpdatedAt = DateTimeOffset.UtcNow
        }).ToArray();
        if (drafts.Length == 0)
        {
            message = "没有确认任何重新绑定。";
            return false;
        }
        if (!FastenerComponentService.CreateOrReplaceMany(doc, drafts, out _, out message))
            return false;
        FastenerDocumentIndexService.Invalidate(doc);
        ViewportOperationFeedback.Show(doc, drafts);
        message = $"已确认并重建 {drafts.Length} 个组件的宿主绑定。";
        return true;
    }

    private static RelinkCandidate? Candidate(
        RhinoDoc doc,
        FastenerComponentData component,
        HoleTargetBinding binding,
        RhinoObject host,
        GeometryBase? cutter)
    {
        if (!FastenerGeometryFactory.TryGetTargetInterval(host.Geometry, component.Placement, doc.ModelAbsoluteTolerance,
                out var interval, out var fallback) || fallback)
            return null;
        if (binding.IncludeHeadSeat && (interval.Min > doc.ModelAbsoluteTolerance || interval.Max < -doc.ModelAbsoluteTolerance))
            return null;
        var center = Math.Abs((interval.Min + interval.Max) / 2);
        var score = 1 / (1 + center);
        var overlap = cutter is null ? 0 : OverlapVolume(host.Geometry.GetBoundingBox(true), cutter.GetBoundingBox(true));
        score += overlap * 1000;
        var reason = $"轴线区间 {interval.Min:0.##}～{interval.Max:0.##} mm；切割体包围盒重叠 {overlap:0.###}；评分 {score:0.###}";
        var name = string.IsNullOrWhiteSpace(host.Attributes.Name) ? host.Id.ToString("N")[..8] : host.Attributes.Name;
        return new RelinkCandidate(host.Id, name, interval, score, reason);
    }

    private static double OverlapVolume(BoundingBox left, BoundingBox right)
    {
        var x = Math.Max(0, Math.Min(left.Max.X, right.Max.X) - Math.Max(left.Min.X, right.Min.X));
        var y = Math.Max(0, Math.Min(left.Max.Y, right.Max.Y) - Math.Max(left.Min.Y, right.Min.Y));
        var z = Math.Max(0, Math.Min(left.Max.Z, right.Max.Z) - Math.Max(left.Min.Z, right.Min.Z));
        return x * y * z;
    }
}
