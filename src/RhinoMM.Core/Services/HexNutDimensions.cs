using System.Reflection;
using System.Text.Json;
using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public readonly record struct HexNutDimensionResult(
    double AcrossFlats,
    double TotalHeight,
    string Standard,
    bool IsEngineeringExtension);

public static class HexNutDimensions
{
    private static readonly Lazy<IReadOnlyDictionary<string, LockingNutSizeSpec>> Locking =
        new(LoadLockingCatalog);

    public static bool Supports(HexNutStyle style, string designation) =>
        style == HexNutStyle.Standard || Locking.Value.ContainsKey(designation);

    public static HexNutDimensionResult Resolve(
        FastenerComponentData component,
        FastenerSizeSpec ordinarySpec)
    {
        if (component.HexNutStyle == HexNutStyle.NylonInsertLocking
            && component.CustomDefinitionSnapshot?.LockingNutSpec is { } custom)
        {
            return new HexNutDimensionResult(
                custom.AcrossFlats,
                custom.TotalHeight,
                custom.Standard,
                custom.IsEngineeringExtension);
        }
        return Resolve(component.HexNutStyle, ordinarySpec);
    }

    public static HexNutDimensionResult Resolve(
        HexNutStyle style,
        FastenerSizeSpec ordinarySpec)
    {
        if (style == HexNutStyle.Standard)
        {
            return new HexNutDimensionResult(
                ordinarySpec.Head.NutAcrossFlats,
                ordinarySpec.Head.NutThickness,
                "普通六角螺母预设",
                false);
        }

        if (!Locking.Value.TryGetValue(ordinarySpec.Designation, out var locking))
        {
            throw new InvalidOperationException(
                $"尼龙防松螺母不支持规格 {ordinarySpec.Designation}；可用范围为 M2–M12。");
        }

        return new HexNutDimensionResult(
            locking.AcrossFlats,
            locking.TotalHeight,
            locking.Standard,
            locking.IsEngineeringExtension);
    }

    public static string StyleLabel(HexNutStyle style) => style switch
    {
        HexNutStyle.Standard => "普通",
        HexNutStyle.NylonInsertLocking => "尼龙防松",
        _ => style.ToString()
    };

    private static IReadOnlyDictionary<string, LockingNutSizeSpec> LoadLockingCatalog()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resource = assembly.GetManifestResourceNames().Single(name =>
            name.EndsWith("locking-nut-presets.v1.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException("无法读取内置尼龙防松螺母规格库。");
        var document = JsonSerializer.Deserialize<LockingNutCatalogDocument>(stream, JsonOptions.Default)
            ?? throw new InvalidOperationException("尼龙防松螺母规格库为空。");
        return document.Sizes.ToDictionary(
            item => item.Designation,
            StringComparer.OrdinalIgnoreCase);
    }
}
