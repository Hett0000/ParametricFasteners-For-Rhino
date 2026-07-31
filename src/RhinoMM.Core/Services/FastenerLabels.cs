using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class FastenerLabels
{
    public static string Kind(FastenerKind kind) => kind switch
    {
        FastenerKind.SocketCap => "内六角杯头螺丝",
        FastenerKind.Countersunk => "内六角沉头螺钉",
        FastenerKind.HexBolt => "六角头螺栓",
        FastenerKind.HexNut => "六角螺母",
        FastenerKind.HeatSetInsert => "热熔螺母",
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
        ShaftFitRole.InstallationPocket => "安装槽/孔",
        _ => role.ToString()
    };

    public static string Depth(DepthMode mode) => mode switch
    {
        DepthMode.ThroughTarget => "完全贯穿",
        DepthMode.FastenerLengthPlusOneDiameter => "螺杆长度 + 1D",
        DepthMode.FastenerLengthPlusCustom => "螺杆长度 + 自定数值",
        DepthMode.FastenerLengthPlusTwoDiameters => "螺杆长度 + 2D",
        DepthMode.Blind => "自定义深度",
        _ => mode.ToString()
    };
}
