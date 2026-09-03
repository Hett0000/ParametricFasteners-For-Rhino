using System.Globalization;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace RhinoMM.Core.Services;

public enum FastenerLanguage
{
    SimplifiedChinese,
    English
}

public enum FastenerTextKey
{
    ProductName,
    NavigatorName,
    Language,
    LanguageAuto,
    LanguageChinese,
    LanguageEnglish,
    PanelTitleRestartNotice
}

/// <summary>
/// Process-wide text source shared by the Rhino UI and the headless report writers.
/// Component data and serialization remain culture invariant.
/// </summary>
public static class FastenerText
{
    private const string ChineseResource = "RhinoMM.Core.Localization.strings.zh-CN.json";
    private const string EnglishResource = "RhinoMM.Core.Localization.strings.en-US.json";
    private static readonly IReadOnlyDictionary<string, string> Chinese = Load(ChineseResource);
    private static readonly IReadOnlyDictionary<string, string> English = Load(EnglishResource);
    private static readonly IReadOnlyList<(string Chinese, string English)> EnglishPhrases = Chinese
        .Where(pair => English.ContainsKey(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
        .Select(pair => (Chinese: pair.Value, English: English[pair.Key]))
        .Where(pair => !string.IsNullOrWhiteSpace(pair.Chinese)
            && !string.Equals(pair.Chinese, pair.English, StringComparison.Ordinal))
        .OrderByDescending(pair => pair.Chinese.Length)
        .ToArray();

    public static FastenerLanguage Language { get; private set; } = FastenerLanguage.SimplifiedChinese;
    public static CultureInfo Culture => Language == FastenerLanguage.SimplifiedChinese
        ? CultureInfo.GetCultureInfo("zh-CN")
        : CultureInfo.GetCultureInfo("en-US");
    public static string ResourceVersion => "1";
    public static event EventHandler? LanguageChanged;

    public static void SetLanguage(FastenerLanguage language)
    {
        if (Language == language)
            return;
        Language = language;
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string Get(FastenerTextKey key, params object?[] arguments) =>
        Get(Key(key), arguments);

    public static string Get(string key, params object?[] arguments)
        => GetFor(Language, key, arguments);

    public static string GetFor(FastenerLanguage language, string key, params object?[] arguments)
    {
        var resource = language == FastenerLanguage.SimplifiedChinese ? Chinese : English;
        if (!resource.TryGetValue(key, out var text) && !English.TryGetValue(key, out text))
        {
            Trace.WriteLine($"[Parametric Fasteners] Missing localization key: {key}");
            text = key;
        }
        var culture = language == FastenerLanguage.SimplifiedChinese
            ? CultureInfo.GetCultureInfo("zh-CN")
            : CultureInfo.GetCultureInfo("en-US");
        return arguments.Length == 0 ? text : string.Format(culture, text, arguments);
    }

    /// <summary>
    /// Localizes legacy display strings while they are migrated to stable resource keys.
    /// Longest phrases are replaced first so composed summaries stay readable.
    /// </summary>
    public static string Translate(string? source)
        => TranslateFor(Language, source);

    public static string TranslateFor(FastenerLanguage language, string? source)
    {
        if (string.IsNullOrEmpty(source) || language == FastenerLanguage.SimplifiedChinese)
            return source ?? string.Empty;

        var exact = Chinese.FirstOrDefault(pair => string.Equals(pair.Value, source, StringComparison.Ordinal));
        if (!string.IsNullOrEmpty(exact.Key) && English.TryGetValue(exact.Key, out var translated))
            return translated;

        var result = source;
        foreach (var pair in EnglishPhrases)
            result = result.Replace(pair.Chinese, pair.English, StringComparison.Ordinal);
        return result;
    }

    public static bool TryParseNumber(string? text, out double value) =>
        double.TryParse(text, NumberStyles.Float, Culture, out value)
        || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
        || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    public static IReadOnlyCollection<string> Keys => Chinese.Keys.ToArray();
    public static bool ResourcesHaveMatchingKeys() => Chinese.Keys.Order().SequenceEqual(English.Keys.Order());

    private static string Key(FastenerTextKey key) => key switch
    {
        FastenerTextKey.ProductName => "Product.Name",
        FastenerTextKey.NavigatorName => "Product.Navigator",
        FastenerTextKey.Language => "Settings.Language",
        FastenerTextKey.LanguageAuto => "Settings.Language.Auto",
        FastenerTextKey.LanguageChinese => "Settings.Language.Chinese",
        FastenerTextKey.LanguageEnglish => "Settings.Language.English",
        FastenerTextKey.PanelTitleRestartNotice => "Settings.Language.PanelRestart",
        _ => key.ToString()
    };

    private static IReadOnlyDictionary<string, string> Load(string resourceName)
    {
        using var stream = typeof(FastenerText).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing localization resource: {resourceName}");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidOperationException($"Invalid localization resource: {resourceName}");
    }
}
