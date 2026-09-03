using System.Diagnostics;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Services;

internal enum DocumentComponentHealthState
{
    Healthy,
    MaintenanceRequired,
    RelinkRequired,
    Corrupt,
    PresentationDrift,
    Configuration,
    LegacyUnverified
}

internal sealed record DocumentComponentHealthEntry(
    Guid ComponentId,
    Guid ControlPointObjectId,
    DocumentComponentHealthState State,
    string Reason,
    string Recommendation,
    FastenerComponentSnapshot? Snapshot);

internal sealed record DocumentHealthReport(
    uint DocumentSerial,
    long DocumentRevision,
    DateTimeOffset CompletedAt,
    IReadOnlyList<DocumentComponentHealthEntry> Entries,
    bool IsStale = false)
{
    public int HealthyCount => Count(DocumentComponentHealthState.Healthy);
    public int MaintenanceCount => Count(DocumentComponentHealthState.MaintenanceRequired);
    public int RelinkRequiredCount => Count(DocumentComponentHealthState.RelinkRequired);
    public int CorruptCount => Count(DocumentComponentHealthState.Corrupt);
    public int PresentationDriftCount => Count(DocumentComponentHealthState.PresentationDrift);
    public int ConfigurationCount => Count(DocumentComponentHealthState.Configuration);
    public int LegacyUnverifiedCount => Count(DocumentComponentHealthState.LegacyUnverified);
    public int BlockingCount => RelinkRequiredCount + CorruptCount;
    public bool HasMaintenance => MaintenanceCount > 0;
    public bool HasBlockingProblems => BlockingCount > 0;
    public bool HasProblems => HasMaintenance || HasBlockingProblems;
    public int NeedsRebuildCount => MaintenanceCount;
    public int NeedsRelinkCount => RelinkRequiredCount;
    public string Summary =>
        $"文档健康 · 需维护 {MaintenanceCount} · 待重绑 {RelinkRequiredCount} · 损坏 {CorruptCount}";
    public string ReportSignature => string.Join(";", Entries
        .OrderBy(entry => entry.ComponentId)
        .Select(entry => $"{entry.ComponentId:N}:{entry.State}"));

    private int Count(DocumentComponentHealthState state) =>
        Entries.Where(entry => entry.State == state).Select(entry => entry.ComponentId).Distinct().Count();
}

internal sealed class DocumentHealthReportChangedEventArgs(
    RhinoDoc document,
    DocumentHealthReport report) : EventArgs
{
    public RhinoDoc Document { get; } = document;
    public DocumentHealthReport Report { get; } = report;
}

/// <summary>
/// Cheap deterministic document scan. The passive path never reconciles hosts,
/// rebuilds Breps or runs Boolean checks; those belong to explicit tools.
/// </summary>
internal static class ComponentDocumentHealthService
{
    private sealed class ScanWork(RhinoDoc document, IReadOnlyList<Guid> componentIds)
    {
        public RhinoDoc Document { get; } = document;
        public IReadOnlyList<Guid> ComponentIds { get; } = componentIds;
        public List<DocumentComponentHealthEntry> Entries { get; } = [];
        public int Index { get; set; }
        public long Revision { get; } = FastenerDocumentIndexService.CurrentRevision(document);
    }

    private sealed class MutationScope(RhinoDoc document) : IDisposable
    {
        private RhinoDoc? _document = document;
        public void Dispose()
        {
            if (_document is null) return;
            EndMutation(_document);
            _document = null;
        }
    }

    private sealed record StableCandidate(string Signature, int Count);
    private static readonly Dictionary<uint, DocumentHealthReport> Reports = [];
    private static readonly Dictionary<uint, DateTimeOffset> Pending = [];
    private static readonly Dictionary<uint, int> MutationDepth = [];
    private static readonly HashSet<uint> DirtyDuringMutation = [];
    private static readonly Dictionary<uint, StableCandidate> StableCandidates = [];
    private static readonly Dictionary<uint, string> LastCommandLineReports = [];
    private static ScanWork? _active;
    private static bool _initialized;

    public static event EventHandler<DocumentHealthReportChangedEventArgs>? ReportChanged;

