using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class FastenerSelectionSummaryFormatter
{
    public static string Compact(FastenerComponentData component)
    {
        var installationCount = component.Bindings.Count(binding =>
            binding.Role == ShaftFitRole.InstallationPocket);
        if (component.Kind == FastenerKind.HexNut)
            return $"{FastenerLabels.NutStyle(component.HexNutStyle)} {component.Size} 嵌入{Number(component.HeadEmbedDepth)} 安装{installationCount}";
        if (component.Kind == FastenerKind.HeatSetInsert)
        {
            return $"热熔螺母 {component.Size}*{Number(component.Length)} "
                + $"Ø{Number(component.InsertOuterDiameter)} 安装{installationCount}";
        }

        var clearanceCount = component.Bindings.Count(binding =>
            binding.Role == ShaftFitRole.Clearance);
        var engagementBindings = component.Bindings
            .Where(binding => binding.Role == ShaftFitRole.ThreadEngagement)
            .ToArray();
        var depth = CompactDepth(engagementBindings);
        if (component.AssemblyMode == ScrewAssemblyMode.NutFastened)
        {
            var pocketCount = component.Bindings.Count(binding => binding.Role == ShaftFitRole.NutPocket);
            return $"{ShortKind(component.Kind)} {component.Size}*{Number(component.Length)} 螺母固定"
                + $" 通{clearanceCount}/槽{pocketCount} · 露出{Number(component.NutTipProtrusion)}";
        }
        var mode = component.AssemblyMode == ScrewAssemblyMode.EngagementOnly ? " 只咬合" : string.Empty;
        return $"{ShortKind(component.Kind)} {component.Size}*{Number(component.Length)}{mode}"
            + $"{depth} 通{clearanceCount}/咬{engagementBindings.Length}";
    }

    public static string Full(FastenerComponentData component)
    {
        var clearanceCount = component.Bindings.Count(binding =>
            binding.Role == ShaftFitRole.Clearance);
        var engagementBindings = component.Bindings
            .Where(binding => binding.Role == ShaftFitRole.ThreadEngagement)
            .ToArray();
        var installationCount = component.Bindings.Count(binding =>
            binding.Role == ShaftFitRole.InstallationPocket);

        if (component.Kind == FastenerKind.HexNut)
        {
            return $"{FastenerLabels.NutStyle(component.HexNutStyle)} {component.Size}；"
                + $"尺寸标准：{FastenerLabels.NutStandard(component)}；"
                + $"嵌入深度 {Number(component.HeadEmbedDepth)} mm；安装宿主 {installationCount}";
        }
        if (component.Kind == FastenerKind.HeatSetInsert)
        {
            return $"{FastenerLabels.Kind(component.Kind)} {component.Size}×{Number(component.Length)} mm；"
                + $"外径 Ø{Number(component.InsertOuterDiameter)} mm；安装宿主 {installationCount}";
        }

        if (component.AssemblyMode == ScrewAssemblyMode.NutFastened)
        {
            var pocketCount = component.Bindings.Count(binding => binding.Role == ShaftFitRole.NutPocket);
            return $"{FastenerLabels.Kind(component.Kind)} {component.Size}×{Number(component.Length)} mm；"
                + $"模式：螺母固定；配套{FastenerLabels.NutStyle(component.PairedNutStyle)}；"
                + $"末端露出 {Number(component.NutTipProtrusion)} mm；通孔宿主 {clearanceCount}；螺母槽 {pocketCount}";
        }
        var mode = component.AssemblyMode == ScrewAssemblyMode.EngagementOnly ? "；模式：只咬合" : string.Empty;
        return $"{FastenerLabels.Kind(component.Kind)} {component.Size}×{Number(component.Length)} mm{mode}；"
            + $"{FullDepth(engagementBindings)}；通孔宿主 {clearanceCount}；"
            + $"咬合宿主 {engagementBindings.Length}";
    }

    private static string CompactDepth(IReadOnlyList<HoleTargetBinding> bindings)
    {
        if (bindings.Count == 0)
            return string.Empty;
        var labels = bindings
            .Select(CompactDepth)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return labels.Length == 1 ? $" {labels[0]}" : " 混合切割";
    }

    private static string FullDepth(IReadOnlyList<HoleTargetBinding> bindings)
    {
        if (bindings.Count == 0)
            return "无咬合孔切割";
        var labels = bindings
            .Select(binding => binding.DepthMode switch
            {
                DepthMode.FastenerLengthPlusCustom =>
                    $"螺杆长度 {Signed(binding.BlindDepth)} mm",
                _ => FastenerLabels.Depth(binding.DepthMode)
            })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return labels.Length == 1 ? $"咬合孔深度：{labels[0]}" : "咬合孔深度：混合设置";
    }

    private static string CompactDepth(HoleTargetBinding binding) => binding.DepthMode switch
    {
        DepthMode.ThroughTarget => "贯穿切割",
        DepthMode.FastenerLengthPlusOneDiameter => "+1切割",
        DepthMode.FastenerLengthPlusCustom => $"{Signed(binding.BlindDepth)}切割",
        DepthMode.FastenerLengthPlusTwoDiameters => "+2切割",
        DepthMode.Blind => "盲孔切割",
        _ => "切割"
    };

    private static string ShortKind(FastenerKind kind) => kind switch
    {
        FastenerKind.SocketCap => "杯头",
        FastenerKind.Countersunk => "沉头",
        FastenerKind.HexBolt => "六角头",
        _ => FastenerLabels.Kind(kind)
    };

    private static string Signed(double value) =>
        value >= 0 ? $"+{Number(value)}" : Number(value);

    private static string Number(double value) =>
        value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
