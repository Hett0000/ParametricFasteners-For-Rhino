using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class EngagementOnlyHostValidatorTests
{
    [Fact]
    public void AcceptsSingleHostThatContainsEntireShaft()
    {
        var host = Guid.NewGuid();

        var result = EngagementOnlyHostValidator.Validate(
            host,
            [new SmartHostInterval(host, 0, 20)],
            headEmbedDepth: 2,
            fastenerLength: 18,
            tolerance: 0.001);

        Assert.True(result.IsValid);
        Assert.Equal(18, result.MaximumLength);
        Assert.Equal(host, result.Assignment!.ObjectId);
        Assert.Equal(ShaftFitRole.ThreadEngagement, result.Assignment.Role);
    }

    [Fact]
    public void RejectsShaftLongerThanAvailableHostDepthAndReportsMaximum()
    {
        var host = Guid.NewGuid();

        var result = EngagementOnlyHostValidator.Validate(
            host,
            [new SmartHostInterval(host, 0, 20)],
            headEmbedDepth: 3,
            fastenerLength: 18,
            tolerance: 0.001);

        Assert.False(result.IsValid);
        Assert.Equal(17, result.MaximumLength);
        Assert.Contains("缩短至 17 mm", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsSecondHostInsidePhysicalShaftReach()
    {
        var host = Guid.NewGuid();
        var second = Guid.NewGuid();

        var result = EngagementOnlyHostValidator.Validate(
            host,
            [
                new SmartHostInterval(host, 0, 30),
                new SmartHostInterval(second, 8, 12)
            ],
            headEmbedDepth: 0,
            fastenerLength: 20,
            tolerance: 0.001);

        Assert.False(result.IsValid);
        Assert.Contains("第二个实体", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IgnoresHostBeyondPhysicalShaftReach()
    {
        var host = Guid.NewGuid();
        var second = Guid.NewGuid();

        var result = EngagementOnlyHostValidator.Validate(
            host,
            [
                new SmartHostInterval(host, 0, 30),
                new SmartHostInterval(second, 21, 25)
            ],
            headEmbedDepth: 0,
            fastenerLength: 20,
            tolerance: 0.001);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void SchemaElevenMigratesWithEngagementOnlyDisabled()
    {
        var migrated = ComponentJson.Migrate(new FastenerComponentData
        {
            SchemaVersion = 11,
            Kind = FastenerKind.SocketCap,
            EngagementOnly = true
        });

        Assert.Equal(FastenerComponentData.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.False(migrated.EngagementOnly);
    }

    [Fact]
    public void SchemaTwelveEngagementOnlyRepairsRoleSwitchesFromSmartProfile()
    {
        var target = Guid.NewGuid();
        var migrated = ComponentJson.Migrate(new FastenerComponentData
        {
            SchemaVersion = 12,
            Kind = FastenerKind.SocketCap,
            EngagementOnly = true,
            AutoRecognizeHosts = true,
            SmartBindingProfile = new SmartBindingProfile(
                ClearanceFitClass.Normal,
                0.35,
                DepthMode.FastenerLengthPlusOneDiameter,
                0,
                false,
                false,
                true,
                true),
            Bindings =
            [
                new HoleTargetBinding
                {
                    TargetObjectId = target,
                    Role = ShaftFitRole.ThreadEngagement,
                    IsPreviewVisible = false,
                    IsBooleanEnabled = false,
                    IncludeHeadSeat = true
                }
            ]
        });

        var binding = Assert.Single(migrated.Bindings);
        Assert.Equal(FastenerComponentData.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.True(binding.IsPreviewVisible);
        Assert.True(binding.IsBooleanEnabled);
        Assert.Equal(target, binding.TargetObjectId);
    }

    [Fact]
    public void SchemaTwelveEngagementOnlyKeepsExplicitlyDisabledSmartExport()
    {
        var migrated = ComponentJson.Migrate(new FastenerComponentData
        {
            SchemaVersion = 12,
            Kind = FastenerKind.SocketCap,
            EngagementOnly = true,
            AutoRecognizeHosts = true,
            SmartBindingProfile = new SmartBindingProfile(
                ClearanceFitClass.Normal,
                0.35,
                DepthMode.FastenerLengthPlusOneDiameter,
                0,
                true,
                true,
                true,
                false),
            Bindings =
            [
                new HoleTargetBinding
                {
                    TargetObjectId = Guid.NewGuid(),
                    Role = ShaftFitRole.ThreadEngagement,
                    IsBooleanEnabled = true,
                    IncludeHeadSeat = true
                }
            ]
        });

        Assert.False(Assert.Single(migrated.Bindings).IsBooleanEnabled);
    }
}
