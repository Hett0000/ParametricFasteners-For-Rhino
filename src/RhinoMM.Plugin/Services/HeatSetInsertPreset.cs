using Rhino;

namespace RhinoMM.Plugin.Services;

internal sealed record HeatSetInsertPreset(
    double Length,
    double OuterDiameter,
    double DiameterCompensation,
    double DepthCompensation,
    bool PreviewVisible,
    bool BooleanEnabled)
{
    public static HeatSetInsertPreset Default { get; } = new(0, 0, 0, 1, true, true);
}

internal static class HeatSetInsertPresetService
{
    private const string Prefix = "HeatSetInsertPreset.";

    public static HeatSetInsertPreset Current { get; private set; } = HeatSetInsertPreset.Default;

    public static void Load(PersistentSettings settings)
    {
        var defaults = HeatSetInsertPreset.Default;
        try
        {
            Current = new HeatSetInsertPreset(
                Finite(settings.GetDouble(Prefix + "Length", defaults.Length), defaults.Length, 0, 1000),
                Finite(settings.GetDouble(Prefix + "OuterDiameter", defaults.OuterDiameter), defaults.OuterDiameter, 0, 1000),
                Finite(settings.GetDouble(Prefix + "DiameterCompensation", defaults.DiameterCompensation), defaults.DiameterCompensation, -20, 20),
                Finite(settings.GetDouble(Prefix + "DepthCompensation", defaults.DepthCompensation), defaults.DepthCompensation, 0, 1000),
                settings.GetBool(Prefix + "PreviewVisible", defaults.PreviewVisible),
                settings.GetBool(Prefix + "BooleanEnabled", defaults.BooleanEnabled));
        }
        catch
        {
            Current = defaults;
        }
    }

    public static bool Save(PersistentSettings settings, HeatSetInsertPreset preset, out string message)
    {
        Current = preset;
        try
        {
            settings.SetDouble(Prefix + "Length", preset.Length);
            settings.SetDouble(Prefix + "OuterDiameter", preset.OuterDiameter);
            settings.SetDouble(Prefix + "DiameterCompensation", preset.DiameterCompensation);
            settings.SetDouble(Prefix + "DepthCompensation", preset.DepthCompensation);
            settings.SetBool(Prefix + "PreviewVisible", preset.PreviewVisible);
            settings.SetBool(Prefix + "BooleanEnabled", preset.BooleanEnabled);
            message = "热熔螺母参数已保存。";
            return true;
        }
        catch (Exception ex)
        {
            message = $"热熔螺母参数已用于当前会话，但无法写入 Rhino 设置：{ex.Message}";
            return false;
        }
    }

    private static double Finite(double value, double fallback, double minimum, double maximum) =>
        double.IsFinite(value) && value >= minimum && value <= maximum ? value : fallback;
}
