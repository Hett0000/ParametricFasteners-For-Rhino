using System.Reflection;
using System.Text.Json;
using RhinoMM.Core.Services;
using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class FastenerLocalizationTests
{
    [Fact]
    public void EmbeddedResourcesHaveIdenticalKeysAndRequiredMechanicalTerms()
    {
        var assembly = typeof(FastenerText).Assembly;
        var chinese = Read(assembly, "RhinoMM.Core.Localization.strings.zh-CN.json");
        var english = Read(assembly, "RhinoMM.Core.Localization.strings.en-US.json");

        Assert.Equal(chinese.Keys.Order(), english.Keys.Order());
        Assert.Equal("参数化紧固件", chinese["Product.Name"]);
        Assert.Equal("Parametric Fasteners", english["Product.Name"]);
        Assert.Equal("Socket Head Cap Screw", english["Fastener.SocketCap"]);
        Assert.Equal("Thread Engagement", english["Assembly.ThreadEngagement"]);
        Assert.Equal("Engagement Only", english["Assembly.EngagementOnly"]);
        Assert.Equal("Nut Fastened", english["Assembly.NutFastened"]);
        Assert.Equal("Delivery", english["Output.Delivery"]);
    }

    [Theory]
    [InlineData("3.25", 3.25)]
    [InlineData("20", 20)]
    public void NumericInputAlwaysAcceptsInvariantFormat(string text, double expected)
    {
        Assert.True(FastenerText.TryParseNumber(text, out var actual));
        Assert.Equal(expected, actual, 8);
    }

    [Fact]
    public void ExplicitLanguageLookupDoesNotMutateProcessLanguage()
    {
        var before = FastenerText.Language;
        Assert.Equal("Parametric Fasteners", FastenerText.GetFor(FastenerLanguage.English, "Product.Name"));
        Assert.Equal("Socket Cap M3", FastenerText.TranslateFor(FastenerLanguage.English, "杯头 M3"));
        Assert.Equal(before, FastenerText.Language);
    }

    [Fact]
    public void ResourceFormatArgumentsMatchAcrossLanguages()
    {
        var assembly = typeof(FastenerText).Assembly;
        var chinese = Read(assembly, "RhinoMM.Core.Localization.strings.zh-CN.json");
        var english = Read(assembly, "RhinoMM.Core.Localization.strings.en-US.json");
        foreach (var key in chinese.Keys)
            Assert.Equal(Arguments(chinese[key]), Arguments(english[key]));
    }

    private static IReadOnlyDictionary<string, string> Read(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name);
        Assert.NotNull(stream);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream!)!;
    }

    private static string[] Arguments(string value) =>
        System.Text.RegularExpressions.Regex.Matches(value, @"\{\d+(?::[^}]*)?\}")
            .Select(match => match.Value.Split(':')[0] + "}")
            .Distinct()
            .Order()
            .ToArray();
}
