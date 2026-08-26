using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class EngagementEntryChamferTests
{
    [Fact]
    public void PerpendicularEntryCreatesStandardFortyFiveDegreeProfile()
    {
        var ok = EngagementEntryChamferCalculator.TryCreate(
            2.65, 0.5, 10, 10, 20, 10, 0.2, 0.001,
            out var profile, out var message);

        Assert.True(ok, message);
        Assert.NotNull(profile);
        Assert.Equal(1.325, profile!.InnerRadius, 6);
        Assert.Equal(9.8, profile.Start, 6);
        Assert.Equal(10.5, profile.End, 6);
        Assert.Equal(profile.InnerRadius + profile.End - profile.Start, profile.OuterRadius, 6);
        // At the actual entry plane the cone is exactly C0.5 larger radially.
        Assert.Equal(1.825, profile.InnerRadius + profile.End - 10, 6);
    }

    [Fact]
    public void ObliqueEntryKeepsMinimumChamferAtDeepestPoint()
    {
        var ok = EngagementEntryChamferCalculator.TryCreate(
            3, 0.5, 4, 6, 12, 1, 0.2, 0.001,
            out var profile, out var message);

        Assert.True(ok, message);
        Assert.NotNull(profile);
        Assert.Equal(profile!.InnerRadius + 0.5, profile.InnerRadius + profile.End - 6, 6);
        Assert.True(profile.OuterRadius > profile.InnerRadius + 2.5);
    }

    [Fact]
    public void ThinHostRejectsChamferWithoutChangingBlindDepth()
    {
        var ok = EngagementEntryChamferCalculator.TryCreate(
            2.65, 0.5, 10, 10.4, 10.8, 9.8, 0.2, 0.001,
            out var profile, out var message);

        Assert.False(ok);
        Assert.Null(profile);
        Assert.Contains("厚度不足", message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(30)]
    [InlineData(40)]
    [InlineData(44)]
    public void PlanarEntryHasFiniteAnalyticSolutionBelowFortyFiveDegrees(double angleDegrees)
    {
        const double shaftRadius = 1.325;
        const double chamfer = 0.5;
        var slope = Math.Tan(angleDegrees * Math.PI / 180);

        var ok = EngagementEntryChamferCalculator.TrySolvePlanarEntry(
            shaftRadius,
            chamfer,
            surfaceOffset: 10,
            slopeX: slope,
            slopeY: 0,
            tolerance: 0.001,
            out var solution,
            out var message);

        Assert.True(ok, message);
        Assert.NotNull(solution);
        Assert.True(double.IsFinite(solution!.MouthMaximumRadius));
        Assert.Equal(shaftRadius + chamfer,
            shaftRadius + solution.SmallEnd - solution.EntryMaximum,
            6);
        Assert.True(solution.MouthMinimum <= solution.EntryMinimum + 1e-9);
    }

    [Theory]
    [InlineData(45)]
    [InlineData(50)]
    public void PlanarEntryAtOrBeyondFortyFiveDegreesReportsPhysicalLimit(double angleDegrees)
    {
        var ok = EngagementEntryChamferCalculator.TrySolvePlanarEntry(
            shaftRadius: 1.325,
            chamferSize: 0.5,
            surfaceOffset: 10,
            slopeX: Math.Tan(angleDegrees * Math.PI / 180),
            slopeY: 0,
            tolerance: 0.001,
            out var solution,
            out var message);

        Assert.False(ok);
        Assert.Null(solution);
        Assert.Contains("45°", message);
    }

    [Fact]
    public void SchemaSeventeenMigratesWithChamferDisabledCPointFiveAndAxialMode()
    {
        var migrated = ComponentJson.Migrate(new FastenerComponentData
        {
            SchemaVersion = 17,
            EngagementEntryChamferEnabled = true,
            EngagementEntryChamferSize = 2
        });

        Assert.Equal(FastenerComponentData.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.False(migrated.EngagementEntryChamferEnabled);
        Assert.Equal(0.5, migrated.EngagementEntryChamferSize, 6);
        Assert.Equal(
            EngagementEntryChamferMode.AxialFortyFive,
            migrated.EngagementEntryChamferMode);
    }

    [Fact]
    public void SchemaEighteenAlwaysMigratesExistingChamferToAxialMode()
    {
        var migrated = ComponentJson.Migrate(new FastenerComponentData
        {
            SchemaVersion = 18,
            Kind = FastenerKind.SocketCap,
            AssemblyMode = ScrewAssemblyMode.ThreadEngagement,
            EngagementEntryChamferEnabled = true,
            EngagementEntryChamferSize = 0.8,
            EngagementEntryChamferMode = EngagementEntryChamferMode.SurfaceEqualDistance
        });

        Assert.Equal(FastenerComponentData.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.True(migrated.EngagementEntryChamferEnabled);
        Assert.Equal(0.8, migrated.EngagementEntryChamferSize, 6);
        Assert.Equal(
            EngagementEntryChamferMode.AxialFortyFive,
            migrated.EngagementEntryChamferMode);
    }

    [Fact]
    public void UpdateTemplateAppliesChamferOnlyToThreadEngagementScrews()
    {
        var source = new FastenerComponentData
        {
            Kind = FastenerKind.SocketCap,
            Size = "M3",
            Length = 20,
            AssemblyMode = ScrewAssemblyMode.ThreadEngagement,
            Bindings =
            [
                new HoleTargetBinding
                {
                    TargetObjectId = Guid.NewGuid(),
                    Role = ShaftFitRole.ThreadEngagement
                }
            ]
        };
        var template = new FastenerUpdateTemplate(
            Kind: FastenerKind.SocketCap,
            NutStyle: HexNutStyle.Standard,
            Size: "M3",
            Length: 20,
            HeadEmbedDepth: 0,
            InsertOuterDiameter: 0,
            InsertDiameterCompensation: 0,
            InsertDepthCompensation: 0,
            PrinterCorrection: 0.2,
            ClearanceFit: ClearanceFitClass.Normal,
            BiteReduction: 0.35,
            EngagementDepthMode: DepthMode.FastenerLengthPlusOneDiameter,
            EngagementBlindDepth: 0,
            FastenerOpacityPercent: 70,
            CutterOpacityPercent: 35,
            EngagementEntryChamferEnabled: true,
            EngagementEntryChamferSize: 0.8,
            EngagementEntryChamferMode: EngagementEntryChamferMode.SurfaceEqualDistance);

        var threaded = template.ApplyTo(source);
        var engagementOnly = (template with
        {
            AssemblyMode = ScrewAssemblyMode.EngagementOnly
        }).ApplyTo(source);

        Assert.True(threaded.EngagementEntryChamferEnabled);
        Assert.Equal(0.8, threaded.EngagementEntryChamferSize, 6);
        Assert.Equal(
            EngagementEntryChamferMode.SurfaceEqualDistance,
            threaded.EngagementEntryChamferMode);
        Assert.False(engagementOnly.EngagementEntryChamferEnabled);
    }

    [Fact]
    public void PluginUsesSharedChamferGeometryForAdditionalShaftSegment()
    {
        var root = FindRepositoryRoot();
        var cutters = File.ReadAllText(Path.Combine(root, "src", "RhinoMM.Plugin", "Services", "CutterGeometryService.cs"));
        var factory = File.ReadAllText(Path.Combine(root, "src", "RhinoMM.Plugin", "Geometry", "FastenerGeometryFactory.cs"));

        Assert.Contains("EngagementEntryEnvelopeService.TryGet", cutters);
        Assert.Contains("EngagementEntryChamferCalculator.TryCreate", cutters);
        Assert.DoesNotContain("requiredProbeRadius", cutters);
        Assert.DoesNotContain("未能稳定收敛", cutters);
        Assert.Contains("shafts.Add", cutters);
        Assert.Contains("CreateEngagementEntryChamferCutter", factory);

        var surfaceEqual = File.ReadAllText(Path.Combine(
            root,
            "src",
            "RhinoMM.Plugin",
            "Services",
            "EngagementSurfaceEqualChamferService.cs"));
        Assert.Contains("BlendType.Chamfer", surfaceEqual);
        Assert.Contains("RailType.DistanceFromEdge", surfaceEqual);
        Assert.Contains("Brep.CreateFilletEdges", surfaceEqual);
        Assert.Contains("入口不是单一连续平面", surfaceEqual);
        Assert.Contains("EngagementEntryChamferMode.SurfaceEqualDistance", cutters);

        var envelope = File.ReadAllText(Path.Combine(
            root,
            "src",
            "RhinoMM.Plugin",
            "Services",
            "EngagementEntryEnvelopeService.cs"));
        Assert.Contains("TrySolvePlanarEntry", envelope);
        Assert.Contains("TrySolveCurvedMouth", envelope);
        Assert.DoesNotContain("requiredProbeRadius", envelope);
    }

    [Fact]
    public void TemplateSchemaFourPersistsChamferModeAndChangesSignature()
    {
        var axial = new FastenerTemplateData
        {
            Kind = FastenerKind.SocketCap,
            Size = "M3",
            Length = 20,
            EngagementEntryChamferEnabled = true,
            EngagementEntryChamferSize = 0.5,
            EngagementEntryChamferMode = EngagementEntryChamferMode.AxialFortyFive
        }.Normalize();
        var surface = (axial with
        {
            EngagementEntryChamferMode = EngagementEntryChamferMode.SurfaceEqualDistance
        }).Normalize();

        Assert.Equal(5, FastenerTemplateData.CurrentSchemaVersion);
        Assert.Equal(5, surface.SchemaVersion);
        Assert.NotEqual(axial.Signature(), surface.Signature());
        Assert.Equal(
            EngagementEntryChamferMode.SurfaceEqualDistance,
            surface.ToUpdateTemplate(70, 35).EngagementEntryChamferMode);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RhinoMM.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
