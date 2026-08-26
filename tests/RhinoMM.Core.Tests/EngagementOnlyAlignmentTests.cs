using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class EngagementOnlyAlignmentTests
{
    [Fact]
    public void M3_DefaultProfileUsesPositiveGuideAndSixtyDegreeTransition()
    {
        var component = Component();
        var spec = Spec();
        var binding = component.Bindings.Single();

        var ok = EngagementOnlyAlignmentCalculator.TryCreate(
            component,
            spec,
            binding,
            out var profile,
            out var message);

        Assert.True(ok, message);
        Assert.NotNull(profile);
        Assert.Equal(3.2, profile!.GuideDiameter, 6);
        Assert.Equal(2.65, profile.EngagementDiameter, 6);
        Assert.Equal(3, profile.GuideDepth, 6);
        Assert.Equal(0.275 / Math.Tan(Math.PI / 6), profile.TransitionLength, 6);
    }

    [Fact]
    public void BindingOverrideAffectsBothDiametersWithoutChangingTransition()
    {
        var component = Component();
        var binding = component.Bindings.Single() with { BindingOverride = 0.1 };
        var ok = EngagementOnlyAlignmentCalculator.TryCreate(
            component,
            Spec(),
            binding,
            out var profile,
            out var message);

        Assert.True(ok, message);
        Assert.Equal(3.3, profile!.GuideDiameter, 6);
        Assert.Equal(2.75, profile.EngagementDiameter, 6);
        Assert.Equal(0.275 / Math.Tan(Math.PI / 6), profile.TransitionLength, 6);
    }

    [Fact]
    public void SchemaNineteenMigratesWithAlignmentDisabled()
    {
        var migrated = ComponentJson.Migrate(Component() with
        {
            SchemaVersion = 19,
            EngagementOnlyAlignmentDepth = 3,
            EngagementOnlyAlignmentDiameterCompensation = 1
        });

        Assert.Equal(FastenerComponentData.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(0, migrated.EngagementOnlyAlignmentDepth);
        Assert.Equal(0.2, migrated.EngagementOnlyAlignmentDiameterCompensation, 6);
    }

    [Fact]
    public void TemplateSchemaFourMigratesWithAlignmentDisabled()
    {
        var json = """
        {"schemaVersion":4,"kind":"SocketCap","size":"M3","length":20,"assemblyMode":"EngagementOnly"}
        """;

        Assert.True(FastenerTemplateData.TryDeserialize(json, out var template));
        Assert.Equal(5, template.SchemaVersion);
        Assert.Equal(0, template.EngagementOnlyAlignmentDepth);
        Assert.Equal(0.2, template.EngagementOnlyAlignmentDiameterCompensation, 6);
    }

    [Fact]
    public void UpdateTemplatePersistsAlignmentParameters()
    {
        var updated = new FastenerUpdateTemplate(
            FastenerKind.SocketCap,
            HexNutStyle.Standard,
            "M3",
            20,
            0,
            0,
            0,
            0,
            0.2,
            ClearanceFitClass.Normal,
            0.35,
            DepthMode.FastenerLengthPlusOneDiameter,
            3,
            70,
            35,
            AssemblyMode: ScrewAssemblyMode.EngagementOnly,
            EngagementOnlyAlignmentDepth: 4,
            EngagementOnlyAlignmentDiameterCompensation: 0.3).ApplyTo(Component());

        Assert.Equal(4, updated.EngagementOnlyAlignmentDepth);
        Assert.Equal(0.3, updated.EngagementOnlyAlignmentDiameterCompensation, 6);
    }

    [Fact]
    public void PluginGeometryUsesAccurateEnvelopeAndSharedCutterSegments()
    {
        var root = FindRepositoryRoot();
        var cutter = File.ReadAllText(Path.Combine(root, "src", "RhinoMM.Plugin", "Services", "CutterGeometryService.cs"));
        var envelope = File.ReadAllText(Path.Combine(root, "src", "RhinoMM.Plugin", "Services", "EngagementOnlyAlignmentEnvelopeService.cs"));
        var factory = File.ReadAllText(Path.Combine(root, "src", "RhinoMM.Plugin", "Geometry", "FastenerGeometryFactory.cs"));
        var preview = File.ReadAllText(Path.Combine(root, "src", "RhinoMM.Plugin", "Services", "SmartPlacementService.cs"));

        Assert.Contains("EngagementOnlyAlignmentEnvelopeService.TryGet", cutter);
        Assert.Contains("CreateEngagementOnlyAlignmentCutters", cutter);
        Assert.Contains("AngularSamples = 64", envelope);
        Assert.Contains("Intersection.CurveBrep", envelope);
        Assert.Contains("CreateFrustum", factory);
        Assert.Contains("60°过渡", preview);
    }

    private static FastenerComponentData Component() => new()
    {
        Kind = FastenerKind.SocketCap,
        Size = "M3",
        Length = 20,
        AssemblyMode = ScrewAssemblyMode.EngagementOnly,
        EngagementOnly = true,
        EngagementOnlyAlignmentDepth = 3,
        EngagementOnlyAlignmentDiameterCompensation = 0.2,
        Bindings =
        [
            new HoleTargetBinding
            {
                TargetObjectId = Guid.NewGuid(),
                Role = ShaftFitRole.ThreadEngagement,
                BiteReduction = 0.35,
                DepthMode = DepthMode.FastenerLengthPlusOneDiameter,
                BlindDepth = 3,
                IncludeHeadSeat = true
            }
        ]
    };

    private static FastenerSizeSpec Spec() => new(
        "M3",
        3,
        0.5,
        new ClearanceDimensions(3.2, 3.2, 3.2),
        new HeadDimensions(5.5, 3, 6, 90, 5.5, 2, 5.5, 2.4));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("无法定位仓库根目录。");
    }
}
