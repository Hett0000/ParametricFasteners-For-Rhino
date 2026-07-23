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
    public const string SharedFastenerMaterialName = "参数化紧固件::显示::紧固件";
    public const string SharedCutterMaterialName = "参数化紧固件::显示::切割模块";

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
            CleanupUnusedLegacyMaterials(doc);
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

    public static void PromoteLegacyMaterialsForCopy(
        RhinoDoc doc,
        FastenerComponentData source)
    {
        EnsureMaterial(doc, source, false, source.ComponentId);
        EnsureMaterial(doc, source, true, source.ComponentId);
    }

    public static bool HasUnusedLegacyMaterials(RhinoDoc doc) =>
        FindUnusedLegacyMaterialIndices(doc).Count > 0;

    public static bool HasLegacyMaterialAssignments(RhinoDoc doc) =>
        doc.Objects.Any(obj => IsLegacyMaterialIndex(doc, obj.Attributes.MaterialIndex));

    public static int MigrateLegacyMaterialAssignments(RhinoDoc doc)
    {
        var display = GlobalDisplaySettingsService.Current;
        var template = new FastenerComponentData
        {
            FastenerOpacityPercent = display.FastenerOpacityPercent,
            CutterOpacityPercent = display.CutterOpacityPercent
        };
        var fastenerIndex = EnsureMaterial(doc, template, false);
        var cutterIndex = EnsureMaterial(doc, template, true);
        var changed = 0;
        foreach (var obj in doc.Objects.Where(obj =>
                     !string.IsNullOrWhiteSpace(obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey))))
        {
            var role = obj.Attributes.GetUserString(ComponentRepository.RoleKey) ?? "Proxy";
            if (role == "ControlPoint")
                continue;
            var desiredIndex = role is "Cutter" or "HeadCutter" ? cutterIndex : fastenerIndex;
            if (obj.Attributes.MaterialSource == ObjectMaterialSource.MaterialFromObject
                && obj.Attributes.MaterialIndex == desiredIndex)
                continue;
            var attributes = obj.Attributes.Duplicate();
            attributes.MaterialSource = ObjectMaterialSource.MaterialFromObject;
            attributes.MaterialIndex = desiredIndex;
            if (!doc.Objects.ModifyAttributes(obj, attributes, true))
                throw new InvalidOperationException("无法迁移参数化紧固件的共享显示材质。");
            changed++;
        }
        return changed;
    }

    public static int CleanupUnusedLegacyMaterials(RhinoDoc doc)
    {
        var indices = FindUnusedLegacyMaterialIndices(doc);
        var deleted = 0;
        foreach (var index in indices.OrderByDescending(value => value))
        {
            if (doc.Materials.DeleteAt(index))
                deleted++;
        }
        return deleted;
    }

    private static int EnsureMaterial(
        RhinoDoc doc,
        FastenerComponentData data,
        bool isCutter,
        Guid preferredLegacyComponentId = default)
    {
        var name = isCutter ? SharedCutterMaterialName : SharedFastenerMaterialName;
        var opacity = Math.Clamp(isCutter ? data.CutterOpacityPercent : data.FastenerOpacityPercent, 0, 100);
        var color = isCutter ? CutterColor : FastenerColor;
        var transparency = 1.0 - opacity / 100.0;
        var index = doc.Materials.Find(name, true);
        if (index < 0 && preferredLegacyComponentId != Guid.Empty)
            index = FindLegacyMaterialIndex(doc, preferredLegacyComponentId, isCutter);

        if (index >= 0)
        {
            var existing = doc.Materials[index];
            if (string.Equals(existing.Name, name, StringComparison.Ordinal)
                && existing.DiffuseColor.ToArgb() == color.ToArgb()
                && Math.Abs(existing.Transparency - transparency) <= 0.000001)
                return index;
        }

        var material = new Material
        {
            Name = name,
            DiffuseColor = color,
            Transparency = transparency
        };
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

    private static int FindLegacyMaterialIndex(RhinoDoc doc, Guid componentId, bool isCutter)
    {
        var expectedName = LegacyMaterialName(componentId, isCutter);
        var namedIndex = doc.Materials.Find(expectedName, true);
        if (namedIndex >= 0)
            return namedIndex;
        foreach (var obj in ComponentRepository.FindComponentObjects(doc, componentId))
        {
            var role = obj.Attributes.GetUserString(ComponentRepository.RoleKey) ?? "Proxy";
            var objectIsCutter = role is "Cutter" or "HeadCutter";
            if (objectIsCutter != isCutter
                || obj.Attributes.MaterialSource != ObjectMaterialSource.MaterialFromObject
                || obj.Attributes.MaterialIndex < 0
                || obj.Attributes.MaterialIndex >= doc.Materials.Count)
                continue;
            var material = doc.Materials[obj.Attributes.MaterialIndex];
            if (IsLegacyComponentMaterial(material.Name))
                return obj.Attributes.MaterialIndex;
        }
        return RhinoMath.UnsetIntIndex;
    }

    private static IReadOnlyList<int> FindUnusedLegacyMaterialIndices(RhinoDoc doc)
    {
        var used = doc.Objects
            .Where(obj => obj.Attributes.MaterialSource == ObjectMaterialSource.MaterialFromObject)
            .Select(obj => obj.Attributes.MaterialIndex)
            .Where(index => index >= 0)
            .Concat(doc.Layers.Select(layer => layer.RenderMaterialIndex).Where(index => index >= 0))
            .ToHashSet();
        var result = new List<int>();
        for (var index = 0; index < doc.Materials.Count; index++)
        {
            var material = doc.Materials[index];
            if (!material.IsDeleted
                && !used.Contains(index)
                && IsLegacyComponentMaterial(material.Name))
                result.Add(index);
        }
        return result;
    }

    private static string LegacyMaterialName(Guid componentId, bool isCutter) =>
        $"参数化紧固件::{componentId:D}::{(isCutter ? "切割模块" : "紧固件")}";

    private static bool IsLegacyComponentMaterial(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        var parts = name.Split(new[] { "::" }, StringSplitOptions.None);
        return parts.Length == 3
            && string.Equals(parts[0], "参数化紧固件", StringComparison.Ordinal)
            && Guid.TryParse(parts[1], out _)
            && parts[2] is "紧固件" or "切割模块";
    }

    private static bool IsLegacyMaterialIndex(RhinoDoc doc, int index) =>
        index >= 0
        && index < doc.Materials.Count
        && IsLegacyComponentMaterial(doc.Materials[index].Name);
}
