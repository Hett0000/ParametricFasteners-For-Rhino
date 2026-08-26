using System.Globalization;
using System.Text;
using System.Text.Json;
using Rhino;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;

namespace RhinoMM.Plugin.Services;

internal static class UserFastenerLibraryService
{
    private const string LibraryKey = "UserFastenerLibrary.v1";
    private static UserFastenerLibraryDocument _current = new();

    public static event EventHandler? Changed;
    public static UserFastenerLibraryDocument Current => _current;

    public static void Load(PersistentSettings settings)
    {
        var json = settings.GetString(LibraryKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            _current = new();
            return;
        }
        try
        {
            var parsed = JsonSerializer.Deserialize<UserFastenerLibraryDocument>(json, JsonOptions.Default)
                ?? throw new InvalidDataException("用户标准件库为空。");
            if (parsed.SchemaVersion != UserFastenerLibraryDocument.CurrentSchemaVersion)
                throw new InvalidDataException("用户标准件库版本不受支持。");
            var valid = parsed.Definitions.Where(item => UserFastenerDefinitionValidator.Validate(item).IsValid)
                .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First()).ToArray();
            _current = parsed with { Definitions = valid };
        }
        catch
        {
            try { settings.SetString($"UserFastenerLibrary.CorruptBackup.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}", json); }
            catch { }
            _current = new();
        }
    }

    public static UserFastenerDefinition CreateFromBuiltIn(
        FastenerKind kind,
        string size,
        string? name = null)
    {
        var spec = RhinoMMPlugIn.Catalog.Get(size);
        return new UserFastenerDefinition
        {
            DefinitionId = Guid.NewGuid(),
            Name = UniqueName(string.IsNullOrWhiteSpace(name) ? $"{FastenerLabels.Kind(kind)} {size} 自定义" : name.Trim()),
            Kind = kind,
            ThreadDesignation = size,
            Source = "内置规格副本",
            Revision = "1",
            SizeSpec = spec,
            LockingNutSpec = kind == FastenerKind.HexNut
                ? TryLockingNut(size)
                : null,
            DefaultLength = FastenerKindTraits.UsesLengthInStatistics(kind) ? 12 : 0,
            DefaultInsertOuterDiameter = kind == FastenerKind.HeatSetInsert ? spec.NominalDiameter + 1.5 : 0,
            DefaultInsertDepthCompensation = kind == FastenerKind.HeatSetInsert ? 1 : 0
        };
    }

    public static bool Upsert(UserFastenerDefinition definition, out string message)
    {
        definition = Normalize(definition);
        var validation = UserFastenerDefinitionValidator.Validate(definition);
        if (!validation.IsValid)
        {
            message = string.Join(" ", validation.Issues.Where(issue => issue.IsError).Select(issue => issue.Message));
            return false;
        }
        if (_current.Definitions.Any(item => item.DefinitionId != definition.DefinitionId
            && string.Equals(item.Name, definition.Name, StringComparison.OrdinalIgnoreCase)))
        {
            message = "自定义标准件名称必须唯一。";
            return false;
        }
        _current = _current with
        {
            Definitions = _current.Definitions.Where(item => item.DefinitionId != definition.DefinitionId)
                .Append(definition).OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToArray()
        };
        return Save(out message);
    }

    public static bool Delete(Guid id, out string message)
    {
        var next = _current.Definitions.Where(item => item.DefinitionId != id).ToArray();
        if (next.Length == _current.Definitions.Count)
        {
            message = "没有找到要删除的自定义标准件。";
            return false;
        }
        _current = _current with { Definitions = next };
        return Save(out message);
    }

    public static string ExportJson() => JsonSerializer.Serialize(_current, JsonOptions.Default);

    public static string ExportCsv()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Name,Kind,ThreadDesignation,Source,Revision,NominalDiameter,Pitch,SocketDiameter,SocketHeight,CountersunkDiameter,CountersunkAngle,HexAcrossFlats,HexHeight,NutAcrossFlats,NutThickness,DefaultLength,InsertOuterDiameter,InsertDiameterCompensation,InsertDepthCompensation,Notes");
        foreach (var item in _current.Definitions)
        {
            var s = item.SizeSpec;
            var h = s.Head;
            builder.AppendLine(string.Join(",", new[]
            {
                Csv(item.Name), item.Kind.ToString(), Csv(item.ThreadDesignation), Csv(item.Source), Csv(item.Revision),
                N(s.NominalDiameter), N(s.CoarsePitch), N(h.SocketDiameter), N(h.SocketHeight),
                N(h.CountersunkDiameter), N(h.CountersunkAngle), N(h.HexAcrossFlats), N(h.HexHeight),
                N(h.NutAcrossFlats), N(h.NutThickness), N(item.DefaultLength), N(item.DefaultInsertOuterDiameter),
                N(item.DefaultInsertDiameterCompensation), N(item.DefaultInsertDepthCompensation), Csv(item.Notes)
            }));
        }
        return builder.ToString();
    }

    public static bool ImportJson(string json, out int imported, out int skipped, out string message)
    {
        imported = skipped = 0;
        UserFastenerLibraryDocument? parsed;
        try { parsed = JsonSerializer.Deserialize<UserFastenerLibraryDocument>(json, JsonOptions.Default); }
        catch (Exception ex) { message = $"JSON 无法读取：{ex.Message}"; return false; }
        if (parsed?.SchemaVersion != UserFastenerLibraryDocument.CurrentSchemaVersion)
        {
            message = "用户标准件库版本不受支持。";
            return false;
        }
        var list = _current.Definitions.ToList();
        foreach (var source in parsed.Definitions)
        {
            var item = Normalize(source);
            if (!UserFastenerDefinitionValidator.Validate(item).IsValid)
            {
                skipped++;
                continue;
            }
            var sameId = list.FirstOrDefault(existing => existing.DefinitionId == item.DefinitionId);
            if (sameId == item)
            {
                skipped++;
                continue;
            }
            if (sameId is not null)
                item = item with { DefinitionId = Guid.NewGuid(), Name = UniqueImportedName(item.Name, list) };
            else if (list.Any(existing => string.Equals(existing.Name, item.Name, StringComparison.OrdinalIgnoreCase)))
                item = item with { Name = UniqueImportedName(item.Name, list) };
            list.Add(item);
            imported++;
        }
        _current = _current with { Definitions = list.ToArray() };
        if (!Save(out message))
            return false;
        message = $"已导入 {imported} 项，跳过 {skipped} 项。";
        return true;
    }

    public static bool ImportCsv(string csv, out int imported, out int skipped, out string message)
    {
        imported = skipped = 0;
        var lines = csv.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            message = "CSV 没有可导入的数据行。";
            return false;
        }
        var headers = ParseCsvLine(lines[0]).Select((value, index) => (value, index))
            .ToDictionary(item => item.value, item => item.index, StringComparer.OrdinalIgnoreCase);
        string Get(IReadOnlyList<string> values, string name) => headers.TryGetValue(name, out var index) && index < values.Count ? values[index] : string.Empty;
        double GetNumber(IReadOnlyList<string> values, string name, double fallback = 0) =>
            double.TryParse(Get(values, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
        var list = _current.Definitions.ToList();
        for (var row = 1; row < lines.Length; row++)
        {
            try
            {
                var values = ParseCsvLine(lines[row]);
                if (!Enum.TryParse<FastenerKind>(Get(values, "Kind"), true, out var kind))
                    throw new InvalidDataException("Kind 无效。" );
                var designation = Get(values, "ThreadDesignation").Trim();
                var baseSpec = RhinoMMPlugIn.Catalog.Sizes.FirstOrDefault(item => string.Equals(item.Designation, designation, StringComparison.OrdinalIgnoreCase))
                    ?? RhinoMMPlugIn.Catalog.Get("M3");
                var h = baseSpec.Head with
                {
                    SocketDiameter = GetNumber(values, "SocketDiameter", baseSpec.Head.SocketDiameter),
                    SocketHeight = GetNumber(values, "SocketHeight", baseSpec.Head.SocketHeight),
                    CountersunkDiameter = GetNumber(values, "CountersunkDiameter", baseSpec.Head.CountersunkDiameter),
                    CountersunkAngle = GetNumber(values, "CountersunkAngle", baseSpec.Head.CountersunkAngle),
                    HexAcrossFlats = GetNumber(values, "HexAcrossFlats", baseSpec.Head.HexAcrossFlats),
                    HexHeight = GetNumber(values, "HexHeight", baseSpec.Head.HexHeight),
                    NutAcrossFlats = GetNumber(values, "NutAcrossFlats", baseSpec.Head.NutAcrossFlats),
                    NutThickness = GetNumber(values, "NutThickness", baseSpec.Head.NutThickness)
                };
                var item = new UserFastenerDefinition
                {
                    DefinitionId = Guid.NewGuid(),
                    Name = Get(values, "Name").Trim(),
                    Kind = kind,
                    ThreadDesignation = designation,
                    Source = Get(values, "Source"),
                    Revision = Get(values, "Revision"),
                    Notes = Get(values, "Notes"),
                    SizeSpec = baseSpec with
                    {
                        Designation = designation,
                        NominalDiameter = GetNumber(values, "NominalDiameter"),
                        CoarsePitch = GetNumber(values, "Pitch"),
                        Head = h
                    },
                    DefaultLength = GetNumber(values, "DefaultLength"),
                    DefaultInsertOuterDiameter = GetNumber(values, "InsertOuterDiameter"),
                    DefaultInsertDiameterCompensation = GetNumber(values, "InsertDiameterCompensation"),
                    DefaultInsertDepthCompensation = GetNumber(values, "InsertDepthCompensation")
                };
                item = Normalize(item);
                if (!UserFastenerDefinitionValidator.Validate(item).IsValid)
                    throw new InvalidDataException("尺寸关系无效。" );
                if (list.Any(existing => string.Equals(existing.Name, item.Name, StringComparison.OrdinalIgnoreCase)))
                    item = item with { Name = UniqueImportedName(item.Name, list) };
                list.Add(item);
                imported++;
            }
            catch
            {
                skipped++;
            }
        }
        _current = _current with { Definitions = list.ToArray() };
        if (!Save(out message))
            return false;
        message = $"已从 CSV 导入 {imported} 项，跳过 {skipped} 项。";
        return true;
    }

    public static void ApplyToEditor(UserFastenerDefinition definition)
    {
        var snapshot = definition.Snapshot();
        var state = EditorState.Current;
        state.Kind = definition.Kind;
        state.Size = definition.ThreadDesignation;
        state.CustomDefinitionId = definition.DefinitionId;
        state.CustomDefinitionName = definition.Name;
        state.CustomDefinitionSnapshot = snapshot;
        if (FastenerKindTraits.UsesLengthInStatistics(definition.Kind) && definition.DefaultLength > 0)
            state.Length = definition.DefaultLength;
        if (definition.Kind == FastenerKind.HeatSetInsert)
        {
            state.InsertOuterDiameter = definition.DefaultInsertOuterDiameter;
            state.InsertDiameterCompensation = definition.DefaultInsertDiameterCompensation;
            state.InsertDepthCompensation = definition.DefaultInsertDepthCompensation;
        }
        SmartPlacementDraftChangeService.NotifyChanged();
    }

    private static bool Save(out string message)
    {
        try
        {
            RhinoMMPlugIn.Instance?.Settings.SetString(LibraryKey, ExportJson());
            Changed?.Invoke(null, EventArgs.Empty);
            message = "用户标准件库已保存。";
            return true;
        }
        catch (Exception ex)
        {
            message = $"用户标准件库无法保存：{ex.Message}";
            return false;
        }
    }

    private static UserFastenerDefinition Normalize(UserFastenerDefinition item) => item with
    {
        SchemaVersion = UserFastenerDefinition.CurrentSchemaVersion,
        DefinitionId = item.DefinitionId == Guid.Empty ? Guid.NewGuid() : item.DefinitionId,
        Name = item.Name.Trim(),
        ThreadDesignation = item.ThreadDesignation.Trim(),
        Source = item.Source.Trim(),
        Revision = item.Revision.Trim(),
        Notes = item.Notes.Trim(),
        SizeSpec = item.SizeSpec with { Designation = item.ThreadDesignation.Trim() }
    };

    private static LockingNutSizeSpec? TryLockingNut(string size)
    {
        try
        {
            var resolved = HexNutDimensions.Resolve(HexNutStyle.NylonInsertLocking, RhinoMMPlugIn.Catalog.Get(size));
            return new LockingNutSizeSpec(size, resolved.AcrossFlats, resolved.TotalHeight, resolved.Standard, resolved.IsEngineeringExtension);
        }
        catch { return null; }
    }

    private static string UniqueName(string proposed)
    {
        var name = proposed;
        var index = 2;
        while (_current.Definitions.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
            name = $"{proposed} {index++}";
        return name;
    }

    private static string UniqueImportedName(string proposed, IReadOnlyCollection<UserFastenerDefinition> values)
    {
        var name = proposed + "（导入）";
        var index = 2;
        while (values.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
            name = $"{proposed}（导入 {index++}）";
        return name;
    }

    private static string Csv(string value) => '"' + (value ?? string.Empty).Replace("\"", "\"\"") + '"';
    private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var builder = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    builder.Append('"');
                    index++;
                }
                else quoted = !quoted;
            }
            else if (character == ',' && !quoted)
            {
                result.Add(builder.ToString());
                builder.Clear();
            }
            else builder.Append(character);
        }
        result.Add(builder.ToString());
        return result;
    }
}
