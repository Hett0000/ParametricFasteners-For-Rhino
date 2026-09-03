using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class PluginSourceRegressionTests
{
    [Fact]
    public void Version032_AddsAtomicPointSetPlacementAndDocumentNavigator()
    {
        var batch = ReadSource("src", "RhinoMM.Plugin", "Commands", "BatchPlaceCommand.cs");
        var batchService = ReadSource("src", "RhinoMM.Plugin", "Services", "BatchPlacementService.cs");
        var batchDialog = ReadSource("src", "RhinoMM.Plugin", "UI", "BatchPlacementDialog.cs");
        var smart = ReadSource("src", "RhinoMM.Plugin", "Services", "SmartPlacementService.cs");
        var index = ReadSource("src", "RhinoMM.Plugin", "Services", "FastenerDocumentIndexService.cs");
        var cache = ReadSource("src", "RhinoMM.Plugin", "Services", "FastenerPreviewGeometryCache.cs");
        var navigator = ReadSource("src", "RhinoMM.Plugin", "UI", "ComponentNavigatorPanel.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var toolbar = ReadSource("build", "generate-toolbar.ps1");

        Assert.Contains("ParametricFastenersBatchPlace", batch, StringComparison.Ordinal);
        Assert.Contains("ObjectType.Point | ObjectType.PointSet | ObjectType.Curve", batch, StringComparison.Ordinal);
        Assert.Contains("CreateOrReplaceMany", batch, StringComparison.Ordinal);
        Assert.Contains("PointCloud", batchService, StringComparison.Ordinal);
        Assert.Contains("TryGetCircle", batchService, StringComparison.Ordinal);
        Assert.Contains("TryGetArc", batchService, StringComparison.Ordinal);
        Assert.Contains("ClientSize = new Size(DialogWidth, InitialHeight", batchDialog, StringComparison.Ordinal);
        Assert.Contains("new TableRow(_grid) { ScaleHeight = true }", batchDialog, StringComparison.Ordinal);
        Assert.Contains("Height = FooterHeight", batchDialog, StringComparison.Ordinal);
        Assert.Contains("FastenerThemeRole.StatusSuccess", batchDialog, StringComparison.Ordinal);
        Assert.Contains("FastenerThemeRole.StatusWarning", batchDialog, StringComparison.Ordinal);
        Assert.Contains("FastenerThemeRole.StatusError", batchDialog, StringComparison.Ordinal);
        Assert.Contains("_create.Text = $\"创建 {creatable} 项\"", batchDialog, StringComparison.Ordinal);
        Assert.Contains("采用长度 mm", batchDialog, StringComparison.Ordinal);
        Assert.Contains("全部采用建议", batchDialog, StringComparison.Ordinal);
        Assert.Contains("gridChromeAndScrollbarGutter = 24", batchDialog, StringComparison.Ordinal);
        Assert.DoesNotContain("new DynamicRow(\n                    _skipFailures", batchDialog, StringComparison.Ordinal);
        Assert.Contains("EvaluateAtPoint", smart, StringComparison.Ordinal);
        Assert.Contains("点位同时落在多个宿主表面", smart, StringComparison.Ordinal);
        Assert.Contains("new SmartPlacementHostIndex(doc)", smart, StringComparison.Ordinal);
        Assert.Contains("未能与实体精确相交", smart, StringComparison.Ordinal);
        Assert.Contains("RTree", index, StringComparison.Ordinal);
        Assert.Contains("doc.Objects.FindId(host.ObjectId)", index, StringComparison.Ordinal);
        Assert.Contains("BoxesIntersect(host.BoundingBox, searchBox)", index, StringComparison.Ordinal);
        Assert.Contains("SmartHostBindingService.CaptureHosts(doc)", index, StringComparison.Ordinal);
        Assert.DoesNotContain("host.Object.Document?.Objects.FindId", index, StringComparison.Ordinal);
        Assert.Contains("Capacity = 48", cache, StringComparison.Ordinal);
        Assert.Contains("Placement = FastenerGeometryFactory.FromPlane(Plane.WorldXY)", cache, StringComparison.Ordinal);
        Assert.DoesNotContain("CachedCutter", cache, StringComparison.Ordinal);
        Assert.Contains("ComponentHealthState", index, StringComparison.Ordinal);
        Assert.Contains("选择结果", navigator, StringComparison.Ordinal);
        Assert.Contains("深度检查", navigator, StringComparison.Ordinal);
        Assert.Contains("ComponentBooleanHealthService.Check", navigator, StringComparison.Ordinal);
        Assert.Contains("组件导航器与问题中心", panel, StringComparison.Ordinal);
        Assert.Contains("MouseButtons.Alternate", panel, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersBatchPlace", toolbar, StringComparison.Ordinal);
    }

    [Fact]
    public void PhaseOneQuickEditing_UsesSharedPreparationAndPersistentTemplates()
    {
        var quickService = ReadSource("src", "RhinoMM.Plugin", "Services", "ViewportQuickEditorService.cs");
        var quickWindow = ReadSource("src", "RhinoMM.Plugin", "UI", "ViewportQuickEditorWindow.cs");
        var handles = ReadSource("src", "RhinoMM.Plugin", "Commands", "ParameterHandleCommand.cs");
        var templates = ReadSource("src", "RhinoMM.Plugin", "Services", "FastenerTemplateLibraryService.cs");
        var repeat = ReadSource("src", "RhinoMM.Plugin", "Commands", "RepeatCommands.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");

        Assert.Contains("Interval = 0.25", quickService, StringComparison.Ordinal);
        Assert.Contains("ReadSelectedControlPoints", quickService, StringComparison.Ordinal);
        Assert.Contains("Command.BeginCommand", quickService.Replace("RhinoCommand", "Command"), StringComparison.Ordinal);
        Assert.Contains("TryReposition", quickService, StringComparison.Ordinal);
        Assert.Contains("QuickEditor.AutoShow", quickService, StringComparison.Ordinal);
        Assert.Contains("SuppressForComponent", quickService, StringComparison.Ordinal);
        Assert.DoesNotContain("EditorState.Current", quickWindow, StringComparison.Ordinal);
        Assert.Contains("ContextualEditSessionService.TrySetDraft", quickWindow, StringComparison.Ordinal);
        Assert.Contains("ContextualEditSessionService.TryCommit", quickWindow, StringComparison.Ordinal);
        Assert.Contains("CompactWidth = 268", quickWindow, StringComparison.Ordinal);
        Assert.Contains("Text = \"应用\"", quickWindow, StringComparison.Ordinal);
        Assert.Contains("Text = \"取消\"", quickWindow, StringComparison.Ordinal);
        Assert.Contains("$\"{shortKind} {size}X{CompactNumber(length)}\"", quickWindow, StringComparison.Ordinal);
        Assert.Contains("e.Key == Keys.Enter", quickWindow, StringComparison.Ordinal);
        Assert.Contains("e.Key == Keys.Escape", quickWindow, StringComparison.Ordinal);
        Assert.Contains("_preview.Set(session.Draft.Prepared)", quickWindow, StringComparison.Ordinal);
        Assert.Contains("ViewportQuickEditorService.SuppressForComponent", quickWindow, StringComparison.Ordinal);
        Assert.Contains("private readonly Button _read", quickWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly Button _handles", quickWindow, StringComparison.Ordinal);
        Assert.Contains("private readonly Button _delete", quickWindow, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersEditHandles", panel, StringComparison.Ordinal);
        Assert.Contains("class ParameterHandlePicker : GetPoint", handles, StringComparison.Ordinal);
        Assert.Contains("OnDynamicDraw", handles, StringComparison.Ordinal);
        Assert.Contains("SnapLength", handles, StringComparison.Ordinal);
        Assert.Contains("SnapEmbed", handles, StringComparison.Ordinal);
        Assert.Contains("SnapProtrusion", handles, StringComparison.Ordinal);
        Assert.Contains("SnapAngle", handles, StringComparison.Ordinal);
        Assert.Contains("RecentLimit = 5", templates, StringComparison.Ordinal);
        Assert.Contains("FavoriteLimit = 50", templates, StringComparison.Ordinal);
        Assert.Contains("CorruptBackup", templates, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersRepeatPlace", repeat, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersRepeatUpdate", repeat, StringComparison.Ordinal);
        Assert.Contains("_favoriteTemplateButton", panel, StringComparison.Ordinal);
        Assert.Contains("模板管理", panel, StringComparison.Ordinal);
        Assert.Contains("Text = \"快速小窗\"", panel, StringComparison.Ordinal);
        Assert.Contains("ViewportQuickEditorService.SetAutoShowEnabled", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("Text = \"自动快速编辑器\"", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicCommands_CallSharedHandlersWithoutNestedRunScript()
    {
        var source = ReadSource("src", "RhinoMM.Plugin", "Commands", "AliasCommands.cs");

        Assert.DoesNotContain("CommandAlias", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RunScript", source, StringComparison.Ordinal);
        Assert.Contains("SmartPlacementCommand.Execute(doc, mode)", source, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersPlaceClassic", source, StringComparison.Ordinal);
        Assert.Contains("RhinoMMPlaceHoleCommand.ExecuteClassic(doc, mode)", source, StringComparison.Ordinal);
        Assert.Contains("RhinoMMEditHoleCommand.Execute(doc, mode)", source, StringComparison.Ordinal);
        Assert.Contains("RhinoMMAdoptFastenerCommand.Execute(doc, mode)", source, StringComparison.Ordinal);
        Assert.Contains("RhinoMMValidateCommand.Execute(doc, mode)", source, StringComparison.Ordinal);
        Assert.Contains("RhinoMMExportPrintCommand.Execute(doc, mode)", source, StringComparison.Ordinal);
        Assert.Contains("RhinoMMExportToRhinoCommand.Execute(doc, mode, false)", source, StringComparison.Ordinal);
        Assert.Contains("RhinoMMExportToRhinoCommand.Execute(doc, mode, true)", source, StringComparison.Ordinal);
        Assert.Contains("PrintExportFormat.Stl", source, StringComparison.Ordinal);
        Assert.Contains("PrintExportFormat.Step", source, StringComparison.Ordinal);

        var editSource = ReadSource("src", "RhinoMM.Plugin", "Commands", "EditCommand.cs");
        Assert.DoesNotContain("RunScript", editSource, StringComparison.Ordinal);
        Assert.Contains("Panels.OpenPanel", editSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ResponsivePanel_ReusesOneParameterLayout()
    {
        var source = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var actions = ReadSource("src", "RhinoMM.Plugin", "UI", "FastenerUiActions.cs");

        Assert.Contains("_parameterLayout.Clear();", source, StringComparison.Ordinal);
        Assert.Contains("_parameterLayout.Create();", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_contentHost.Content = null", source, StringComparison.Ordinal);
        Assert.Contains("模板 ·", source, StringComparison.Ordinal);
        Assert.Contains("SelectedComponentSummary.Capture", source, StringComparison.Ordinal);
        Assert.Contains("Enabled = true", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_updateTimer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("参数修改后自动重建", source, StringComparison.Ordinal);
        Assert.Contains("嵌入深度 mm", source, StringComparison.Ordinal);
        Assert.Contains("private readonly CardSelector _kind", source, StringComparison.Ordinal);
        Assert.Contains("private readonly CardSelector _size", source, StringComparison.Ordinal);
        Assert.Contains("CommonLengths = [8, 10, 12, 15, 20, 25, 30, 35, 40, 45, 50, 60]", source, StringComparison.Ordinal);
        Assert.Contains("ResponsiveLayoutProfile.ForWidth(availableWidth)", source, StringComparison.Ordinal);
        Assert.Contains("_scrollable.ClientSize.Width", source, StringComparison.Ordinal);
        Assert.Contains("_kind.SetColumns(profile.KindColumns)", source, StringComparison.Ordinal);
        Assert.Contains("_size.SetColumns(profile.SizeColumns)", source, StringComparison.Ordinal);
        Assert.Contains("_lengthCards.SetColumns(profile.LengthColumns)", source, StringComparison.Ordinal);
        Assert.Contains("刷新 / 维护", actions, StringComparison.Ordinal);
        Assert.Contains("FastenerUiTheme.SectionTitle(\"基础尺寸\")", source, StringComparison.Ordinal);
        Assert.Contains("FastenerUiTheme.SectionTitle(\"孔与切割\")", source, StringComparison.Ordinal);
        Assert.DoesNotContain("宿主切割模块（", source, StringComparison.Ordinal);
        Assert.Contains("KindCardText", source, StringComparison.Ordinal);
        Assert.Contains("PanelActionIcon.Rhino", actions, StringComparison.Ordinal);
        Assert.Contains("PanelActionIcon.Step", actions, StringComparison.Ordinal);
        Assert.Contains("RebuildActionLayout", source, StringComparison.Ordinal);
        Assert.Contains("MinimumSize = new Size(0", source, StringComparison.Ordinal);
        Assert.Contains("descriptor.ToolTip", source, StringComparison.Ordinal);
        Assert.Contains("_-ParametricFastenersExportStl", source, StringComparison.Ordinal);
        Assert.Contains("_-ParametricFastenersExportStep", actions, StringComparison.Ordinal);
        Assert.Contains("RunExport", source, StringComparison.Ordinal);
        Assert.Contains("ApplyDraftForExport()", source, StringComparison.Ordinal);
        Assert.Contains("ComponentActivationIntent.SynchronizeOnly", source, StringComparison.Ordinal);
        Assert.Contains("_zeroHeadButton.Click", source, StringComparison.Ordinal);
        Assert.Contains("_zeroHeadButton.Enabled = supportsEmbedDepth", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_-ParametricFastenersExportExcel", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CardSelector_UsesEqualWidthAccessibleCards()
    {
        var source = ReadSource("src", "RhinoMM.Plugin", "UI", "CardSelector.cs");

        Assert.Contains("TableLayout", source, StringComparison.Ordinal);
        Assert.Contains("new TableCell(item.Button, true)", source, StringComparison.Ordinal);
        Assert.Contains("Content = layout", source, StringComparison.Ordinal);
        Assert.Contains("rowCount * CardHeight", source, StringComparison.Ordinal);
        Assert.Contains("MinimumSize = new Size(0, CardHeight)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new Panel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_layout.Rows.Clear()", source, StringComparison.Ordinal);
        Assert.Contains("CardHeight = FastenerUiTheme.ControlHeight", source, StringComparison.Ordinal);
        Assert.Contains("FastenerThemeRole.PrimaryAction", source, StringComparison.Ordinal);
        Assert.Contains("FastenerThemeRole.SecondaryAction", source, StringComparison.Ordinal);
        Assert.Contains("SystemFonts.Bold()", source, StringComparison.Ordinal);
        Assert.Contains("ApplySelectionStyle", source, StringComparison.Ordinal);
        Assert.DoesNotContain("row.Add(null!)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Presentation_UsesAttachedDocumentMaterialIndex()
    {
        var source = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentPresentationService.cs");
        var clone = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentCloneService.cs");
        var refresh = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentRefreshService.cs");

        Assert.Contains("attributes.MaterialIndex = materialIndex", source, StringComparison.Ordinal);
        Assert.DoesNotContain("attributes.RenderMaterial =", source, StringComparison.Ordinal);
        Assert.Contains("参数化紧固件::显示::紧固件", source, StringComparison.Ordinal);
        Assert.Contains("参数化紧固件::显示::切割模块", source, StringComparison.Ordinal);
        Assert.Contains("Math.Abs(existing.Transparency - transparency) <= 0.000001", source, StringComparison.Ordinal);
        Assert.Contains("doc.Materials.Add(material)", source, StringComparison.Ordinal);
        Assert.Contains("doc.Materials.Modify(material, index, true)", source, StringComparison.Ordinal);
        Assert.Contains("doc.Materials[index].RenderMaterial", source, StringComparison.Ordinal);
        Assert.Contains("PromoteLegacyMaterialsForCopy", clone, StringComparison.Ordinal);
        Assert.Contains("MigrateLegacyMaterialAssignments", refresh, StringComparison.Ordinal);
        Assert.Contains("CleanupUnusedLegacyMaterials", refresh, StringComparison.Ordinal);
        Assert.DoesNotContain("ObjectMode.Locked", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HeatSetInsertPanel_UsesCompactWidthConstrainedInformation()
    {
        var source = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");

        Assert.Contains("孔 Ø{finalDiameter:0.###} · 深 {finalDepth:0.###} mm", source, StringComparison.Ordinal);
        Assert.Contains("入口 45°×0.5 mm ⓘ", source, StringComparison.Ordinal);
        Assert.Contains("_insertSummaryHost = new() { MinimumSize = new Size(0, 0) }", source, StringComparison.Ordinal);
        Assert.Contains("_insertNoteHost = new() { MinimumSize = new Size(0, 0) }", source, StringComparison.Ordinal);
        Assert.Contains("AvailableContentWidth() - 32", source, StringComparison.Ordinal);
        Assert.Contains("if (profile.PairDimensionFields)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("最终安装孔：Ø", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Placement_RejectsExistingPluginGeometry()
    {
        var source = ReadSource("src", "RhinoMM.Plugin", "Commands", "PlaceCommand.cs");

        Assert.Contains("SetCustomGeometryFilter(IsHostGeometry)", source, StringComparison.Ordinal);
        Assert.Contains("GroupSelect = false", source, StringComparison.Ordinal);
        Assert.Contains("ComponentRepository.ComponentIdKey", source, StringComparison.Ordinal);
        Assert.Contains("AddOption(new LocalizeStringPair(\"SnapPoint\"", source, StringComparison.Ordinal);
        Assert.Contains("RhinoGet.GetPoint", source, StringComparison.Ordinal);
        Assert.Contains("FindClosestPlacementHost", source, StringComparison.Ordinal);
        Assert.Contains("OrientAxisIntoHost", source, StringComparison.Ordinal);
        Assert.Contains("multiSelect.CurrentValue ? go.GetMultiple(1, 0) : go.Get()", source, StringComparison.Ordinal);
        Assert.Contains("continuousPointPlacement", source, StringComparison.Ordinal);
        Assert.Contains("pointGetter.AcceptNothing(true)", source, StringComparison.Ordinal);
        Assert.Contains("var reusableAxis = plane.ZAxis", source, StringComparison.Ordinal);
        Assert.Contains("TryPlaceAt", source, StringComparison.Ordinal);
        Assert.Contains("placed.Add(nextSaved)", source, StringComparison.Ordinal);
        Assert.Contains("ExecuteSingleHostPlacement", source, StringComparison.Ordinal);
        Assert.Contains("FastenerKindTraits.UsesSingleHostPlacement", source, StringComparison.Ordinal);
        Assert.Contains("ShaftFitRole.InstallationPocket", source, StringComparison.Ordinal);
        Assert.Contains("face.Brep.IsSolid", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmartPlacement_PreviewsAndClassifiesVisibleHostsWithoutDocumentObjects()
    {
        var command = ReadSource("src", "RhinoMM.Plugin", "Commands", "SmartPlaceCommand.cs");
        var service = ReadSource("src", "RhinoMM.Plugin", "Services", "SmartPlacementService.cs");
        var aliases = ReadSource("src", "RhinoMM.Plugin", "Commands", "AliasCommands.cs");

        Assert.Contains("class SmartPlacementGetter : GetPoint", command, StringComparison.Ordinal);
        Assert.Contains("OnMouseMove", command, StringComparison.Ordinal);
        Assert.Contains("OnDynamicDraw", command, StringComparison.Ordinal);
        Assert.Contains("PointOnObject()", command, StringComparison.Ordinal);
        Assert.Contains("DrawBrepShaded", command, StringComparison.Ordinal);
        Assert.Contains("DrawBrepWires", command, StringComparison.Ordinal);
        Assert.Contains("DrawStatusHud", command, StringComparison.Ordinal);
        Assert.Contains("e.Viewport.Size", command, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawDot(\n            preview.Anchor", command, StringComparison.Ordinal);
        Assert.Contains("RefreshForCommit", command, StringComparison.Ordinal);
        Assert.Contains("FastenerGeometryParameters.Match(preview.Draft, saved)", command, StringComparison.Ordinal);
        Assert.Contains("SmartPlacementRecognitionMode", command, StringComparison.Ordinal);
        Assert.Contains("FastenerComponentService.CreateOrReplace", command, StringComparison.Ordinal);
        Assert.Contains("SmartPlacementParameterSnapshot.Capture", service, StringComparison.Ordinal);
        Assert.Contains("parameters.CreateDraft", service, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly FastenerSizeSpec _spec", service, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly PlacementCutterPreset _preset", service, StringComparison.Ordinal);
        Assert.Contains("SmartHostClassifier.Classify", service, StringComparison.Ordinal);
        var hostBinding = ReadSource(
            "src", "RhinoMM.Plugin", "Services", "SmartHostBindingService.cs");
        Assert.Contains("FastenerGeometryFactory.TryGetTargetInterval", hostBinding, StringComparison.Ordinal);
        Assert.Contains("SmartHostBindingService.FindIntervals", service, StringComparison.Ordinal);
        Assert.Contains("|| usedFallback", hostBinding, StringComparison.Ordinal);
        var pickRay = ReadSource("src", "RhinoMM.Plugin", "Services", "ViewportPickRayService.cs");
        var hostIndex = ReadSource("src", "RhinoMM.Plugin", "Services", "SmartPlacementHostIndex.cs");
        Assert.Contains("ClientToWorld(clientPoint)", pickRay, StringComparison.Ordinal);
        Assert.Contains("WorldToClient", pickRay, StringComparison.Ordinal);
        Assert.DoesNotContain("ClientToScreen", pickRay, StringComparison.Ordinal);
        Assert.DoesNotContain("GetFrustumLine", pickRay, StringComparison.Ordinal);
        Assert.DoesNotContain("GetFrustumLine", service, StringComparison.Ordinal);
        Assert.DoesNotContain("ClientToWorld", service, StringComparison.Ordinal);
        Assert.Contains("Math.Min(line.From.X, line.To.X)", hostIndex, StringComparison.Ordinal);
        Assert.Contains("Math.Max(line.From.X, line.To.X)", hostIndex, StringComparison.Ordinal);
        Assert.DoesNotContain("new BoundingBox(line.From, line.To)", hostIndex, StringComparison.Ordinal);
        Assert.Contains("Intersection.CurveBrep(", service, StringComparison.Ordinal);
        Assert.DoesNotContain("Intersection.CurveBrepFace", service, StringComparison.Ordinal);
        Assert.Contains("point - ray.From", service, StringComparison.Ordinal);
        Assert.Contains("存在深度重合的多个实体面", service, StringComparison.Ordinal);
        Assert.Contains("ComponentHostResolver.IsOrdinaryHost", hostBinding, StringComparison.Ordinal);
        Assert.Contains("!obj.IsLocked", hostBinding, StringComparison.Ordinal);
        Assert.Contains("FastenerGeometryPreparationService.TryPrepare", service, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersPlaceClassic", aliases, StringComparison.Ordinal);
        Assert.Contains("SmartPlacementCommand.Execute", aliases, StringComparison.Ordinal);

        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        Assert.Contains("SmartPlacementDraftChangeService.NotifyChanged", panel, StringComparison.Ordinal);
        Assert.Contains("RhinoDoc.ActiveDoc?.Views.Redraw()", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void EditorState_LoadsFitValuesFromTheirMatchingBindingRoles()
    {
        var source = ReadSource("src", "RhinoMM.Plugin", "Services", "EditorState.cs");

        Assert.Contains("binding.Role == ShaftFitRole.Clearance", source, StringComparison.Ordinal);
        Assert.Contains("binding.Role == ShaftFitRole.ThreadEngagement", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BiteReduction = component.Bindings[0].BiteReduction", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ComponentPresentation_HasASeparateControlPointRole()
    {
        var presentation = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentPresentationService.cs");
        var componentService = ReadSource("src", "RhinoMM.Plugin", "Services", "FastenerComponentService.cs");

        Assert.Contains("参数化紧固件::控制点", presentation, StringComparison.Ordinal);
        Assert.Contains("ObjectMode.Normal", presentation, StringComparison.Ordinal);
        Assert.Contains("doc.Objects.AddPoint", componentService, StringComparison.Ordinal);
    }

    [Fact]
    public void ControlPointDeletion_CascadesWithInternalMutationSuppression()
    {
        var lifecycle = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentLifecycleService.cs");
        var componentService = ReadSource("src", "RhinoMM.Plugin", "Services", "FastenerComponentService.cs");

        Assert.Contains("RhinoDoc.DeleteRhinoObject +=", lifecycle, StringComparison.Ordinal);
        Assert.Contains("RoleKey) != \"ControlPoint\"", lifecycle, StringComparison.Ordinal);
        Assert.Contains("FindComponentObjects", lifecycle, StringComparison.Ordinal);
        Assert.Contains("ForgetComponent", lifecycle, StringComparison.Ordinal);
        Assert.Contains("ComponentLifecycleService.Suppress", componentService, StringComparison.Ordinal);
        Assert.Contains("RhinoDoc.SelectObjects +=", lifecycle, StringComparison.Ordinal);
        Assert.Contains("is \"Proxy\" or \"Cutter\" or \"HeadCutter\"", lifecycle, StringComparison.Ordinal);
        Assert.Contains("FindControlPoint", lifecycle, StringComparison.Ordinal);
    }

    [Fact]
    public void ComponentMove_SuppressesCascadeAndRebindsTransformedObjects()
    {
        var lifecycle = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentLifecycleService.cs");

        Assert.Contains("RhinoDoc.BeforeTransformObjects +=", lifecycle, StringComparison.Ordinal);
        Assert.Contains("RhinoDoc.AddRhinoObject +=", lifecycle, StringComparison.Ordinal);
        Assert.Contains("RhinoDoc.AfterTransformObjects +=", lifecycle, StringComparison.Ordinal);
        Assert.Contains("Command.EndCommand +=", lifecycle, StringComparison.Ordinal);
        Assert.Contains("e.ObjectsWillBeCopied", lifecycle, StringComparison.Ordinal);
        Assert.Contains("IsPendingTransform(doc, componentId)", lifecycle, StringComparison.Ordinal);
        Assert.Contains("doc.UndoActive", lifecycle, StringComparison.Ordinal);
        Assert.Contains("doc.RedoActive", lifecycle, StringComparison.Ordinal);
        Assert.Contains("doc.Objects.Transform(oldId, pending.Transform, true)", lifecycle, StringComparison.Ordinal);
        Assert.Contains("TargetIdMap", lifecycle, StringComparison.Ordinal);
        Assert.Contains("PlacementTransformService.TryTransform", lifecycle, StringComparison.Ordinal);
        Assert.Contains("ControlPointObjectId = controlPoint.Id", lifecycle, StringComparison.Ordinal);
        Assert.Contains("ComponentEditorSession.UpdateCache", lifecycle, StringComparison.Ordinal);
        Assert.Contains("e.CommandEnglishName is not (\"Undo\" or \"Redo\")", lifecycle, StringComparison.Ordinal);
        Assert.Contains("ComponentEditorSession.ActivateMany", lifecycle, StringComparison.Ordinal);
        Assert.Contains("sender as RhinoDoc ?? e.Objects.FirstOrDefault()?.Document", lifecycle, StringComparison.Ordinal);
        Assert.Contains("CompletePendingTransform", lifecycle, StringComparison.Ordinal);
    }

    [Fact]
    public void ComponentCopyPasteAndImport_CreateIndependentComponents()
    {
        var lifecycle = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentLifecycleService.cs");
        var clone = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentCloneService.cs");
        var models = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentTransformModels.cs");

        Assert.Contains("ComponentTransformKind.Copy", lifecycle, StringComparison.Ordinal);
        Assert.Contains("Command.BeginCommand += BeginCommand", lifecycle, StringComparison.Ordinal);
        Assert.Contains("\"Paste\" => ComponentTransformKind.Paste", lifecycle, StringComparison.Ordinal);
        Assert.Contains("\"Import\" => ComponentTransformKind.Import", lifecycle, StringComparison.Ordinal);
        Assert.Contains("NormalizeCopiedComponents", lifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("_synchronizingTransform || e.ObjectsWillBeCopied", lifecycle, StringComparison.Ordinal);
        Assert.Contains("Guid.NewGuid()", clone, StringComparison.Ordinal);
        Assert.Contains("bindingIdMap", clone, StringComparison.Ordinal);
        Assert.Contains("DirectTargetMap", clone, StringComparison.Ordinal);
        Assert.Contains("CaptureExternalGroups", clone, StringComparison.Ordinal);
        Assert.Contains("RestoreExternalGroups", clone, StringComparison.Ordinal);
        Assert.Contains("DeleteRawCopies", clone, StringComparison.Ordinal);
        Assert.Contains("IsRigidTransform(e.Transform)", lifecycle, StringComparison.Ordinal);
        Assert.Contains("旋转与镜像已完整支持", lifecycle, StringComparison.Ordinal);
        var placementTransform = ReadSource("src", "RhinoMM.Plugin", "Services", "PlacementTransformService.cs");
        Assert.Contains("var zAxis = new Vector3d(source.ZAxisX", placementTransform, StringComparison.Ordinal);
        Assert.Contains("Vector3d.CrossProduct(zAxis, xAxis)", placementTransform, StringComparison.Ordinal);
        Assert.Contains("ComponentClonePlan", models, StringComparison.Ordinal);
        Assert.Contains("ComponentCloneResult", models, StringComparison.Ordinal);
    }

    [Fact]
    public void UnresolvedCopiesAreRetainedButBlockedUntilRefreshRelinksThem()
    {
        var clone = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentCloneService.cs");
        var resolver = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentHostResolver.cs");
        var refresh = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentRefreshService.cs");
        var export = ReadSource("src", "RhinoMM.Plugin", "Services", "BooleanExportService.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");

        Assert.Contains("TargetObjectId = Guid.Empty", clone, StringComparison.Ordinal);
        Assert.Contains("CreateUnresolved", clone, StringComparison.Ordinal);
        Assert.Contains("NeedsRelink", resolver, StringComparison.Ordinal);
        Assert.Contains("PendingRelinkComponents", refresh, StringComparison.Ordinal);
        Assert.Contains("未选宿主绑定失效，已按范围忽略", export, StringComparison.Ordinal);
        Assert.Contains("_selectedSummary.HasBlockingIssues", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void BlindDepth_StopsAtExactLimitWithoutExitPadding()
    {
        var service = ReadSource("src", "RhinoMM.Plugin", "Services", "CutterGeometryService.cs");

        Assert.Contains("DepthMode.FastenerLengthPlusOneDiameter", ReadSource(
            "src", "RhinoMM.Core", "Services", "HoleDepthCalculator.cs"), StringComparison.Ordinal);
        Assert.Contains("end = reachesExit", service, StringComparison.Ordinal);
        Assert.Contains(": limit;", service, StringComparison.Ordinal);
        Assert.DoesNotContain("limit + padding", service, StringComparison.Ordinal);
        Assert.Contains("计算深度超过宿主厚度，将贯穿", service, StringComparison.Ordinal);
    }

    [Fact]
    public void ThroughCuttersUseFullCircularFootprintEnvelope()
    {
        var cutter = ReadSource("src", "RhinoMM.Plugin", "Services", "CutterGeometryService.cs");
        var envelope = ReadSource(
            "src", "RhinoMM.Plugin", "Services", "CutterFootprintEnvelopeService.cs");

        Assert.Contains("CutterFootprintEnvelopeService.TryGet", cutter, StringComparison.Ordinal);
        Assert.Contains("Brep.CreateBooleanIntersection", envelope, StringComparison.Ordinal);
        Assert.Contains("minimum = Math.Min", envelope, StringComparison.Ordinal);
        Assert.Contains("maximum = Math.Max", envelope, StringComparison.Ordinal);
        Assert.Contains("避免倾斜背面残留", envelope, StringComparison.Ordinal);
    }

    [Fact]
    public void BlindEngagementCuttersClearObliqueEntranceButKeepExactBottom()
    {
        var cutter = ReadSource("src", "RhinoMM.Plugin", "Services", "CutterGeometryService.cs");

        var footprintIndex = cutter.IndexOf(
            "CutterFootprintEnvelopeService.TryGet",
            StringComparison.Ordinal);
        var throughIndex = cutter.IndexOf(
            "var isThrough",
            StringComparison.Ordinal);
        Assert.True(footprintIndex >= 0 && footprintIndex < throughIndex);
        Assert.Contains("var start = footprint.Min - padding", cutter, StringComparison.Ordinal);
        Assert.Contains("limit >= footprint.Max", cutter, StringComparison.Ordinal);
        Assert.Contains("end = reachesExit ? footprint.Max + padding : limit", cutter, StringComparison.Ordinal);
        Assert.DoesNotContain("start = Math.Max(start, -padding)", cutter, StringComparison.Ordinal);
    }

    [Fact]
    public void PanelHidesLegacyClearanceFitSelector()
    {
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");

        Assert.DoesNotContain("_clearanceFit", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("通孔配合", panel, StringComparison.Ordinal);
        Assert.Contains("通孔 Ø", panel, StringComparison.Ordinal);
        Assert.Contains("咬合 Ø", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void Refresh_RepairsMovedComponentsAndOnlyDeletesMissingControlPoints()
    {
        var refresh = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentRefreshService.cs");

        Assert.Contains("GroupBy", refresh, StringComparison.Ordinal);
        Assert.Contains("controlPoint is null", refresh, StringComparison.Ordinal);
        Assert.Contains("obj.Geometry is Point", refresh, StringComparison.Ordinal);
        Assert.Contains("ComponentLifecycleService.Suppress", refresh, StringComparison.Ordinal);
        Assert.Contains("ComponentPresentationService.RemoveGroup", refresh, StringComparison.Ordinal);
        Assert.Contains("doc.BeginUndoRecord", refresh, StringComparison.Ordinal);
        Assert.Contains("RefreshAndRepair", refresh, StringComparison.Ordinal);
        Assert.Contains("actualPoint", refresh, StringComparison.Ordinal);
        Assert.Contains("FindUniqueReplacementHost", refresh, StringComparison.Ordinal);
        Assert.Contains("FastenerComponentService.CreateOrReplaceMany", refresh, StringComparison.Ordinal);
        Assert.Contains("ComponentsNeedingRelink", refresh, StringComparison.Ordinal);
        Assert.Contains("FailedComponents", refresh, StringComparison.Ordinal);
    }

    [Fact]
    public void CountersunkGeometry_UsesMatchingFrustumsForProxyAndCutter()
    {
        var geometry = ReadSource("src", "RhinoMM.Plugin", "Geometry", "FastenerGeometryFactory.cs");
        var calculator = ReadSource("src", "RhinoMM.Core", "Services", "HeadGeometryCalculator.cs");

        Assert.Contains("CreateFrustum", geometry, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateCone", geometry, StringComparison.Ordinal);
        Assert.Contains("(spec.Head.CountersunkDiameter - spec.NominalDiameter) / 2", calculator, StringComparison.Ordinal);
        Assert.Contains("GetCountersunkSeatProfile", geometry, StringComparison.Ordinal);
        Assert.Contains("EnsureOutward", geometry, StringComparison.Ordinal);
        Assert.Contains("BrepSolidOrientation.Inward", geometry, StringComparison.Ordinal);
    }

    [Fact]
    public void ProxyGeometry_ValidatesUnionAndKeepsShaftAsStablePrimaryPart()
    {
        var geometry = ReadSource("src", "RhinoMM.Plugin", "Geometry", "FastenerGeometryFactory.cs");
        var service = ReadSource("src", "RhinoMM.Plugin", "Services", "FastenerComponentService.cs");
        var repository = ReadSource("src", "RhinoMM.Plugin", "Persistence", "ComponentRepository.cs");

        Assert.Contains("data.Length + overlap", geometry, StringComparison.Ordinal);
        Assert.Contains("CoversExpectedAxialSpan", geometry, StringComparison.Ordinal);
        Assert.DoesNotContain("Brep.JoinBreps", geometry, StringComparison.Ordinal);
        Assert.Contains("ProxyPartKey", service, StringComparison.Ordinal);
        Assert.Contains("proxyIndex == 0", service, StringComparison.Ordinal);
        Assert.Contains("ProxyPartKey) == \"Shaft\"", repository, StringComparison.Ordinal);
    }

    [Fact]
    public void BatchUpdate_UnifiesModuleSettingsByBindingRole()
    {
        var template = ReadSource("src", "RhinoMM.Core", "Services", "FastenerUpdateTemplate.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");

        Assert.Contains("ShaftFitRole.Clearance =>", template, StringComparison.Ordinal);
        Assert.Contains("ShaftFitRole.ThreadEngagement =>", template, StringComparison.Ordinal);
        Assert.Contains("DepthMode = EngagementDepthMode", template, StringComparison.Ordinal);
        Assert.Contains("previewVisible ?? binding.IsPreviewVisible", template, StringComparison.Ordinal);
        Assert.Contains("booleanEnabled ?? binding.IsBooleanEnabled", template, StringComparison.Ordinal);
        Assert.Contains("Select(component => template.ApplyTo(component))", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("scrollingContent.AddRow(_moduleCard)", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void CompactFooter_UsesEightMultiResolutionIconButtons()
    {
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var icons = ReadSource("src", "RhinoMM.Plugin", "UI", "PanelIconProvider.cs");
        var theme = ReadSource("src", "RhinoMM.Plugin", "UI", "FastenerUiTheme.cs");
        var actionDefinitions = ReadSource("src", "RhinoMM.Plugin", "UI", "FastenerUiActions.cs");

        foreach (var icon in new[] { "Place", "Apply", "Refresh", "Rhino", "Step", "Statistics", "Inspector", "More" })
            Assert.Contains($"PanelActionIcon.{icon}", actionDefinitions, StringComparison.Ordinal);
        Assert.DoesNotContain("PanelActionIcon.Read,", actionDefinitions, StringComparison.Ordinal);
        Assert.Contains("[(1f, 24), (1.5f, 36), (2f, 48)]", icons, StringComparison.Ordinal);
        Assert.Contains("IconFrame", icons, StringComparison.Ordinal);
        Assert.Contains("LogicalSize = 24", icons, StringComparison.Ordinal);
        Assert.Contains("GetManifestResourceStream", icons, StringComparison.Ordinal);
        Assert.Contains("darkMode ? \"dark\" : \"light\"", icons, StringComparison.Ordinal);
        Assert.Contains("-dark-", icons, StringComparison.Ordinal);
        Assert.DoesNotContain("inverse", icons, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FastenerThemeRole.PrimaryAction", panel, StringComparison.Ordinal);
        Assert.Contains("FastenerThemeRole.SecondaryAction", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("new Graphics", icons, StringComparison.Ordinal);
        Assert.DoesNotContain("ArtworkScale", icons, StringComparison.Ordinal);
        Assert.Contains("ActionButtonHeight = FastenerUiMetrics.ActionButtonHeight", theme, StringComparison.Ordinal);
        Assert.Contains("FastenerUiActionCoordinator.Get", panel, StringComparison.Ordinal);
        Assert.Contains("descriptor.Group != previousGroup", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("FooterSingleRowBreakpoint", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void RhinoTheme_FollowsHostAndRefreshesOpenWindows()
    {
        var theme = ReadSource("src", "RhinoMM.Plugin", "UI", "FastenerUiTheme.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var dialog = ReadSource("src", "RhinoMM.Plugin", "UI", "FastenerStatisticsDialog.cs");
        var generator = ReadSource("build", "generate-icon-assets.cjs");

        Assert.Contains("HostUtils.RunningInDarkMode", theme, StringComparison.Ordinal);
        Assert.Contains("AppearanceSettings.GetPaintColor", theme, StringComparison.Ordinal);
        Assert.Contains("PaintColor.PanelBackground", theme, StringComparison.Ordinal);
        Assert.Contains("PaintColor.EditBoxBackground", theme, StringComparison.Ordinal);
        Assert.Contains("PaintColor.TextEnabled", theme, StringComparison.Ordinal);
        Assert.Contains("PaintColor.TextDisabled", theme, StringComparison.Ordinal);
        Assert.Contains("PaintColor.GridLinesOnPanelBackground", theme, StringComparison.Ordinal);
        Assert.Contains("PaintColor.InactiveTabBackground", theme, StringComparison.Ordinal);
        Assert.Contains("ConditionalWeakTable<Control, ThemeRoleHolder>", theme, StringComparison.Ordinal);
        Assert.Contains("root.VisualControls", theme, StringComparison.Ordinal);
        Assert.Contains("EnsureContrast", theme, StringComparison.Ordinal);
        Assert.Contains("Style = Panels.EtoPanelStyleName", panel, StringComparison.Ordinal);
        Assert.Contains("RhinoApp.AppSettingsChanged += RhinoAppSettingsChanged", panel, StringComparison.Ordinal);
        Assert.Contains("RhinoApp.AppSettingsChanged -= RhinoAppSettingsChanged", panel, StringComparison.Ordinal);
        Assert.Contains("FastenerUiTheme.WatchWindow(this", dialog, StringComparison.Ordinal);
        Assert.Contains("_scopeSelector.RefreshTheme()", dialog, StringComparison.Ordinal);
        Assert.Contains("window.UseRhinoStyle()", theme, StringComparison.Ordinal);
        Assert.Contains("suffix: '-dark'", generator, StringComparison.Ordinal);
        Assert.Contains("color: '#D7DEE8'", generator, StringComparison.Ordinal);
    }

    [Fact]
    public void ResponsiveProfile_UsesProfessionalFullWidthGrid()
    {
        var profile = ReadSource("src", "RhinoMM.Plugin", "UI", "ResponsiveLayoutProfile.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");

        Assert.Contains("NarrowBreakpoint = 280", profile, StringComparison.Ordinal);
        Assert.Contains("CompactBreakpoint = 320", profile, StringComparison.Ordinal);
        Assert.Contains("WideBreakpoint = 420", profile, StringComparison.Ordinal);
        Assert.Contains("new ResponsiveLayoutProfile(5, 5, 6, true, 3", profile, StringComparison.Ordinal);
        Assert.Contains("new ResponsiveLayoutProfile(5, 5, 6, false, 1", profile, StringComparison.Ordinal);
        Assert.Contains("NumericFieldWidth", profile, StringComparison.Ordinal);
        Assert.Contains("DepthFieldWidth", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("DepthPresetRow()", panel, StringComparison.Ordinal);
        Assert.Contains("var assemblyMode = SelectedAssemblyMode()", panel, StringComparison.Ordinal);
        Assert.Contains("_assemblyLayout.AddRow(_assemblyMode)", panel, StringComparison.Ordinal);
        Assert.Contains("FieldStack(\"追加深度 mm\", _presetBlindDepth)", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void Toolbar_UsesRhino8LightAndDarkSvgIcons()
    {
        var toolbar = ReadSource("build", "generate-toolbar.ps1");

        Assert.Contains("UI\\Icons\\Source", toolbar, StringComparison.Ordinal);
        Assert.Contains("major_ver=\"8\"", toolbar, StringComparison.Ordinal);
        Assert.Contains("plug_in_guid=\"ddc747eb-360e-4629-b65b-6bb1ddb4dc8f\"", toolbar, StringComparison.Ordinal);
        Assert.Contains("<icons>", toolbar, StringComparison.Ordinal);
        Assert.Contains("<light>$light</light>", toolbar, StringComparison.Ordinal);
        Assert.Contains("<dark>$dark</dark>", toolbar, StringComparison.Ordinal);
        Assert.Contains("$lightIconColor = \"#34495E\"", toolbar, StringComparison.Ordinal);
        Assert.Contains("$darkIconColor = \"#D7DEE8\"", toolbar, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawImageUnscaled", toolbar, StringComparison.Ordinal);
    }

    [Fact]
    public void FileAndInDocumentExports_ShareOneBooleanPipeline()
    {
        var service = ReadSource("src", "RhinoMM.Plugin", "Services", "BooleanExportService.cs");
        var rhinoPlacement = ReadSource("src", "RhinoMM.Plugin", "Services", "RhinoPlacementExportService.cs");
        var fileExport = ReadSource("src", "RhinoMM.Plugin", "Commands", "ExportPrintCommand.cs");
        var rhinoExport = ReadSource("src", "RhinoMM.Plugin", "Commands", "ExportToRhinoCommand.cs");

        Assert.Contains("BooleanExportService.TryBuild", fileExport, StringComparison.Ordinal);
        Assert.Contains("BooleanExportService.TryBuild", rhinoPlacement, StringComparison.Ordinal);
        Assert.Contains("RhinoPlacementExportService.TryBuild", rhinoExport, StringComparison.Ordinal);
        Assert.Contains("ComponentRepository.ReadAllControlPoints", service, StringComparison.Ordinal);
        Assert.Contains("CutterGeometryService.TryBuild", service, StringComparison.Ordinal);
        Assert.Contains("selectedHostIds", service, StringComparison.Ordinal);
        Assert.DoesNotContain("TryResolveBooleanState", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CutterParameterComparer", service, StringComparison.Ordinal);
        Assert.Contains("Brep.CreateBooleanDifference", service, StringComparison.Ordinal);
        Assert.Contains("Brep.CreateBooleanUnion", service, StringComparison.Ordinal);
        Assert.Contains("booleanCutters", service, StringComparison.Ordinal);
        Assert.Contains("SaveFileDialog", fileExport, StringComparison.Ordinal);
        Assert.DoesNotContain("RhinoGet.GetString", fileExport, StringComparison.Ordinal);
        Assert.Contains("没有启用的补偿切割模块", service, StringComparison.Ordinal);
        Assert.Contains("result.Warnings", fileExport, StringComparison.Ordinal);
        Assert.Contains("result.Warnings", rhinoExport, StringComparison.Ordinal);
    }

    [Fact]
    public void RhinoPlacementExport_CanIncludeRelatedRenderFastenersWithSharedPbrMaterials()
    {
        var placement = ReadSource("src", "RhinoMM.Plugin", "Services", "RhinoPlacementExportService.cs");
        var presentation = ReadSource("src", "RhinoMM.Plugin", "Services", "RhinoRenderExportPresentationService.cs");
        var command = ReadSource("src", "RhinoMM.Plugin", "Commands", "ExportToRhinoCommand.cs");
        var aliases = ReadSource("src", "RhinoMM.Plugin", "Commands", "AliasCommands.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var actions = ReadSource("src", "RhinoMM.Plugin", "UI", "FastenerUiActions.cs");

        Assert.Contains("RhinoPlacementExportOptions", placement, StringComparison.Ordinal);
        Assert.Contains("includeFastenerSolids", command, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersExportToRhinoWithFasteners", aliases, StringComparison.Ordinal);
        Assert.Contains("MouseButtons.Alternate", panel, StringComparison.Ordinal);
        Assert.Contains("e.Handled = true", panel, StringComparison.Ordinal);
        Assert.Contains("Application.Instance.AsyncInvoke", panel, StringComparison.Ordinal);
        Assert.Contains("左击：仅布尔宿主", actions, StringComparison.Ordinal);
        Assert.Contains("右击：布尔宿主 + 紧固件实体", actions, StringComparison.Ordinal);
        Assert.DoesNotContain("RhinoPlacementExportSettingsService", panel, StringComparison.Ordinal);
        Assert.Contains("booleanResult.RelatedComponents", placement, StringComparison.Ordinal);
        Assert.Contains("DistinctBy(component => component.ComponentId)", placement, StringComparison.Ordinal);
        Assert.Contains("FastenerGeometryFactory.CreateProxy", placement, StringComparison.Ordinal);
        Assert.Contains("FastenerKind.HeatSetInsert", placement, StringComparison.Ordinal);
        Assert.Contains("参数化紧固件::渲染紧固件", presentation, StringComparison.Ordinal);
        Assert.Contains("参数化紧固件::拉丝钢", presentation, StringComparison.Ordinal);
        Assert.Contains("参数化紧固件::黄铜", presentation, StringComparison.Ordinal);
        Assert.Contains("ToPhysicallyBased", presentation, StringComparison.Ordinal);
        Assert.Contains("Metallic = 1.0", presentation, StringComparison.Ordinal);
        Assert.Contains("BooleanResult.BottomCenter", placement, StringComparison.Ordinal);
        Assert.Contains("result.FastenerBodies", command, StringComparison.Ordinal);
        Assert.Contains("EnsureResources", command, StringComparison.Ordinal);
        Assert.Contains("RollbackCreatedResources", command, StringComparison.Ordinal);
    }

    [Fact]
    public void OpacityControls_AreGlobalPersistentAndUpdateTheDocumentInOneTransaction()
    {
        var settings = ReadSource("src", "RhinoMM.Plugin", "Services", "GlobalDisplaySettings.cs");
        var presentation = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentPresentationService.cs");
        var editor = ReadSource("src", "RhinoMM.Plugin", "Services", "EditorState.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var dialog = ReadSource(
            "src", "RhinoMM.Plugin", "UI", "GlobalDisplaySettingsDialog.cs");
        var plugin = ReadSource("src", "RhinoMM.Plugin", "RhinoMMPlugIn.cs");

        Assert.Contains("new(70, 35)", settings, StringComparison.Ordinal);
        Assert.Contains("settings.GetDouble", settings, StringComparison.Ordinal);
        Assert.Contains("settings.SetDouble", settings, StringComparison.Ordinal);
        Assert.Contains("GlobalDisplaySettingsService.Load(Settings)", plugin, StringComparison.Ordinal);
        Assert.Contains("GlobalDisplaySettingsDialog.Show", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("使用说明", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyGlobalDisplaySettings", panel, StringComparison.Ordinal);
        Assert.Contains("应用于当前文档全部参数化组件", dialog, StringComparison.Ordinal);
        Assert.Contains("ComponentRepository.ReadAllControlPoints", dialog, StringComparison.Ordinal);
        Assert.Contains("manageUndoRecord: false", dialog, StringComparison.Ordinal);
        Assert.Contains("RestoreOriginal", dialog, StringComparison.Ordinal);
        Assert.Contains("UpdateCachedComponents", dialog, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<FastenerComponentData> components", presentation, StringComparison.Ordinal);
        Assert.Contains("GroupBy(component => component.ComponentId)", presentation, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(presentation, "doc.BeginUndoRecord"));
        Assert.DoesNotContain("FastenerOpacityPercent = component.FastenerOpacityPercent", editor, StringComparison.Ordinal);
        Assert.DoesNotContain("CutterOpacityPercent = component.CutterOpacityPercent", editor, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportRegeneratesCuttersFromCanonicalControlPointData()
    {
        var export = ReadSource("src", "RhinoMM.Plugin", "Services", "BooleanExportService.cs");
        var cutter = ReadSource("src", "RhinoMM.Plugin", "Services", "CutterGeometryService.cs");
        var component = ReadSource("src", "RhinoMM.Plugin", "Services", "FastenerComponentService.cs");
        var preparation = ReadSource("src", "RhinoMM.Plugin", "Services", "FastenerGeometryPreparationService.cs");
        var geometry = ReadSource("src", "RhinoMM.Plugin", "Geometry", "FastenerGeometryFactory.cs");
        var smartPlacement = ReadSource("src", "RhinoMM.Plugin", "Commands", "SmartPlaceCommand.cs");
        var refresh = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentRefreshService.cs");

        Assert.Contains("ReadAllControlPoints", export, StringComparison.Ordinal);
        Assert.Contains("binding.IsBooleanEnabled", export, StringComparison.Ordinal);
        Assert.Contains("selectedHostIds.Contains(binding.TargetObjectId)", export, StringComparison.Ordinal);
        Assert.Contains("CutterGeometryService.TryBuild", export, StringComparison.Ordinal);
        Assert.DoesNotContain("FastenerGeometryPreparationService.TryPrepare", export, StringComparison.Ordinal);
        Assert.DoesNotContain("ToBreps(obj.Geometry)", export, StringComparison.Ordinal);
        Assert.Contains("CutterGeometryService.TryBuild", preparation, StringComparison.Ordinal);
        Assert.Contains("FastenerGeometryFactory.DepthLimit", cutter, StringComparison.Ordinal);
        Assert.Contains("CreateShaftCutter", cutter, StringComparison.Ordinal);
        Assert.Contains("CreateHeadSeatCutters", cutter, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<Brep> Heads", cutter, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<Brep> Shafts", cutter, StringComparison.Ordinal);
        Assert.Contains("depth <= 0", cutter, StringComparison.Ordinal);
        Assert.Contains("cutters.AddRange(build.Shafts)", export, StringComparison.Ordinal);
        Assert.Contains("AddRange(build.Heads)", export, StringComparison.Ordinal);
        Assert.Contains("GetHeadSeatAxialEnvelope", geometry, StringComparison.Ordinal);
        Assert.Contains("envelope.End - envelope.CombinedStart", geometry, StringComparison.Ordinal);
        Assert.Contains("envelope.RequiresAccess", geometry, StringComparison.Ordinal);
        Assert.Contains("headIndex < item.Heads.Count", component, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach (var head in cutter.Heads)", smartPlacement, StringComparison.Ordinal);
        Assert.Contains("ExpectedHeadCutterCount", refresh, StringComparison.Ordinal);
        Assert.Contains("CountBindingObjects", refresh, StringComparison.Ordinal);
        Assert.Contains("HeadCuttersCoverExpectedEnvelope", refresh, StringComparison.Ordinal);
        Assert.Contains("GetBoundingBox(placementPlane)", refresh, StringComparison.Ordinal);
        Assert.Contains("CreateHexNutPocketCutter", cutter, StringComparison.Ordinal);
        Assert.Contains("CreateHeatSetPocketCutters", cutter, StringComparison.Ordinal);
        Assert.Contains("InstallationPocketCalculator.RequiredHostDepth", cutter, StringComparison.Ordinal);
        Assert.Contains("InstallationPocketCalculator.CuttingDepth", cutter, StringComparison.Ordinal);
        Assert.Contains("深度补偿超过宿主厚度，安装孔将贯穿", cutter, StringComparison.Ordinal);
    }

    [Fact]
    public void HexNutEmbedDepthSupportsZeroFullAndCustomDepth()
    {
        var traits = ReadSource(
            "src", "RhinoMM.Core", "Services", "FastenerKindTraits.cs");
        var geometry = ReadSource(
            "src", "RhinoMM.Plugin", "Geometry", "FastenerGeometryFactory.cs");
        var panel = ReadSource(
            "src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var migration = ReadSource(
            "src", "RhinoMM.Core", "Services", "ComponentJson.cs");

        Assert.Contains("SupportsEmbedDepth", traits, StringComparison.Ordinal);
        Assert.Contains("embed - nutDimensions.TotalHeight", geometry, StringComparison.Ordinal);
        Assert.Contains("\"全埋\"", panel, StringComparison.Ordinal);
        Assert.Contains("HexNutDimensions.Resolve", panel, StringComparison.Ordinal);
        Assert.Contains("sourceVersion < 9 && data.Kind == FastenerKind.HexNut", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void NylonLockingNutStyleUsesDedicatedDimensionsAndFullHexPocket()
    {
        var models = ReadSource("src", "RhinoMM.Core", "Domain", "FastenerModels.cs");
        var dimensions = ReadSource("src", "RhinoMM.Core", "Services", "HexNutDimensions.cs");
        var geometry = ReadSource("src", "RhinoMM.Plugin", "Geometry", "FastenerGeometryFactory.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");

        Assert.Contains("enum HexNutStyle", models, StringComparison.Ordinal);
        Assert.Contains("locking-nut-presets.v1.json", dimensions, StringComparison.Ordinal);
        Assert.Contains("AddNylonLockingNutProxy", geometry, StringComparison.Ordinal);
        Assert.Contains("CreateHexNutPocketCutter", geometry, StringComparison.Ordinal);
        Assert.Contains("CreateCylinder(", geometry, StringComparison.Ordinal);
        Assert.Contains("dimensions.AcrossFlats / 2", geometry, StringComparison.Ordinal);
        Assert.Contains("CreateSteppedHollowProxy", geometry, StringComparison.Ordinal);
        Assert.Contains("Brep.CreateBooleanUnion([body, collar]", geometry, StringComparison.Ordinal);
        Assert.Contains("无法合并尼龙防松螺母的六角本体与圆柱上盖", geometry, StringComparison.Ordinal);
        Assert.Contains("private readonly CheckBox _nylonLockingNut", panel, StringComparison.Ordinal);
        Assert.Contains("Text = \"尼龙防松螺母\"", panel, StringComparison.Ordinal);
        Assert.Contains("_parameterLayout.AddRow(CompactRow(_nylonLockingNut))", panel, StringComparison.Ordinal);
        Assert.Contains("_nylonLockingNut.CheckedChanged", panel, StringComparison.Ordinal);
        Assert.Contains("_nylonLockingNut.Checked = state.Kind == FastenerKind.HexNut", panel, StringComparison.Ordinal);
        Assert.Contains("_nylonLockingNut.Checked == true", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("LockingNutKindKey", panel, StringComparison.Ordinal);
        Assert.Contains("$\"{standardLine}\\n\"", panel, StringComparison.Ordinal);
        Assert.Contains("UpdateHexNutSizeAvailability", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void PlacementPresetDefaultsToLPlusOneDAndPersistsAcrossSessions()
    {
        var preset = ReadSource("src", "RhinoMM.Plugin", "Services", "PlacementCutterPreset.cs");
        var persistence = ReadSource("src", "RhinoMM.Plugin", "Services", "PlacementPresetService.cs");
        var place = ReadSource("src", "RhinoMM.Plugin", "Commands", "PlaceCommand.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var plugin = ReadSource("src", "RhinoMM.Plugin", "RhinoMMPlugIn.cs");

        Assert.Contains("DepthMode.FastenerLengthPlusOneDiameter", preset, StringComparison.Ordinal);
        Assert.Contains("DepthMode.FastenerLengthPlusCustom", persistence, StringComparison.Ordinal);
        Assert.Contains("CreateClearanceBinding", place, StringComparison.Ordinal);
        Assert.Contains("CreateEngagementBinding", place, StringComparison.Ordinal);
        Assert.Contains("preset.PrinterCorrection", place, StringComparison.Ordinal);
        Assert.Contains("settings.SetString", persistence, StringComparison.Ordinal);
        Assert.Contains("settings.GetString", persistence, StringComparison.Ordinal);
        Assert.Contains("PlacementPresetService.Load(Settings)", plugin, StringComparison.Ordinal);
        Assert.Contains("\"放置预设\"", panel, StringComparison.Ordinal);
        Assert.Contains("SavePlacementPresetControls", panel, StringComparison.Ordinal);
        Assert.Contains("HeatSetInsertPresetService.Load(Settings)", plugin, StringComparison.Ordinal);
        Assert.Contains("SaveHeatSetPresetControls", panel, StringComparison.Ordinal);
        Assert.Contains("DepthCompensation", panel, StringComparison.Ordinal);
        Assert.Contains("new(0, 0, 0, 1, true, true)", ReadSource(
            "src", "RhinoMM.Plugin", "Services", "HeatSetInsertPreset.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void HoleEditor_UsesOneCompactControlSetWithIsolatedContexts()
    {
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var context = ReadSource("src", "RhinoMM.Plugin", "UI", "HoleEditingContext.cs");

        Assert.Contains("CurrentComponent", context, StringComparison.Ordinal);
        Assert.Contains("PlacementPreset", context, StringComparison.Ordinal);
        Assert.Contains("_holeContext.Add(HoleEditingContext.CurrentComponent", panel, StringComparison.Ordinal);
        Assert.Contains("_holeContext.Add(HoleEditingContext.PlacementPreset", panel, StringComparison.Ordinal);
        Assert.Contains("_presetOptionsLayout.Visible", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("scrollingContent.AddRow(_moduleCard)", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("SectionHeader(\"宿主切割模块", panel, StringComparison.Ordinal);
        Assert.Contains("_applyButton.Enabled = true", panel, StringComparison.Ordinal);
        Assert.Contains("ApplyCompactFieldWidths", panel, StringComparison.Ordinal);
        Assert.Contains("CompactRow", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("_presetPrinterCorrection", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("_presetClearanceFit", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("_presetBite", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("_placementPresetLayout", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void Toolbar_UsesDedicatedStepAndRhinoExportCommands()
    {
        var toolbar = ReadSource("build", "generate-toolbar.ps1");

        Assert.Contains("ParametricFastenersExportStep", toolbar, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersExportToRhino", toolbar, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ParametricFastenersExport\",", toolbar, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportToRhino_PreviewsBottomCenterAndCommitsAtomically()
    {
        var service = ReadSource("src", "RhinoMM.Plugin", "Services", "BooleanExportService.cs");
        var command = ReadSource("src", "RhinoMM.Plugin", "Commands", "ExportToRhinoCommand.cs");

        Assert.Contains("box.Min.Z", service, StringComparison.Ordinal);
        Assert.Contains("pointGetter.DynamicDraw +=", command, StringComparison.Ordinal);
        Assert.Contains("PushModelTransform", command, StringComparison.Ordinal);
        Assert.Contains("DrawBrepShaded", command, StringComparison.Ordinal);
        Assert.DoesNotContain("doc.BeginUndoRecord", command, StringComparison.Ordinal);
        Assert.Contains("Rhino already owns the", command, StringComparison.Ordinal);
        Assert.Contains("RollbackPartialCommit", command, StringComparison.Ordinal);
        Assert.Contains("doc.Objects.Delete(id, true)", command, StringComparison.Ordinal);
        Assert.Contains("attributes.RemoveFromAllGroups()", command, StringComparison.Ordinal);
        Assert.Contains("attributes.DeleteUserString(ComponentRepository.ComponentIdKey)", command, StringComparison.Ordinal);
        Assert.Contains("doc.Groups.Add", command, StringComparison.Ordinal);
        Assert.DoesNotContain("无法创建 Rhino 撤销记录", command, StringComparison.Ordinal);
    }

    [Fact]
    public void BatchUpdate_PreparesAllComponentsBeforeStartingUndoRecord()
    {
        var service = ReadSource("src", "RhinoMM.Plugin", "Services", "FastenerComponentService.cs");
        var preparation = ReadSource("src", "RhinoMM.Plugin", "Services", "FastenerGeometryPreparationService.cs");
        var prepareIndex = service.IndexOf("foreach (var draft in drafts)", StringComparison.Ordinal);
        var failureIndex = service.IndexOf("preparationErrors.Count > 0", StringComparison.Ordinal);
        var undoIndex = service.IndexOf("doc.BeginUndoRecord", StringComparison.Ordinal);

        Assert.True(prepareIndex >= 0);
        Assert.True(failureIndex > prepareIndex);
        Assert.True(undoIndex > failureIndex);
        Assert.Contains("CreateOrReplaceMany", service, StringComparison.Ordinal);
        Assert.Contains("SmartHostBindingService.TryReconcile", preparation, StringComparison.Ordinal);
    }

    [Fact]
    public void SmartComponentsPersistRecognitionModeAndPlacementTemplates()
    {
        var models = ReadSource("src", "RhinoMM.Core", "Domain", "FastenerModels.cs");
        var migration = ReadSource("src", "RhinoMM.Core", "Services", "ComponentJson.cs");
        var smartPlacement = ReadSource(
            "src", "RhinoMM.Plugin", "Services", "SmartPlacementService.cs");
        var hostBinding = ReadSource(
            "src", "RhinoMM.Plugin", "Services", "SmartHostBindingService.cs");
        var editor = ReadSource("src", "RhinoMM.Plugin", "Services", "EditorState.cs");

        Assert.Contains("CurrentSchemaVersion = 21", models, StringComparison.Ordinal);
        Assert.Contains("AutoRecognizeHosts", models, StringComparison.Ordinal);
        Assert.Contains("SmartRecognitionMode", models, StringComparison.Ordinal);
        Assert.Contains("SmartBindingProfile", models, StringComparison.Ordinal);
        Assert.Contains("sourceVersion >= 8 && data.AutoRecognizeHosts", migration, StringComparison.Ordinal);
        Assert.Contains("AutoRecognizeHosts = recognitionMode.HasValue", smartPlacement, StringComparison.Ordinal);
        Assert.Contains("Preset.ToSmartBindingProfile()", smartPlacement, StringComparison.Ordinal);
        Assert.Contains("SmartHostClassifier.Classify", hostBinding, StringComparison.Ordinal);
        Assert.Contains("SmartBindingReconciler.Reconcile", hostBinding, StringComparison.Ordinal);
        var updateTemplate = ReadSource(
            "src", "RhinoMM.Core", "Services", "FastenerUpdateTemplate.cs");
        Assert.Contains("UpdateSmartBindingProfile", updateTemplate, StringComparison.Ordinal);
    }

    [Fact]
    public void EngagementOnlyUsesStrictSingleHostPipeline()
    {
        var models = ReadSource("src", "RhinoMM.Core", "Domain", "FastenerModels.cs");
        var validator = ReadSource(
            "src", "RhinoMM.Core", "Services", "EngagementOnlyHostValidator.cs");
        var smartPlacement = ReadSource(
            "src", "RhinoMM.Plugin", "Services", "SmartPlacementService.cs");
        var hostBinding = ReadSource(
            "src", "RhinoMM.Plugin", "Services", "SmartHostBindingService.cs");
        var classic = ReadSource(
            "src", "RhinoMM.Plugin", "Commands", "PlaceCommand.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var coordinator = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentUpdateCoordinator.cs");
        var booleanExport = ReadSource(
            "src", "RhinoMM.Plugin", "Services", "BooleanExportService.cs");

        Assert.Contains("EngagementOnly", models, StringComparison.Ordinal);
        Assert.Contains("placement.Exit - headEmbedDepth", validator, StringComparison.Ordinal);
        Assert.Contains("检测到螺杆范围内存在第二个实体", validator, StringComparison.Ordinal);
        Assert.Contains("EngagementOnlyHostValidator.Validate", smartPlacement, StringComparison.Ordinal);
        Assert.Contains("preserveFullExit: true", hostBinding, StringComparison.Ordinal);
        Assert.Contains("SelectSingleTarget", classic, StringComparison.Ordinal);
        Assert.Contains("只咬合", panel, StringComparison.Ordinal);
        Assert.Contains("CutterGeometryService.TryBuild", booleanExport, StringComparison.Ordinal);
        Assert.Contains("UpdateSmartModuleSwitch", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void NutFastenedModeUsesSharedHostRecognitionGeometryAndUiPipeline()
    {
        var models = ReadSource("src", "RhinoMM.Core", "Domain", "FastenerModels.cs");
        var resolver = ReadSource("src", "RhinoMM.Core", "Services", "NutFastenedHostResolver.cs");
        var placement = ReadSource("src", "RhinoMM.Plugin", "Services", "SmartPlacementService.cs");
        var binding = ReadSource("src", "RhinoMM.Plugin", "Services", "SmartHostBindingService.cs");
        var cutter = ReadSource("src", "RhinoMM.Plugin", "Services", "CutterGeometryService.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");

        Assert.Contains("NutFastened", models, StringComparison.Ordinal);
        Assert.Contains("NutPocket", models, StringComparison.Ordinal);
        Assert.Contains("NutFastenedHostResolver.Resolve", placement, StringComparison.Ordinal);
        Assert.Contains("TryReconcileNutFastened", binding, StringComparison.Ordinal);
        Assert.Contains("CreatePairedNutPocketCutter", cutter, StringComparison.Ordinal);
        Assert.Contains("_assemblyLayout.AddRow(_assemblyMode)", panel, StringComparison.Ordinal);
        Assert.Contains("槽补偿 mm", panel, StringComparison.Ordinal);
        Assert.Contains("末端露出 mm", panel, StringComparison.Ordinal);
        Assert.Contains("ClearanceAssignments", resolver, StringComparison.Ordinal);
    }

    [Fact]
    public void CounterboreBridgeUsesSharedThreeStageCutterPipeline()
    {
        var models = ReadSource("src", "RhinoMM.Core", "Domain", "FastenerModels.cs");
        var calculator = ReadSource(
            "src", "RhinoMM.Core", "Services", "CounterboreBridgeCalculator.cs");
        var geometry = ReadSource(
            "src", "RhinoMM.Plugin", "Geometry", "FastenerGeometryFactory.cs");
        var cutter = ReadSource(
            "src", "RhinoMM.Plugin", "Services", "CutterGeometryService.cs");
        var preset = ReadSource(
            "src", "RhinoMM.Plugin", "Services", "PlacementPresetService.cs");
        var smartPlacement = ReadSource(
            "src", "RhinoMM.Plugin", "Services", "SmartPlacementService.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");

        Assert.Contains("CounterboreBridgeEnabled", models, StringComparison.Ordinal);
        Assert.Contains("CounterboreBridgeLayerHeight", models, StringComparison.Ordinal);
        Assert.Contains("HoleDiameterCalculator", calculator, StringComparison.Ordinal);
        Assert.Contains("Math.Sqrt(2) * shaftRadius", calculator, StringComparison.Ordinal);
        Assert.Contains("CreateCounterboreBridgeSlot", geometry, StringComparison.Ordinal);
        Assert.Contains("CreateSquarePrism", geometry, StringComparison.Ordinal);
        Assert.Contains("CounterboreBridgeCalculator.Create", geometry, StringComparison.Ordinal);
        Assert.Contains("component.CounterboreBridgeLayerHeight * 2", cutter, StringComparison.Ordinal);
        Assert.Contains("CounterboreBridgeEnabled", preset, StringComparison.Ordinal);
        Assert.Contains("CounterboreBridgeEnabled", smartPlacement, StringComparison.Ordinal);
        Assert.Contains("悬垂沉孔架桥", panel, StringComparison.Ordinal);
        Assert.Contains("每层", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void Selection_ReadsOnlyControlPointsAndSupportsMultipleComponents()
    {
        var repository = ReadSource("src", "RhinoMM.Plugin", "Persistence", "ComponentRepository.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var coordinator = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentUpdateCoordinator.cs");
        var edit = ReadSource("src", "RhinoMM.Plugin", "Commands", "EditCommand.cs");

        Assert.Contains("ReadSelectedControlPoints", repository, StringComparison.Ordinal);
        Assert.Contains("RoleKey) != \"ControlPoint\"", repository, StringComparison.Ordinal);
        Assert.Contains("ActivateMany", panel, StringComparison.Ordinal);
        Assert.Contains("ComponentUpdateCoordinator.TryApplyTemplate", panel, StringComparison.Ordinal);
        Assert.Contains("CreateOrReplaceMany", coordinator, StringComparison.Ordinal);
        Assert.Contains("ActivateMany(doc, components, false)", edit, StringComparison.Ordinal);
        var activateIndex = panel.IndexOf(
            "private void ActivateComponents",
            StringComparison.Ordinal);
        var guardIndex = panel.IndexOf("_loadingControls = true;", activateIndex, StringComparison.Ordinal);
        var loadIndex = panel.IndexOf("EditorState.Current.Load(_loadedComponent)", activateIndex, StringComparison.Ordinal);
        var contextIndex = panel.IndexOf(
            "_holeEditingContext = HoleEditingContext.CurrentComponent",
            activateIndex,
            StringComparison.Ordinal);
        Assert.True(guardIndex > activateIndex);
        Assert.True(loadIndex > guardIndex);
        Assert.True(contextIndex > loadIndex);
        Assert.Contains("_kind.Select(state.Kind.ToString(), false)", panel, StringComparison.Ordinal);
        Assert.Contains("_nylonLockingNut.Checked = state.Kind == FastenerKind.HexNut", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyUpdate_UsesCurrentControlPointSelectionWithoutSessionFallback()
    {
        var command = ReadSource("src", "RhinoMM.Plugin", "Commands", "ApplyUpdateCommand.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");

        Assert.Contains("return ComponentRepository.ReadSelectedControlPoints(doc);", command, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetActiveSet", command, StringComparison.Ordinal);
        var applyIndex = panel.IndexOf("private bool ApplyLoaded()", StringComparison.Ordinal);
        var selectionIndex = panel.IndexOf("_selectedSummary = SelectedComponentSummary.Capture(doc)", applyIndex, StringComparison.Ordinal);
        var draftIndex = panel.IndexOf("ComponentUpdateCoordinator.TryApplyTemplate", applyIndex, StringComparison.Ordinal);
        Assert.True(selectionIndex > applyIndex);
        Assert.True(draftIndex > selectionIndex);
        Assert.Contains("_selectedSummary.Components", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportCommit_UsesActiveDraftWithoutRequiringControlPointSelection()
    {
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var exportIndex = panel.IndexOf("private bool ApplyDraftForExport()", StringComparison.Ordinal);
        var nextMethodIndex = panel.IndexOf("private static string KindCardText", exportIndex, StringComparison.Ordinal);
        Assert.True(exportIndex >= 0);
        Assert.True(nextMethodIndex > exportIndex);
        var exportCommit = panel[exportIndex..nextMethodIndex];

        Assert.Contains("CaptureUpdateTemplate()", exportCommit, StringComparison.Ordinal);
        Assert.Contains("template.ApplyTo(component)", exportCommit, StringComparison.Ordinal);
        Assert.Contains("CreateOrReplaceMany", exportCommit, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadSelectedControlPoints", exportCommit, StringComparison.Ordinal);
        Assert.Contains("_loadedComponents = _loadedComponents", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void Statistics_UsesActualControlPointsForSelectedAndAllScopes()
    {
        var repository = ReadSource("src", "RhinoMM.Plugin", "Persistence", "ComponentRepository.cs");
        var service = ReadSource("src", "RhinoMM.Plugin", "Services", "FastenerStatisticsDocumentService.cs");

        Assert.Contains("ReadSelectedControlPoints", service, StringComparison.Ordinal);
        Assert.Contains("ReadAllControlPoints", service, StringComparison.Ordinal);
        Assert.Contains("TryReadControlPoint", repository, StringComparison.Ordinal);
        Assert.Contains("obj.Geometry is Point", repository, StringComparison.Ordinal);
        Assert.Contains("ignoredComponentCount", repository, StringComparison.Ordinal);
        Assert.Contains("FastenerStatisticsBuilder.Build", service, StringComparison.Ordinal);
    }

    [Fact]
    public void StatisticsWindowOwnsExcelExportAndToolbarKeepsOnlyStatisticsEntry()
    {
        var dialog = ReadSource("src", "RhinoMM.Plugin", "UI", "FastenerStatisticsDialog.cs");
        var commands = ReadSource("src", "RhinoMM.Plugin", "Commands", "StatisticsCommand.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var actionDefinitions = ReadSource("src", "RhinoMM.Plugin", "UI", "FastenerUiActions.cs");
        var toolbar = ReadSource("build", "generate-toolbar.ps1");

        Assert.Contains("FastenerStatisticsScope.Selected", dialog, StringComparison.Ordinal);
        Assert.Contains("FastenerStatisticsScope.All", dialog, StringComparison.Ordinal);
        Assert.Contains("FastenerStatisticsWorkbookWriter.Write", dialog, StringComparison.Ordinal);
        Assert.Contains("CardSelector _scopeSelector", dialog, StringComparison.Ordinal);
        Assert.Contains("MetricCard", dialog, StringComparison.Ordinal);
        Assert.Contains("FastenerUiTheme.ApplyPrimary(_exportButton, true)", dialog, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersStatistics", commands, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersExportExcel", commands, StringComparison.Ordinal);
        Assert.Contains("_-ParametricFastenersStatistics", actionDefinitions, StringComparison.Ordinal);
        Assert.DoesNotContain("_-ParametricFastenersExportExcel", panel, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersStatistics", toolbar, StringComparison.Ordinal);
        Assert.DoesNotContain("ParametricFastenersExportExcel", toolbar, StringComparison.Ordinal);
    }

    [Fact]
    public void HeatSetInsert_CanCutThroughTheFullHostFootprint()
    {
        var cutter = ReadSource("src", "RhinoMM.Plugin", "Services", "CutterGeometryService.cs");

        Assert.Contains(
            "component.Kind == FastenerKind.HexNut\n                    && interval.Max < requiredHostDepth",
            cutter.Replace("\r\n", "\n"),
            StringComparison.Ordinal);
        Assert.Contains("HeatSetMouthDiameter(component)", cutter, StringComparison.Ordinal);
        Assert.Contains("CutterFootprintEnvelopeService.TryGet", cutter, StringComparison.Ordinal);
        Assert.Contains("heatSetFootprint.Max + padding", cutter, StringComparison.Ordinal);
        Assert.Contains("热熔螺母长度超过宿主厚度，安装孔将贯穿", cutter, StringComparison.Ordinal);
        Assert.Contains("深度补偿超过宿主厚度，安装孔将贯穿", cutter, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickEditor_UsesTheLargerControlPointOffset()
    {
        var editor = ReadSource("src", "RhinoMM.Plugin", "UI", "ViewportQuickEditorWindow.cs");

        Assert.Contains("OffsetX = 36", editor, StringComparison.Ordinal);
        Assert.Contains("OffsetY = 40", editor, StringComparison.Ordinal);
        Assert.Contains("PositionScore", editor, StringComparison.Ordinal);
        Assert.Contains("TryGetComponentScreenBounds", editor, StringComparison.Ordinal);
        Assert.Contains("Math.Clamp(best.X", editor, StringComparison.Ordinal);
        Assert.Contains("Math.Clamp(best.Y", editor, StringComparison.Ordinal);
    }

    [Fact]
    public void Toolbar_MatchesTheEightPanelActionsAndSupportsAlternateRhinoExport()
    {
        var toolbar = ReadSource("build", "generate-toolbar.ps1");
        var aliases = ReadSource("src", "RhinoMM.Plugin", "Commands", "AliasCommands.cs");

        var expectedCommands = new[]
        {
            "ParametricFastenersPlace",
            "ParametricFastenersApplyUpdate",
            "ParametricFastenersRefresh",
            "ParametricFastenersExportToRhino",
            "ParametricFastenersExportStep",
            "ParametricFastenersStatistics",
            "ParametricFastenersAssemblyInspector",
            "ParametricFastenersMore"
        };
        var previous = -1;
        foreach (var command in expectedCommands)
        {
            var index = toolbar.IndexOf($"\"{command}\"", StringComparison.Ordinal);
            Assert.True(index > previous, $"Toolbar command order is wrong at {command}.");
            previous = index;
        }

        Assert.Contains("<right_macro_id>$rightRhinoMacroId</right_macro_id>", toolbar, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersExportToRhinoWithFasteners", toolbar, StringComparison.Ordinal);
        Assert.Contains("class ParametricFastenersApplyUpdateCommand", aliases, StringComparison.Ordinal);
        Assert.Contains("class ParametricFastenersRefreshCommand", aliases, StringComparison.Ordinal);
        Assert.Contains("class ParametricFastenersMoreCommand", aliases, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ParametricFastenersAdopt\",", toolbar, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ParametricFastenersValidate\",", toolbar, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickEditor_ShowsFastenerTypeAndPanelRegistersAnEmbeddedIcon()
    {
        var labels = ReadSource("src", "RhinoMM.Core", "Services", "FastenerLabels.cs");
        var editor = ReadSource("src", "RhinoMM.Plugin", "UI", "ViewportQuickEditorWindow.cs");
        var plugin = ReadSource("src", "RhinoMM.Plugin", "RhinoMMPlugIn.cs");
        var iconProvider = ReadSource("src", "RhinoMM.Plugin", "UI", "PanelIconProvider.cs");
        var generator = ReadSource("build", "generate-icon-assets.cjs");
        var project = ReadSource("src", "RhinoMM.Plugin", "RhinoMM.Plugin.csproj");

        Assert.Contains("ShortKind(FastenerComponentData component)", labels, StringComparison.Ordinal);
        Assert.Contains("FastenerKind.SocketCap => FastenerText.Get(\"Fastener.SocketCap.Short\")", labels, StringComparison.Ordinal);
        Assert.Contains("FastenerText.Get(\"Fastener.LockNut.Short\")", editor, StringComparison.Ordinal);
        Assert.Contains("FastenerLabels.ShortKind(value.Kind)", editor, StringComparison.Ordinal);
        Assert.Contains("$\"{shortKind} {size}X{CompactNumber(length)}\"", editor, StringComparison.Ordinal);
        Assert.Contains("PanelIconProvider.PanelResourceName", plugin, StringComparison.Ordinal);
        Assert.Contains("PanelType.System", plugin, StringComparison.Ordinal);
        Assert.DoesNotContain("\"参数化紧固件\", null", plugin, StringComparison.Ordinal);
        Assert.Contains("panel.ico", project, StringComparison.Ordinal);
        Assert.Contains("PanelResourceName", iconProvider, StringComparison.Ordinal);
        Assert.Contains("panelSizes = [16, 24, 32, 48]", generator, StringComparison.Ordinal);
        Assert.Contains("writePngIco", generator, StringComparison.Ordinal);

        var ico = ReadBytes("src", "RhinoMM.Plugin", "UI", "Icons", "Generated", "panel.ico");
        Assert.True(ico.Length > 6 + 4 * 16);
        Assert.Equal(0, BitConverter.ToUInt16(ico, 0));
        Assert.Equal(1, BitConverter.ToUInt16(ico, 2));
        Assert.Equal(4, BitConverter.ToUInt16(ico, 4));
        Assert.Equal(new byte[] { 16, 24, 32, 48 }, Enumerable.Range(0, 4)
            .Select(index => ico[6 + index * 16])
            .ToArray());
    }

    [Fact]
    public void Version034_WiresInspectionMaintenanceAtomicOutputAndCustomLibrary()
    {
        var aliases = ReadSource("src", "RhinoMM.Plugin", "Commands", "AliasCommands.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var navigator = ReadSource("src", "RhinoMM.Plugin", "UI", "ComponentNavigatorPanel.cs");
        var output = ReadSource("src", "RhinoMM.Plugin", "Services", "OutputCenterService.cs");
        var maintenance = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentMaintenanceService.cs");
        var library = ReadSource("src", "RhinoMM.Plugin", "Services", "UserFastenerLibraryService.cs");
        var resolver = ReadSource("src", "RhinoMM.Core", "Services", "FastenerSpecResolver.cs");

        Assert.Contains("ParametricFastenersAssemblyInspector", aliases, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersQuickRefresh", aliases, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersRelink", aliases, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersCleanup", aliases, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersOutputCenter", aliases, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersCustomLibrary", aliases, StringComparison.Ordinal);
        Assert.Contains("\"装配检查器\"", panel, StringComparison.Ordinal);
        Assert.Contains("\"维护中心\"", panel, StringComparison.Ordinal);
        Assert.Contains("\"输出中心\"", panel, StringComparison.Ordinal);
        Assert.Contains("\"自定义标准件库 Beta\"", panel, StringComparison.Ordinal);
        Assert.Contains("交付信息", navigator, StringComparison.Ordinal);
        Assert.Contains("Directory.Move(staging, finalDirectory)", output, StringComparison.Ordinal);
        Assert.Contains("Directory.Delete(staging, true)", output, StringComparison.Ordinal);
        Assert.Contains("BuildRelinkRequests", maintenance, StringComparison.Ordinal);
        Assert.Contains("ImportCsv", library, StringComparison.Ordinal);
        Assert.Contains("CustomDefinitionSnapshot?.SizeSpec", resolver, StringComparison.Ordinal);
    }

    [Fact]
    public void Version035_UnifiesWindowShellsProgressCancellationAndRoadmap()
    {
        var theme = ReadSource("src", "RhinoMM.Plugin", "UI", "FastenerUiTheme.cs");
        var operation = ReadSource("src", "RhinoMM.Plugin", "UI", "FastenerOperationUi.cs");
        var inspector = ReadSource("src", "RhinoMM.Plugin", "UI", "AssemblyInspectorDialog.cs");
        var output = ReadSource("src", "RhinoMM.Plugin", "Services", "OutputCenterService.cs");
        var library = ReadSource("src", "RhinoMM.Plugin", "UI", "UserFastenerLibraryDialog.cs");
        var navigator = ReadSource("src", "RhinoMM.Plugin", "UI", "ComponentNavigatorPanel.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var roadmap = ReadSource("docs", "FUTURE_DEVELOPMENT_ROADMAP.md");

        Assert.Contains("CreateWindowShell", theme, StringComparison.Ordinal);
        Assert.Contains("WatchWindow", theme, StringComparison.Ordinal);
        Assert.Contains("FastenerOperationState", operation, StringComparison.Ordinal);
        Assert.Contains("RhinoApp.Wait()", operation, StringComparison.Ordinal);
        Assert.Contains("FastenerProgressWindow", operation, StringComparison.Ordinal);
        Assert.Contains("class AssemblyInspectorDialog : Form", inspector, StringComparison.Ordinal);
        Assert.Contains("模型已变化 · 当前结果已过期", inspector, StringComparison.Ordinal);
        Assert.Contains("operation?.IsCancellationRequested", output, StringComparison.Ordinal);
        Assert.Contains("Directory.Delete(staging, true)", output, StringComparison.Ordinal);
        Assert.Contains("ClientSize.Width >= 720", library, StringComparison.Ordinal);
        Assert.Contains("new TableRow(_grid) { ScaleHeight = true }", navigator, StringComparison.Ordinal);
        Assert.Contains("Interval = 1.2", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("0.31", roadmap, StringComparison.Ordinal);
        Assert.Contains("0.36：可靠内核与文档自愈", roadmap, StringComparison.Ordinal);
        Assert.Contains("0.37：上下文编辑与交互收敛", roadmap, StringComparison.Ordinal);
        Assert.Contains("0.38：智能装配加速", roadmap, StringComparison.Ordinal);
        Assert.Contains("1.0：稳定发布", roadmap, StringComparison.Ordinal);
    }

    [Fact]
    public void Version0351_SynchronizesActualSelectionBeforeEnablingAndApplyingUpdates()
    {
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var lifecycle = ReadSource(
            "src", "RhinoMM.Plugin", "Services", "ComponentLifecycleService.cs");

        Assert.Contains("RhinoDoc.DeselectAllObjects += DocumentSelectionCleared", panel, StringComparison.Ordinal);
        Assert.Contains("private void SynchronizeSelectedTargets()", panel, StringComparison.Ordinal);
        Assert.Contains("_selectedSummary = SelectedComponentSummary.Capture(doc)", panel, StringComparison.Ordinal);
        Assert.Contains("SynchronizeSelectedTargets();", panel, StringComparison.Ordinal);
        Assert.Contains("_selectionSummary.Text = text", panel, StringComparison.Ordinal);
        Assert.Contains("将面板模板应用到 {selected.Components.Count} 个控制点组件", panel, StringComparison.Ordinal);
        Assert.Contains("SynchronizeRedirectedSelection(doc)", lifecycle, StringComparison.Ordinal);
        Assert.Contains("ComponentActivationIntent.SynchronizeOnly", lifecycle, StringComparison.Ordinal);
    }

    [Fact]
    public void Version0352_ReordersEntrypointsThemesWindowsAndReportsAssemblyVersion()
    {
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var quick = ReadSource("src", "RhinoMM.Plugin", "UI", "ViewportQuickEditorWindow.cs");
        var theme = ReadSource("src", "RhinoMM.Plugin", "UI", "FastenerUiTheme.cs");
        var about = ReadSource("src", "RhinoMM.Plugin", "UI", "FastenerAboutDialog.cs");
        var toolbar = ReadSource("build", "generate-toolbar.ps1");
        var actionDefinitions = ReadSource("src", "RhinoMM.Plugin", "UI", "FastenerUiActions.cs");
        Assert.Contains("new TableCell(_read, true)", quick, StringComparison.Ordinal);
        Assert.Contains("ComponentRepository.TryReadComponent", quick, StringComparison.Ordinal);
        Assert.Contains("ComponentActivationIntent.LoadIntoEditor", quick, StringComparison.Ordinal);
        Assert.Contains("读取选中组件参数", panel, StringComparison.Ordinal);
        Assert.Contains("PanelActionIcon.Inspector", actionDefinitions, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersAssemblyInspector", toolbar, StringComparison.Ordinal);
        Assert.Contains("window.UseRhinoStyle()", theme, StringComparison.Ordinal);
        Assert.Contains("AssemblyInformationalVersionAttribute", about, StringComparison.Ordinal);
        Assert.Contains("Clipboard.Instance.Text", about, StringComparison.Ordinal);
        Assert.Contains("FastenerComponentData.CurrentSchemaVersion", about, StringComparison.Ordinal);
        Assert.Contains("FastenerTemplateData.CurrentSchemaVersion", about, StringComparison.Ordinal);
        Assert.Contains("UserFastenerLibraryDocument.CurrentSchemaVersion", about, StringComparison.Ordinal);
    }

    [Fact]
    public void Version0353_AssemblyInspectorSupportsEditableBatchLengthChoices()
    {
        var inspector = ReadSource("src", "RhinoMM.Plugin", "UI", "AssemblyInspectorDialog.cs");
        var choices = ReadSource("src", "RhinoMM.Core", "Services", "AssemblyLengthChoiceService.cs");
        var project = ReadSource("src", "RhinoMM.Plugin", "RhinoMM.Plugin.csproj");

        Assert.Contains("HeaderText = \"采用长度 mm\"", inspector, StringComparison.Ordinal);
        Assert.Contains("CreateLengthEditorCell", inspector, StringComparison.Ordinal);
        Assert.Contains("ReadOnly = false", inspector, StringComparison.Ordinal);
        Assert.Contains("pendingRows.GroupBy(row => row.Issue.ComponentId)", inspector, StringComparison.Ordinal);
        Assert.Contains("ComponentUpdateCoordinator.TryApplyDrafts", inspector, StringComparison.Ordinal);
        Assert.Contains("应用建议（{pending}）", inspector, StringComparison.Ordinal);
        Assert.Contains("HeaderText = \"替代装配\"", inspector, StringComparison.Ordinal);
        Assert.Contains("SuggestedAssemblyMode", inspector, StringComparison.Ordinal);
        Assert.Contains("8, 10, 12, 15, 20, 25, 30, 35, 40, 45, 50, 60", choices, StringComparison.Ordinal);
        Assert.Contains("value > 0 && value <= 1000", choices, StringComparison.Ordinal);
        Assert.Contains("AssemblyLengthChoiceService", inspector, StringComparison.Ordinal);
    }

    [Fact]
    public void Version0354_AllowsNegativeScrewHeadOffsetWithoutChangingSchema()
    {
        var traits = ReadSource("src", "RhinoMM.Core", "Services", "FastenerKindTraits.cs");
        var validator = ReadSource("src", "RhinoMM.Core", "Services", "FastenerComponentValidator.cs");
        var template = ReadSource("src", "RhinoMM.Core", "Services", "FastenerTemplateData.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var quick = ReadSource("src", "RhinoMM.Plugin", "UI", "ViewportQuickEditorWindow.cs");
        var handles = ReadSource("src", "RhinoMM.Plugin", "Commands", "ParameterHandleCommand.cs");
        var geometry = ReadSource("src", "RhinoMM.Plugin", "Geometry", "FastenerGeometryFactory.cs");
        var cutters = ReadSource("src", "RhinoMM.Plugin", "Services", "CutterGeometryService.cs");
        var models = ReadSource("src", "RhinoMM.Core", "Domain", "FastenerModels.cs");
        var project = ReadSource("src", "RhinoMM.Plugin", "RhinoMM.Plugin.csproj");

        Assert.Contains("SupportsHeadGap", traits, StringComparison.Ordinal);
        Assert.Contains("head-gap-reach", validator, StringComparison.Ordinal);
        Assert.Contains("HeadEmbedDepth + component.Length <= 0", validator, StringComparison.Ordinal);
        Assert.Contains("HeadEmbedDepth > 0", template, StringComparison.Ordinal);
        Assert.Contains("嵌入/离面 mm", panel, StringComparison.Ordinal);
        Assert.Contains("MinValue = -500", panel, StringComparison.Ordinal);
        Assert.Contains("InlineField(\"偏移\"", quick, StringComparison.Ordinal);
        Assert.Contains("SupportsHeadGap(component.Kind)", handles, StringComparison.Ordinal);
        Assert.Contains("snapped > 0", handles, StringComparison.Ordinal);
        Assert.Contains("if (data.HeadEmbedDepth <= 0", geometry, StringComparison.Ordinal);
        Assert.Contains("var shaftReach = component.HeadEmbedDepth + component.Length", cutters, StringComparison.Ordinal);
        Assert.Contains("减小离面距离", cutters, StringComparison.Ordinal);
        Assert.Contains("CurrentSchemaVersion = 21", models, StringComparison.Ordinal);
        Assert.Contains("<Version>0.40.4</Version>", project, StringComparison.Ordinal);
    }

    [Fact]
    public void Version0384_UsesYakAsTheOnlyRegistrationAuthority()
    {
        var plugin = ReadSource("src", "RhinoMM.Plugin", "RhinoMMPlugIn.cs");
        var about = ReadSource("src", "RhinoMM.Plugin", "UI", "FastenerAboutDialog.cs");
        var build = ReadSource("build", "build.ps1");
        var installer = ReadSource("packaging", "install.ps1");
        var diagnostic = ReadSource("packaging", "diagnose-install.ps1");
        var manifest = ReadSource("packaging", "manifest.yml");
        var project = ReadSource("src", "RhinoMM.Plugin", "RhinoMM.Plugin.csproj");

        Assert.Contains("PlugInLoadTime.AtStartup", plugin, StringComparison.Ordinal);
        Assert.Contains("Rhino.RhinoApp.Idle += OnFirstIdle", plugin, StringComparison.Ordinal);
        Assert.Contains("candidate.Id != ToolbarFileId", plugin, StringComparison.Ordinal);
        Assert.Contains("PathsEqual(candidate.Path, toolbarPath)", plugin, StringComparison.Ordinal);
        Assert.DoesNotContain("loadedToolbar.Close(false)", plugin, StringComparison.Ordinal);
        Assert.Contains("安装方式：{InstallationType}", about, StringComparison.Ordinal);
        Assert.Contains("RHP：{RhinoMMPlugIn.AssemblyPath}", about, StringComparison.Ordinal);
        Assert.Contains("yak build --platform win", build, StringComparison.Ordinal);
        Assert.Contains("stable Yak package path", build, StringComparison.Ordinal);
        Assert.Contains("Rhinoceros", build, StringComparison.Ordinal);
        Assert.Contains("packages", build, StringComparison.Ordinal);
        Assert.Contains("Yak.exe", installer, StringComparison.Ordinal);
        Assert.Contains("Get-Process -Name Rhino", installer, StringComparison.Ordinal);
        Assert.Contains("version: 0.40.4", manifest, StringComparison.Ordinal);
        Assert.Contains("guid:ddc747eb-360e-4629-b65b-6bb1ddb4dc8f", manifest, StringComparison.Ordinal);
        Assert.Contains("$pluginBaseName = \"ParametricFasteners\"", build, StringComparison.Ordinal);
        Assert.Contains("DestinationFiles=\"$(TargetDir)ParametricFasteners.rhp\"", project, StringComparison.Ordinal);
        Assert.Contains("Assert-YakPackage", build, StringComparison.Ordinal);
        Assert.DoesNotContain("$pluginBaseName = [System.Text.Encoding]", build, StringComparison.Ordinal);
        Assert.DoesNotContain("New-ItemProperty -LiteralPath $userRegistry -Name FileName", installer, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-ItemProperty -LiteralPath $userRegistry -Name FileName", installer, StringComparison.Ordinal);
        Assert.Contains("Remove-ItemProperty -LiteralPath $userRegistry -Name FileName", installer, StringComparison.Ordinal);
        Assert.Contains("Yak is now the only registration authority", installer, StringComparison.Ordinal);
        Assert.Contains("diagnose-install.ps1", build, StringComparison.Ordinal);
        Assert.Contains("Parametric Fasteners installation diagnostics", diagnostic, StringComparison.Ordinal);
        Assert.Contains("$pluginPath = Join-Path $registryPath \"PlugIn\"", diagnostic, StringComparison.Ordinal);
        Assert.Contains("$commandListPath = Join-Path $registryPath \"CommandList\"", diagnostic, StringComparison.Ordinal);
        Assert.Contains("Test-PathSafe", diagnostic, StringComparison.Ordinal);
        Assert.Contains("if ([string]::IsNullOrWhiteSpace($path)) { return $false }", diagnostic, StringComparison.Ordinal);
        Assert.Contains("Registration status: fully registered and path is healthy", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotMatch("[^\\x00-\\x7F]", diagnostic);
        Assert.DoesNotMatch("[^\\x00-\\x7F]", installer);
    }

    [Fact]
    public void Version0385_IsolatesExportAddsInspectorDisposalAndRobustHostIntervals()
    {
        var export = ReadSource("src", "RhinoMM.Plugin", "Services", "BooleanExportService.cs");
        var interval = ReadSource("src", "RhinoMM.Plugin", "Services", "SmartHostIntervalService.cs");
        var placement = ReadSource("src", "RhinoMM.Plugin", "Commands", "SmartPlaceCommand.cs");
        var binding = ReadSource("src", "RhinoMM.Plugin", "Services", "SmartHostBindingService.cs");
        var inspector = ReadSource("src", "RhinoMM.Plugin", "UI", "AssemblyInspectorDialog.cs");
        var deletion = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentDeletionService.cs");
        var models = ReadSource("src", "RhinoMM.Core", "Domain", "FastenerModels.cs");

        Assert.Contains("selectedHostIds.Contains(binding.TargetObjectId)", export, StringComparison.Ordinal);
        Assert.Contains("CutterGeometryService.TryBuild", export, StringComparison.Ordinal);
        Assert.DoesNotContain("FastenerGeometryPreparationService.TryPrepare", export, StringComparison.Ordinal);
        Assert.Contains("brep.IsPointInside", interval, StringComparison.Ordinal);
        Assert.Contains("recovered.Count < 5", interval, StringComparison.Ordinal);
        Assert.DoesNotContain("interval = new Interval(boxMin, boxMax)", interval, StringComparison.Ordinal);
        Assert.Contains("TryConfirmEngagementHost", placement, StringComparison.Ordinal);
        Assert.Contains("ClassifyConfirmed", binding, StringComparison.Ordinal);
        Assert.Contains("ConfirmedEngagementHostId", models, StringComparison.Ordinal);
        Assert.Contains("AllowMultipleSelection = true", inspector, StringComparison.Ordinal);
        Assert.Contains("DeleteSelectedComponents", inspector, StringComparison.Ordinal);
        Assert.Contains("doc.Undo()", deletion, StringComparison.Ordinal);
    }

    [Fact]
    public void Version0360_UsesControlPointSnapshotsSignaturesAndReadOnlyDocumentHealth()
    {
        var repository = ReadSource("src", "RhinoMM.Plugin", "Persistence", "ComponentRepository.cs");
        var reliability = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentReliabilityModels.cs");
        var health = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentDocumentHealthService.cs");
        var component = ReadSource("src", "RhinoMM.Plugin", "Services", "FastenerComponentService.cs");
        var export = ReadSource("src", "RhinoMM.Plugin", "Services", "BooleanExportService.cs");
        var session = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentEditorSession.cs");

        Assert.Contains("role == \"ControlPoint\"", repository, StringComparison.Ordinal);
        Assert.Contains("attributes.DeleteUserString(ComponentKey)", repository, StringComparison.Ordinal);
        Assert.Contains("FindControlPoints(doc, componentId).ToArray()", repository, StringComparison.Ordinal);
        Assert.Contains("record FastenerComponentSnapshot", reliability, StringComparison.Ordinal);
        Assert.Contains("record ComponentOperationPlan", reliability, StringComparison.Ordinal);
        Assert.Contains("SHA256.HashData", reliability, StringComparison.Ordinal);
        Assert.Contains("point.DistanceTo(savedPlane.Origin)", reliability, StringComparison.Ordinal);
        Assert.Contains("timer.ElapsedMilliseconds < 12", health, StringComparison.Ordinal);
        Assert.Contains("RepairDeterministic", health, StringComparison.Ordinal);
        Assert.Contains("DocumentComponentHealthState.MaintenanceRequired", health, StringComparison.Ordinal);
        Assert.Contains("ComponentMutationJournal", component, StringComparison.Ordinal);
        Assert.Contains("CutterGeometryService.TryBuild", export, StringComparison.Ordinal);
        Assert.Contains("Dictionary<uint, IReadOnlyList<Guid>>", session, StringComparison.Ordinal);
    }

    [Fact]
    public void Version0371_SimplifiesContextActionsAndRemovesFlipAndSection()
    {
        var context = ReadSource("src", "RhinoMM.Plugin", "Services", "ContextualEditSessionService.cs");
        var quick = ReadSource("src", "RhinoMM.Plugin", "UI", "ViewportQuickEditorWindow.cs");
        var batch = ReadSource("src", "RhinoMM.Plugin", "UI", "ViewportBatchEditBar.cs");
        var handles = ReadSource("src", "RhinoMM.Plugin", "Commands", "ParameterHandleCommand.cs");
        var smartPlace = ReadSource("src", "RhinoMM.Plugin", "Commands", "SmartPlaceCommand.cs");
        var repeat = ReadSource("src", "RhinoMM.Plugin", "Commands", "RepeatCommands.cs");
        var plugin = ReadSource("src", "RhinoMM.Plugin", "RhinoMMPlugIn.cs");
        var actions = ReadSource("src", "RhinoMM.Plugin", "UI", "FastenerActionPalette.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");

        Assert.Contains("record ContextualEditSession", context, StringComparison.Ordinal);
        Assert.Contains("FastenerGeometryPreparationService.TryPrepare", context, StringComparison.Ordinal);
        Assert.Contains("ComponentUpdateCoordinator.TryApplyDrafts", context, StringComparison.Ordinal);
        Assert.Contains("ContextualEditSessionService.TrySetDraft", quick, StringComparison.Ordinal);
        Assert.Contains("读取到创建模板", quick, StringComparison.Ordinal);
        Assert.Contains("new TableCell(_read, true)", quick, StringComparison.Ordinal);
        Assert.DoesNotContain("_handles", quick, StringComparison.Ordinal);
        Assert.DoesNotContain("_flip", quick, StringComparison.Ordinal);
        Assert.DoesNotContain("_copy", quick, StringComparison.Ordinal);
        Assert.DoesNotContain("_inspect", quick, StringComparison.Ordinal);
        Assert.DoesNotContain("_section", quick, StringComparison.Ordinal);
        Assert.Contains("应用使用主面板当前创建模板", batch, StringComparison.Ordinal);
        Assert.DoesNotContain("_section", batch, StringComparison.Ordinal);
        Assert.Contains("上下文草稿", handles, StringComparison.Ordinal);
        Assert.DoesNotContain("ComponentUpdateCoordinator.TryApplyDrafts", handles, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersEditHandles", panel, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersAssemblyInspector", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("翻转方向", smartPlace, StringComparison.Ordinal);
        Assert.DoesNotContain("flipDirection", smartPlace, StringComparison.Ordinal);
        Assert.DoesNotContain("ParametricFastenersSectionView", repeat, StringComparison.Ordinal);
        Assert.DoesNotContain("FastenerSectionViewService", plugin, StringComparison.Ordinal);
        Assert.Contains("PlaceholderText = \"搜索操作…\"", actions, StringComparison.Ordinal);
        Assert.Contains("模板 ·", panel, StringComparison.Ordinal);
        Assert.Contains("目标 ·", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("ParametricFastenersSectionView", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void Version0372_UsesIndexedLightweightHoverAndPreparedCommit()
    {
        var command = ReadSource("src", "RhinoMM.Plugin", "Commands", "SmartPlaceCommand.cs");
        var service = ReadSource("src", "RhinoMM.Plugin", "Services", "SmartPlacementService.cs");
        var hostIndex = ReadSource("src", "RhinoMM.Plugin", "Services", "SmartPlacementHostIndex.cs");
        var cache = ReadSource("src", "RhinoMM.Plugin", "Services", "FastenerPreviewGeometryCache.cs");
        var component = ReadSource("src", "RhinoMM.Plugin", "Services", "FastenerComponentService.cs");

        Assert.Contains("new UITimer { Interval = 0.016 }", command, StringComparison.Ordinal);
        Assert.Contains("EvaluateCursor", command, StringComparison.Ordinal);
        Assert.DoesNotContain("EvaluateLightweight", command, StringComparison.Ordinal);
        Assert.Contains("EvaluateExact", command, StringComparison.Ordinal);
        Assert.Contains("OnDynamicDraw", command, StringComparison.Ordinal);
        Assert.DoesNotContain("RebuildPreviewFromLastInput", command, StringComparison.Ordinal);
        Assert.Contains("CreateOrReplacePrepared", command, StringComparison.Ordinal);
        Assert.Contains("FastenerLightweightProxyCache.GetLocalOrCreate", service, StringComparison.Ordinal);
        Assert.Contains("record SmartPlacementCursorPreview", service, StringComparison.Ordinal);
        Assert.Contains("if (!exact)", service, StringComparison.Ordinal);
        Assert.Contains("FastenerGeometryPreparationService.TryPrepare", service, StringComparison.Ordinal);
        Assert.Contains("RTree", hostIndex, StringComparison.Ordinal);
        Assert.Contains("_tree.Search", hostIndex, StringComparison.Ordinal);
        Assert.Contains("Bindings = []", cache, StringComparison.Ordinal);
        Assert.Contains("Callers must treat returned Breps as immutable", cache, StringComparison.Ordinal);
        Assert.Contains("PushModelTransform(preview.DisplayTransform)", command, StringComparison.Ordinal);
        Assert.DoesNotContain("CutterGeometryBuild", cache, StringComparison.Ordinal);
        Assert.Contains("CreateOrReplacePrepared", component, StringComparison.Ordinal);
        Assert.Contains("expectedDocumentRevision", component, StringComparison.Ordinal);
    }

    [Fact]
    public void Version0386_KeepsFloatingUiVisibleAndCursorPreviewHostFree()
    {
        var quick = ReadSource("src", "RhinoMM.Plugin", "UI", "ViewportQuickEditorWindow.cs");
        var palette = ReadSource("src", "RhinoMM.Plugin", "UI", "FastenerActionPalette.cs");
        var command = ReadSource("src", "RhinoMM.Plugin", "Commands", "SmartPlaceCommand.cs");
        var service = ReadSource("src", "RhinoMM.Plugin", "Services", "SmartPlacementService.cs");

        Assert.Contains("_status.Wrap = WrapMode.None", quick, StringComparison.Ordinal);
        Assert.Contains("GetPreferredSize", quick, StringComparison.Ordinal);
        Assert.Contains("FitSingleLine", quick, StringComparison.Ordinal);
        Assert.DoesNotContain("CalculateHeight()", quick, StringComparison.Ordinal);
        Assert.Contains("RepositionToActiveViewport", palette, StringComparison.Ordinal);
        Assert.Contains("screen.WorkingArea", palette, StringComparison.Ordinal);
        Assert.DoesNotContain("anchor.PointToScreen", palette, StringComparison.Ordinal);
        Assert.Contains("PushModelTransform(preview.DisplayTransform)", command, StringComparison.Ordinal);
        Assert.Contains("SmartPlacementAnchorSignature", service, StringComparison.Ordinal);

        var cursorStart = service.IndexOf("public SmartPlacementCursorPreview EvaluateCursor", StringComparison.Ordinal);
        var exactStart = service.IndexOf("public SmartPlacementPreview EvaluateLightweight", cursorStart, StringComparison.Ordinal);
        Assert.True(cursorStart >= 0 && exactStart > cursorStart);
        var cursorPath = service[cursorStart..exactStart];
        Assert.DoesNotContain("FindAxisHosts", cursorPath, StringComparison.Ordinal);
        Assert.DoesNotContain("SmartHostClassifier", cursorPath, StringComparison.Ordinal);
        Assert.DoesNotContain("FastenerGeometryPreparationService", cursorPath, StringComparison.Ordinal);
    }

    [Fact]
    public void Version0387_KeepsUpdateAvailableAndUsesSingleRowDestructiveActions()
    {
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var quick = ReadSource("src", "RhinoMM.Plugin", "UI", "ViewportQuickEditorWindow.cs");
        var theme = ReadSource("src", "RhinoMM.Plugin", "UI", "FastenerUiTheme.cs");

        Assert.Contains("_applyButton.Enabled = true", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("_applyButton.Enabled = selected", panel, StringComparison.Ordinal);
        Assert.Contains("_applyInProgress", panel, StringComparison.Ordinal);
        Assert.Contains("_selectedSummary = SelectedComponentSummary.Capture(doc)", panel, StringComparison.Ordinal);
        Assert.Contains("Application.Instance.AsyncInvoke(SynchronizeSelectedTargets)", panel, StringComparison.Ordinal);

        var read = quick.IndexOf("new TableCell(_read, true)", StringComparison.Ordinal);
        var cancel = quick.IndexOf("new TableCell(_cancel, true)", read, StringComparison.Ordinal);
        var apply = quick.IndexOf("new TableCell(_apply, true)", cancel, StringComparison.Ordinal);
        var delete = quick.IndexOf("new TableCell(_delete, true)", apply, StringComparison.Ordinal);
        Assert.True(read >= 0 && cancel > read && apply > cancel && delete > apply);
        Assert.Contains("_delete.Text = \"确认删除\"", quick, StringComparison.Ordinal);
        Assert.Contains("FastenerThemeRole.DestructiveAction", quick, StringComparison.Ordinal);
        Assert.Contains("FastenerThemeRole.DestructiveConfirm", quick, StringComparison.Ordinal);
        Assert.DoesNotContain("_read.Width = _delete.Width = 80", quick, StringComparison.Ordinal);
        Assert.Contains("DestructiveAction", theme, StringComparison.Ordinal);
        Assert.Contains("DestructiveConfirm", theme, StringComparison.Ordinal);
    }

    [Fact]
    public void Version0390_UsesOneCompactVisualSystemAndUnifiedActions()
    {
        var metrics = ReadSource("src", "RhinoMM.Plugin", "UI", "FastenerUiMetrics.cs");
        var actions = ReadSource("src", "RhinoMM.Plugin", "UI", "FastenerUiActions.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var quick = ReadSource("src", "RhinoMM.Plugin", "UI", "ViewportQuickEditorWindow.cs");
        var profile = ReadSource("src", "RhinoMM.Plugin", "UI", "ResponsiveLayoutProfile.cs");

        Assert.Contains("ControlHeight = 26", metrics, StringComparison.Ordinal);
        Assert.Contains("ActionButtonHeight = 36", metrics, StringComparison.Ordinal);
        Assert.Contains("SummaryFontSize = 11", metrics, StringComparison.Ordinal);
        Assert.Contains("SpaceTight = 3", metrics, StringComparison.Ordinal);
        Assert.Contains("enum FastenerUiActionId", actions, StringComparison.Ordinal);
        Assert.Contains("FastenerUiActionCoordinator", actions, StringComparison.Ordinal);
        Assert.Contains("AlternateCommand", actions, StringComparison.Ordinal);
        Assert.Contains("Items = { _summary, _selectionSummary, _templateStripHost }", panel, StringComparison.Ordinal);
        Assert.Contains("CreateSectionDivider()", panel, StringComparison.Ordinal);
        Assert.Contains("ModuleRoleRow", panel, StringComparison.Ordinal);
        Assert.Contains("_statusHost", panel, StringComparison.Ordinal);
        Assert.Contains("new ResponsiveLayoutProfile(5, 5, 6", profile, StringComparison.Ordinal);
        Assert.Contains("CompactWidth = 268", quick, StringComparison.Ordinal);
        Assert.DoesNotContain("_expand", quick, StringComparison.Ordinal);
        Assert.Contains("PositionScore", quick, StringComparison.Ordinal);
        Assert.Contains("new TableCell(_read, true)", quick, StringComparison.Ordinal);
        Assert.Contains("new TableCell(_delete, true)", quick, StringComparison.Ordinal);
    }

    [Fact]
    public void Version040_UsesEmbeddedBilingualResourcesAndPersistentRuntimeSwitching()
    {
        var coreProject = ReadSource("src", "RhinoMM.Core", "RhinoMM.Core.csproj");
        var text = ReadSource("src", "RhinoMM.Core", "Services", "FastenerLocalization.cs");
        var service = ReadSource("src", "RhinoMM.Plugin", "Services", "FastenerLocalizationService.cs");
        var plugin = ReadSource("src", "RhinoMM.Plugin", "RhinoMMPlugIn.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var theme = ReadSource("src", "RhinoMM.Plugin", "UI", "FastenerUiTheme.cs");
        var output = ReadSource("src", "RhinoMM.Plugin", "Services", "OutputCenterService.cs");

        Assert.Contains("strings.zh-CN.json", coreProject, StringComparison.Ordinal);
        Assert.Contains("strings.en-US.json", coreProject, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(coreProject, "WithCulture=\"false\""));
        Assert.Contains("ResourcesHaveMatchingKeys", text, StringComparison.Ordinal);
        Assert.Contains("Localization.LanguageMode", service, StringComparison.Ordinal);
        Assert.Contains("AppearanceSettings.LanguageIdentifier", service, StringComparison.Ordinal);
        Assert.True(plugin.IndexOf("FastenerLocalizationService.Load", StringComparison.Ordinal)
            < plugin.IndexOf("Panels.RegisterPanel", StringComparison.Ordinal));
        Assert.Contains("FastenerLanguageMode.Auto", panel, StringComparison.Ordinal);
        Assert.Contains("FastenerLanguageMode.SimplifiedChinese", panel, StringComparison.Ordinal);
        Assert.Contains("FastenerLanguageMode.English", panel, StringComparison.Ordinal);
        Assert.Contains("FastenerLocalizationService.Changed", theme, StringComparison.Ordinal);
        Assert.Contains("FastenerText.Get(\"Output.Delivery\")", output, StringComparison.Ordinal);
        Assert.Contains("FastenerText.Get(\"Output.Assembly\")", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Version0401_UsesQuietStructuralHealthChecksAndPostCommitSignatures()
    {
        var repository = ReadSource("src", "RhinoMM.Plugin", "Persistence", "ComponentRepository.cs");
        var health = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentDocumentHealthService.cs");
        var component = ReadSource("src", "RhinoMM.Plugin", "Services", "FastenerComponentService.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var navigator = ReadSource("src", "RhinoMM.Plugin", "UI", "ComponentNavigatorPanel.cs");

        Assert.Contains("SourceSignatureKey", repository, StringComparison.Ordinal);
        Assert.Contains("DerivedManifestKey", repository, StringComparison.Ordinal);
        Assert.Contains("DerivedManifestSignature", repository, StringComparison.Ordinal);
        Assert.Contains("SuppressDuringMutation", health, StringComparison.Ordinal);
        Assert.Contains("CurrentRevision(active.Document) != active.Revision", health, StringComparison.Ordinal);
        Assert.Contains("StableCandidates", health, StringComparison.Ordinal);
        Assert.Contains("ReportChanged?.Invoke", health, StringComparison.Ordinal);
        Assert.Contains("DocumentComponentHealthState.PresentationDrift", health, StringComparison.Ordinal);
        Assert.Contains("DocumentComponentHealthState.Configuration", health, StringComparison.Ordinal);
        Assert.Contains("DocumentComponentHealthState.LegacyUnverified", health, StringComparison.Ordinal);
        Assert.DoesNotContain("FastenerGeometryPreparationService.TryPrepare", health, StringComparison.Ordinal);
        Assert.DoesNotContain("SmartHostBindingService", health, StringComparison.Ordinal);
        Assert.Contains("StampCommittedHealthMetadata", component, StringComparison.Ordinal);
        Assert.Contains("GeometrySignature(obj.Geometry)", component, StringComparison.Ordinal);
        Assert.DoesNotContain("_healthBadge", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("SetStatus(e.Report.Summary", panel, StringComparison.Ordinal);
        Assert.Contains("IsProblem(row.States)", navigator, StringComparison.Ordinal);
        Assert.Contains("ComponentHealthState.Configuration", navigator, StringComparison.Ordinal);
    }

    [Fact]
    public void Version0403_RemovesDuplicateTemplateTextAndKeepsQuickEditorVisible()
    {
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var project = ReadSource("src", "RhinoMM.Plugin", "RhinoMM.Plugin.csproj");
        var manifest = ReadSource("packaging", "manifest.yml");

        Assert.Contains("_templateStripHost", panel, StringComparison.Ordinal);
        Assert.Contains("RebuildTemplateStripLayout", panel, StringComparison.Ordinal);
        Assert.Contains("PreferredTextControlWidth(_quickEditorToggle, 74, 28)", panel, StringComparison.Ordinal);
        Assert.Contains("maximumMenuWidth", panel, StringComparison.Ordinal);
        Assert.Contains("_favoriteTemplateButton,", panel, StringComparison.Ordinal);
        Assert.Contains("_templateMenuButton,", panel, StringComparison.Ordinal);
        Assert.Contains("new TableCell(new Panel { MinimumSize = new Size(0, 0) }, true)", panel, StringComparison.Ordinal);
        Assert.Contains("_quickEditorToggle)", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("_activeTemplate", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("FitSingleLine(Label", panel, StringComparison.Ordinal);
        Assert.Contains("RebuildTemplateStripLayout(AvailableContentWidth(), force: true)", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("Text = \"快速小窗\",\r\n        Width = 74", panel, StringComparison.Ordinal);
        Assert.Contains("<Version>0.40.4</Version>", project, StringComparison.Ordinal);
        Assert.Contains("version: 0.40.4", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void Version0404_HidesBackgroundHealthFromMainPanelOnly()
    {
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var navigator = ReadSource("src", "RhinoMM.Plugin", "UI", "ComponentNavigatorPanel.cs");
        var actions = ReadSource("src", "RhinoMM.Plugin", "UI", "FastenerUiActions.cs");

        Assert.DoesNotContain("_healthBadge", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateHealthBadge", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("ComponentDocumentHealthService.ReportChanged", panel, StringComparison.Ordinal);
        Assert.Contains("Items = { _summary, _selectionSummary, _templateStripHost }", panel, StringComparison.Ordinal);
        Assert.Contains("ComponentDocumentHealthService.ReportChanged", navigator, StringComparison.Ordinal);
        Assert.Contains("刷新 / 维护", actions, StringComparison.Ordinal);
        Assert.Contains("SetStatus(message, StatusKind.Error)", panel, StringComparison.Ordinal);
    }

    private static string ReadSource(params string[] relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RhinoMM.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine([directory!.FullName, .. relativePath]));
    }

    private static byte[] ReadBytes(params string[] relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RhinoMM.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return File.ReadAllBytes(Path.Combine([directory!.FullName, .. relativePath]));
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }
}
