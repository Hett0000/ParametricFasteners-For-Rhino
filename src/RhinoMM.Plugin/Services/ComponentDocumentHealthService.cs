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
    NeedsRebuild,
    NeedsRelink,
    Corrupt
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
    public int HealthyCount => Entries.Count(entry => entry.State == DocumentComponentHealthState.Healthy);
    public int NeedsRebuildCount => Entries.Count(entry => entry.State == DocumentComponentHealthState.NeedsRebuild);
    public int NeedsRelinkCount => Entries.Count(entry => entry.State == DocumentComponentHealthState.NeedsRelink);
    public int CorruptCount => Entries.Count(entry => entry.State == DocumentComponentHealthState.Corrupt);
    public bool HasProblems => NeedsRebuildCount + NeedsRelinkCount + CorruptCount > 0;
    public string Summary => $"文档健康 · 需重建 {NeedsRebuildCount} · 待重绑 {NeedsRelinkCount} · 损坏 {CorruptCount}";
}

internal sealed class DocumentHealthReportChangedEventArgs(
    RhinoDoc document,
    DocumentHealthReport report) : EventArgs
{
    public RhinoDoc Document { get; } = document;
    public DocumentHealthReport Report { get; } = report;
}

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

    private static readonly Dictionary<uint, DocumentHealthReport> Reports = [];
    private static readonly Dictionary<uint, DateTimeOffset> Pending = [];
    private static ScanWork? _active;
    private static bool _initialized;

    public static event EventHandler<DocumentHealthReportChangedEventArgs>? ReportChanged;

    public static void Initialize()
    {
        if (_initialized)
            return;
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
        if (!_initialized)
            return;
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
        _active = null;
        _initialized = false;
    }

    public static DocumentHealthReport? Current(RhinoDoc doc) =>
        Reports.GetValueOrDefault(doc.RuntimeSerialNumber);

    public static void Schedule(RhinoDoc doc, bool immediate = false)
    {
        if (doc is null)
            return;
        Pending[doc.RuntimeSerialNumber] = DateTimeOffset.UtcNow.AddMilliseconds(immediate ? 0 : 180);
        if (Reports.TryGetValue(doc.RuntimeSerialNumber, out var current) && !current.IsStale)
            Reports[doc.RuntimeSerialNumber] = current with { IsStale = true };
    }

    public static DocumentHealthReport ScanNow(RhinoDoc doc)
    {
        var ids = ComponentIds(doc);
        var entries = ids.Select(id => Evaluate(doc, id)).ToArray();
        var report = new DocumentHealthReport(
            doc.RuntimeSerialNumber,
            FastenerDocumentIndexService.CurrentRevision(doc),
            DateTimeOffset.UtcNow,
            entries);
        Reports[doc.RuntimeSerialNumber] = report;
        ReportChanged?.Invoke(null, new DocumentHealthReportChangedEventArgs(doc, report));
        return report;
    }

    public static bool RepairDeterministic(RhinoDoc doc, out string message)
    {
        var report = ScanNow(doc);
        var repairable = report.Entries
            .Where(entry => entry.State == DocumentComponentHealthState.NeedsRebuild && entry.Snapshot is not null)
            .Select(entry => entry.Snapshot!.EffectiveData)
            .ToArray();
        if (repairable.Length == 0)
        {
            message = report.HasProblems
                ? $"没有可自动修复的确定性问题。{report.Summary}；待重绑和损坏项需要人工处理。"
                : "文档中的参数化紧固件状态正常。";
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
                .Where(pair => pair.Value <= DateTimeOffset.UtcNow)
                .OrderBy(pair => pair.Value)
                .FirstOrDefault();
            if (ready.Key == 0)
                return;
            Pending.Remove(ready.Key);
            var doc = RhinoDoc.FromRuntimeSerialNumber(ready.Key);
            if (doc is null)
                return;
            _active = new ScanWork(doc, ComponentIds(doc));
        }

        var active = _active;
        if (active is null)
            return;
        if (RhinoDoc.FromRuntimeSerialNumber(active.Document.RuntimeSerialNumber) is null)
        {
            _active = null;
            return;
        }
        var timer = Stopwatch.StartNew();
        while (active.Index < active.ComponentIds.Count && timer.ElapsedMilliseconds < 12)
            active.Entries.Add(Evaluate(active.Document, active.ComponentIds[active.Index++]));
        if (active.Index < active.ComponentIds.Count)
            return;

        var report = new DocumentHealthReport(
            active.Document.RuntimeSerialNumber,
            active.Revision,
            DateTimeOffset.UtcNow,
            active.Entries.ToArray());
        Reports[active.Document.RuntimeSerialNumber] = report;
        _active = null;
        if (report.HasProblems)
        {
            RhinoApp.WriteLine($"参数化紧固件：{report.Summary}。打开维护中心查看或修复。");
            ReportChanged?.Invoke(null, new DocumentHealthReportChangedEventArgs(active.Document, report));
        }
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
            return Entry(snapshot, DocumentComponentHealthState.NeedsRelink,
                "存在丢失、为空或无效的宿主绑定。",
                "使用重新绑定向导确认正确宿主。");

        var validation = FastenerComponentValidator.Validate(
            snapshot.EffectiveData,
            snapshot.ResolvedSpecification);
        if (!validation.IsValid)
            return Entry(snapshot, DocumentComponentHealthState.Corrupt,
                string.Join(" ", validation.Issues.Where(item => item.IsError).Select(item => item.Message)),
                "读取组件参数并修正无效值，或从备份恢复。 ");

        if (!FastenerGeometryPreparationService.TryPrepare(
                doc,
                snapshot.EffectiveData,
                out var prepared,
                out var preparationError))
            return Entry(snapshot, DocumentComponentHealthState.Corrupt,
                preparationError,
                "检查宿主、规格与切割参数后重新运行健康检查。");

        var derived = ComponentRepository.FindComponentObjects(doc, componentId)
            .Where(obj => obj.Id != snapshot.ControlPointObjectId)
            .ToArray();
        var expectedCount = prepared!.Proxies.Count
            + prepared.Cutters.Sum(item => item.Shafts.Count + item.Heads.Count);
        var expectedGeometry = new Dictionary<(string Role, Guid BindingId, int PartIndex), string>();
        var expectedVisibility = new Dictionary<(string Role, Guid BindingId, int PartIndex), bool>();
        for (var index = 0; index < prepared.Proxies.Count; index++)
        {
            expectedGeometry[("Proxy", Guid.Empty, index)] =
                ComponentReliabilitySignatureService.GeometrySignature(prepared.Proxies[index]);
            expectedVisibility[("Proxy", Guid.Empty, index)] = true;
        }
        foreach (var cutter in prepared.Cutters)
        {
            for (var index = 0; index < cutter.Shafts.Count; index++)
            {
                expectedGeometry[("Cutter", cutter.Binding.BindingId, index)] =
                    ComponentReliabilitySignatureService.GeometrySignature(cutter.Shafts[index]);
                expectedVisibility[("Cutter", cutter.Binding.BindingId, index)] =
                    cutter.Binding.IsPreviewVisible;
            }
            for (var index = 0; index < cutter.Heads.Count; index++)
            {
                expectedGeometry[("HeadCutter", cutter.Binding.BindingId, index)] =
                    ComponentReliabilitySignatureService.GeometrySignature(cutter.Heads[index]);
                expectedVisibility[("HeadCutter", cutter.Binding.BindingId, index)] =
                    cutter.Binding.IsPreviewVisible;
            }
        }
        var signaturesValid = derived.Length == expectedCount
            && derived.All(obj =>
            {
                var role = obj.Attributes.GetUserString(ComponentRepository.RoleKey) ?? string.Empty;
                var bindingId = Guid.TryParse(
                    obj.Attributes.GetUserString(ComponentRepository.BindingIdKey),
                    out var parsedBindingId)
                    ? parsedBindingId
                    : Guid.Empty;
                var partIndex = int.TryParse(
                    obj.Attributes.GetUserString(ComponentRepository.PartIndexKey),
                    out var parsedPartIndex)
                    ? parsedPartIndex
                    : -1;
                var actualSignature = ComponentReliabilitySignatureService.GeometrySignature(obj.Geometry);
                return string.IsNullOrWhiteSpace(obj.Attributes.GetUserString(ComponentRepository.ComponentKey))
                    && obj.Attributes.GetUserString(ComponentRepository.PreparedSignatureKey) == prepared.PreparationSignature
                    && obj.Attributes.GetUserString(ComponentRepository.GeometrySignatureKey) == actualSignature
                    && expectedGeometry.GetValueOrDefault((role, bindingId, partIndex)) == actualSignature
                    && expectedVisibility.GetValueOrDefault((role, bindingId, partIndex)) == obj.Attributes.Visible;
            });
        var idsValid = snapshot.EffectiveData.ControlPointObjectId == snapshot.ControlPointObjectId
            && derived.Any(obj => obj.Id == snapshot.EffectiveData.ProxyObjectId)
            && snapshot.EffectiveData.Bindings.All(binding =>
                binding.CutterObjectId == Guid.Empty
                || derived.Any(obj => obj.Id == binding.CutterObjectId));
        var presentationValid = derived.All(obj =>
        {
            var cutter = obj.Attributes.GetUserString(ComponentRepository.RoleKey) is "Cutter" or "HeadCutter";
            var expectedLayer = cutter
                ? ComponentPresentationService.CutterLayerPath
                : ComponentPresentationService.FastenerLayerPath;
            var expectedMaterial = cutter
                ? ComponentPresentationService.SharedCutterMaterialName
                : ComponentPresentationService.SharedFastenerMaterialName;
            var layer = doc.Layers.FindIndex(obj.Attributes.LayerIndex);
            var material = obj.Attributes.MaterialIndex >= 0
                && obj.Attributes.MaterialIndex < doc.Materials.Count
                    ? doc.Materials[obj.Attributes.MaterialIndex]
                    : null;
            return layer?.FullPath == expectedLayer
                && material?.Name == expectedMaterial;
        });
        var groupIndex = doc.Groups.Find(ComponentPresentationService.GroupName(componentId));
        var groupValid = expectedCount == 0
            || groupIndex >= 0 && derived.All(obj => (obj.GetGroupList() ?? []).Contains(groupIndex));
        if (snapshot.PlacementMismatch || !signaturesValid || !idsValid || !groupValid || !presentationValid
            || prepared.ParameterSignature != snapshot.ParameterSignature)
            return Entry(snapshot, DocumentComponentHealthState.NeedsRebuild,
                snapshot.PlacementMismatch
                    ? "控制点位置与保存放置原点不同，派生几何需要同步。"
                    : "代理体、切割体、对象引用、组或派生签名不同步。",
                "运行一键修复；组件参数、组件 ID、绑定 ID和宿主将保持不变。 ");

        return Entry(snapshot, DocumentComponentHealthState.Healthy, "状态正常。", string.Empty);
    }

    private static DocumentComponentHealthEntry Entry(
        FastenerComponentSnapshot snapshot,
        DocumentComponentHealthState state,
        string reason,
        string recommendation) => new(
        snapshot.EffectiveData.ComponentId,
        snapshot.ControlPointObjectId,
        state,
        reason.Trim(),
        recommendation.Trim(),
        snapshot);

    private static IReadOnlyList<Guid> ComponentIds(RhinoDoc doc) => doc.Objects
        .Select(obj => obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey))
        .Where(text => Guid.TryParse(text, out _))
        .Select(Guid.Parse)
        .Distinct()
        .ToArray();

    private static void DocumentOpened(object? sender, DocumentOpenEventArgs e) => Schedule(e.Document, true);

    private static void DocumentClosed(object? sender, DocumentEventArgs e)
    {
        Reports.Remove(e.Document.RuntimeSerialNumber);
        Pending.Remove(e.Document.RuntimeSerialNumber);
        if (_active?.Document.RuntimeSerialNumber == e.Document.RuntimeSerialNumber)
            _active = null;
    }

    private static void DocumentChanged(object? sender, RhinoObjectEventArgs e)
    {
        var doc = sender as RhinoDoc ?? e.TheObject.Document;
        if (doc is not null)
            Schedule(doc);
    }

    private static void ObjectReplaced(object? sender, RhinoReplaceObjectEventArgs e) => Schedule(e.Document);
    private static void AttributesChanged(object? sender, RhinoModifyObjectAttributesEventArgs e) => Schedule(e.Document);

    private static void UndoRedo(object? sender, UndoRedoEventArgs e)
    {
        if ((e.IsEndUndo || e.IsEndRedo) && RhinoDoc.ActiveDoc is { } doc)
            Schedule(doc, true);
    }
}
