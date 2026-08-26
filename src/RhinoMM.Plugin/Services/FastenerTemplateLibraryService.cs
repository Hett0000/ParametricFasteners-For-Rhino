using System.Text.Json;
using Rhino;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;

namespace RhinoMM.Plugin.Services;

internal enum FastenerOperationKind
{
    Placement,
    Update
}

internal static class FastenerTemplateLibraryService
{
    private const string LibraryKey = "TemplateLibrary.v1";
    private const int RecentLimit = 5;
    private const int FavoriteLimit = 50;
    private static FastenerTemplateLibraryDocument _current = new();
    private static AssemblySchemeOptions? _activeScheme;

    public static event EventHandler? Changed;

    public static FastenerTemplateLibraryDocument Current => _current;
    public static AssemblySchemeOptions? ActiveScheme => _activeScheme;

    public static void Load(PersistentSettings settings)
    {
        var json = settings.GetString(LibraryKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            _current = new FastenerTemplateLibraryDocument();
            return;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<FastenerTemplateLibraryDocument>(
                json,
                FastenerTemplateData.JsonOptions());
            if (parsed is null
                || parsed.SchemaVersion is < 1 or > FastenerTemplateLibraryDocument.CurrentSchemaVersion)
                throw new InvalidDataException("模板库版本不受支持。");
            _current = Normalize(parsed);
        }
        catch
        {
            try
            {
                settings.SetString(
                    $"TemplateLibrary.CorruptBackup.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
                    json);
            }
            catch
            {
                // A broken settings store must not stop plug-in loading.
            }
            _current = new FastenerTemplateLibraryDocument();
        }
    }

    public static bool RecordSuccessfulOperation(
        FastenerTemplateData data,
        FastenerOperationKind operation,
        out string message)
    {
        data = data.Normalize();
        if (!TryValidate(data, out message))
            return false;
        var signature = data.Signature();
        var recent = _current.Recent
            .Where(item => item.Data.Signature() != signature)
            .Prepend(new FastenerTemplateEntry(
                Guid.NewGuid(),
                FastenerTemplateFormatter.Compact(data),
                data,
                DateTimeOffset.UtcNow))
            .Take(RecentLimit)
            .ToArray();
        _current = _current with
        {
            Recent = recent,
            LastPlacement = operation == FastenerOperationKind.Placement
                ? data
                : _current.LastPlacement,
            LastUpdate = operation == FastenerOperationKind.Update
                ? data
                : _current.LastUpdate
        };
        return Save(out message);
    }

    public static bool AddFavorite(
        string? name,
        FastenerTemplateData data,
        out FastenerTemplateEntry entry,
        out string message)
    {
        data = data.Normalize();
        if (!TryValidate(data, out message))
        {
            entry = new FastenerTemplateEntry(Guid.Empty, string.Empty, data, DateTimeOffset.MinValue);
            return false;
        }

        var signature = data.Signature();
        var existing = _current.Favorites.FirstOrDefault(item => item.Data.Signature() == signature);
        if (existing is not null)
        {
            entry = existing;
            message = $"收藏中已存在“{existing.Name}”。";
            return true;
        }
        if (_current.Favorites.Count >= FavoriteLimit)
        {
            entry = new FastenerTemplateEntry(Guid.Empty, string.Empty, data, DateTimeOffset.MinValue);
            message = $"收藏模板最多保存 {FavoriteLimit} 个。";
            return false;
        }

        entry = new FastenerTemplateEntry(
            Guid.NewGuid(),
            UniqueName(string.IsNullOrWhiteSpace(name) ? FastenerTemplateFormatter.Compact(data) : name.Trim()),
            data,
            DateTimeOffset.UtcNow);
        _current = _current with { Favorites = _current.Favorites.Append(entry).ToArray() };
        return Save(out message);
    }

