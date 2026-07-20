using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class FastenerKindTraits
{
    public static bool IsScrew(FastenerKind kind) => kind is
        FastenerKind.SocketCap or FastenerKind.Countersunk or FastenerKind.HexBolt;

    public static bool IsNut(FastenerKind kind) => kind is
        FastenerKind.HexNut or FastenerKind.HeatSetInsert;

    public static bool UsesSingleHostPlacement(FastenerKind kind) => IsNut(kind);

    public static bool SupportsHeadEmbed(FastenerKind kind) => IsScrew(kind);

    public static bool HasShaftProxy(FastenerKind kind) => IsScrew(kind);

    public static bool UsesLengthInStatistics(FastenerKind kind) => kind != FastenerKind.HexNut;
}
