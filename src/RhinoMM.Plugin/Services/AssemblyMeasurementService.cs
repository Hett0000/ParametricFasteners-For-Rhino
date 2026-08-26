using Rhino;
using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Plugin.Geometry;

namespace RhinoMM.Plugin.Services;

internal sealed record SmartPlacementMeasurement(
    Plane Plane,
    Guid PlacementHostId,
    AssemblyMeasurementSnapshot Snapshot);

internal static class AssemblyMeasurementService
{
    public static AssemblyMeasurementSnapshot Measure(
        RhinoDoc doc,
        PlacementFrame placement,
        Guid placementHostId,
        IReadOnlyList<SmartPlacementHost> hosts)
    {
        var tolerance = Math.Max(doc.ModelAbsoluteTolerance, 1e-6);
        if (placementHostId == Guid.Empty)
            return Invalid("缺少放置面宿主。", placementHostId);
        if (hosts.Count == 0)
            return Invalid("当前文档没有可测量的封闭宿主。", placementHostId);

        var plane = FastenerGeometryFactory.ToPlane(placement);
        var reach = MaximumForwardReach(plane, hosts, tolerance);
        var intervals = SmartHostBindingService.FindIntervals(
                hosts,
                placement,
                reach,
                tolerance,
                preserveFullExit: true)
            .OrderBy(item => item.Entry)
            .ThenBy(item => item.Exit)
            .Select((item, index) => new AssemblyHostMeasurement(
                item.ObjectId,
                item.Entry,
                item.Exit,
                index))
            .ToArray();
        if (intervals.Length == 0)
            return Invalid("螺丝轴向没有检测到可用宿主。", placementHostId);
        if (intervals[0].HostId != placementHostId)
            return new AssemblyMeasurementSnapshot(
                placementHostId,
                intervals,
                false,
                "点击面所属实体不是轴向遇到的第一个宿主。");
        return new AssemblyMeasurementSnapshot(
            placementHostId,
            intervals,
            true,
            $"识别 {intervals.Length} 个轴向宿主。" );
    }

    public static AssemblyMeasurementSnapshot MeasureComponent(
        RhinoDoc doc,
        FastenerComponentData component)
    {
        var placementHostId = component.Bindings
            .FirstOrDefault(item => item.IncludeHeadSeat)?.TargetObjectId
            ?? component.Bindings.FirstOrDefault()?.TargetObjectId
            ?? Guid.Empty;
        var existing = component.Bindings
            .Select(item => item.TargetObjectId)
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        var hosts = SmartHostBindingService.CaptureHosts(doc, existing);
        try
        {
            return Measure(doc, component.Placement, placementHostId, hosts);
        }
        finally
        {
            foreach (var host in hosts)
            {
                if (host.Object.Geometry is not Brep)
                    host.Brep.Dispose();
            }
        }
    }

    private static double MaximumForwardReach(
        Plane plane,
        IReadOnlyList<SmartPlacementHost> hosts,
        double tolerance)
    {
        var maximum = tolerance;
        foreach (var host in hosts)
        {
            foreach (var corner in host.BoundingBox.GetCorners())
                maximum = Math.Max(maximum, Vector3d.Multiply(corner - plane.Origin, plane.ZAxis));
        }
        return Math.Max(maximum + Math.Max(tolerance * 10, 0.2), tolerance);
    }

    private static AssemblyMeasurementSnapshot Invalid(string message, Guid hostId) =>
        new(hostId, [], false, message);
}
