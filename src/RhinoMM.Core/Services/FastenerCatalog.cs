using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public sealed class FastenerCatalog
{
    private readonly Dictionary<string, FastenerSizeSpec> _bySize;

    public FastenerCatalog(FastenerCatalogDocument document)
    {
        Document = document;
        _bySize = document.Sizes.ToDictionary(x => x.Designation, StringComparer.OrdinalIgnoreCase);
    }

    public FastenerCatalogDocument Document { get; }
    public IReadOnlyList<FastenerSizeSpec> Sizes => Document.Sizes;

    public FastenerSizeSpec Get(string designation) =>
        _bySize.TryGetValue(designation, out var spec)
            ? spec
            : throw new KeyNotFoundException($"未找到紧固件规格：{designation}");

    public static FastenerCatalog LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resource = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("fastener-presets.v1.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException("无法读取内置紧固件规格库。");
        var document = JsonSerializer.Deserialize<FastenerCatalogDocument>(stream, JsonOptions.Default)
            ?? throw new InvalidOperationException("紧固件规格库为空。");
        return new FastenerCatalog(document);
    }
}

public static class JsonOptions
{
    public static JsonSerializerOptions Default { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