    public static void Initialize()
    {
        if (_initialized) return;
        RhinoDoc.EndOpenDocument += DocumentOpened;
        RhinoDoc.CloseDocument += DocumentClosed;
        RhinoDoc.AddRhinoObject += DocumentChanged;
        RhinoDoc.DeleteRhinoObject += DocumentChanged;
        RhinoDoc.ReplaceRhinoObject += ObjectReplaced;
        RhinoDoc.UndeleteRhinoObject += DocumentChanged;
        RhinoDoc.ModifyObjectAttributes += AttributesChanged;
        Command.UndoRedo += UndoRedo;
        RhinoApp.Idle += OnIdle;
        _initialized = true;
    }

    public static void Shutdown()
    {
        if (!_initialized) return;
        RhinoDoc.EndOpenDocument -= DocumentOpened;
        RhinoDoc.CloseDocument -= DocumentClosed;
        RhinoDoc.AddRhinoObject -= DocumentChanged;
        RhinoDoc.DeleteRhinoObject -= DocumentChanged;
        RhinoDoc.ReplaceRhinoObject -= ObjectReplaced;
        RhinoDoc.UndeleteRhinoObject -= DocumentChanged;
        RhinoDoc.ModifyObjectAttributes -= AttributesChanged;
        Command.UndoRedo -= UndoRedo;
        RhinoApp.Idle -= OnIdle;
        Reports.Clear();
        Pending.Clear();
        MutationDepth.Clear();
        DirtyDuringMutation.Clear();
        StableCandidates.Clear();
        LastCommandLineReports.Clear();
        _active = null;
        _initialized = false;
    }

    public static DocumentHealthReport? Current(RhinoDoc doc) =>
        Reports.GetValueOrDefault(doc.RuntimeSerialNumber);

    public static IDisposable SuppressDuringMutation(RhinoDoc doc)
    {
        var serial = doc.RuntimeSerialNumber;
        MutationDepth[serial] = MutationDepth.GetValueOrDefault(serial) + 1;
        if (_active?.Document.RuntimeSerialNumber == serial) _active = null;
        Pending.Remove(serial);
        MarkCurrentStale(serial);
        return new MutationScope(doc);
    }

    public static void Schedule(RhinoDoc doc, bool immediate = false)
    {
        if (doc is null) return;
        var serial = doc.RuntimeSerialNumber;
        MarkCurrentStale(serial);
        if (MutationDepth.GetValueOrDefault(serial) > 0)
        {
            DirtyDuringMutation.Add(serial);
            if (_active?.Document.RuntimeSerialNumber == serial) _active = null;
            return;
        }
        Pending[serial] = DateTimeOffset.UtcNow.AddMilliseconds(immediate ? 0 : 240);
    }

    public static DocumentHealthReport ScanNow(RhinoDoc doc)
    {
        var report = BuildReport(doc, ComponentIds(doc).Select(id => Evaluate(doc, id)).ToArray());
        StableCandidates.Remove(doc.RuntimeSerialNumber);
        Publish(doc, report, true);
        return report;
    }

    public static bool RepairDeterministic(RhinoDoc doc, out string message)
    {
        var report = ScanNow(doc);
        var repairable = report.Entries
            .Where(entry => entry.State == DocumentComponentHealthState.MaintenanceRequired
                && entry.Snapshot is not null)
            .Select(entry => entry.Snapshot!.EffectiveData)
            .ToArray();
        if (repairable.Length == 0)
        {
            message = report.HasBlockingProblems
                ? $"没有可自动修复的确定性问题。{report.Summary}；待重绑和损坏项需要人工处理。"
                : "文档中的参数化紧固件无需重建。";
            return true;
        }
        if (!FastenerComponentService.CreateOrReplaceMany(doc, repairable, out _, out var repairMessage))
        {
            message = $"确定性修复失败：{repairMessage}";
            return false;
        }
        FastenerDocumentIndexService.Invalidate(doc);
        var refreshed = ScanNow(doc);
        message = $"已修复 {repairable.Length} 个可确定恢复的组件。{refreshed.Summary}。";
        return true;
    }

