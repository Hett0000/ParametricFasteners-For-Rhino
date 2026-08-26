using Rhino;
using Rhino.DocObjects;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.Services;

/// <summary>
/// In-memory inverse log used when an operation participates in an existing
/// Rhino command undo record. It never commits data by itself.
/// </summary>
internal sealed class ComponentMutationJournal(RhinoDoc document, IEnumerable<Guid> componentIds)
{
    private readonly RhinoDoc _doc = document;
    private readonly List<Guid> _created = [];
    private readonly List<RhinoObject> _deleted = [];
    private readonly Dictionary<Guid, ObjectAttributes> _attributes = [];
    private readonly Dictionary<Guid, Guid[]> _groupMembers = componentIds
        .Distinct()
        .ToDictionary(
            id => id,
            id => ComponentRepository.FindComponentObjects(document, id)
                .Where(obj => obj.Attributes.GetUserString(ComponentRepository.RoleKey) != "ControlPoint")
                .Select(obj => obj.Id)
                .ToArray());
    private readonly int _initialMaterialCount = document.Materials.Count;
    private bool _completed;

    public void TrackCreated(Guid objectId)
    {
        if (objectId != Guid.Empty)
            _created.Add(objectId);
    }

    public bool Delete(RhinoObject obj)
    {
        if (_doc.Objects.FindId(obj.Id) is null)
            return true;
        if (!_doc.Objects.Delete(obj, true))
            return false;
        _deleted.Add(obj);
        return true;
    }

    public bool ModifyAttributes(RhinoObject obj, ObjectAttributes attributes, bool quiet)
    {
        _attributes.TryAdd(obj.Id, obj.Attributes.Duplicate());
        return _doc.Objects.ModifyAttributes(obj, attributes, quiet);
    }

    public void Complete() => _completed = true;

    public void Rollback()
    {
        if (_completed)
            return;
        foreach (var id in _created.AsEnumerable().Reverse())
        {
            if (_doc.Objects.FindId(id) is { } created)
                _doc.Objects.Delete(created, true);
        }
        foreach (var deleted in _deleted.AsEnumerable().Reverse())
        {
            if (deleted.IsDeleted)
                _doc.Objects.Undelete(deleted);
        }
        foreach (var pair in _attributes)
        {
            if (_doc.Objects.FindId(pair.Key) is { } obj)
                _doc.Objects.ModifyAttributes(obj, pair.Value, true);
        }
        foreach (var pair in _groupMembers)
        {
            ComponentPresentationService.RemoveGroup(_doc, pair.Key);
            var live = pair.Value.Where(id => _doc.Objects.FindId(id) is not null).ToArray();
            if (live.Length > 0)
                _doc.Groups.Add(ComponentPresentationService.GroupName(pair.Key), live);
        }
        for (var index = _doc.Materials.Count - 1; index >= _initialMaterialCount; index--)
            _doc.Materials.DeleteAt(index);
        _doc.Views.Redraw();
        _completed = true;
    }
}
