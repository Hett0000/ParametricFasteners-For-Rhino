using Rhino;
using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Geometry;

namespace RhinoMM.Plugin.Services;

/// <summary>
/// Runtime-only local proxy cache used by lightweight smart-placement previews.
/// Cutters and host-dependent geometry are deliberately never cached here.
/// Geometry is built at WorldXY once and drawn under a display transform while
/// the pointer moves. Callers must treat returned Breps as immutable and must
/// not dispose them; cache eviction owns their lifetime.
/// </summary>
internal static class FastenerLightweightProxyCache
{
    private const int Capacity = 48;

    private sealed record CacheValue(IReadOnlyList<Brep> LocalProxies);

    private static readonly Dictionary<string, LinkedListNode<KeyValuePair<string, CacheValue>>> Entries = [];
    private static readonly LinkedList<KeyValuePair<string, CacheValue>> Recency = [];
    private static readonly object Gate = new();

    public static IReadOnlyList<Brep> GetLocalOrCreate(
        RhinoDoc doc,
        FastenerComponentData draft,
        FastenerSizeSpec spec)
    {
        var key = Key(doc, draft);
        lock (Gate)
        {
            if (Entries.TryGetValue(key, out var node))
            {
                Recency.Remove(node);
                Recency.AddFirst(node);
                return node.Value.Value.LocalProxies;
            }
        }

        var localDraft = Normalize(draft);
        var local = FastenerGeometryFactory.CreateProxy(localDraft, spec);
        var value = new CacheValue(local);
        lock (Gate)
        {
            if (Entries.Remove(key, out var existing))
            {
                Recency.Remove(existing);
                Dispose(existing.Value.Value);
            }
            var node = new LinkedListNode<KeyValuePair<string, CacheValue>>(
                new KeyValuePair<string, CacheValue>(key, value));
            Recency.AddFirst(node);
            Entries[key] = node;
            while (Entries.Count > Capacity && Recency.Last is { } last)
            {
                Recency.RemoveLast();
                Entries.Remove(last.Value.Key);
                Dispose(last.Value.Value);
            }
        }
        return value.LocalProxies;
    }

    // Compatibility path for non-cursor callers. Smart placement hover must use
    // GetLocalOrCreate and draw with PushModelTransform to avoid per-frame copies.
    public static IReadOnlyList<Brep> GetOrCreate(
        RhinoDoc doc,
        FastenerComponentData draft,
        FastenerSizeSpec spec)
    {
        var transform = Transform.PlaneToPlane(
            Plane.WorldXY,
            FastenerGeometryFactory.ToPlane(draft.Placement));
        return GetLocalOrCreate(doc, draft, spec).Select(item =>
        {
            var duplicate = item.DuplicateBrep();
            duplicate.Transform(transform);
            return duplicate;
        }).ToArray();
    }

    private static string Key(RhinoDoc doc, FastenerComponentData source)
    {
        var normalized = Normalize(source);
        var spec = FastenerSpecResolver.Resolve(normalized, RhinoMMPlugIn.Catalog);
        return ComponentReliabilitySignatureService.ParameterSignature(doc, normalized, spec);
    }

    private static FastenerComponentData Normalize(FastenerComponentData source) =>
        source with
        {
            ComponentId = Guid.Empty,
            ProxyObjectId = Guid.Empty,
            ControlPointObjectId = Guid.Empty,
            AdoptedSourceObjectId = Guid.Empty,
            UpdatedAt = DateTimeOffset.UnixEpoch,
            Placement = FastenerGeometryFactory.FromPlane(Plane.WorldXY),
            Bindings = []
        };

    private static void Dispose(CacheValue value)
    {
        foreach (var brep in value.LocalProxies)
            brep.Dispose();
    }
}