    private static void OnIdle(object? sender, EventArgs e)
    {
        if (_active is null)
        {
            var ready = Pending
                .Where(pair => pair.Value <= DateTimeOffset.UtcNow
                    && MutationDepth.GetValueOrDefault(pair.Key) == 0)
                .OrderBy(pair => pair.Value)
                .FirstOrDefault();
            if (ready.Key == 0) return;
            Pending.Remove(ready.Key);
            var doc = RhinoDoc.FromRuntimeSerialNumber(ready.Key);
            if (doc is null) return;
            _active = new ScanWork(doc, ComponentIds(doc));
        }

        var active = _active;
        if (active is null) return;
        var serial = active.Document.RuntimeSerialNumber;
        if (RhinoDoc.FromRuntimeSerialNumber(serial) is null)
        {
            _active = null;
            return;
        }
        if (MutationDepth.GetValueOrDefault(serial) > 0)
        {
            _active = null;
            DirtyDuringMutation.Add(serial);
            return;
        }

        var timer = Stopwatch.StartNew();
        while (active.Index < active.ComponentIds.Count && timer.ElapsedMilliseconds < 12)
            active.Entries.Add(Evaluate(active.Document, active.ComponentIds[active.Index++]));
        if (active.Index < active.ComponentIds.Count) return;

        _active = null;
        if (FastenerDocumentIndexService.CurrentRevision(active.Document) != active.Revision
            || Pending.ContainsKey(serial))
        {
            Schedule(active.Document);
            return;
        }

        var report = BuildReport(active.Document, active.Entries.ToArray());
        if (report.HasMaintenance && !report.HasBlockingProblems)
        {
            var candidate = StableCandidates.GetValueOrDefault(serial);
            var count = candidate?.Signature == report.ReportSignature ? candidate.Count + 1 : 1;
            StableCandidates[serial] = new StableCandidate(report.ReportSignature, count);
            if (count < 2)
            {
                Pending[serial] = DateTimeOffset.UtcNow.AddMilliseconds(260);
                return;
            }
        }
        else StableCandidates.Remove(serial);
        Publish(active.Document, report, false);
    }

    private static DocumentHealthReport BuildReport(
        RhinoDoc doc,
        IReadOnlyList<DocumentComponentHealthEntry> entries) => new(
        doc.RuntimeSerialNumber,
        FastenerDocumentIndexService.CurrentRevision(doc),
        DateTimeOffset.UtcNow,
        entries);

    private static void Publish(RhinoDoc doc, DocumentHealthReport report, bool explicitScan)
    {
        Reports[doc.RuntimeSerialNumber] = report;
        ReportChanged?.Invoke(null, new DocumentHealthReportChangedEventArgs(doc, report));
        if (!report.HasBlockingProblems || explicitScan) return;
        if (LastCommandLineReports.GetValueOrDefault(doc.RuntimeSerialNumber) == report.ReportSignature) return;
        LastCommandLineReports[doc.RuntimeSerialNumber] = report.ReportSignature;
        FastenerCommandText.WriteLine(
            $"参数化紧固件：{report.Summary}。存在需要处理的组件，请打开维护中心。");
    }

