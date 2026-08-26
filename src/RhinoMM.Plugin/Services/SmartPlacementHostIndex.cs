using Rhino;
using Rhino.Geometry;

namespace RhinoMM.Plugin.Services;

/// <summary>
/// Command-scoped spatial index for smart placement. Breps (including converted
/// extrusions) are captured once, then every hover query starts from the RTree
/// instead of enumerating the document object table.
/// </summary>
internal sealed class SmartPlacementHostIndex : IDisposable
{
    private readonly RhinoDoc _doc;
    private RTree _tree = new();
    private readonly Dictionary<int, SmartPlacementHost> _hostsByIndex = [];
    private readonly Dictionary<Guid, SmartPlacementHost> _hostsById = [];
    private long _documentRevision;

    public SmartPlacementHostIndex(RhinoDoc doc)
    {
        _doc = doc;
        Rebuild();
    }

    public void EnsureCurrent()
    {
        if (_documentRevision != FastenerDocumentIndexService.CurrentRevision(_doc))
            Rebuild();
    }

    private void Rebuild()
    {
        DisposeHosts();
        _tree.Dispose();
        _tree = new RTree();
        var index = 0;
        foreach (var host in SmartHostBindingService.CaptureHosts(_doc))
        {
            _hostsByIndex[index] = host;
            _hostsById[host.ObjectId] = host;
            _tree.Insert(host.BoundingBox, index++);
        }
        _documentRevision = FastenerDocumentIndexService.CurrentRevision(_doc);
    }

    public int Count => _hostsByIndex.Count;

    public IReadOnlyList<SmartPlacementHost> All => _hostsByIndex.Values.ToArray();

    public SmartPlacementHost? Find(Guid objectId) =>
        _hostsById.TryGetValue(objectId, out var host) ? host : null;

    public IReadOnlyList<SmartPlacementHost> Query(BoundingBox box)
    {
        if (!box.IsValid || _hostsByIndex.Count == 0)
            return [];
        var result = new List<SmartPlacementHost>();
        var seen = new HashSet<Guid>();
        _tree.Search(box, (_, args) =>
        {
            if (_hostsByIndex.TryGetValue(args.Id, out var host)
                && seen.Add(host.ObjectId))
                result.Add(host);
        });
        return result;
    }

    public IReadOnlyList<SmartPlacementHost> Query(Line line, double padding)
    {
        if (!line.IsValid)
            return [];
        var amount = Math.Max(padding, RhinoMath.ZeroTolerance);
        // BoundingBox(Point3d min, Point3d max) does not reorder coordinate
        // components. A view ray commonly travels toward negative world axes,
        // so treating its near end as Min creates an invalid query box and the
        // RTree incorrectly reports no candidates.
        var min = new Point3d(
            Math.Min(line.From.X, line.To.X),
            Math.Min(line.From.Y, line.To.Y),
            Math.Min(line.From.Z, line.To.Z));
        var max = new Point3d(
            Math.Max(line.From.X, line.To.X),
            Math.Max(line.From.Y, line.To.Y),
            Math.Max(line.From.Z, line.To.Z));
        var box = new BoundingBox(min, max);
        box.Inflate(amount);
        return Query(box);
    }

    public void Dispose()
    {
        _tree.Dispose();
        DisposeHosts();
    }

    private void DisposeHosts()
    {
        foreach (var host in _hostsByIndex.Values)
        {
            if (host.Object.Geometry is not Brep)
                host.Brep.Dispose();
        }
        _hostsByIndex.Clear();
        _hostsById.Clear();
    }
}
