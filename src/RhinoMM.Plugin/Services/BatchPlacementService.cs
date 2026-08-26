using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.UI;

namespace RhinoMM.Plugin.Services;

internal enum BatchPlacementSourceKind
{
    Point,
    PointCloud,
    CurveEnd,
    CircleCenter,
    ArcCenter,
    BrepCircularEdge,
    PlanarFaceLoop
}

internal enum BatchPlacementStatus
{
    Valid,
    Warning,
    Failure
}

internal sealed record AssemblyPlacementSeed(
    int Number,
    Point3d Point,
    BatchPlacementSourceKind SourceKind,
    Guid SourceObjectId,
    string SourceLabel,
    Vector3d? AxisHint = null,
    Guid PlacementHostId = default,
    ComponentIndex SourceComponent = default,
    string Reliability = "明确选择");

internal sealed class BatchPlacementPreflightItem
{
    public BatchPlacementPreflightItem(
        AssemblyPlacementSeed candidate,
        BatchPlacementStatus status,
        string message,
        SmartPlacementPreview preview,
        AssemblySuggestion? suggestion = null)
    {
        Candidate = candidate;
        Status = status;
        Message = message;
        Preview = preview;
        Suggestion = suggestion;
        AdoptedLengthText = AssemblyLengthChoiceService.Format(
            suggestion?.SuggestedLength ?? preview.Draft?.Length ?? 0);
    }

    public AssemblyPlacementSeed Candidate { get; }
    public BatchPlacementStatus Status { get; set; }
    public string Message { get; set; }
    public SmartPlacementPreview Preview { get; set; }
    public AssemblySuggestion? Suggestion { get; }
    public double CurrentLength => Suggestion?.CurrentLength ?? Preview.Draft?.Length ?? 0;
    public double SuggestedLength => Suggestion?.SuggestedLength ?? CurrentLength;
    public string AdoptedLengthText { get; private set; }
    public bool CanEditLength => Suggestion is not null && !Suggestion.IsBlocked;
    public bool HasValidLength => !CanEditLength || AssemblyLengthChoiceService.TryParse(AdoptedLengthText, out _);
    public bool HasLengthChange => CanEditLength
        && TryAdoptedLength(out var value)
        && Math.Abs(value - CurrentLength) > 1e-6;
    public string NumberText => Candidate.Number.ToString();
    public string CoordinateText =>
        $"{Candidate.Point.X:0.###}, {Candidate.Point.Y:0.###}, {Candidate.Point.Z:0.###}";
    public string StatusText => Status switch
    {
        BatchPlacementStatus.Valid => "有效",
        BatchPlacementStatus.Warning => "警告",
        _ => "失败"
    };

    public void SetAdoptedLength(string text) => AdoptedLengthText = text.Trim();

    public bool TryAdoptedLength(out double value) =>
        AssemblyLengthChoiceService.TryParse(AdoptedLengthText, out value);
}

internal sealed record BatchPlacementPreflightResult(
    IReadOnlyList<BatchPlacementPreflightItem> Items,
    int DuplicateCount,
    int UnsupportedCount)
{
    public int ValidCount => Items.Count(item => item.Status == BatchPlacementStatus.Valid);
    public int WarningCount => Items.Count(item => item.Status == BatchPlacementStatus.Warning);
    public int FailureCount => Items.Count(item => item.Status == BatchPlacementStatus.Failure);
    public int CreatableCount => ValidCount + WarningCount;
}