    private static DocumentComponentHealthEntry Evaluate(RhinoDoc doc, Guid componentId)
    {
        if (!FastenerComponentSnapshotService.TryCapture(doc, componentId, out var snapshot, out var issue))
            return new DocumentComponentHealthEntry(
                componentId,
                ComponentRepository.FindControlPoint(doc, componentId)?.Id ?? Guid.Empty,
                DocumentComponentHealthState.Corrupt,
                issue?.Reason ?? "无法捕获组件快照。",
                issue?.Recommendation ?? "打开维护中心定位组件。",
                null);

        if (snapshot!.Hosts.Any(host => !host.IsAvailable))
            return Entry(snapshot, DocumentComponentHealthState.RelinkRequired,
                "存在丢失、为空或无效的宿主绑定。", "使用重新绑定向导确认正确宿主。");

        var validation = FastenerComponentValidator.Validate(snapshot.EffectiveData, snapshot.ResolvedSpecification);
        if (!validation.IsValid)
            return Entry(snapshot, DocumentComponentHealthState.Corrupt,
                string.Join(" ", validation.Issues.Where(item => item.IsError).Select(item => item.Message)),
                "读取组件参数并修正无效值，或从备份恢复。");

        var objects = ComponentRepository.FindComponentObjects(doc, componentId).ToArray();
        var control = objects.SingleOrDefault(obj => obj.Id == snapshot.ControlPointObjectId);
        var derived = objects.Where(obj => obj.Id != snapshot.ControlPointObjectId).ToArray();
        if (control is null)
            return Entry(snapshot, DocumentComponentHealthState.Corrupt,
                "组件缺少唯一有效控制点。", "定位问题组件并从备份恢复或删除组件。");

        var keys = new HashSet<string>(StringComparer.Ordinal);
        var metadataValid = derived.All(obj =>
        {
            var role = obj.Attributes.GetUserString(ComponentRepository.RoleKey) ?? string.Empty;
            var binding = obj.Attributes.GetUserString(ComponentRepository.BindingIdKey) ?? string.Empty;
            var part = obj.Attributes.GetUserString(ComponentRepository.PartIndexKey) ?? string.Empty;
            return role is "Proxy" or "Cutter" or "HeadCutter"
                && int.TryParse(part, out var partIndex) && partIndex >= 0
                && keys.Add($"{role}|{binding}|{part}")
                && string.IsNullOrWhiteSpace(obj.Attributes.GetUserString(ComponentRepository.ComponentKey));
        });
        var idsValid = snapshot.EffectiveData.ControlPointObjectId == snapshot.ControlPointObjectId
            && snapshot.EffectiveData.ProxyObjectId != Guid.Empty
            && derived.Any(obj => obj.Id == snapshot.EffectiveData.ProxyObjectId
                && obj.Attributes.GetUserString(ComponentRepository.RoleKey) == "Proxy")
            && snapshot.EffectiveData.Bindings.All(binding => binding.CutterObjectId == Guid.Empty
                || derived.Any(obj => obj.Id == binding.CutterObjectId
                    && string.Equals(obj.Attributes.GetUserString(ComponentRepository.BindingIdKey),
                        binding.BindingId.ToString("D"), StringComparison.OrdinalIgnoreCase)));
        var manifest = control.Attributes.GetUserString(ComponentRepository.DerivedManifestKey);
        var controlSourceSignature = control.Attributes.GetUserString(ComponentRepository.SourceSignatureKey);
        var hasCurrentMetadata = !string.IsNullOrWhiteSpace(manifest)
            && !string.IsNullOrWhiteSpace(controlSourceSignature)
            && derived.All(obj => string.Equals(
                obj.Attributes.GetUserString(ComponentRepository.SourceSignatureKey),
                controlSourceSignature,
                StringComparison.Ordinal));
        if (snapshot.PlacementMismatch || !metadataValid || !idsValid)
            return Entry(snapshot, DocumentComponentHealthState.MaintenanceRequired,
                snapshot.PlacementMismatch
                    ? "控制点位置与保存放置原点不同，派生几何需要同步。"
                    : "派生对象缺失，或对象引用与角色元数据不同步。",
                "运行一键修复；组件参数、组件ID、绑定ID和宿主将保持不变。");

        if (hasCurrentMetadata)
        {
            var geometryValid = derived.All(obj =>
            {
                var saved = obj.Attributes.GetUserString(ComponentRepository.GeometrySignatureKey);
                return !string.IsNullOrWhiteSpace(saved)
                    && saved == ComponentReliabilitySignatureService.GeometrySignature(obj.Geometry);
            });
            var manifestValid = manifest == ComponentRepository.DerivedManifestSignature(derived);
            if (!geometryValid || !manifestValid || controlSourceSignature != snapshot.ParameterSignature)
                return Entry(snapshot, DocumentComponentHealthState.MaintenanceRequired,
                    "组件参数、宿主几何、派生对象或派生清单已经变化。",
                    "运行一键修复以重新生成派生对象。");
        }

        if (!PresentationValid(doc, componentId, derived, snapshot.EffectiveData))
            return Entry(snapshot, DocumentComponentHealthState.PresentationDrift,
                "图层、材质、组或可见性与当前显示设置不同。",
                "需要时运行快速刷新以同步显示；这不会阻止更新或导出。");
        if (snapshot.EffectiveData.Bindings.Any(binding =>
                !binding.IsBooleanEnabled || !binding.IsPreviewVisible))
            return Entry(snapshot, DocumentComponentHealthState.Configuration,
                "一个或多个模块的预览或导出布尔已由用户关闭。",
                "这是当前组件配置，不属于组件故障。");
        if (!hasCurrentMetadata)
            return Entry(snapshot, DocumentComponentHealthState.LegacyUnverified,
                "组件使用旧版派生元数据，尚未建立快速健康清单。",
                "组件仍可正常使用；下次更新或一键修复后会自动补齐。");
        return Entry(snapshot, DocumentComponentHealthState.Healthy, "状态正常。", string.Empty);
    }

