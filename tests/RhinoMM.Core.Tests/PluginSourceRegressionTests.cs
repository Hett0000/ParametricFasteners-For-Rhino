using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class PluginSourceRegressionTests
{
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
        Assert.Contains("ResponsiveLayoutProfile.ForWidth(AvailableContentWidth())", source, StringComparison.Ordinal);
        Assert.Contains("_scrollable.ClientSize.Width", source, StringComparison.Ordinal);
        Assert.Contains("_kind.SetColumns(profile.KindColumns)", source, StringComparison.Ordinal);
        Assert.Contains("_size.SetColumns(profile.SizeColumns)", source, StringComparison.Ordinal);
        Assert.Contains("_lengthCards.SetColumns(profile.LengthColumns)", source, StringComparison.Ordinal);
        Assert.Contains("刷新 / 清理", source, StringComparison.Ordinal);
        Assert.Contains("FastenerUiTheme.SectionTitle(\"紧固件尺寸\")", source, StringComparison.Ordinal);
        Assert.Contains("FastenerUiTheme.SectionTitle(\"孔与切割\")", source, StringComparison.Ordinal);
        Assert.DoesNotContain("宿主切割模块（", source, StringComparison.Ordinal);
        Assert.Contains("KindCardText", source, StringComparison.Ordinal);
        Assert.Contains("PanelActionIcon.Rhino", source, StringComparison.Ordinal);
        Assert.Contains("PanelActionIcon.Step", source, StringComparison.Ordinal);
        Assert.Contains("RebuildActionLayout", source, StringComparison.Ordinal);
        Assert.Contains("MinimumSize = new Size(0", source, StringComparison.Ordinal);
        Assert.Contains("ToolTip = toolTip", source, StringComparison.Ordinal);
        Assert.Contains("_-ParametricFastenersExportStl", source, StringComparison.Ordinal);
        Assert.Contains("_-ParametricFastenersExportStep", source, StringComparison.Ordinal);
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
        Assert.Contains("GetFrustumLine", service, StringComparison.Ordinal);
        Assert.DoesNotContain("ClientToWorld", service, StringComparison.Ordinal);
        Assert.Contains("Intersection.CurveBrepFace", service, StringComparison.Ordinal);
        Assert.Contains("point - ray.From", service, StringComparison.Ordinal);
        Assert.Contains("存在深度重合的多个实体面", service, StringComparison.Ordinal);
        Assert.Contains("ComponentHostResolver.IsOrdinaryHost", hostBinding, StringComparison.Ordinal);
        Assert.Contains("!obj.IsLocked", hostBinding, StringComparison.Ordinal);
        Assert.Contains("FastenerGeometryFactory.CreateProxy", service, StringComparison.Ordinal);
        Assert.Contains("CutterGeometryService.TryBuild", service, StringComparison.Ordinal);
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
        Assert.Contains("尚未绑定宿主", export, StringComparison.Ordinal);
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

        foreach (var icon in new[] { "Read", "Place", "Apply", "Refresh", "Rhino", "Step", "Statistics", "More" })
            Assert.Contains($"PanelActionIcon.{icon}", panel, StringComparison.Ordinal);
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
        Assert.Contains("ActionButtonHeight = 40", theme, StringComparison.Ordinal);
        Assert.Contains("Spacing = new Size(2, 0)", panel, StringComparison.Ordinal);
        Assert.Contains("index == 4", panel, StringComparison.Ordinal);
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
        Assert.Contains("RhinoApp.AppSettingsChanged += RhinoAppSettingsChanged", dialog, StringComparison.Ordinal);
        Assert.Contains("_scopeSelector.RefreshTheme()", dialog, StringComparison.Ordinal);
        Assert.Contains("suffix: '-dark'", generator, StringComparison.Ordinal);
        Assert.Contains("color: '#D7DEE8'", generator, StringComparison.Ordinal);
    }

    [Fact]
    public void ResponsiveProfile_UsesProfessionalFullWidthGrid()
    {
        var profile = ReadSource("src", "RhinoMM.Plugin", "UI", "ResponsiveLayoutProfile.cs");

        Assert.Contains("CompactBreakpoint = 340", profile, StringComparison.Ordinal);
        Assert.Contains("new ResponsiveLayoutProfile(5, 5, 6, true, 3, 88, 108, 104, 156)", profile, StringComparison.Ordinal);
        Assert.Contains("new ResponsiveLayoutProfile(5, 5, 4, false", profile, StringComparison.Ordinal);
        Assert.Contains("NumericFieldWidth", profile, StringComparison.Ordinal);
        Assert.Contains("DepthFieldWidth", profile, StringComparison.Ordinal);
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

        Assert.Contains("RhinoPlacementExportOptions", placement, StringComparison.Ordinal);
        Assert.Contains("includeFastenerSolids", command, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersExportToRhinoWithFasteners", aliases, StringComparison.Ordinal);
        Assert.Contains("MouseButtons.Alternate", panel, StringComparison.Ordinal);
        Assert.Contains("e.Handled = true", panel, StringComparison.Ordinal);
        Assert.Contains("Application.Instance.AsyncInvoke", panel, StringComparison.Ordinal);
        Assert.Contains("左击：仅布尔宿主", panel, StringComparison.Ordinal);
        Assert.Contains("右击：布尔宿主 + 紧固件实体", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("RhinoPlacementExportSettingsService", panel, StringComparison.Ordinal);
        Assert.Contains("binding.TargetObjectId", placement, StringComparison.Ordinal);
        Assert.Contains("hostIds.Contains", placement, StringComparison.Ordinal);
        Assert.Contains("GroupBy(component => component.ComponentId)", placement, StringComparison.Ordinal);
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
        var plugin = ReadSource("src", "RhinoMM.Plugin", "RhinoMMPlugIn.cs");

        Assert.Contains("new(70, 35)", settings, StringComparison.Ordinal);
        Assert.Contains("settings.GetDouble", settings, StringComparison.Ordinal);
        Assert.Contains("settings.SetDouble", settings, StringComparison.Ordinal);
        Assert.Contains("GlobalDisplaySettingsService.Load(Settings)", plugin, StringComparison.Ordinal);
        Assert.Contains("全局显示", panel, StringComparison.Ordinal);
        Assert.Contains("应用于当前文档全部组件", panel, StringComparison.Ordinal);
        Assert.Contains("ComponentRepository.ReadAllControlPoints", panel, StringComparison.Ordinal);
        Assert.Contains("ApplyGlobalDisplaySettings", panel, StringComparison.Ordinal);
        Assert.Contains("UpdateCachedComponents", panel, StringComparison.Ordinal);
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
        var geometry = ReadSource("src", "RhinoMM.Plugin", "Geometry", "FastenerGeometryFactory.cs");
        var smartPlacement = ReadSource("src", "RhinoMM.Plugin", "Commands", "SmartPlaceCommand.cs");
        var refresh = ReadSource("src", "RhinoMM.Plugin", "Services", "ComponentRefreshService.cs");

        Assert.Contains("ReadAllControlPoints", export, StringComparison.Ordinal);
        Assert.Contains("binding.IsBooleanEnabled", export, StringComparison.Ordinal);
        Assert.Contains("CutterGeometryService.TryBuild", export, StringComparison.Ordinal);
        Assert.DoesNotContain("ToBreps(obj.Geometry)", export, StringComparison.Ordinal);
        Assert.Contains("CutterGeometryService.TryBuild", component, StringComparison.Ordinal);
        Assert.Contains("FastenerGeometryFactory.DepthLimit", cutter, StringComparison.Ordinal);
        Assert.Contains("CreateShaftCutter", cutter, StringComparison.Ordinal);
        Assert.Contains("CreateHeadSeatCutters", cutter, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<Brep> Heads", cutter, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<Brep> Shafts", cutter, StringComparison.Ordinal);
        Assert.Contains("depth <= 0", cutter, StringComparison.Ordinal);
        Assert.Contains("cutters.AddRange(build!.Shafts)", export, StringComparison.Ordinal);
        Assert.Contains("AddRange(build.Heads)", export, StringComparison.Ordinal);
        Assert.Contains("GetHeadSeatAxialEnvelope", geometry, StringComparison.Ordinal);
        Assert.Contains("envelope.End - envelope.CombinedStart", geometry, StringComparison.Ordinal);
        Assert.Contains("envelope.RequiresAccess", geometry, StringComparison.Ordinal);
        Assert.Contains("foreach (var head in item.Heads)", component, StringComparison.Ordinal);
        Assert.Contains("foreach (var head in cutter.Heads)", smartPlacement, StringComparison.Ordinal);
        Assert.Contains("ExpectedHeadCutterCount", refresh, StringComparison.Ordinal);
        Assert.Contains("CountBindingObjects", refresh, StringComparison.Ordinal);
        Assert.Contains("HeadCuttersCoverExpectedEnvelope", refresh, StringComparison.Ordinal);
        Assert.Contains("GetBoundingBox(placementPlane)", refresh, StringComparison.Ordinal);
        Assert.Contains("CreateHexNutPocketCutter", cutter, StringComparison.Ordinal);
        Assert.Contains("CreateHeatSetPocketCutters", cutter, StringComparison.Ordinal);
        Assert.Contains("InstallationPocketCalculator.RequiredHostDepth", cutter, StringComparison.Ordinal);
        Assert.Contains("InstallationPocketCalculator.CuttingDepth", cutter, StringComparison.Ordinal);
        Assert.Contains("补偿深度超过宿主厚度，将贯穿", cutter, StringComparison.Ordinal);
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
        Assert.Contains("embed - spec.Head.NutThickness", geometry, StringComparison.Ordinal);
        Assert.Contains("\"全埋\"", panel, StringComparison.Ordinal);
        Assert.Contains("spec.Head.NutThickness", panel, StringComparison.Ordinal);
        Assert.Contains("sourceVersion < 9 && data.Kind == FastenerKind.HexNut", migration, StringComparison.Ordinal);
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
        Assert.Contains("HasTargets: true, HasBlockingIssues: false", panel, StringComparison.Ordinal);
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
        var prepareIndex = service.IndexOf("foreach (var draft in drafts)", StringComparison.Ordinal);
        var failureIndex = service.IndexOf("preparationErrors.Count > 0", StringComparison.Ordinal);
        var undoIndex = service.IndexOf("doc.BeginUndoRecord", StringComparison.Ordinal);

        Assert.True(prepareIndex >= 0);
        Assert.True(failureIndex > prepareIndex);
        Assert.True(undoIndex > failureIndex);
        Assert.Contains("CreateOrReplaceMany", service, StringComparison.Ordinal);
        Assert.Contains("SmartHostBindingService.TryReconcile", service, StringComparison.Ordinal);
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

        Assert.Contains("CurrentSchemaVersion = 10", models, StringComparison.Ordinal);
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
    public void Selection_ReadsOnlyControlPointsAndSupportsMultipleComponents()
    {
        var repository = ReadSource("src", "RhinoMM.Plugin", "Persistence", "ComponentRepository.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");
        var edit = ReadSource("src", "RhinoMM.Plugin", "Commands", "EditCommand.cs");

        Assert.Contains("ReadSelectedControlPoints", repository, StringComparison.Ordinal);
        Assert.Contains("RoleKey) != \"ControlPoint\"", repository, StringComparison.Ordinal);
        Assert.Contains("ActivateMany", panel, StringComparison.Ordinal);
        Assert.Contains("CreateOrReplaceMany", panel, StringComparison.Ordinal);
        Assert.Contains("批量更新", panel, StringComparison.Ordinal);
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
    }

    [Fact]
    public void ApplyUpdate_UsesCurrentControlPointSelectionWithoutSessionFallback()
    {
        var command = ReadSource("src", "RhinoMM.Plugin", "Commands", "ApplyUpdateCommand.cs");
        var panel = ReadSource("src", "RhinoMM.Plugin", "UI", "RhinoMMPanel.cs");

        Assert.Contains("return ComponentRepository.ReadSelectedControlPoints(doc);", command, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetActiveSet", command, StringComparison.Ordinal);
        var applyIndex = panel.IndexOf("private bool ApplyLoaded()", StringComparison.Ordinal);
        var selectionIndex = panel.IndexOf("RefreshSelectedTargets()", applyIndex, StringComparison.Ordinal);
        var draftIndex = panel.IndexOf("var drafts = selectedComponents", applyIndex, StringComparison.Ordinal);
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
        Assert.Contains("obj.Geometry is not Point", repository, StringComparison.Ordinal);
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
        var toolbar = ReadSource("build", "generate-toolbar.ps1");

        Assert.Contains("FastenerStatisticsScope.Selected", dialog, StringComparison.Ordinal);
        Assert.Contains("FastenerStatisticsScope.All", dialog, StringComparison.Ordinal);
        Assert.Contains("FastenerStatisticsWorkbookWriter.Write", dialog, StringComparison.Ordinal);
        Assert.Contains("CardSelector _scopeSelector", dialog, StringComparison.Ordinal);
        Assert.Contains("MetricCard", dialog, StringComparison.Ordinal);
        Assert.Contains("FastenerUiTheme.ApplyPrimary(_exportButton, true)", dialog, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersStatistics", commands, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersExportExcel", commands, StringComparison.Ordinal);
        Assert.Contains("_-ParametricFastenersStatistics", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("_-ParametricFastenersExportExcel", panel, StringComparison.Ordinal);
        Assert.Contains("ParametricFastenersStatistics", toolbar, StringComparison.Ordinal);
        Assert.DoesNotContain("ParametricFastenersExportExcel", toolbar, StringComparison.Ordinal);
    }

    private static string ReadSource(params string[] relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RhinoMM.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine([directory!.FullName, .. relativePath]));
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