internal static class BatchPlacementService
{
    public static IReadOnlyList<AssemblyPlacementSeed> ExtractCandidates(
        IEnumerable<ObjRef> references,
        double tolerance,
        out int duplicateCount,
        out int unsupportedCount)
    {
        var raw = new List<(Point3d Point, BatchPlacementSourceKind Kind, Guid ObjectId, string Label, Vector3d? Axis, Guid HostId, ComponentIndex Component)>();
        unsupportedCount = 0;
        foreach (var reference in references)
        {
            var obj = reference.Object();
            if (obj is null)
            {
                unsupportedCount++;
                continue;
            }
            var component = reference.GeometryComponentIndex;
            var edge = reference.Edge();
            if (edge is not null && edge.TryGetCircle(out var edgeCircle))
            {
                var faceIndex = edge.AdjacentFaces()
                    .Where(index => index >= 0 && index < edge.Brep.Faces.Count)
                    .Select(index => edge.Brep.Faces[index])
                    .Where(candidate => candidate.TryGetPlane(out _))
                    .Select(candidate => candidate.FaceIndex)
                    .DefaultIfEmpty(-1)
                    .First();
                if (faceIndex < 0)
                {
                    unsupportedCount++;
                    continue;
                }
                raw.Add((edgeCircle.Center, BatchPlacementSourceKind.BrepCircularEdge,
                    obj.Id, "圆形边", edgeCircle.Plane.Normal, obj.Id,
                    new ComponentIndex(ComponentIndexType.BrepFace, faceIndex)));
                continue;
            }
            var face = reference.Face();
            if (face is not null)
            {
                if (!TryExtractFaceLoops(face, obj.Id, component, raw))
                    unsupportedCount++;
                continue;
            }

            switch (obj.Geometry)
            {
                case Point point:
                    raw.Add((point.Location, BatchPlacementSourceKind.Point, obj.Id, "点", null, Guid.Empty, component));
                    break;
                case PointCloud cloud:
                    for (var index = 0; index < cloud.Count; index++)
                        raw.Add((cloud[index].Location, BatchPlacementSourceKind.PointCloud, obj.Id, $"点云 {index + 1}", null, Guid.Empty, component));
                    break;
                case Curve curve when curve.TryGetCircle(out var circle):
                    raw.Add((circle.Center, BatchPlacementSourceKind.CircleCenter, obj.Id, "圆心", circle.Plane.Normal, Guid.Empty, component));
                    break;
                case Curve curve when curve.TryGetArc(out var arc):
                    raw.Add((arc.Center, BatchPlacementSourceKind.ArcCenter, obj.Id, "圆弧中心", arc.Plane.Normal, Guid.Empty, component));
                    break;
                case Curve curve when !curve.IsClosed:
                    raw.Add((curve.PointAtStart, BatchPlacementSourceKind.CurveEnd, obj.Id, "曲线起点", null, Guid.Empty, component));
                    raw.Add((curve.PointAtEnd, BatchPlacementSourceKind.CurveEnd, obj.Id, "曲线终点", null, Guid.Empty, component));
                    break;
                default:
                    unsupportedCount++;
                    break;
            }
        }

        var unique = new List<(Point3d Point, BatchPlacementSourceKind Kind, Guid ObjectId, string Label, Vector3d? Axis, Guid HostId, ComponentIndex Component)>();
        var mergeTolerance = Math.Max(tolerance, RhinoMath.ZeroTolerance);
        duplicateCount = 0;
        foreach (var item in raw)
        {
            if (!item.Point.IsValid)
            {
                unsupportedCount++;
                continue;
            }
            if (unique.Any(existing => existing.Point.DistanceTo(item.Point) <= mergeTolerance))
            {
                duplicateCount++;
                continue;
            }
            unique.Add(item);
        }

        return unique.Select((item, index) => new AssemblyPlacementSeed(
            index + 1,
            item.Point,
            item.Kind,
            item.ObjectId,
            item.Label,
            item.Axis,
            item.HostId,
            item.Component)).ToArray();
    }

