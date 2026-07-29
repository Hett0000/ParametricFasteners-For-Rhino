using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Geometry;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Services;

public sealed record ComponentRefreshResult(
    int RebuiltComponents,
    int RelinkedBindings,
    int DeletedComponents,
    int RetainedComponents,
    int FailedComponents,
    int PendingRelinkComponents,
    IReadOnlyList<Guid> ComponentsNeedingRelink);

public static class ComponentRefreshService
{
    public static bool CleanMissingControlPoints(
        RhinoDoc doc,
        out ComponentRefreshResult result,
        out string message) => RefreshAndRepair(doc, out result, out message);

    public static bool RefreshAndRepair(
        RhinoDoc doc,
        out ComponentRefreshResult result,
        out string message)
    {
        var componentObjects = doc.Objects
            .Where(obj => Guid.TryParse(
                obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey),
                out _))
            .GroupBy(obj => Guid.Parse(
                obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey)!))
            .ToDictionary(group => group.Key, group => group.ToList());
        var selectedHosts = doc.Objects.GetSelectedObjects(false, false)
            .Where(IsOrdinaryHost)
            .ToArray();
        var selectedComponentIds = doc.Objects.GetSelectedObjects(false, false)
            .Select(obj => obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey))
            .Where(value => Guid.TryParse(value, out _))
            .Select(Guid.Parse)
            .ToHashSet();

        var deleteIds = new List<Guid>();
        var repairDrafts = new List<FastenerComponentData>();
        var unresolved = new List<Guid>();
        var relinkedBindings = 0;
        foreach (var pair in componentObjects)
        {
            var controlPoint = pair.Value.FirstOrDefault(IsActualControlPoint);
            if (controlPoint is null)
            {
                deleteIds.Add(pair.Key);
                continue;
            }
            if (!ComponentRepository.TryRead(controlPoint, out var component))
            {
                unresolved.Add(pair.Key);
                continue;
            }
            var storedSchemaVersion = ComponentJson.ReadStoredSchemaVersion(
                controlPoint.Attributes.GetUserString(ComponentRepository.ComponentKey));

            var actualPoint = ((Point)controlPoint.Geometry).Location;
            var savedPlane = FastenerGeometryFactory.ToPlane(component.Placement);
            var repairedPlane = new Plane(actualPoint, savedPlane.XAxis, savedPlane.YAxis);
            var repairedPlacement = FastenerGeometryFactory.FromPlane(repairedPlane);
            var objects = pair.Value;
            var proxy = ComponentRepository.FindProxy(doc, component.ComponentId);
            var bindings = new List<HoleTargetBinding>();
            var assignedTargetIds = component.Bindings
                .Select(binding => binding.TargetObjectId)
                .Where(id => IsValidHost(doc.Objects.FindId(id)))
                .ToHashSet();
            var componentNeedsRelink = false;
            var componentRelinkCount = 0;
            foreach (var binding in component.Bindings)
            {
                var cutter = FindBindingObject(objects, binding.BindingId, "Cutter");
                var targetId = binding.TargetObjectId;
                if (!IsValidHost(doc.Objects.FindId(targetId)))
                {
                    var match = FindUniqueReplacementHost(
                        doc,
                        selectedHosts,
                        repairedPlacement,
                        cutter,
                        assignedTargetIds);
                    if (match is null)
                    {
                        componentNeedsRelink = true;
                        break;
                    }
                    targetId = match.Id;
                    assignedTargetIds.Add(targetId);
                    componentRelinkCount++;
                }
                bindings.Add(binding with
                {
                    TargetObjectId = targetId,
                    CutterObjectId = cutter?.Id ?? Guid.Empty
                });
            }
            if (componentNeedsRelink)
            {
                unresolved.Add(component.ComponentId);
                continue;
            }

            var pointMoved = actualPoint.DistanceTo(savedPlane.Origin) > Math.Max(doc.ModelAbsoluteTolerance, 1e-6);
            var idsChanged = component.ControlPointObjectId != controlPoint.Id
                || proxy is null
                || component.ProxyObjectId != proxy.Id
                || bindings.Where((binding, index) =>
                        binding.TargetObjectId != component.Bindings[index].TargetObjectId
                        || binding.CutterObjectId != component.Bindings[index].CutterObjectId)
                    .Any();
            var expectedHeadCutterCount = ExpectedHeadCutterCount(
                component,
                doc.ModelAbsoluteTolerance);
            var missingHeadCutter = expectedHeadCutterCount > 0
                && component.Bindings.Any(binding =>
                    binding.IncludeHeadSeat
                    && (CountBindingObjects(objects, binding.BindingId, "HeadCutter")
                        != expectedHeadCutterCount
                        || !HeadCuttersCoverExpectedEnvelope(
                            objects,
                            binding.BindingId,
                            component,
                            doc.ModelAbsoluteTolerance)));
            var missingHeatSetLeadIn = component.Kind == FastenerKind.HeatSetInsert
                && component.Bindings.Any(binding =>
                    binding.Role == ShaftFitRole.InstallationPocket
                    && FindBindingObject(objects, binding.BindingId, "HeadCutter") is null);
            var legacyHexNutCutter = component.Kind == FastenerKind.HexNut
                && storedSchemaVersion < FastenerComponentData.CurrentSchemaVersion;
            if (!pointMoved
                && !idsChanged
                && !missingHeadCutter
                && !missingHeatSetLeadIn
                && !legacyHexNutCutter)
                continue;

            repairDrafts.Add(component with
            {
                Placement = repairedPlacement,
                ProxyObjectId = proxy?.Id ?? Guid.Empty,
                ControlPointObjectId = controlPoint.Id,
                Bindings = bindings,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            relinkedBindings += componentRelinkCount;
        }

        var retained = componentObjects.Count - deleteIds.Count;
        result = new ComponentRefreshResult(
            repairDrafts.Count,
            relinkedBindings,
            deleteIds.Count,
            retained,
            unresolved.Count,
            unresolved.Count,
            unresolved);
        var hasLegacyMaterialAssignments = ComponentPresentationService.HasLegacyMaterialAssignments(doc);
        var hasUnusedLegacyMaterials = ComponentPresentationService.HasUnusedLegacyMaterials(doc);
        if (repairDrafts.Count == 0
            && deleteIds.Count == 0
            && !hasLegacyMaterialAssignments
            && !hasUnusedLegacyMaterials)
        {
            message = BuildMessage(result);
            return true;
        }

        var undo = doc.BeginUndoRecord("参数化紧固件：刷新修复");
        var ownsUndoRecord = undo != 0;
        try
        {
            var migratedLegacyAssignments = 0;
            var cleanedLegacyMaterials = 0;
            IReadOnlyList<FastenerComponentData> repaired = [];
            if (repairDrafts.Count > 0
                && !FastenerComponentService.CreateOrReplaceMany(doc, repairDrafts, out repaired, out var repairMessage))
                throw new InvalidOperationException(repairMessage);

            foreach (var componentId in deleteIds)
            {
                using var suppression = ComponentLifecycleService.Suppress(doc, componentId);
                ComponentPresentationService.RemoveGroup(doc, componentId);
                foreach (var obj in componentObjects[componentId])
                {
                    if (doc.Objects.FindId(obj.Id) is not null && !doc.Objects.Delete(obj, true))
                        throw new InvalidOperationException($"无法删除缺少控制点的组件 {componentId:D}。");
                }
                ComponentEditorSession.ForgetComponent(doc, componentId);
            }
            migratedLegacyAssignments = ComponentPresentationService.MigrateLegacyMaterialAssignments(doc);
            cleanedLegacyMaterials = ComponentPresentationService.CleanupUnusedLegacyMaterials(doc);

            if (ownsUndoRecord)
            {
                doc.EndUndoRecord(undo);
                undo = 0;
            }
            foreach (var component in repaired)
                ComponentEditorSession.UpdateCache(doc, component);
            RestoreControlPointSelection(doc, selectedComponentIds);
            doc.Views.Redraw();
            message = BuildMessage(result);
            if (migratedLegacyAssignments > 0)
                message += $" 已将 {migratedLegacyAssignments} 个插件对象切换到共享显示材质。";
            if (cleanedLegacyMaterials > 0)
                message += $" 已清理 {cleanedLegacyMaterials} 个未引用的旧显示材质。";
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
            message = $"刷新修复失败，已回滚：{ex.Message}";
            return false;
        }
        finally
        {
            if (ownsUndoRecord && undo != 0)
                doc.EndUndoRecord(undo);
        }
    }

    private static RhinoObject? FindUniqueReplacementHost(
        RhinoDoc doc,
        IReadOnlyList<RhinoObject> candidates,
        PlacementFrame placement,
        RhinoObject? cutter,
        IReadOnlySet<Guid> oldTargetIds)
    {
        var scored = candidates
            .Where(candidate => !oldTargetIds.Contains(candidate.Id))
            .Select(candidate => new
            {
                Candidate = candidate,
                Score = HostScore(doc, candidate, placement, cutter)
            })
            .Where(item => item.Score >= 0)
            .OrderByDescending(item => item.Score)
            .ToArray();
        if (scored.Length == 0)
            return null;
        if (scored.Length == 1)
            return scored[0].Candidate;
        var epsilon = Math.Max(Math.Pow(doc.ModelAbsoluteTolerance, 3), 1e-9);
        return scored[0].Score - scored[1].Score > epsilon ? scored[0].Candidate : null;
    }

    private static double HostScore(
        RhinoDoc doc,
        RhinoObject candidate,
        PlacementFrame placement,
        RhinoObject? cutter)
    {
        if (!FastenerGeometryFactory.TryGetTargetInterval(
                candidate.Geometry,
                placement,
                doc.ModelAbsoluteTolerance,
                out var interval,
                out _))
            return -1;
        var axialLength = Math.Max(0, interval.Max - interval.Min);
        if (cutter is null)
            return 1.0 / (1.0 + Math.Abs((interval.Min + interval.Max) / 2.0));
        var plane = FastenerGeometryFactory.ToPlane(placement);
        var cutterCenter = cutter.Geometry.GetBoundingBox(true).Center;
        var cutterAxisPosition = Vector3d.Multiply(cutterCenter - plane.Origin, plane.ZAxis);
        var candidateAxisCenter = (interval.Min + interval.Max) / 2.0;
        var axialOrderScore = 1.0 / (1.0 + Math.Abs(candidateAxisCenter - cutterAxisPosition));
        var overlap = BoundingBoxOverlapVolume(
            candidate.Geometry.GetBoundingBox(true),
            cutter.Geometry.GetBoundingBox(true));
        return overlap * 1000.0 + axialOrderScore + axialLength * 1e-6;
    }

    private static double BoundingBoxOverlapVolume(BoundingBox left, BoundingBox right)
    {
        var x = Math.Max(0, Math.Min(left.Max.X, right.Max.X) - Math.Max(left.Min.X, right.Min.X));
        var y = Math.Max(0, Math.Min(left.Max.Y, right.Max.Y) - Math.Max(left.Min.Y, right.Min.Y));
        var z = Math.Max(0, Math.Min(left.Max.Z, right.Max.Z) - Math.Max(left.Min.Z, right.Min.Z));
        return x * y * z;
    }

    private static RhinoObject? FindBindingObject(
        IEnumerable<RhinoObject> objects,
        Guid bindingId,
        string role) => objects.FirstOrDefault(obj =>
            obj.Attributes.GetUserString(ComponentRepository.RoleKey) == role
            && Guid.TryParse(
                obj.Attributes.GetUserString(ComponentRepository.BindingIdKey),
                out var objectBindingId)
            && objectBindingId == bindingId);

    private static int CountBindingObjects(
        IEnumerable<RhinoObject> objects,
        Guid bindingId,
        string role) => objects.Count(obj =>
        obj.Attributes.GetUserString(ComponentRepository.RoleKey) == role
        && Guid.TryParse(
            obj.Attributes.GetUserString(ComponentRepository.BindingIdKey),
            out var objectBindingId)
        && objectBindingId == bindingId);

    private static int ExpectedHeadCutterCount(
        FastenerComponentData component,
        double documentTolerance)
    {
        if (component.Kind == FastenerKind.HeatSetInsert)
            return 1;
        if (component.HeadEmbedDepth <= 0
            || !FastenerKindTraits.SupportsHeadEmbed(component.Kind))
            return 0;
        if (component.Kind != FastenerKind.Countersunk)
            return 1;

        var spec = RhinoMMPlugIn.Catalog.Get(component.Size);
        var padding = Math.Max(0.2, documentTolerance * 10);
        var envelope = HeadGeometryCalculator.GetHeadSeatAxialEnvelope(
            component.HeadEmbedDepth,
            HeadGeometryCalculator.GetHeadHeight(component.Kind, spec),
            padding);
        return envelope.RequiresAccess ? 2 : 1;
    }

    private static bool HeadCuttersCoverExpectedEnvelope(
        IEnumerable<RhinoObject> objects,
        Guid bindingId,
        FastenerComponentData component,
        double documentTolerance)
    {
        if (component.HeadEmbedDepth <= 0
            || !FastenerKindTraits.SupportsHeadEmbed(component.Kind))
            return true;

        var spec = RhinoMMPlugIn.Catalog.Get(component.Size);
        var padding = Math.Max(0.2, documentTolerance * 10);
        var envelope = HeadGeometryCalculator.GetHeadSeatAxialEnvelope(
            component.HeadEmbedDepth,
            HeadGeometryCalculator.GetHeadHeight(component.Kind, spec),
            padding);
        var placementPlane = FastenerGeometryFactory.ToPlane(component.Placement);
        var bounds = objects
            .Where(obj =>
                obj.Attributes.GetUserString(ComponentRepository.RoleKey) == "HeadCutter"
                && Guid.TryParse(
                    obj.Attributes.GetUserString(ComponentRepository.BindingIdKey),
                    out var objectBindingId)
                && objectBindingId == bindingId)
            .Select(obj => obj.Geometry.GetBoundingBox(placementPlane))
            .Where(box => box.IsValid)
            .ToArray();
        if (bounds.Length == 0)
            return false;

        var actualStart = bounds.Min(box => box.Min.Z);
        var actualEnd = bounds.Max(box => box.Max.Z);
        return actualStart <= envelope.CombinedStart + documentTolerance
            && actualEnd >= envelope.End - documentTolerance;
    }

    private static bool IsOrdinaryHost(RhinoObject obj) =>
        IsValidHost(obj)
        && string.IsNullOrWhiteSpace(obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey));

    private static bool IsValidHost(RhinoObject? obj) =>
        obj is not null
        && obj.Geometry is Brep or Extrusion
        && string.IsNullOrWhiteSpace(obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey));

    private static bool IsActualControlPoint(RhinoObject obj) =>
        obj.Attributes.GetUserString(ComponentRepository.RoleKey) == "ControlPoint"
        && obj.Geometry is Point;

    private static void RestoreControlPointSelection(RhinoDoc doc, IReadOnlySet<Guid> componentIds)
    {
        doc.Objects.UnselectAll(false);
        foreach (var componentId in componentIds)
        {
            var controlPoint = ComponentRepository.FindControlPoint(doc, componentId);
            if (controlPoint is not null)
                doc.Objects.Select(controlPoint.Id, false);
        }
    }

    private static string BuildMessage(ComponentRefreshResult result)
    {
        var message = $"刷新完成：重建 {result.RebuiltComponents}，重新绑定 {result.RelinkedBindings}，删除 {result.DeletedComponents}，保留 {result.RetainedComponents}，失败 {result.FailedComponents}。";
        if (result.ComponentsNeedingRelink.Count > 0)
            message += $" 待重绑 {result.PendingRelinkComponents} 个；以下控制点需先选中对应宿主后再刷新："
                + string.Join(", ", result.ComponentsNeedingRelink.Select(id => id.ToString("N")[..8]));
        return message;
    }
}
