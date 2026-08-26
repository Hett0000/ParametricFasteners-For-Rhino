using System.Globalization;
using System.Text;
using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class FastenerStatisticsCsvWriter
{
    public static void Write(
        string path,
        FastenerStatisticsReport report,
        IReadOnlyDictionary<Guid, FastenerDeliveryHostInfo>? hostInfo = null)
    {
        var builder = new StringBuilder();
        builder.AppendLine("序号,类型,装配角色,规格,长度mm,外径mm,数量,装配编号,项目分组,用户备注,宿主名称,宿主图层,组件ID");
        var sequence = 1;
        foreach (var component in report.Components)
        {
            Append(builder, sequence++, component, false, hostInfo);
            if (FastenerKindTraits.IsScrew(component.Kind) && component.AssemblyMode == ScrewAssemblyMode.NutFastened)
                Append(builder, sequence++, component, true, hostInfo);
        }
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(true));
    }

    private static void Append(
        StringBuilder builder,
        int sequence,
        FastenerComponentData component,
        bool pairedNut,
        IReadOnlyDictionary<Guid, FastenerDeliveryHostInfo>? hostInfo)
    {
        var role = pairedNut ? "配套螺母" : FastenerKindTraits.IsScrew(component.Kind) ? "螺丝" : "独立螺母";
        var type = pairedNut ? FastenerLabels.NutStyle(component.PairedNutStyle) : FastenerLabels.Kind(component);
        var length = !pairedNut && FastenerKindTraits.UsesLengthInStatistics(component.Kind)
            ? component.Length.ToString("0.###", CultureInfo.InvariantCulture) : string.Empty;
        var outer = !pairedNut && component.Kind == FastenerKind.HeatSetInsert
            ? component.InsertOuterDiameter.ToString("0.###", CultureInfo.InvariantCulture) : string.Empty;
        var hosts = hostInfo is not null && hostInfo.TryGetValue(component.ComponentId, out var value)
            ? value : new FastenerDeliveryHostInfo();
        builder.AppendLine(string.Join(",", new[]
        {
            sequence.ToString(CultureInfo.InvariantCulture), Csv(type), Csv(role), Csv(component.Size), length, outer, "1",
            Csv(component.Delivery.AssemblyNumber), Csv(component.Delivery.ProjectGroup), Csv(component.Delivery.UserNote),
            Csv(hosts.HostNames), Csv(hosts.HostLayers), component.ComponentId.ToString("D")
        }));
    }

    private static string Csv(string value) => '"' + (value ?? string.Empty).Replace("\"", "\"\"") + '"';
}
