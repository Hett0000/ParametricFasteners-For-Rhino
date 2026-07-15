using Rhino;
using Rhino.DocObjects;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;

namespace RhinoMM.Plugin.Persistence;

public static class ComponentRepository
{
    public const string ComponentKey = "RhinoMM.Component";
    public const string ComponentIdKey = "RhinoMM.ComponentId";
    public const string RoleKey = "RhinoMM.Role";
    public const string TargetIdKey = "RhinoMM.TargetId";
    public const string BindingIdKey = "RhinoMM.BindingId";

    public static ObjectAttributes CreateAttributes(
        FastenerComponentData data,
        string role,
        Guid targetId = default,
        Guid bindingId = default)
    {
        var attributes = new ObjectAttributes { Name = ObjectName(data, role) };
        Write(attributes, data, role, targetId, bindingId);
        return attributes;
    }

    public static string ObjectName(FastenerComponentData data, string role)
    {
        var roleName = role switch
        {
            "Proxy" => "紧固件",
            "Cutter" => "轴孔切割模块",
            "HeadCutter" => "头部切割模块",
            _ => role
        };
        return $"参数化紧固件 {data.Size} {roleName}";
    }

    public static void Write(
        ObjectAttributes attributes,
        FastenerComponentData data,
        string role,
        Guid targetId = default,
        Guid bindingId = default)
    {
        attributes.SetUserString(ComponentKey, ComponentJson.Serialize(data));
        attributes.SetUserString(ComponentIdKey, data.ComponentId.ToString("D"));
        attributes.SetUserString(RoleKey, role);
        if (targetId != Guid.Empty)
            attributes.SetUserString(TargetIdKey, targetId.ToString("D"));
        if (bindingId != Guid.Empty)
            attributes.SetUserString(BindingIdKey, bindingId.ToString("D"));
    }

    public static bool TryRead(RhinoObject? obj, out FastenerComponentData data)
    {
        data = new FastenerComponentData();
        if (obj is null)
            return false;
        var json = obj.Attributes.GetUserString(ComponentKey);
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            data = ComponentJson.Deserialize(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryReadSelection(RhinoDoc doc, out FastenerComponentData data)
    {
        foreach (var obj in doc.Objects.GetSelectedObjects(false, false))
        {
            if (TryRead(obj, out data))
                return true;
        }
        data = new FastenerComponentData();
        return false;
    }

    public static RhinoObject? FindProxy(RhinoDoc doc, Guid componentId) =>
        doc.Objects.FirstOrDefault(obj =>
            string.Equals(obj.Attributes.GetUserString(ComponentIdKey), componentId.ToString("D"), StringComparison.OrdinalIgnoreCase)
            && obj.Attributes.GetUserString(RoleKey) == "Proxy");

    public static IEnumerable<RhinoObject> FindComponentObjects(RhinoDoc doc, Guid componentId) =>
        doc.Objects.Where(obj => string.Equals(
            obj.Attributes.GetUserString(ComponentIdKey), componentId.ToString("D"), StringComparison.OrdinalIgnoreCase));
}
