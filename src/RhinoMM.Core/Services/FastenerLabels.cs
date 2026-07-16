using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class FastenerLabels
{
    public static string Kind(FastenerKind kind) => kind switch
    {
        FastenerKind.SocketCap => "内六角圆柱头螺钉",
        FastenerKind.Countersunk => "内六角沉头螺钉",
        FastenerKind.HexBolt => "六角头螺栓",
        FastenerKind.HexNut => "六角螺母",
        _ => kind.ToString()
    };

    public static string ClearanceFit(ClearanceFitClass fit) => fit switch
    {
        ClearanceFitClass.Close => "紧配",
        ClearanceFitClass.Normal => "标准",
        ClearanceFitClass.Loose => "松配",
        _ => fit.ToString()
    };

    public static string Role(ShaftFitRole role) => role switch
    {
        ShaftFitRole.Clearance => "穿过通孔",
        ShaftFitRole.ThreadEngagement => "螺纹咬合孔",
        _ => role.ToString()
    };

    public static string Depth(DepthMode mode) => mode switch
    {
        DepthMode.ThroughTarget => "贯穿宿主",
        DepthMode.FastenerLengthPlusTwoDiameters => "螺杆长度 + 2D",
        DepthMode.Blind => "自定义深度",
        _ => mode.ToString()
    };
}
