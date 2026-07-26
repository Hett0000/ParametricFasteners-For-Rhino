using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public sealed record SmartBindingChangeSummary(
    int Added,
    int Removed,
    int RoleChanged)
{
    public bool HasChanges => Added > 0 || Removed > 0 || RoleChanged > 0;

    public override string ToString() =>
        $"宿主重识别：新增 {Added}，移除 {Removed}，角色调整 {RoleChanged}。";
}

public sealed record SmartBindingReconcileResult(
    IReadOnlyList<HoleTargetBinding> Bindings,
    SmartBindingChangeSummary Changes);

public static class SmartBindingReconciler
{
    public static SmartBindingReconcileResult Reconcile(
        IReadOnlyList<HoleTargetBinding> existingBindings,
        IReadOnlyList<SmartHostAssignment> assignments,
        Guid headSeatTargetId,
        SmartBindingProfile profile)
    {
        if (headSeatTargetId == Guid.Empty)
            throw new InvalidOperationException("智能组件缺少放置面宿主，无法重新识别绑定。");
        if (assignments.Count == 0)
            throw new InvalidOperationException("当前螺杆有效长度内没有可绑定宿主。");
        if (assignments.All(item => item.ObjectId != headSeatTargetId))
            throw new InvalidOperationException("放置面宿主已不在当前螺杆有效长度内。");

        var existingByTarget = existingBindings
            .Where(item => item.TargetObjectId != Guid.Empty)
            .GroupBy(item => item.TargetObjectId)
            .ToDictionary(group => group.Key, group => group.First());
        var assignmentTargets = assignments
            .Select(item => item.ObjectId)
            .ToHashSet();

        var added = 0;
        var roleChanged = 0;
        var rebuilt = new List<HoleTargetBinding>(assignments.Count);
        foreach (var assignment in assignments)
        {
            existingByTarget.TryGetValue(assignment.ObjectId, out var existing);
            if (existing is null)
                added++;
            else if (existing.Role != assignment.Role)
                roleChanged++;

            var includeHeadSeat = assignment.ObjectId == headSeatTargetId;
            if (existing is not null && existing.Role == assignment.Role)
            {
                rebuilt.Add(existing with
                {
                    IncludeHeadSeat = includeHeadSeat,
                    CutterObjectId = Guid.Empty
                });
                continue;
            }

            var replacement = assignment.Role switch
            {
                ShaftFitRole.Clearance => new HoleTargetBinding
                {
                    TargetObjectId = assignment.ObjectId,
                    Role = ShaftFitRole.Clearance,
                    ClearanceFit = profile.ClearanceFit,
                    DepthMode = DepthMode.ThroughTarget,
                    IncludeHeadSeat = includeHeadSeat,
                    IsPreviewVisible = profile.ClearancePreviewVisible,
                    IsBooleanEnabled = profile.ClearanceBooleanEnabled
                },
                ShaftFitRole.ThreadEngagement => new HoleTargetBinding
                {
                    TargetObjectId = assignment.ObjectId,
                    Role = ShaftFitRole.ThreadEngagement,
                    BiteReduction = profile.BiteReduction,
                    DepthMode = profile.EngagementDepthMode,
                    BlindDepth = profile.EngagementBlindDepth,
                    IncludeHeadSeat = includeHeadSeat,
                    IsPreviewVisible = profile.EngagementPreviewVisible,
                    IsBooleanEnabled = profile.EngagementBooleanEnabled
                },
                _ => throw new InvalidOperationException(
                    $"智能螺丝不支持宿主角色 {assignment.Role}。")
            };
            if (existing is not null)
            {
                replacement = replacement with
                {
                    BindingId = existing.BindingId,
                    BindingOverride = existing.BindingOverride
                };
            }
            rebuilt.Add(replacement);
        }

        var removed = existingByTarget.Keys.Count(targetId => !assignmentTargets.Contains(targetId));
        return new SmartBindingReconcileResult(
            rebuilt,
            new SmartBindingChangeSummary(added, removed, roleChanged));
    }
}