    private static bool PresentationValid(
        RhinoDoc doc,
        Guid componentId,
        IReadOnlyList<RhinoObject> derived,
        RhinoMM.Core.Domain.FastenerComponentData component)
    {
        var bindings = component.Bindings.ToDictionary(binding => binding.BindingId);
        var presentation = derived.All(obj =>
        {
            var role = obj.Attributes.GetUserString(ComponentRepository.RoleKey);
            var cutter = role is "Cutter" or "HeadCutter";
            var expectedLayer = cutter ? ComponentPresentationService.CutterLayerPath : ComponentPresentationService.FastenerLayerPath;
            var expectedMaterial = cutter ? ComponentPresentationService.SharedCutterMaterialName : ComponentPresentationService.SharedFastenerMaterialName;
            var layer = doc.Layers.FindIndex(obj.Attributes.LayerIndex);
            var material = obj.Attributes.MaterialIndex >= 0 && obj.Attributes.MaterialIndex < doc.Materials.Count
                ? doc.Materials[obj.Attributes.MaterialIndex] : null;
            var expectedVisible = true;
            if (cutter
                && Guid.TryParse(obj.Attributes.GetUserString(ComponentRepository.BindingIdKey), out var bindingId)
                && bindings.TryGetValue(bindingId, out var binding))
                expectedVisible = binding.IsPreviewVisible;
            return layer?.FullPath == expectedLayer
                && material?.Name == expectedMaterial
                && obj.Attributes.Visible == expectedVisible;
        });
        var groupIndex = doc.Groups.Find(ComponentPresentationService.GroupName(componentId));
        var group = derived.Count == 0 || groupIndex >= 0
            && derived.All(obj => (obj.GetGroupList() ?? []).Contains(groupIndex));
        return presentation && group;
    }

    private static DocumentComponentHealthEntry Entry(
        FastenerComponentSnapshot snapshot,
        DocumentComponentHealthState state,
        string reason,
        string recommendation) => new(
        snapshot.EffectiveData.ComponentId,
        snapshot.ControlPointObjectId,
        state,
        reason.Trim(), recommendation.Trim(), snapshot);

    private static IReadOnlyList<Guid> ComponentIds(RhinoDoc doc) => doc.Objects
        .Select(obj => obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey))
        .Where(text => Guid.TryParse(text, out _))
        .Select(Guid.Parse)
        .Distinct()
        .ToArray();

    private static void EndMutation(RhinoDoc doc)
    {
        var serial = doc.RuntimeSerialNumber;
        var depth = Math.Max(0, MutationDepth.GetValueOrDefault(serial) - 1);
        if (depth > 0)
        {
            MutationDepth[serial] = depth;
            return;
        }
        MutationDepth.Remove(serial);
        if (DirtyDuringMutation.Remove(serial)) Schedule(doc);
    }

    private static void MarkCurrentStale(uint serial)
    {
        if (Reports.TryGetValue(serial, out var current) && !current.IsStale)
            Reports[serial] = current with { IsStale = true };
    }

    private static void DocumentOpened(object? sender, DocumentOpenEventArgs e) => Schedule(e.Document, true);
    private static void DocumentClosed(object? sender, DocumentEventArgs e)
    {
        var serial = e.DocumentSerialNumber;
        Reports.Remove(serial);
        Pending.Remove(serial);
        MutationDepth.Remove(serial);
        DirtyDuringMutation.Remove(serial);
        StableCandidates.Remove(serial);
        LastCommandLineReports.Remove(serial);
        if (_active?.Document.RuntimeSerialNumber == serial) _active = null;
    }
    private static void DocumentChanged(object? sender, RhinoObjectEventArgs e)
    {
        var doc = sender as RhinoDoc ?? e.TheObject.Document;
        if (doc is not null) Schedule(doc);
    }
    private static void ObjectReplaced(object? sender, RhinoReplaceObjectEventArgs e) => Schedule(e.Document);
    private static void AttributesChanged(object? sender, RhinoModifyObjectAttributesEventArgs e) => Schedule(e.Document);
    private static void UndoRedo(object? sender, UndoRedoEventArgs e)
    {
        if ((e.IsEndUndo || e.IsEndRedo) && RhinoDoc.ActiveDoc is { } doc) Schedule(doc, true);
    }
}
