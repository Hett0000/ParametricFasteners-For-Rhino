using Rhino;
using RhinoMM.Core.Domain;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Services;

internal sealed record ContextualEditDraft(
    FastenerComponentData Component,
    PreparedFastenerGeometry? Prepared,
    string Message,
    IReadOnlyList<string> Warnings,
    long DocumentRevision,
    bool IsDirty)
{
    public bool IsValid => Prepared is not null && string.IsNullOrWhiteSpace(Message);
}

internal sealed record ContextualEditSession(
    uint DocumentSerialNumber,
    Guid ComponentId,
    FastenerComponentSnapshot OriginalSnapshot,
    ContextualEditDraft Draft);

internal sealed class ContextualEditSessionChangedEventArgs(
    RhinoDoc document,
    ContextualEditSession session) : EventArgs
{
    public RhinoDoc Document { get; } = document;
    public ContextualEditSession Session { get; } = session;
}

/// <summary>
/// Owns the single, non-persistent edit draft used by the viewport editor,
/// parameter handles and section overlay.  The main-panel template is never
/// read or written here.
/// </summary>
internal static class ContextualEditSessionService
{
    private static readonly Dictionary<(uint Document, Guid Component), ContextualEditSession> Sessions = [];

    public static event EventHandler<ContextualEditSessionChangedEventArgs>? Changed;

    public static bool TryGetOrCreate(
        RhinoDoc doc,
        Guid componentId,
        out ContextualEditSession? session,
        out string message)
    {
        var key = (doc.RuntimeSerialNumber, componentId);
        if (Sessions.TryGetValue(key, out session))
        {
            var revision = FastenerDocumentIndexService.CurrentRevision(doc);
            if (session.Draft.DocumentRevision == revision)
            {
                message = session.Draft.Message;
                return true;
            }

            // A dirty draft is deliberately not rebased on a changed model.
            // This prevents a viewport edit from silently targeting stale hosts.
            if (session.Draft.IsDirty)
            {
                var invalid = session.Draft with
                {
                    Prepared = null,
                    Message = "模型已变化；请取消本次编辑后重新选择组件。",
                    DocumentRevision = revision
                };
                session = session with { Draft = invalid };
                Sessions[key] = session;
                message = invalid.Message;
                Raise(doc, session);
                return true;
            }
        }

        if (!FastenerComponentSnapshotService.TryCapture(doc, componentId, out var snapshot, out var issue)
            || snapshot is null)
        {
            session = null;
            message = issue?.Reason ?? "无法读取组件快照。";
            return false;
        }

        var draft = Prepare(doc, snapshot.EffectiveData, snapshot.EffectiveData, false);
        session = new ContextualEditSession(
            doc.RuntimeSerialNumber,
            componentId,
            snapshot,
            draft);
        Sessions[key] = session;
        message = draft.Message;
        Raise(doc, session);
        return true;
    }

    public static bool TryGet(
        RhinoDoc doc,
        Guid componentId,
        out ContextualEditSession? session) =>
        Sessions.TryGetValue((doc.RuntimeSerialNumber, componentId), out session);

    public static bool TrySetDraft(
        RhinoDoc doc,
        Guid componentId,
        FastenerComponentData component,
        out ContextualEditSession? session,
        out string message)
    {
        if (!TryGetOrCreate(doc, componentId, out session, out message) || session is null)
            return false;
        if (component.ComponentId != componentId)
        {
            message = "上下文草稿的组件 ID 不一致。";
            return false;
        }

        var next = Prepare(doc, session.OriginalSnapshot.EffectiveData, component, true);
        session = session with { Draft = next };
        Sessions[(doc.RuntimeSerialNumber, componentId)] = session;
        message = next.Message;
        Raise(doc, session);
        return true;
    }

    public static bool TryMutate(
        RhinoDoc doc,
        Guid componentId,
        Func<FastenerComponentData, FastenerComponentData> mutation,
        out ContextualEditSession? session,
        out string message)
    {
        if (!TryGetOrCreate(doc, componentId, out session, out message) || session is null)
            return false;
        return TrySetDraft(doc, componentId, mutation(session.Draft.Component), out session, out message);
    }

    public static bool TryCommit(
        RhinoDoc doc,
        Guid componentId,
        out FastenerComponentData? saved,
        out string message)
    {
        saved = null;
        if (!TryGetOrCreate(doc, componentId, out var session, out message) || session is null)
            return false;
        if (!session.Draft.IsValid || session.Draft.Prepared is null)
        {
            message = string.IsNullOrWhiteSpace(session.Draft.Message)
                ? "当前上下文草稿未通过几何预检。"
                : session.Draft.Message;
            return false;
        }
        if (session.Draft.DocumentRevision != FastenerDocumentIndexService.CurrentRevision(doc))
        {
            message = "模型已变化；请取消本次编辑后重新选择组件。";
            return false;
        }
        if (!ComponentUpdateCoordinator.TryApplyDrafts(
                doc,
                [session.Draft.Prepared.Draft],
                out var savedItems,
                out message))
            return false;

        saved = savedItems[0];
        Sessions.Remove((doc.RuntimeSerialNumber, componentId));
        return true;
    }

    public static void Cancel(RhinoDoc doc, Guid componentId) =>
        Sessions.Remove((doc.RuntimeSerialNumber, componentId));

    public static void ClearDocument(RhinoDoc doc)
    {
        foreach (var key in Sessions.Keys.Where(key => key.Document == doc.RuntimeSerialNumber).ToArray())
            Sessions.Remove(key);
    }

    private static ContextualEditDraft Prepare(
        RhinoDoc doc,
        FastenerComponentData original,
        FastenerComponentData component,
        bool dirty)
    {
        var revision = FastenerDocumentIndexService.CurrentRevision(doc);
        if (!FastenerGeometryPreparationService.TryPrepare(doc, component, out var prepared, out var message))
        {
            return new ContextualEditDraft(component, null, message, [], revision, dirty);
        }

        var ready = prepared!;
        var isDirty = dirty && ready.Draft != original;
        return new ContextualEditDraft(
            ready.Draft,
            ready,
            string.Empty,
            ready.Warnings,
            revision,
            isDirty);
    }

    private static void Raise(RhinoDoc doc, ContextualEditSession session) =>
        Changed?.Invoke(null, new ContextualEditSessionChangedEventArgs(doc, session));
}
