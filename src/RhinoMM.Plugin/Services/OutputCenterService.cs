using Rhino;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Geometry;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.UI;

namespace RhinoMM.Plugin.Services;

[Flags]
internal enum DeliveryOutputFormat
{
    None = 0,
    Rhino3dm = 1,
    Step = 2,
    Stl = 4,
    Excel = 8,
    Csv = 16
}

internal enum DeliveryScope
{
    CurrentSelection,
    Layers,
    AllReferencedHosts
}

internal sealed record DeliveryOutputRequest(
    string ParentDirectory,
    string ProjectName,
    DeliveryScope Scope,
    IReadOnlyList<int> LayerIndices,
    DeliveryOutputFormat Formats,
    bool IncludeFastenerSolids,
    bool AcceptWarnings);

internal sealed record DeliveryOutputResult(string Directory, IReadOnlyList<string> Files, IReadOnlyList<string> Warnings);

internal sealed record OutputCenterSettings(
    DeliveryScope Scope = DeliveryScope.CurrentSelection,
    DeliveryOutputFormat Formats = DeliveryOutputFormat.Step | DeliveryOutputFormat.Excel,
    bool IncludeFasteners = false,
    string LastDirectory = "");

internal static class OutputCenterSettingsService
{
    private const string Prefix = "OutputCenter.";
    public static OutputCenterSettings Current { get; private set; } = new();

    public static void Load(PersistentSettings settings)
    {
        var scope = Enum.TryParse<DeliveryScope>(settings.GetString(Prefix + "Scope", string.Empty), out var parsedScope)
            ? parsedScope : DeliveryScope.CurrentSelection;
        var formats = (DeliveryOutputFormat)settings.GetInteger(Prefix + "Formats", (int)(DeliveryOutputFormat.Step | DeliveryOutputFormat.Excel));
        Current = new OutputCenterSettings(scope, formats, settings.GetBool(Prefix + "IncludeFasteners", false), settings.GetString(Prefix + "Directory", string.Empty));
    }

    public static void Save(OutputCenterSettings value)
    {
        Current = value;
        var settings = RhinoMMPlugIn.Instance?.Settings;
        if (settings is null) return;
        settings.SetString(Prefix + "Scope", value.Scope.ToString());
        settings.SetInteger(Prefix + "Formats", (int)value.Formats);
        settings.SetBool(Prefix + "IncludeFasteners", value.IncludeFasteners);
        settings.SetString(Prefix + "Directory", value.LastDirectory);
    }
}

internal static class OutputCenterService
{
    public static bool TryResolveHosts(
        RhinoDoc doc,
        DeliveryOutputRequest request,
        out IReadOnlyList<RhinoObject> hosts,
        out string message)
    {
        var allComponents = ComponentRepository.ReadAllControlPoints(doc, out _);
        var referenced = allComponents.SelectMany(component => component.Bindings)
            .Select(binding => binding.TargetObjectId).Where(id => id != Guid.Empty).ToHashSet();
        IEnumerable<RhinoObject> query = request.Scope switch
        {
            DeliveryScope.CurrentSelection => ResolveSelection(doc, referenced),
            DeliveryScope.Layers => doc.Objects.Where(obj => request.LayerIndices.Contains(obj.Attributes.LayerIndex)
                && ComponentHostResolver.IsOrdinaryHost(obj)),
            _ => referenced.Select(doc.Objects.FindId).Where(ComponentHostResolver.IsOrdinaryHost).Cast<RhinoObject>()
        };
        hosts = query.DistinctBy(host => host.Id).ToArray();
        if (hosts.Count == 0)
        {
            message = "当前输出范围没有有效宿主。";
            return false;
        }
        message = $"已解析 {hosts.Count} 个输出宿主。";
        return true;
    }