    public static bool RenameFavorite(Guid id, string name, out string message)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            message = "模板名称不能为空。";
            return false;
        }
        var found = false;
        var favorites = _current.Favorites.Select(item =>
        {
            if (item.Id != id)
                return item;
            found = true;
            return item with { Name = UniqueName(name, id), UpdatedAt = DateTimeOffset.UtcNow };
        }).ToArray();
        if (!found)
        {
            message = "没有找到要重命名的收藏模板。";
            return false;
        }
        _current = _current with { Favorites = favorites };
        return Save(out message);
    }

    public static bool SetSchemeOptions(Guid id, AssemblySchemeOptions? options, out string message)
    {
        var found = false;
        var favorites = _current.Favorites.Select(item =>
        {
            if (item.Id != id)
                return item;
            found = true;
            return item with { Scheme = options, UpdatedAt = DateTimeOffset.UtcNow };
        }).ToArray();
        if (!found)
        {
            message = "没有找到要设置的收藏模板。";
            return false;
        }
        _current = _current with { Favorites = favorites };
        return Save(out message);
    }

    public static void ActivateScheme(AssemblySchemeOptions? options) => _activeScheme = options;

    public static bool DeleteFavorite(Guid id, out string message)
    {
        var favorites = _current.Favorites.Where(item => item.Id != id).ToArray();
        if (favorites.Length == _current.Favorites.Count)
        {
            message = "没有找到要删除的收藏模板。";
            return false;
        }
        _current = _current with { Favorites = favorites };
        return Save(out message);
    }

    public static bool DuplicateFavorite(Guid id, out FastenerTemplateEntry duplicate, out string message)
    {
        var source = _current.Favorites.FirstOrDefault(item => item.Id == id);
        if (source is null)
        {
            duplicate = new FastenerTemplateEntry(Guid.Empty, string.Empty, new FastenerTemplateData(), DateTimeOffset.MinValue);
            message = "没有找到要复制的收藏模板。";
            return false;
        }
        if (_current.Favorites.Count >= FavoriteLimit)
        {
            duplicate = source;
            message = $"收藏模板最多保存 {FavoriteLimit} 个。";
            return false;
        }
        duplicate = new FastenerTemplateEntry(
            Guid.NewGuid(),
            UniqueName(source.Name + " 副本"),
            source.Data,
            DateTimeOffset.UtcNow,
            source.Scheme);
        _current = _current with { Favorites = _current.Favorites.Append(duplicate).ToArray() };
        return Save(out message);
    }

    public static bool MoveFavorite(Guid id, int delta, out string message)
    {
        var list = _current.Favorites.ToList();
        var index = list.FindIndex(item => item.Id == id);
        if (index < 0)
        {
            message = "没有找到要排序的收藏模板。";
            return false;
        }
        var target = Math.Clamp(index + delta, 0, list.Count - 1);
        if (target == index)
        {
            message = "模板已经位于该方向的边界。";
            return true;
        }
        (list[index], list[target]) = (list[target], list[index]);
        _current = _current with { Favorites = list.ToArray() };
        return Save(out message);
    }

    public static string ExportFavoritesJson() => JsonSerializer.Serialize(
        new FastenerTemplateLibraryDocument { Favorites = _current.Favorites },
        FastenerTemplateData.JsonOptions());

    public static bool ImportFavoritesJson(string json, out int imported, out int skipped, out string message)
    {
        imported = 0;
        skipped = 0;
        FastenerTemplateLibraryDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<FastenerTemplateLibraryDocument>(
                json,
                FastenerTemplateData.JsonOptions());
        }
        catch (Exception ex)
        {
            message = $"模板文件无法读取：{ex.Message}";
            return false;
        }
        if (document is null
            || document.SchemaVersion is < 1 or > FastenerTemplateLibraryDocument.CurrentSchemaVersion)
        {
            message = "模板文件版本不受支持。";
            return false;
        }

        var list = _current.Favorites.ToList();
        var signatures = list.Select(item => item.Data.Signature()).ToHashSet(StringComparer.Ordinal);
        foreach (var source in document.Favorites)
        {
            var data = source.Data.Normalize();
            if (!TryValidate(data, out _) || signatures.Contains(data.Signature()))
            {
                skipped++;
                continue;
            }
            if (list.Count >= FavoriteLimit)
            {
                skipped += document.Favorites.Count - imported - skipped;
                break;
            }
            var preferredName = string.IsNullOrWhiteSpace(source.Name)
                ? FastenerTemplateFormatter.Compact(data)
                : source.Name.Trim();
            var name = list.Any(item => string.Equals(item.Name, preferredName, StringComparison.OrdinalIgnoreCase))
                ? UniqueImportedName(preferredName, list)
                : preferredName;
            list.Add(new FastenerTemplateEntry(
                Guid.NewGuid(),
                name,
                data,
                DateTimeOffset.UtcNow,
                source.Scheme));
            signatures.Add(data.Signature());
            imported++;
        }
        _current = _current with { Favorites = list.ToArray() };
        if (!Save(out var saveMessage))
        {
            message = saveMessage;
            return false;
        }
        message = $"已导入 {imported} 个收藏模板，跳过 {skipped} 个重复或无效模板。";
        return true;
    }

    public static bool TryValidate(FastenerTemplateData data, out string message)
    {
        try
        {
            var spec = FastenerSpecResolver.Resolve(data, RhinoMMPlugIn.Catalog);
            if (!double.IsFinite(data.PrinterCorrection)
                || !double.IsFinite(data.Length)
                || !double.IsFinite(data.HeadEmbedDepth))
                throw new InvalidDataException("模板包含非法的尺寸数值。");
            if (data.HeadEmbedDepth < 0
                && !FastenerKindTraits.SupportsHeadGap(data.Kind))
                throw new InvalidDataException("只有螺丝模板支持负值离面间隙。");
            if (FastenerKindTraits.UsesLengthInStatistics(data.Kind) && data.Length <= 0)
                throw new InvalidDataException("紧固件长度必须大于 0。" );
            if (FastenerKindTraits.IsScrew(data.Kind)
                && data.HeadEmbedDepth + data.Length <= 0)
                throw new InvalidDataException("离面距离必须小于螺杆长度。" );
            if (data.Kind == FastenerKind.HeatSetInsert
                && (data.InsertOuterDiameter <= spec.NominalDiameter
                    || data.InsertDepthCompensation < 0))
                throw new InvalidDataException("热熔螺母外径或深度补偿无效。" );
            if (data.Kind == FastenerKind.HexNut
                && data.NutStyle == HexNutStyle.NylonInsertLocking
                && spec.NominalDiameter < 2)
                throw new InvalidDataException("尼龙防松螺母规格必须从 M2 开始。" );
            message = "模板有效。";
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    private static FastenerTemplateLibraryDocument Normalize(FastenerTemplateLibraryDocument source)
    {
        static IReadOnlyList<FastenerTemplateEntry> NormalizeEntries(
            IEnumerable<FastenerTemplateEntry> entries,
            int limit,
            bool deduplicate)
        {
            var signatures = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<FastenerTemplateEntry>();
            foreach (var entry in entries)
            {
                var data = entry.Data.Normalize();
                if (!TryValidate(data, out _)
                    || (deduplicate && !signatures.Add(data.Signature())))
                    continue;
                if (!deduplicate)
                    signatures.Add(data.Signature());
                result.Add(entry with
                {
                    Id = entry.Id == Guid.Empty ? Guid.NewGuid() : entry.Id,
                    Name = string.IsNullOrWhiteSpace(entry.Name)
                        ? FastenerTemplateFormatter.Compact(data)
                        : entry.Name.Trim(),
                    Data = data
                });
                if (result.Count == limit)
                    break;
            }
            return result;
        }

        return source with
        {
            SchemaVersion = FastenerTemplateLibraryDocument.CurrentSchemaVersion,
            Recent = NormalizeEntries(source.Recent, RecentLimit, true),
            Favorites = NormalizeEntries(source.Favorites, FavoriteLimit, false),
            LastPlacement = source.LastPlacement?.Normalize(),
            LastUpdate = source.LastUpdate?.Normalize()
        };
    }

    private static bool Save(out string message)
    {
        if (RhinoMMPlugIn.Instance is null)
        {
            message = "模板已用于当前会话，但插件设置尚不可用。";
            Changed?.Invoke(null, EventArgs.Empty);
            return false;
        }
        try
        {
            RhinoMMPlugIn.Instance.Settings.SetString(
                LibraryKey,
                JsonSerializer.Serialize(_current, FastenerTemplateData.JsonOptions()));
            message = "模板库已保存。";
            Changed?.Invoke(null, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            message = $"模板已用于当前会话，但无法保存到 Rhino 设置：{ex.Message}";
            Changed?.Invoke(null, EventArgs.Empty);
            return false;
        }
    }

    private static string UniqueName(string desired, Guid? ignore = null)
    {
        if (_current.Favorites.All(item => item.Id == ignore
            || !string.Equals(item.Name, desired, StringComparison.OrdinalIgnoreCase)))
            return desired;
        var suffix = 2;
        while (_current.Favorites.Any(item => item.Id != ignore
            && string.Equals(item.Name, $"{desired} {suffix}", StringComparison.OrdinalIgnoreCase)))
            suffix++;
        return $"{desired} {suffix}";
    }

    private static string UniqueImportedName(string desired, IReadOnlyList<FastenerTemplateEntry> entries)
    {
        var candidate = $"{desired}（导入）";
        var suffix = 2;
        while (entries.Any(item => string.Equals(item.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            candidate = $"{desired}（导入 {suffix++}）";
        return candidate;
    }
}

internal static class FastenerTemplateFormatter
{
    public static string Compact(FastenerTemplateData data)
    {
        var kind = data.Kind switch
        {
            FastenerKind.SocketCap => "杯头",
            FastenerKind.Countersunk => "沉头",
            FastenerKind.HexBolt => "六角头",
            FastenerKind.HexNut => data.NutStyle == HexNutStyle.NylonInsertLocking
                ? "防松螺母"
                : "六角螺母",
            FastenerKind.HeatSetInsert => "热熔螺母",
            _ => FastenerLabels.Kind(data.Kind)
        };
        if (data.Kind == FastenerKind.HexNut)
            return $"{data.Size} {kind} · 嵌入{data.HeadEmbedDepth:0.##}";
        if (data.Kind == FastenerKind.HeatSetInsert)
            return $"{data.Size} {kind} · Ø{data.InsertOuterDiameter:0.##}×{data.Length:0.##}";
        var mode = data.AssemblyMode switch
        {
            ScrewAssemblyMode.EngagementOnly => "只咬合",
            ScrewAssemblyMode.NutFastened => "螺母固定",
            _ => FastenerLabels.Depth(data.EngagementDepthMode)
        };
        var gap = data.HeadEmbedDepth < 0
            ? $" · 离面{Math.Abs(data.HeadEmbedDepth):0.##}"
            : string.Empty;
        return $"{data.Size}×{data.Length:0.##} {kind}{gap} · {mode}";
    }
}
