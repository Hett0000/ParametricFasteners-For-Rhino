using Rhino;
using RhinoMM.Core.Domain;

namespace RhinoMM.Plugin.Services;

internal static class PlacementPresetService
{
    private const string Prefix = "PlacementPreset.";

    public static PlacementCutterPreset Current { get; private set; } = PlacementCutterPreset.Default;

    public static void Load(PersistentSettings settings)
    {
        var defaults = PlacementCutterPreset.Default;
        try
        {
            var fit = ParseEnum(settings.GetString(Prefix + "ClearanceFit", defaults.ClearanceFit.ToString()), defaults.ClearanceFit);
            var depth = ParseDepthMode(
                settings.GetString(Prefix + "EngagementDepthMode", defaults.EngagementDepthMode.ToString()),
                defaults.EngagementDepthMode);
            Current = new PlacementCutterPreset(
                FiniteOrDefault(settings.GetDouble(Prefix + "PrinterCorrection", defaults.PrinterCorrection), defaults.PrinterCorrection, -2, 2),
                fit,
                FiniteOrDefault(settings.GetDouble(Prefix + "BiteReduction", defaults.BiteReduction), defaults.BiteReduction, 0.001, 5),
                settings.GetBool(Prefix + "CounterboreBridgeEnabled", defaults.CounterboreBridgeEnabled),
                FiniteOrDefault(
                    settings.GetDouble(
                        Prefix + "CounterboreBridgeLayerHeight",
                        defaults.CounterboreBridgeLayerHeight),
                    defaults.CounterboreBridgeLayerHeight,
                    0.05,
                    1.0),
                depth,
                FiniteOrDefault(settings.GetDouble(Prefix + "EngagementBlindDepth", defaults.EngagementBlindDepth), defaults.EngagementBlindDepth, -1000, 1000),
                settings.GetBool(Prefix + "ClearancePreviewVisible", defaults.ClearancePreviewVisible),
                settings.GetBool(Prefix + "ClearanceBooleanEnabled", defaults.ClearanceBooleanEnabled),
                settings.GetBool(Prefix + "EngagementPreviewVisible", defaults.EngagementPreviewVisible),
                settings.GetBool(Prefix + "EngagementBooleanEnabled", defaults.EngagementBooleanEnabled),
                settings.GetBool(Prefix + "EngagementOnly", defaults.EngagementOnly));
        }
        catch
        {
            Current = defaults;
        }
    }

    public static bool Save(PersistentSettings settings, PlacementCutterPreset preset, out string message)
    {
        Current = preset;
        try
        {
            settings.SetDouble(Prefix + "PrinterCorrection", preset.PrinterCorrection);
            settings.SetString(Prefix + "ClearanceFit", preset.ClearanceFit.ToString());
            settings.SetDouble(Prefix + "BiteReduction", preset.BiteReduction);
            settings.SetBool(
                Prefix + "CounterboreBridgeEnabled",
                preset.CounterboreBridgeEnabled);
            settings.SetDouble(
                Prefix + "CounterboreBridgeLayerHeight",
                preset.CounterboreBridgeLayerHeight);
            settings.SetString(Prefix + "EngagementDepthMode", preset.EngagementDepthMode.ToString());
            settings.SetDouble(Prefix + "EngagementBlindDepth", preset.EngagementBlindDepth);
            settings.SetBool(Prefix + "ClearancePreviewVisible", preset.ClearancePreviewVisible);
            settings.SetBool(Prefix + "ClearanceBooleanEnabled", preset.ClearanceBooleanEnabled);
            settings.SetBool(Prefix + "EngagementPreviewVisible", preset.EngagementPreviewVisible);
            settings.SetBool(Prefix + "EngagementBooleanEnabled", preset.EngagementBooleanEnabled);
            settings.SetBool(Prefix + "EngagementOnly", preset.EngagementOnly);
            message = "放置切割预设已保存。";
            return true;
        }
        catch (Exception ex)
        {
            message = $"预设已用于当前会话，但无法写入 Rhino 设置：{ex.Message}";
            return false;
        }
    }

    private static T ParseEnum<T>(string value, T fallback) where T : struct, Enum =>
        Enum.TryParse<T>(value, out var parsed) && Enum.IsDefined(parsed) ? parsed : fallback;

    private static DepthMode ParseDepthMode(string value, DepthMode fallback) =>
        Enum.TryParse<DepthMode>(value, out var parsed)
        && parsed is DepthMode.ThroughTarget
            or DepthMode.FastenerLengthPlusOneDiameter
            or DepthMode.FastenerLengthPlusCustom
            ? parsed
            : fallback;

    private static double FiniteOrDefault(double value, double fallback, double minimum, double maximum) =>
        double.IsFinite(value) && value >= minimum && value <= maximum ? value : fallback;
}
