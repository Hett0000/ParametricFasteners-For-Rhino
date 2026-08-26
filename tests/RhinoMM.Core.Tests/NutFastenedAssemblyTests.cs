using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class NutFastenedAssemblyTests
{
    private static readonly FastenerSizeSpec M3 = FastenerCatalog.LoadEmbedded().Get("M3");

    [Fact]
    public void AxialRange_UsesShaftTipProtrusionAndNutHeight()
    {
        var component = Component();

        var range = PairedNutAssemblyCalculator.AxialRange(component, M3);

        Assert.Equal(20, range.ShaftTip, 6);
        Assert.Equal(18, range.OuterFace, 6);
        Assert.Equal(15.6, range.InnerFace, 6);
        Assert.Equal(2.4, range.NutHeight, 6);
    }

    [Fact]
    public void Resolver_AssignsAllHostsClearanceAndDeepestNutHost()
    {
        var first = Guid.NewGuid();
        var last = Guid.NewGuid();
        var result = NutFastenedHostResolver.Resolve(
        [
            new SmartHostInterval(first, 0, 8),
            new SmartHostInterval(last, 15, 20)
        ], first, Component(), M3, 0.001);

        Assert.True(result.IsValid, result.Message);
        Assert.Equal(last, result.NutPocketTargetId);
        Assert.Equal(2, result.ClearanceAssignments.Count);
        Assert.All(result.ClearanceAssignments, item => Assert.Equal(ShaftFitRole.Clearance, item.Role));
    }

    [Fact]
    public void Resolver_AllowsSingleHostToCarryClearanceAndNutPocket()
    {
        var host = Guid.NewGuid();
        var result = NutFastenedHostResolver.Resolve(
            [new SmartHostInterval(host, 0, 22)],
            host,
            Component(),
            M3,
            0.001);

        Assert.True(result.IsValid, result.Message);
        Assert.Equal(host, result.NutPocketTargetId);
        Assert.Single(result.ClearanceAssignments);
    }

    [Fact]
    public void Validator_RequiresOneNutPocketAndPositiveClearanceHost()
    {
        var host = Guid.NewGuid();
        var component = Component() with
        {
            Bindings =
            [
                new HoleTargetBinding
                {
                    TargetObjectId = host,
                    Role = ShaftFitRole.Clearance,
                    IncludeHeadSeat = true
                },
                new HoleTargetBinding
                {
                    TargetObjectId = host,
                    Role = ShaftFitRole.NutPocket,
                    DepthMode = DepthMode.ThroughTarget
                }
            ]
        };

        Assert.True(FastenerComponentValidator.Validate(component, M3).IsValid);
        Assert.False(FastenerComponentValidator.Validate(
            component with { Bindings = component.Bindings.Take(1).ToArray() }, M3).IsValid);
    }

    [Fact]
    public void SchemaFifteenMigratesToThreadEngagementWithPairedDefaults()
    {
        var migrated = ComponentJson.Migrate(Component() with
        {
            SchemaVersion = 15,
            AssemblyMode = default,
            NutTipProtrusion = 0,
            NutPocketCompensation = 0
        });

        Assert.Equal(FastenerComponentData.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(ScrewAssemblyMode.ThreadEngagement, migrated.AssemblyMode);
        Assert.Equal(HexNutStyle.Standard, migrated.PairedNutStyle);
        Assert.Equal(2, migrated.NutTipProtrusion);
        Assert.Equal(0.2, migrated.NutPocketCompensation);
    }

    [Fact]
    public void UpdateTemplateCarriesNutAssemblyParametersAndModuleSwitches()
    {
        var host = Guid.NewGuid();
        var existing = Component() with
        {
            Bindings =
            [
                new HoleTargetBinding
                {
                    TargetObjectId = host,
                    Role = ShaftFitRole.Clearance,
                    IncludeHeadSeat = true
                },
                new HoleTargetBinding
                {
                    TargetObjectId = host,
                    Role = ShaftFitRole.NutPocket,
                    DepthMode = DepthMode.ThroughTarget
                }
            ]
        };
        var template = new FastenerUpdateTemplate(
            FastenerKind.SocketCap,
            HexNutStyle.Standard,
            "M3",
            24,
            0,
            0,
            0,
            0,
            0.25,
            ClearanceFitClass.Normal,
            0.35,
            DepthMode.FastenerLengthPlusOneDiameter,
            0,
            70,
            35,
            AssemblyMode: ScrewAssemblyMode.NutFastened,
            PairedNutStyle: HexNutStyle.NylonInsertLocking,
            NutTipProtrusion: 3,
            NutPocketCompensation: 0.3,
            NutPocketPreviewVisible: false,
            NutPocketBooleanEnabled: true);

        var updated = template.ApplyTo(existing);

        Assert.Equal(ScrewAssemblyMode.NutFastened, updated.AssemblyMode);
        Assert.Equal(HexNutStyle.NylonInsertLocking, updated.PairedNutStyle);
        Assert.Equal(3, updated.NutTipProtrusion);
        Assert.Equal(0.3, updated.NutPocketCompensation);
        Assert.False(updated.Bindings.Single(binding => binding.Role == ShaftFitRole.NutPocket).IsPreviewVisible);
        Assert.True(updated.Bindings.Single(binding => binding.Role == ShaftFitRole.NutPocket).IsBooleanEnabled);
    }

    private static FastenerComponentData Component() => new()
    {
        Kind = FastenerKind.SocketCap,
        Size = "M3",
        Length = 20,
        AssemblyMode = ScrewAssemblyMode.NutFastened,
        PairedNutStyle = HexNutStyle.Standard,
        NutTipProtrusion = 2,
        NutPocketCompensation = 0.2,
        PrintProfile = new PrintProfileSnapshot("FDM", 0.2)
    };
}
