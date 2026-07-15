using System.Text.Json;
using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class ComponentJson
{
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

        return data with
        {
            SchemaVersion = FastenerComponentData.CurrentSchemaVersion,
            FastenerOpacityPercent = 70,
            CutterOpacityPercent = 35,
            Bindings = data.Bindings.Select(binding => binding with { IsPreviewVisible = true }).ToArray()
        };
    }
}
