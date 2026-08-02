using Rhino;

namespace RhinoMM.Plugin.Services;

public sealed record GlobalDisplaySettings(
    double FastenerOpacityPercent,
    double CutterOpacityPercent)
{
    public static GlobalDisplaySettings Default { get; } = new(70, 35);
}

internal static class GlobalDisplaySettingsService
{
    private const string Prefix = "GlobalDisplay.";

    public static GlobalDisplaySettings Current { get; private set; } =
        GlobalDisplaySettings.Default;

    public static void Load(PersistentSettings settings)
    {
        var defaults = GlobalDisplaySettings.Default;
        try
        {
            Current = new GlobalDisplaySettings(
                Clamp(settings.GetDouble(
                    Prefix + "FastenerOpacityPercent",
                    defaults.FastenerOpacityPercent),
                    defaults.FastenerOpacityPercent),
                Clamp(settings.GetDouble(
                    Prefix + "CutterOpacityPercent",
                    defaults.CutterOpacityPercent),
                    defaults.CutterOpacityPercent));
        }
        catch
        {
            Current = defaults;
        }
        ApplyToEditorState();
    }

    public static bool Save(
        PersistentSettings settings,
        GlobalDisplaySettings displaySettings,
        out string message)
    {
        SetCurrent(displaySettings);
        try
        {
            settings.SetDouble(
                Prefix + "FastenerOpacityPercent",
                Current.FastenerOpacityPercent);
            settings.SetDouble(
                Prefix + "CutterOpacityPercent",
                Current.CutterOpacityPercent);
            message = "全局显示默认值已保存。";
            return true;
        }
        catch (Exception ex)
        {
            message = $"设置已用于当前会话，但无法写入 Rhino 设置：{ex.Message}";
            return false;
        }
    }

    public static void SetCurrent(GlobalDisplaySettings displaySettings)
    {
        Current = new GlobalDisplaySettings(
            Clamp(displaySettings.FastenerOpacityPercent, GlobalDisplaySettings.Default.FastenerOpacityPercent),
            Clamp(displaySettings.CutterOpacityPercent, GlobalDisplaySettings.Default.CutterOpacityPercent));
        ApplyToEditorState();
    }

    public static void ApplyToEditorState()
    {
        EditorState.Current.FastenerOpacityPercent = Current.FastenerOpacityPercent;
        EditorState.Current.CutterOpacityPercent = Current.CutterOpacityPercent;
    }

    private static double Clamp(double value, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, 0, 100) : fallback;
}
