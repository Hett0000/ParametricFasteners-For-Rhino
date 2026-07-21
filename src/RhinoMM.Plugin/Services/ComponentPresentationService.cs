using Rhino;
using Rhino.DocObjects;
using RhinoMM.Core.Domain;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Services;

public static class ComponentPresentationService
{
    public const string FastenerLayerPath = "参数化紧固件::紧固件";
    public const string CutterLayerPath = "参数化紧固件::切割模块";
    public const string ControlPointLayerPath = "参数化紧固件::控制点";

    private static readonly System.Drawing.Color FastenerColor = System.Drawing.Color.FromArgb(70, 130, 180);
    private static readonly System.Drawing.Color CutterColor = System.Drawing.Color.FromArgb(255, 140, 0);
    private static readonly System.Drawing.Color ControlPointColor = System.Drawing.Color.FromArgb(0, 180, 220);

    public static string GroupName(Guid componentId) => $"参数化紧固件::{componentId:D}";

    public static void ConfigureAttributes(
        RhinoDoc doc,
        ObjectAttributes attributes,
        FastenerComponentData data,
        bool isCutter,
        bool visible)
    {
        attributes.LayerIndex = EnsureLayer(doc, isCutter ? CutterLayerPath : FastenerLayerPath, isCutter ? CutterColor : FastenerColor);
        attributes.MaterialSource = ObjectMaterialSource.MaterialFromObject;
        var materialIndex = EnsureMaterial(doc, data, isCutter);
        attributes.MaterialIndex = materialIndex;
        // MaterialIndex is Rhino's stable path for simple object materials. The
        // related RenderMaterial is created from the material-table entry below,
        // so it is attached to this document before Rhino renders the object.
        attributes.Visible = visible;
        attributes.Mode = ObjectMode.Normal;
    }

    public static void ConfigureControlPointAttributes(RhinoDoc doc, ObjectAttributes attributes)
    {
        attributes.LayerIndex = EnsureLayer(doc, ControlPointLayerPath, ControlPointColor);
        attributes.ColorSource = ObjectColorSource.ColorFromObject;
        attributes.ObjectColor = ControlPointColor;
        attributes.MaterialSource = ObjectMaterialSource.MaterialFromLayer;
        attributes.MaterialIndex = -1;
        attributes.Visible = true;
        attributes.Mode = ObjectMode.Normal;
    }

    public static bool ApplyDisplaySettings(RhinoDoc doc, FastenerComponentData data, out string message)
    {
        var success = ApplyDisplaySettings(
            doc,
            [data],
            out _,
            out message);
        return success;
    }