    public static bool TryExport(
        RhinoDoc doc,
        DeliveryOutputRequest request,
        out DeliveryOutputResult result,
        out AssemblyInspectionReport inspection,
        out string message,
        FastenerUiOperationContext? operation = null)
    {
        result = new DeliveryOutputResult(string.Empty, [], []);
        inspection = new AssemblyInspectionReport([], 0, DateTimeOffset.UtcNow);
        if (request.Formats == DeliveryOutputFormat.None)
        {
            message = "请至少选择一种输出格式。";
            return false;
        }
        if (!TryResolveHosts(doc, request, out var hosts, out message))
            return false;
        var hostIds = hosts.Select(host => host.Id).ToHashSet();
        var components = ComponentRepository.ReadAllControlPoints(doc, out _)
            .Where(component => component.Bindings.Any(binding => hostIds.Contains(binding.TargetObjectId)))
            .DistinctBy(component => component.ComponentId).ToArray();
        if (!BooleanExportService.TryBuild(doc, hosts, out var booleanResult, out message, operation))
            return false;
        inspection = AssemblyInspectionService.Inspect(doc, components, operation: operation);
        if (operation?.IsCancellationRequested == true)
        {
            message = "交付已取消；没有生成文件。";
            return false;
        }
        var scopedErrors = inspection.Issues.Count(issue =>
            issue.Severity == AssemblyInspectionSeverity.Error
            && issue.HostId != Guid.Empty
            && hostIds.Contains(issue.HostId));
        if (scopedErrors > 0)
        {
            message = $"所选宿主范围内发现 {scopedErrors} 个装配错误，已停止输出。";
            return false;
        }
        if (inspection.WarningCount > 0 && !request.AcceptWarnings)
        {
            message = $"装配预检有 {inspection.WarningCount} 个警告；确认警告后才能继续。";
            return false;
        }
        var cleanProject = SanitizeFileName(string.IsNullOrWhiteSpace(request.ProjectName)
            ? Path.GetFileNameWithoutExtension(doc.Path) : request.ProjectName.Trim());
        if (string.IsNullOrWhiteSpace(cleanProject))
            cleanProject = "参数化紧固件";
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmm");
        var finalDirectory = UniqueDirectory(Path.Combine(request.ParentDirectory, $"{cleanProject}_交付_{timestamp}"));
        var staging = Path.Combine(request.ParentDirectory, $".{Path.GetFileName(finalDirectory)}.{Guid.NewGuid():N}.tmp");
        var files = new List<string>();
        try
        {
            Directory.CreateDirectory(staging);
            var hostInfo = BuildHostInfo(doc, components);
            var report = FastenerStatisticsBuilder.Build(components, FastenerStatisticsScope.All);
            var fasteners = request.IncludeFastenerSolids
                ? BuildFastenerBodies(components)
                : [];
            var formatCount = Enum.GetValues<DeliveryOutputFormat>()
                .Count(format => format != DeliveryOutputFormat.None && request.Formats.HasFlag(format));
            var formatIndex = 0;
            if (request.Formats.HasFlag(DeliveryOutputFormat.Rhino3dm))
            {
                if (!ContinueFormat(operation, "写入 3DM", formatIndex++, formatCount))
                    throw new OperationCanceledException();
                var name = $"{cleanProject}_装配.3dm";
                WriteGeometry(Path.Combine(staging, name), booleanResult, fasteners, request.IncludeFastenerSolids);
                files.Add(name);
            }
            if (request.Formats.HasFlag(DeliveryOutputFormat.Step))
            {
                if (!ContinueFormat(operation, "写入 STEP", formatIndex++, formatCount))
                    throw new OperationCanceledException();
                var name = $"{cleanProject}_模型.step";
                WriteGeometry(Path.Combine(staging, name), booleanResult, fasteners, request.IncludeFastenerSolids);
                files.Add(name);
            }
            if (request.Formats.HasFlag(DeliveryOutputFormat.Stl))
            {
                if (!ContinueFormat(operation, "写入 STL", formatIndex++, formatCount))
                    throw new OperationCanceledException();
                var name = $"{cleanProject}_打印.stl";
                WriteGeometry(Path.Combine(staging, name), booleanResult, [], false);
                files.Add(name);
            }
            if (request.Formats.HasFlag(DeliveryOutputFormat.Excel))
            {
                if (!ContinueFormat(operation, "写入 Excel", formatIndex++, formatCount))
                    throw new OperationCanceledException();
                var name = $"{cleanProject}_BOM.xlsx";
                FastenerStatisticsWorkbookWriter.Write(Path.Combine(staging, name), cleanProject, DateTimeOffset.Now, report, hostInfo);
                files.Add(name);
            }
            if (request.Formats.HasFlag(DeliveryOutputFormat.Csv))
            {
                if (!ContinueFormat(operation, "写入 CSV", formatIndex++, formatCount))
                    throw new OperationCanceledException();
                var name = $"{cleanProject}_BOM.csv";
                FastenerStatisticsCsvWriter.Write(Path.Combine(staging, name), report, hostInfo);
                files.Add(name);
            }
            if (files.Any(name => !File.Exists(Path.Combine(staging, name))))
                throw new IOException("一个或多个交付文件未成功写入。" );
            Directory.Move(staging, finalDirectory);
            var warnings = booleanResult.Warnings.Concat(inspection.Issues
                .Where(issue => issue.Severity == AssemblyInspectionSeverity.Warning)
                .Select(issue => issue.Message)).Distinct().ToArray();
            result = new DeliveryOutputResult(finalDirectory, files, warnings);
            message = $"已原子生成 {files.Count} 个交付文件：{finalDirectory}";
            return true;
        }
        catch (Exception ex)
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
            message = ex is OperationCanceledException
                ? "交付已取消；临时成果已清理。"
                : $"交付失败，未保留半套文件：{ex.Message}";
            return false;
        }
    }

    private static bool ContinueFormat(
        FastenerUiOperationContext? operation,
        string phase,
        int completed,
        int total) => operation is null || operation.Yield(phase, completed, total);

    private static IEnumerable<RhinoObject> ResolveSelection(RhinoDoc doc, IReadOnlySet<Guid> referenced)
    {
        var selected = doc.Objects.GetSelectedObjects(false, false).ToArray();
        var componentIds = selected.Select(obj => obj.Attributes.GetUserString(Persistence.ComponentRepository.ComponentIdKey))
            .Where(text => Guid.TryParse(text, out _)).Select(Guid.Parse).ToHashSet();
        var fromControls = ComponentRepository.ReadAllControlPoints(doc, out _)
            .Where(component => componentIds.Contains(component.ComponentId))
            .SelectMany(component => component.Bindings).Select(binding => binding.TargetObjectId);
        return selected.Where(ComponentHostResolver.IsOrdinaryHost)
            .Concat(fromControls.Where(referenced.Contains).Select(doc.Objects.FindId)
                .Where(ComponentHostResolver.IsOrdinaryHost).Cast<RhinoObject>());
    }

    private static IReadOnlyList<RhinoExportFastenerBody> BuildFastenerBodies(IEnumerable<FastenerComponentData> components) => components
        .SelectMany(component => FastenerGeometryFactory.CreateProxy(component, FastenerSpecResolver.Resolve(component, RhinoMMPlugIn.Catalog))
            .Select(proxy => new RhinoExportFastenerBody(
                component.ComponentId,
                component.Kind,
                $"{component.Size} · {FastenerLabels.Kind(component)}",
                component.Kind == FastenerKind.HeatSetInsert ? RhinoExportMaterialKind.Brass : RhinoExportMaterialKind.Steel,
                proxy)))
        .ToArray();

    private static void WriteGeometry(
        string path,
        BooleanExportResult result,
        IReadOnlyList<RhinoExportFastenerBody> fasteners,
        bool includeFasteners)
    {
        using var output = RhinoDoc.CreateHeadless(null);
        foreach (var body in result.Bodies)
            output.Objects.AddBrep(body.Geometry, body.SourceAttributes.Duplicate());
        if (includeFasteners)
        {
            var steel = AddDeliveryMaterial(output, "参数化紧固件·拉丝钢", System.Drawing.Color.FromArgb(150, 160, 170));
            var brass = AddDeliveryMaterial(output, "参数化紧固件·黄铜", System.Drawing.Color.FromArgb(190, 145, 55));
            var steelLayer = AddDeliveryLayer(output, "参数化紧固件::渲染::钢", System.Drawing.Color.SlateGray);
            var brassLayer = AddDeliveryLayer(output, "参数化紧固件::渲染::黄铜", System.Drawing.Color.Goldenrod);
            foreach (var body in fasteners)
            {
                var attributes = new ObjectAttributes
                {
                    Name = body.Name,
                    LayerIndex = body.MaterialKind == RhinoExportMaterialKind.Brass ? brassLayer : steelLayer,
                    MaterialIndex = body.MaterialKind == RhinoExportMaterialKind.Brass ? brass : steel,
                    MaterialSource = ObjectMaterialSource.MaterialFromObject
                };
                output.Objects.AddBrep(body.Geometry, attributes);
            }
        }
        if (!output.WriteFile(path, new FileWriteOptions { SuppressDialogBoxes = true }))
            throw new IOException($"无法写入 {Path.GetFileName(path)}。" );
    }

    private static int AddDeliveryMaterial(RhinoDoc doc, string name, System.Drawing.Color color)
    {
        var material = new Material { Name = name, DiffuseColor = color, Shine = 0.55 };
        return doc.Materials.Add(material);
    }

    private static int AddDeliveryLayer(RhinoDoc doc, string fullPath, System.Drawing.Color color)
    {
        var parent = doc.Layers.FindName("参数化紧固件");
        var parentId = parent?.Id ?? Guid.Empty;
        if (parent is null)
        {
            var index = doc.Layers.Add(new Layer { Name = "参数化紧固件" });
            parentId = doc.Layers[index].Id;
        }
        var render = doc.Layers.FindName("渲染", doc.Layers.FindId(parentId)?.Index ?? -1);
        var renderId = render?.Id ?? Guid.Empty;
        if (render is null)
        {
            var index = doc.Layers.Add(new Layer { Name = "渲染", ParentLayerId = parentId });
            renderId = doc.Layers[index].Id;
        }
        var name = fullPath.EndsWith("黄铜", StringComparison.Ordinal) ? "黄铜" : "钢";
        return doc.Layers.Add(new Layer { Name = name, ParentLayerId = renderId, Color = color });
    }

    private static IReadOnlyDictionary<Guid, FastenerDeliveryHostInfo> BuildHostInfo(
        RhinoDoc doc,
        IEnumerable<FastenerComponentData> components) => components.ToDictionary(
        component => component.ComponentId,
        component =>
        {
            var hosts = component.Bindings.Select(binding => doc.Objects.FindId(binding.TargetObjectId))
                .Where(host => host is not null).Cast<RhinoObject>().DistinctBy(host => host.Id).ToArray();
            var names = string.Join("; ", hosts.Select(host => string.IsNullOrWhiteSpace(host.Attributes.Name)
                ? host.Id.ToString("N")[..8] : host.Attributes.Name));
            var layers = string.Join("; ", hosts.Select(host => doc.Layers.FindIndex(host.Attributes.LayerIndex)?.FullPath)
                .Where(name => !string.IsNullOrWhiteSpace(name)).Distinct());
            return new FastenerDeliveryHostInfo(names, layers);
        });

    private static string SanitizeFileName(string value)
    {
        foreach (var character in Path.GetInvalidFileNameChars())
            value = value.Replace(character, '_');
        return value.Trim();
    }

    private static string UniqueDirectory(string preferred)
    {
        if (!Directory.Exists(preferred) && !File.Exists(preferred))
            return preferred;
        var index = 2;
        while (Directory.Exists(preferred + $"-{index}") || File.Exists(preferred + $"-{index}"))
            index++;
        return preferred + $"-{index}";
    }
}
