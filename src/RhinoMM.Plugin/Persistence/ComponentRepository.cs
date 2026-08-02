using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
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
    public const string ProxyPartKey = "RhinoMM.ProxyPart";

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
            "ControlPoint" => "控制点",
            _ => role
        };
        var typeName = data.Kind == FastenerKind.HexNut
            ? FastenerLabels.NutStyle(data.HexNutStyle)
            : FastenerLabels.Kind(data.Kind);
        return $"参数化紧固件 {data.Size} {typeName} {roleName}";
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
        else
            attributes.DeleteUserString(TargetIdKey);
        if (bindingId != Guid.Empty)
            attributes.SetUserString(BindingIdKey, bindingId.ToString("D"));
        else
            attributes.DeleteUserString(BindingIdKey);
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
        var selected = ReadSelectedControlPoints(doc);
        if (selected.Count > 0)
        {
            data = selected[0];
            return true;
        }
        data = new FastenerComponentData();
        return false;
    }

    public static IReadOnlyList<FastenerComponentData> ReadSelectedControlPoints(RhinoDoc doc)
    {
        var result = new List<FastenerComponentData>();
        var componentIds = new HashSet<Guid>();
        foreach (var obj in doc.Objects.GetSelectedObjects(false, false))
        {
            if (obj.Attributes.GetUserString(RoleKey) != "ControlPoint"
                || obj.Geometry is not Point
                || !TryRead(obj, out var data)
                || !componentIds.Add(data.ComponentId))
                continue;
            result.Add(data);
        }
        return result;
    }

    public static IReadOnlyList<FastenerComponentData> ReadAllControlPoints(
        RhinoDoc doc,
        out int ignoredComponentCount)
    {
        var result = new List<FastenerComponentData>();
        var componentIds = new HashSet<Guid>();
        var invalidComponentIds = new HashSet<Guid>();
        foreach (var obj in doc.Objects)
        {
            var componentIdText = obj.Attributes.GetUserString(ComponentIdKey);
            if (!Guid.TryParse(componentIdText, out var componentId))
                continue;
            if (obj.Attributes.GetUserString(RoleKey) != "ControlPoint")
            {
                invalidComponentIds.Add(componentId);
                continue;
            }
            if (obj.Geometry is Point && TryRead(obj, out var data) && componentIds.Add(data.ComponentId))
            {
                result.Add(data);
                invalidComponentIds.Remove(data.ComponentId);
            }
            else
            {
                invalidComponentIds.Add(componentId);
            }
        }
        invalidComponentIds.ExceptWith(componentIds);
        ignoredComponentCount = invalidComponentIds.Count;
        return result;
    }

    public static bool TryReadComponent(RhinoDoc doc, Guid componentId, out FastenerComponentData data)
    {
        foreach (var obj in FindComponentObjects(doc, componentId))
        {
            if (TryRead(obj, out data))
                return true;
        }
        data = new FastenerComponentData();
        return false;
    }

    public static RhinoObject? FindProxy(RhinoDoc doc, Guid componentId)
    {
        var proxies = doc.Objects.Where(obj =>
            string.Equals(obj.Attributes.GetUserString(ComponentIdKey), componentId.ToString("D"), StringComparison.OrdinalIgnoreCase)
            && obj.Attributes.GetUserString(RoleKey) == "Proxy").ToArray();
        return proxies.FirstOrDefault(obj => obj.Attributes.GetUserString(ProxyPartKey) == "Shaft")
            ?? proxies.FirstOrDefault(obj =>
                TryRead(obj, out var data) && data.ProxyObjectId == obj.Id)
            ?? proxies.FirstOrDefault();
    }

    public static RhinoObject? FindControlPoint(RhinoDoc doc, Guid componentId) =>
        doc.Objects.FirstOrDefault(obj =>
            string.Equals(obj.Attributes.GetUserString(ComponentIdKey), componentId.ToString("D"), StringComparison.OrdinalIgnoreCase)
            && obj.Attributes.GetUserString(RoleKey) == "ControlPoint");

    public static IEnumerable<RhinoObject> FindComponentObjects(RhinoDoc doc, Guid componentId) =>
        doc.Objects.Where(obj => string.Equals(
            obj.Attributes.GetUserString(ComponentIdKey), componentId.ToString("D"), StringComparison.OrdinalIgnoreCase));
}
