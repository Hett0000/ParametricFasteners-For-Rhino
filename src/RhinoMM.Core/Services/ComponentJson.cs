using System.Text.Json;
using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class ComponentJson
{
    public static string Serialize(FastenerComponentData data) =>
        JsonSerializer.Serialize(data, JsonOptions.Default);

    public static FastenerComponentData Deserialize(string json) =>
        JsonSerializer.Deserialize<FastenerComponentData>(json, JsonOptions.Default)
        ?? throw new InvalidOperationException("组件元数据为空或格式错误。");
}
