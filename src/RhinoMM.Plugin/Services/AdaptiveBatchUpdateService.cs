using Rhino;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;

namespace RhinoMM.Plugin.Services;

internal static class AdaptiveBatchUpdateService
{
    public static bool TryApply(
        RhinoDoc doc,
        IReadOnlyList<FastenerComponentData> components,
        FastenerUpdateTemplate template,
        out IReadOnlyList<FastenerComponentData> saved,
        out string message)
    {
        saved = [];
        if (components.Count == 0)
        {
            message = "请先选择一个或多个参数化紧固件控制点。";
            return false;
        }
        if (!FastenerKindTraits.IsScrew(template.Kind)
            || components.Any(item => !FastenerKindTraits.IsScrew(item.Kind)))
        {
            message = "长度按宿主自适应仅适用于螺丝组件。";
            return false;
        }

        var drafts = new List<FastenerComponentData>(components.Count);
        var existingHostIds = components
            .SelectMany(item => item.Bindings)
            .Select(item => item.TargetObjectId)
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        var sharedHosts = SmartHostBindingService.CaptureHosts(doc, existingHostIds);
        try
        {
            foreach (var component in components)
            {
                if (ComponentHostResolver.NeedsRelink(component)
                    || component.Bindings.Any(binding => binding.TargetObjectId == Guid.Empty
                        || doc.Objects.FindId(binding.TargetObjectId) is null))
                {
                    message = $"组件 {component.ComponentId.ToString("N")[..8]} 需要重新绑定宿主。";
                    return false;
                }
                var draft = template.ApplyTo(component);
                var placementHostId = draft.Bindings
                    .FirstOrDefault(item => item.IncludeHeadSeat)?.TargetObjectId
                    ?? draft.Bindings.FirstOrDefault()?.TargetObjectId
                    ?? Guid.Empty;
                var measurement = AssemblyMeasurementService.Measure(
                    doc,
                    draft.Placement,
                    placementHostId,
                    sharedHosts);
                var spec = FastenerSpecResolver.Resolve(draft, RhinoMMPlugIn.Catalog);
                var suggestion = AssemblySuggestionCalculator.Create(draft, spec, measurement);
                if (suggestion.IsBlocked)
                {
                    message = $"组件 {component.ComponentId.ToString("N")[..8]} 无法计算长度：{suggestion.MeasurementBasis}";
                    return false;
                }
                if (suggestion.RecommendedMode != draft.AssemblyMode)
                {
                    message = $"组件 {component.ComponentId.ToString("N")[..8]} 的当前装配方式不可行；请先在装配建议中确认替代方式。";
                    return false;
                }
                drafts.Add(draft with
                {
                    Length = suggestion.SuggestedLength,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
        }
        finally
        {
            foreach (var host in sharedHosts)
                if (host.Object.Geometry is not Rhino.Geometry.Brep)
                    host.Brep.Dispose();
        }
        return ComponentUpdateCoordinator.TryApplyDrafts(doc, drafts, out saved, out message);
    }
}
