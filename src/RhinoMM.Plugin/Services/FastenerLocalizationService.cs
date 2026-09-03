using System.Globalization;
using Rhino;
using Rhino.ApplicationSettings;
using Rhino.PlugIns;
using RhinoMM.Core.Services;

namespace RhinoMM.Plugin.Services;

public enum FastenerLanguageMode
{
    Auto,
    SimplifiedChinese,
    English
}

public interface ILocalizableView
{
    void ApplyLocalization();
}

internal static class FastenerLocalizationService
{
    private const string SettingKey = "Localization.LanguageMode";
    private static readonly List<WeakReference<ILocalizableView>> Views = [];

    public static FastenerLanguageMode Mode { get; private set; } = FastenerLanguageMode.Auto;
    public static FastenerLanguage ResolvedLanguage { get; private set; } = FastenerLanguage.SimplifiedChinese;
    public static int RhinoLanguageId => SafeRhinoLanguageId();
    public static event EventHandler? Changed;

    public static void Load(PersistentSettings settings)
    {
        var raw = settings.GetString(SettingKey, FastenerLanguageMode.Auto.ToString());
        Mode = Enum.TryParse<FastenerLanguageMode>(raw, true, out var parsed)
            ? parsed
            : FastenerLanguageMode.Auto;
        ApplyResolvedLanguage(notify: false);
    }

    public static bool SetMode(FastenerLanguageMode mode, out string message)
    {
        Mode = mode;
        var settings = RhinoMMPlugIn.Instance?.Settings;
        if (settings is not null)
            settings.SetString(SettingKey, mode.ToString());
        ApplyResolvedLanguage(notify: true);
        message = FastenerText.Get(FastenerTextKey.PanelTitleRestartNotice);
        return true;
    }

    public static void Register(ILocalizableView view)
    {
        Cleanup();
        if (!Views.Any(reference => reference.TryGetTarget(out var current) && ReferenceEquals(current, view)))
            Views.Add(new WeakReference<ILocalizableView>(view));
    }

    public static void Unregister(ILocalizableView view) =>
        Views.RemoveAll(reference => !reference.TryGetTarget(out var current) || ReferenceEquals(current, view));

    public static string ModeLabel() => Mode switch
    {
        FastenerLanguageMode.SimplifiedChinese => FastenerText.Get(FastenerTextKey.LanguageChinese),
        FastenerLanguageMode.English => FastenerText.Get(FastenerTextKey.LanguageEnglish),
        _ => FastenerText.Get(FastenerTextKey.LanguageAuto)
    };

    private static void ApplyResolvedLanguage(bool notify)
    {
        ResolvedLanguage = Mode switch
        {
            FastenerLanguageMode.SimplifiedChinese => FastenerLanguage.SimplifiedChinese,
            FastenerLanguageMode.English => FastenerLanguage.English,
            _ => ResolveRhinoLanguage()
        };
        FastenerText.SetLanguage(ResolvedLanguage);
        if (!notify)
            return;
        RhinoApp.InvokeOnUiThread((Action)(() =>
        {
            Cleanup();
            foreach (var reference in Views.ToArray())
                if (reference.TryGetTarget(out var view))
                    view.ApplyLocalization();
            Changed?.Invoke(null, EventArgs.Empty);
        }));
    }

    private static FastenerLanguage ResolveRhinoLanguage()
    {
        var id = SafeRhinoLanguageId();
        return id is 2052 or 4100
            ? FastenerLanguage.SimplifiedChinese
            : FastenerLanguage.English;
    }

    private static int SafeRhinoLanguageId()
    {
        try { return AppearanceSettings.LanguageIdentifier; }
        catch { return CultureInfo.CurrentUICulture.LCID; }
    }

    private static void Cleanup() => Views.RemoveAll(reference => !reference.TryGetTarget(out _));
}
