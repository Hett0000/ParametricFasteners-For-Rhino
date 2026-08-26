using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class AssemblySuggestionCalculator
{
    public static AssemblySuggestion Create(
        FastenerComponentData component,
        FastenerSizeSpec spec,
        AssemblyMeasurementSnapshot measurement)
    {
        if (!FastenerKindTraits.IsScrew(component.Kind))
            return Blocked(component, "智能装配建议仅适用于螺丝组件。", []);
        if (!measurement.IsReliable || measurement.Hosts.Count == 0)
            return Blocked(component, measurement.Message, []);
        if (measurement.FirstHost?.HostId != measurement.PlacementHostId)
            return Blocked(component, "放置面宿主不是螺杆轴向遇到的第一个宿主。", []);
        if (HasOverlap(measurement.Hosts))
            return Blocked(component, "宿主轴向区间重叠，无法可靠提出装配建议。", []);

        var options = new List<AssemblySuggestionOption>();
        AddThreadEngagement(component, spec, measurement, options);
        AddEngagementOnly(component, measurement, options);
        AddNutFastened(component, spec, measurement, options);

        var current = options.FirstOrDefault(item => item.AssemblyMode == component.AssemblyMode);
        var recommended = current ?? options.FirstOrDefault();
        if (recommended is null)
            return Blocked(component, "当前宿主组合不支持可靠的螺纹咬合、只咬合或螺母固定装配。", options);

        var modeChanged = recommended.AssemblyMode != component.AssemblyMode;
        var status = modeChanged || !recommended.IsCurrentLengthValid
            ? AssemblySuggestionStatus.Warning
            : AssemblySuggestionStatus.Available;
        var risk = modeChanged
            ? $"当前“{FastenerLabels.AssemblyMode(component.AssemblyMode)}”不可行；替代方式必须由用户确认。"
            : recommended.IsCurrentLengthValid
                ? string.Empty
                : "当前长度不满足测量条件，建议采用新的长度。";
        return new AssemblySuggestion(
            status,
            component.AssemblyMode,
            component.Length,
            recommended.AssemblyMode,
            recommended.SuggestedLength,
            recommended.Explanation,
            risk,
            options);
    }

    private static void AddThreadEngagement(
        FastenerComponentData component,
        FastenerSizeSpec spec,
        AssemblyMeasurementSnapshot measurement,
        ICollection<AssemblySuggestionOption> options)
    {
        if (measurement.Hosts.Count < 2)
            return;
        var target = measurement.Hosts[^1];
        var requiredEngagement = component.SmartBindingProfile?.EngagementDepthMode switch
        {
            DepthMode.ThroughTarget => target.Thickness,
            DepthMode.FastenerLengthPlusCustom => Math.Max(
                spec.CoarsePitch,
                component.SmartBindingProfile.EngagementBlindDepth),
            _ => spec.NominalDiameter
        };
        requiredEngagement = Math.Min(target.Thickness, Math.Max(spec.CoarsePitch, requiredEngagement));
        var required = target.Entry - component.HeadEmbedDepth + requiredEngagement;
        var suggested = SelectAtLeast(required);
        var reach = component.HeadEmbedDepth + component.Length;
        options.Add(new AssemblySuggestionOption(
            ScrewAssemblyMode.ThreadEngagement,
            suggested,
            $"穿过 {measurement.Hosts.Count - 1} 个前方宿主，并进入末端宿主 {requiredEngagement:0.##} mm。",
            component.AssemblyMode == ScrewAssemblyMode.ThreadEngagement,
            reach + 1e-9 >= target.Entry + requiredEngagement));
    }

    private static void AddEngagementOnly(
        FastenerComponentData component,
        AssemblyMeasurementSnapshot measurement,
        ICollection<AssemblySuggestionOption> options)
    {
        var first = measurement.FirstHost!;
        var maximum = first.Exit - component.HeadEmbedDepth;
        if (maximum <= 0)
            return;
        var suggested = SelectAtMost(maximum);
        var reach = component.HeadEmbedDepth + component.Length;
        var nextEntry = measurement.Hosts.Count > 1
            ? measurement.Hosts[1].Entry
            : double.PositiveInfinity;
        options.Add(new AssemblySuggestionOption(
            ScrewAssemblyMode.EngagementOnly,
            suggested,
            $"第一宿主可容纳的最大螺杆长度为 {maximum:0.##} mm。",
            component.AssemblyMode == ScrewAssemblyMode.EngagementOnly,
            reach > 0 && reach <= first.Exit + 1e-9 && reach < nextEntry - 1e-9));
    }

    private static void AddNutFastened(
        FastenerComponentData component,
        FastenerSizeSpec spec,
        AssemblyMeasurementSnapshot measurement,
        ICollection<AssemblySuggestionOption> options)
    {
        var nut = HexNutDimensions.Resolve(component.PairedNutStyle, spec);
        foreach (var host in measurement.Hosts.Reverse())
        {
            var required = host.Entry + nut.TotalHeight + component.NutTipProtrusion
                - component.HeadEmbedDepth;
            if (required <= 0)
                continue;
            var suggested = SelectAtLeast(required);
            var outer = component.HeadEmbedDepth + component.Length - component.NutTipProtrusion;
            var inner = outer - nut.TotalHeight;
            var currentValid = outer >= host.Entry - 1e-9 && inner <= host.Exit + 1e-9;
            options.Add(new AssemblySuggestionOption(
                ScrewAssemblyMode.NutFastened,
                suggested,
                $"长度需容纳 {nut.TotalHeight:0.##} mm 螺母和 {component.NutTipProtrusion:0.##} mm 末端露出。",
                component.AssemblyMode == ScrewAssemblyMode.NutFastened,
                currentValid));
            return;
        }
    }

    private static bool HasOverlap(IReadOnlyList<AssemblyHostMeasurement> hosts)
    {
        for (var index = 1; index < hosts.Count; index++)
        {
            if (hosts[index].Entry < hosts[index - 1].Exit - 1e-9)
                return true;
        }
        return false;
    }

    private static AssemblySuggestion Blocked(
        FastenerComponentData component,
        string reason,
        IReadOnlyList<AssemblySuggestionOption> options) => new(
        AssemblySuggestionStatus.Blocked,
        component.AssemblyMode,
        component.Length,
        component.AssemblyMode,
        component.Length,
        reason,
        reason,
        options);

    private static double SelectAtLeast(double required) =>
        AssemblyLengthChoiceService.CommonLengths.FirstOrDefault(value => value + 1e-9 >= required) is var match
        && match > 0
            ? match
            : Math.Ceiling(required * 2) / 2;

    private static double SelectAtMost(double maximum)
    {
        var match = AssemblyLengthChoiceService.CommonLengths
            .Where(value => value <= maximum + 1e-9)
            .DefaultIfEmpty(0)
            .Max();
        return match > 0 ? match : Math.Max(0.5, Math.Floor(maximum * 2) / 2);
    }
}
