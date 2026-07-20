using Rhino.Geometry;
using RhinoMM.Core.Domain;

namespace RhinoMM.Plugin.Services;

internal enum ComponentTransformKind
{
    Move,
    Copy,
    Paste,
    Import
}

internal sealed record ComponentClonePlan(
    ComponentTransformKind Kind,
    FastenerComponentData Source,
    Transform Transform,
    IReadOnlyList<Guid> RawObjectIds,
    IReadOnlyList<Guid> CandidateHostIds,
    IReadOnlyDictionary<Guid, Guid> DirectTargetMap);

internal sealed record ComponentCloneResult(
    IReadOnlyList<FastenerComponentData> Components,
    int RelinkedBindings,
    int UnresolvedComponents,
    IReadOnlyList<Guid> ComponentsNeedingRelink);