    public static BatchPlacementPreflightResult Preflight(
        RhinoDoc doc,
        IReadOnlyList<AssemblyPlacementSeed> candidates,
        FastenerTemplateData template,
        int duplicateCount,
        int unsupportedCount,
        SmartPlacementRecognitionMode recognitionMode = SmartPlacementRecognitionMode.Automatic,
        FastenerUiOperationContext? operation = null)
    {
        using var service = new SmartPlacementService(doc, template);
        var viewport = doc.Views.ActiveView?.ActiveViewport;
        var items = new List<BatchPlacementPreflightItem>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (operation is not null && !operation.Yield(
                    "批量点位预检",
                    items.Count,
                    candidates.Count,
                    $"点位 {candidate.Number}"))
                break;
            AssemblySuggestion? suggestion = null;
            var adoptedLength = template.Length;
            if (FastenerKindTraits.IsScrew(template.Kind)
                && service.TryMeasureSeed(candidate, viewport, out var measurement, out _))
            {
                suggestion = service.CreateSuggestion(measurement);
                if (!suggestion.IsBlocked && suggestion.RecommendedMode == template.AssemblyMode)
                    adoptedLength = suggestion.SuggestedLength;
            }
            var preview = service.EvaluateSeed(
                candidate,
                viewport,
                recognitionMode,
                FastenerKindTraits.IsScrew(template.Kind) ? adoptedLength : null);
            if (!preview.IsValid || preview.Draft is null)
            {
                var modeAdvice = suggestion is { IsBlocked: false, ChangesAssemblyMode: true }
                    ? $" 建议改用“{FastenerLabels.AssemblyMode(suggestion.RecommendedMode)}”，需整批确认后重新预检。"
                    : string.Empty;
                items.Add(new BatchPlacementPreflightItem(
                    candidate,
                    BatchPlacementStatus.Failure,
                    preview.Message + modeAdvice,
                    preview,
                    suggestion));
                continue;
            }
            var warnings = preview.Cutters
                .SelectMany(cutter => cutter.Warnings)
                .Distinct()
                .ToArray();
            items.Add(new BatchPlacementPreflightItem(
                candidate,
                warnings.Length > 0 || suggestion is { IsBlocked: false } && Math.Abs(adoptedLength - template.Length) > 1e-6
                    ? BatchPlacementStatus.Warning
                    : BatchPlacementStatus.Valid,
                warnings.Length > 0
                    ? string.Join(" ", warnings)
                    : suggestion is { IsBlocked: false }
                        ? suggestion.MeasurementBasis
                        : preview.Message,
                preview,
                suggestion));
        }
        return new BatchPlacementPreflightResult(items, duplicateCount, unsupportedCount);
    }

    public static bool RevalidateAdoptedLengths(
        RhinoDoc doc,
        IReadOnlyList<BatchPlacementPreflightItem> items,
        FastenerTemplateData template,
        SmartPlacementRecognitionMode recognitionMode,
        out IReadOnlyList<FastenerComponentData> drafts,
        out string message)
    {
        drafts = [];
        var result = new List<FastenerComponentData>();
        using var service = new SmartPlacementService(doc, template);
        var viewport = doc.Views.ActiveView?.ActiveViewport;
        foreach (var item in items)
        {
            if (!item.TryAdoptedLength(out var length))
            {
                message = $"点位 #{item.Candidate.Number} 的采用长度无效。";
                return false;
            }
            var preview = service.EvaluateSeed(
                item.Candidate,
                viewport,
                recognitionMode,
                length);
            if (!preview.IsValid || preview.Draft is null)
            {
                message = $"点位 #{item.Candidate.Number} 重新预检失败：{preview.Message}";
                return false;
            }
            result.Add(preview.Draft);
        }
        drafts = result;
        message = $"已重新验证 {result.Count} 个点位。";
        return true;
    }

    private static bool TryExtractFaceLoops(
        BrepFace face,
        Guid objectId,
        ComponentIndex component,
        ICollection<(Point3d Point, BatchPlacementSourceKind Kind, Guid ObjectId, string Label, Vector3d? Axis, Guid HostId, ComponentIndex Component)> output)
    {
        if (!face.TryGetPlane(out var plane))
            return false;
        var found = false;
        foreach (var loop in face.Loops.Where(item => item.LoopType == BrepLoopType.Inner))
        {
            var curves = loop.Trims
                .Select(trim => trim.Edge?.DuplicateCurve())
                .Where(curve => curve is not null)
                .Cast<Curve>()
                .ToArray();
            try
            {
                var joined = Curve.JoinCurves(curves);
                try
                {
                    if (joined.Length != 1 || !joined[0].TryGetCircle(out var circle))
                        continue;
                    output.Add((circle.Center, BatchPlacementSourceKind.PlanarFaceLoop,
                        objectId, "平面圆孔", plane.Normal, objectId, component));
                    found = true;
                }
                finally
                {
                    foreach (var curve in joined)
                        curve.Dispose();
                }
            }
            finally
            {
                foreach (var curve in curves)
                    curve.Dispose();
            }
        }
        return found;
    }
}