    public static bool ApplyDisplaySettings(
        RhinoDoc doc,
        IReadOnlyList<FastenerComponentData> components,
        out IReadOnlyList<FastenerComponentData> savedComponents,
        out string message)
    {
        savedComponents = [];
        var uniqueComponents = components
            .GroupBy(component => component.ComponentId)
            .Select(group => group.First())
            .ToArray();
        if (uniqueComponents.Length == 0)
        {
            message = "当前文档没有可更新的参数化紧固件。";
            return true;
        }
        var undo = doc.BeginUndoRecord("参数化紧固件：更新显示设置");
        var ownsUndoRecord = undo != 0;
        try
        {
            foreach (var data in uniqueComponents)
            {
                EnsureMaterial(doc, data, false);
                EnsureMaterial(doc, data, true);
                var bindings = data.Bindings.ToDictionary(binding => binding.BindingId);
                var componentObjects = ComponentRepository.FindComponentObjects(doc, data.ComponentId).ToList();
                foreach (var obj in componentObjects)
                {
                    var role = obj.Attributes.GetUserString(ComponentRepository.RoleKey) ?? "Proxy";
                    if (role == "ControlPoint")
                    {
                        var controlAttributes = obj.Attributes.Duplicate();
                        controlAttributes.Name = ComponentRepository.ObjectName(data, role);
                        ConfigureControlPointAttributes(doc, controlAttributes);
                        ComponentRepository.Write(controlAttributes, data, role);
                        if (!doc.Objects.ModifyAttributes(obj, controlAttributes, true))
                            throw new InvalidOperationException("无法更新紧固件控制点的显示元数据。");
                        continue;
                    }
                    var isCutter = role is "Cutter" or "HeadCutter";
                    var visible = true;
                    var targetId = Guid.TryParse(obj.Attributes.GetUserString(ComponentRepository.TargetIdKey), out var parsedTargetId)
                        ? parsedTargetId
                        : Guid.Empty;
                    var bindingId = Guid.TryParse(obj.Attributes.GetUserString(ComponentRepository.BindingIdKey), out var parsedBindingId)
                        ? parsedBindingId
                        : Guid.Empty;
                    if (isCutter && bindingId == Guid.Empty && targetId != Guid.Empty)
                        bindingId = data.Bindings.FirstOrDefault(item => item.TargetObjectId == targetId)?.BindingId ?? Guid.Empty;
                    if (isCutter && bindings.TryGetValue(bindingId, out var binding))
                        visible = binding.IsPreviewVisible;

                    var attributes = obj.Attributes.Duplicate();
                    attributes.Name = ComponentRepository.ObjectName(data, role);
                    ConfigureAttributes(doc, attributes, data, isCutter, visible);
                    ComponentRepository.Write(
                        attributes,
                        data,
                        role,
                        targetId,
                        bindingId);
                    if (!doc.Objects.ModifyAttributes(obj, attributes, true))
                        throw new InvalidOperationException("无法更新紧固件对象的显示属性。");
                }
            }
            doc.Views.Redraw();
            savedComponents = uniqueComponents;
            message = $"已同步当前文档 {uniqueComponents.Length} 个组件的全局透明度。";
            return true;
        }
        catch (Exception ex)
        {
            if (ownsUndoRecord && undo != 0)
            {
                doc.EndUndoRecord(undo);
                undo = 0;
                doc.Undo();
            }
            message = ownsUndoRecord
                ? $"更新显示设置失败，已回滚：{ex.Message}"
                : $"更新显示设置失败：{ex.Message}。当前操作由 Rhino 命令撤销记录管理。";
            return false;
        }
        finally
        {
            if (ownsUndoRecord && undo != 0)
                doc.EndUndoRecord(undo);
        }
    }

    public static void RecreateGroup(RhinoDoc doc, Guid componentId, IEnumerable<Guid> objectIds)
    {
        RemoveGroup(doc, componentId);
        var ids = objectIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0 || doc.Groups.Add(GroupName(componentId), ids) < 0)
            throw new InvalidOperationException("无法创建参数化紧固件组件组。");
    }

    public static void RemoveGroup(RhinoDoc doc, Guid componentId)
    {
        var index = doc.Groups.Find(GroupName(componentId));
        if (index != RhinoMath.UnsetIntIndex && index >= 0)
            doc.Groups.Delete(index);
    }

    private static int EnsureLayer(RhinoDoc doc, string path, System.Drawing.Color color)
    {
        var index = doc.Layers.FindByFullPath(path, RhinoMath.UnsetIntIndex);
        if (index != RhinoMath.UnsetIntIndex)
            return index;
        index = doc.Layers.AddPath(path, color);
        if (index < 0)
            throw new InvalidOperationException($"无法创建图层：{path}");
        return index;
    }

    private static int EnsureMaterial(RhinoDoc doc, FastenerComponentData data, bool isCutter)
    {
        var name = $"参数化紧固件::{data.ComponentId:D}::{(isCutter ? "切割模块" : "紧固件")}";
        var opacity = Math.Clamp(isCutter ? data.CutterOpacityPercent : data.FastenerOpacityPercent, 0, 100);
        var material = new Material
        {
            Name = name,
            DiffuseColor = isCutter ? CutterColor : FastenerColor,
            Transparency = 1.0 - opacity / 100.0
        };
        var index = doc.Materials.Find(name, true);
        if (index < 0)
            index = doc.Materials.Add(material);
        else if (!doc.Materials.Modify(material, index, true))
            throw new InvalidOperationException($"无法更新材质：{name}");
        if (index < 0)
            throw new InvalidOperationException($"无法创建材质：{name}");
        // Access the table-owned material, never the transient local Material
        // instance. This keeps shaded and rendered display in sync without
        // assigning an unattached RenderMaterial to ObjectAttributes.
        _ = doc.Materials[index].RenderMaterial;
        return index;
    }
}
