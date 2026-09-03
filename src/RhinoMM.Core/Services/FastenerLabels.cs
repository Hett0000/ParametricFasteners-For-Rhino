using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class FastenerLabels
{
    public static string ShortKind(FastenerComponentData component) =>
        component.Kind == FastenerKind.HexNut
            && component.HexNutStyle == HexNutStyle.NylonInsertLocking
                ? FastenerText.Get("Fastener.LockNut.Short")
                : ShortKind(component.Kind);

    public static string ShortKind(FastenerKind kind) => kind switch
    {
        FastenerKind.SocketCap => FastenerText.Get("Fastener.SocketCap.Short"),
        FastenerKind.Countersunk => FastenerText.Get("Fastener.Countersunk.Short"),
        FastenerKind.HexBolt => FastenerText.Get("Fastener.HexBolt.Short"),
        FastenerKind.HexNut => FastenerText.Get("Fastener.HexNut.Short"),
        FastenerKind.HeatSetInsert => FastenerText.Get("Fastener.HeatSet.Short"),
        _ => Kind(kind)
    };

    public static string Kind(FastenerComponentData component) =>
        component.Kind == FastenerKind.HexNut
            ? NutStyle(component.HexNutStyle)
            : Kind(component.Kind);

    public static string Kind(FastenerKind kind) => kind switch
    {
        FastenerKind.SocketCap => FastenerText.Get("Fastener.SocketCap"),
        FastenerKind.Countersunk => FastenerText.Get("Fastener.Countersunk"),
        FastenerKind.HexBolt => FastenerText.Get("Fastener.HexBolt"),
        FastenerKind.HexNut => FastenerText.Get("Fastener.HexNut"),
        FastenerKind.HeatSetInsert => FastenerText.Get("Fastener.HeatSet"),
        _ => kind.ToString()
    };

    public static string NutStyle(HexNutStyle style) => style switch
    {
        HexNutStyle.Standard => FastenerText.Get("Fastener.StandardNut"),
        HexNutStyle.NylonInsertLocking => FastenerText.Get("Fastener.LockNut"),
        _ => style.ToString()
    };

    public static string NutStandard(FastenerComponentData component) =>
        component.Kind != FastenerKind.HexNut
            ? string.Empty
            : NutStandard(component.HexNutStyle, component.Size);

    public static string NutStandard(HexNutStyle style, string size) =>
        FastenerText.Translate(style == HexNutStyle.NylonInsertLocking
            ? size is "M2" or "M2.5"
                ? "DIN 985 工程扩展"
                : "GB/T 889.1-2015"
            : "普通六角螺母预设");

    public static string AssemblyMode(ScrewAssemblyMode mode) => mode switch
    {
        ScrewAssemblyMode.ThreadEngagement => FastenerText.Get("Assembly.ThreadEngagement"),
        ScrewAssemblyMode.EngagementOnly => FastenerText.Get("Assembly.EngagementOnly"),
        ScrewAssemblyMode.NutFastened => FastenerText.Get("Assembly.NutFastened"),
        _ => mode.ToString()
    };

    public static string HeadOffset(double value) => value switch
    {
        < 0 => FastenerText.Translate($"离面 {Math.Abs(value):0.##} mm"),
        > 0 => FastenerText.Translate($"嵌入 {value:0.##} mm"),
        _ => FastenerText.Translate("头底贴面")
    };

    public static string ClearanceFit(ClearanceFitClass fit) => fit switch
    {
        ClearanceFitClass.Close => FastenerText.Translate("紧配"),
        ClearanceFitClass.Normal => FastenerText.Translate("标准"),
        ClearanceFitClass.Loose => FastenerText.Translate("松配"),
        _ => fit.ToString()
    };

    public static string Role(ShaftFitRole role) => role switch
    {
        ShaftFitRole.Clearance => FastenerText.Get("Role.Clearance"),
        ShaftFitRole.ThreadEngagement => FastenerText.Get("Role.Engagement"),
        ShaftFitRole.InstallationPocket => FastenerText.Get("Role.Installation"),
        ShaftFitRole.NutPocket => FastenerText.Get("Role.NutPocket"),
        _ => role.ToString()
    };

    public static string Depth(DepthMode mode) => mode switch
    {
        DepthMode.ThroughTarget => FastenerText.Get("Depth.Through"),
        DepthMode.FastenerLengthPlusOneDiameter => FastenerText.Get("Depth.OneDiameter"),
        DepthMode.FastenerLengthPlusCustom => FastenerText.Get("Depth.Custom"),
        DepthMode.FastenerLengthPlusTwoDiameters => FastenerText.Get("Depth.TwoDiameters"),
        DepthMode.Blind => FastenerText.Get("Depth.Blind"),
        _ => mode.ToString()
    };
}
