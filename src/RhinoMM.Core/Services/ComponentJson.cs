using System.Text.Json;
using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class ComponentJson
{
    private static readonly Lazy<FastenerCatalog> Catalog = new(FastenerCatalog.LoadEmbedded);

    public static string Serialize(FastenerComponentData data) =>
        JsonSerializer.Serialize(data, JsonOptions.Default);

    public static FastenerComponentData Deserialize(string json)
    {
        var data = JsonSerializer.Deserialize<FastenerComponentData>(json, JsonOptions.Default)
            ?? throw new InvalidOperationException("组件元数据为空或格式错误。");
        return Migrate(data);
    }

    public static FastenerComponentData Migrate(FastenerComponentData data)
    {
        if (data.SchemaVersion >= FastenerComponentData.CurrentSchemaVersion)
            return data;

        var sourceVersion = data.SchemaVersion;
        return data with
        {
            SchemaVersion = FastenerComponentData.CurrentSchemaVersion,
            FastenerOpacityPercent = sourceVersion < 2 ? 70 : data.FastenerOpacityPercent,
            CutterOpacityPercent = sourceVersion < 2 ? 35 : data.CutterOpacityPercent,
            HeadEmbedDepth = MigrateHeadEmbedDepth(data, sourceVersion),
            ControlPointObjectId = sourceVersion < 4 ? Guid.Empty : data.ControlPointObjectId,
            Bindings = data.Bindings.Select(binding => binding with
            {
                IsPreviewVisible = sourceVersion < 2 || binding.IsPreviewVisible,
                IsBooleanEnabled = sourceVersion < 3 || binding.IsBooleanEnabled
            }).ToArray()
        };
    }

    private static double MigrateHeadEmbedDepth(FastenerComponentData data, int sourceVersion)
    {
        if (sourceVersion < 4)
            return LegacyHeadEmbedDepth(data);
        if (sourceVersion == 4 && data.Kind == FastenerKind.Countersunk)
        {
            try
            {
                var spec = Catalog.Value.Get(data.Size);
                var legacyHeight = HeadGeometryCalculator.GetLegacyCountersunkConeHeight(spec);
                if (Math.Abs(data.HeadEmbedDepth - legacyHeight) <= 0.01)
                    return HeadGeometryCalculator.GetHeadHeight(data.Kind, spec);
            }
            catch
            {
                return data.HeadEmbedDepth;
            }
        }
        return data.HeadEmbedDepth;
    }

    private static double LegacyHeadEmbedDepth(FastenerComponentData data)
    {
        if (data.Bindings.All(binding => !binding.IncludeHeadSeat))
            return 0;
        try
        {
            return HeadGeometryCalculator.GetHeadHeight(data.Kind, Catalog.Value.Get(data.Size));
        }
        catch
        {
            return 0;
        }
    }
}
