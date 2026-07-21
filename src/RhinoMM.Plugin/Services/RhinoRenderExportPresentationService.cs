using Rhino;
using Rhino.DocObjects;

namespace RhinoMM.Plugin.Services;

public sealed record RhinoRenderExportResources(
    int LayerIndex,
    int SteelMaterialIndex,
    int BrassMaterialIndex,
    bool LayerCreated,
    bool SteelMaterialCreated,
    bool BrassMaterialCreated);

public static class RhinoRenderExportPresentationService
{
    public const string LayerPath = "参数化紧固件::渲染紧固件";
    public const string SteelMaterialName = "参数化紧固件::拉丝钢";
    public const string BrassMaterialName = "参数化紧固件::黄铜";

    private static readonly System.Drawing.Color LayerColor =
        System.Drawing.Color.FromArgb(132, 139, 146);
    private static readonly System.Drawing.Color SteelColor =
        System.Drawing.Color.FromArgb(169, 173, 178);
    private static readonly System.Drawing.Color BrassColor =
        System.Drawing.Color.FromArgb(184, 138, 50);

    public static RhinoRenderExportResources EnsureResources(RhinoDoc doc)
    {
        var existingLayer = doc.Layers.FindByFullPath(LayerPath, RhinoMath.UnsetIntIndex);
        var layerCreated = existingLayer == RhinoMath.UnsetIntIndex;
        var layerIndex = layerCreated
            ? doc.Layers.AddPath(LayerPath, LayerColor)
            : existingLayer;
        if (layerIndex < 0)
            throw new InvalidOperationException($"无法创建图层：{LayerPath}");
        var steelIndex = -1;
        var brassIndex = -1;
        var steelCreated = false;
        var brassCreated = false;
        try
        {
            steelIndex = EnsurePbrMaterial(
                doc,
                SteelMaterialName,
                SteelColor,
                roughness: 0.32,
                anisotropic: 0.18,
                out steelCreated);
            brassIndex = EnsurePbrMaterial(
                doc,
                BrassMaterialName,
                BrassColor,
                roughness: 0.28,
                anisotropic: 0.08,
                out brassCreated);
            return new RhinoRenderExportResources(
                layerIndex,
                steelIndex,
                brassIndex,
                layerCreated,
                steelCreated,
                brassCreated);
        }
        catch
        {
            if (steelCreated && steelIndex >= 0)
                doc.Materials.DeleteAt(steelIndex);
            if (brassCreated && brassIndex >= 0)
                doc.Materials.DeleteAt(brassIndex);
            if (layerCreated)
                doc.Layers.Delete(layerIndex, true);
            throw;
        }
    }

    public static ObjectAttributes CreateFastenerAttributes(
        RhinoRenderExportResources resources,
        RhinoExportFastenerBody body)
    {
        var materialIndex = body.MaterialKind == RhinoExportMaterialKind.Brass
            ? resources.BrassMaterialIndex
            : resources.SteelMaterialIndex;
        return new ObjectAttributes
        {
            Name = body.Name,
            LayerIndex = resources.LayerIndex,
            MaterialSource = ObjectMaterialSource.MaterialFromObject,
            MaterialIndex = materialIndex,
            Visible = true,
            Mode = ObjectMode.Normal
        };
    }

    public static void RollbackCreatedResources(
        RhinoDoc doc,
        RhinoRenderExportResources? resources)
    {
        if (resources is null)
            return;
        if (resources.SteelMaterialCreated)
            doc.Materials.DeleteAt(resources.SteelMaterialIndex);
        if (resources.BrassMaterialCreated)
            doc.Materials.DeleteAt(resources.BrassMaterialIndex);
        if (resources.LayerCreated)
            doc.Layers.Delete(resources.LayerIndex, true);
    }

    private static int EnsurePbrMaterial(
        RhinoDoc doc,
        string name,
        System.Drawing.Color color,
        double roughness,
        double anisotropic,
        out bool created)
    {
        var existing = doc.Materials.Find(name, true);
        if (existing >= 0)
        {
            created = false;
            return existing;
        }

        var material = new Material { Name = name };
        material.ToPhysicallyBased();
        var pbr = material.PhysicallyBased;
        pbr.BaseColor = new Rhino.Display.Color4f(color);
        pbr.Metallic = 1.0;
        pbr.Roughness = roughness;
        pbr.Anisotropic = anisotropic;
        pbr.Opacity = 1.0;
        pbr.SynchronizeLegacyMaterial();
        var index = doc.Materials.Add(material);
        if (index < 0)
            throw new InvalidOperationException($"无法创建材质：{name}");
        _ = doc.Materials[index].RenderMaterial;
        created = true;
        return index;
    }
}
