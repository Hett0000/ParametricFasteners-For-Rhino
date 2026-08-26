using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class SmartBindingReconcilerTests
{
    private static readonly SmartBindingProfile Profile = new(
        ClearanceFitClass.Loose,
        0.35,
        DepthMode.FastenerLengthPlusCustom,
        6,
        true,
        false,
        false,
        true);

    [Fact]
    public void LengthIncreaseAddsDeepHostAndReclassifiesOriginalHost()
    {
        var front = Guid.NewGuid();
        var rear = Guid.NewGuid();
        var originalId = Guid.NewGuid();
        var existing = new HoleTargetBinding
        {
            BindingId = originalId,
            TargetObjectId = front,
            Role = ShaftFitRole.ThreadEngagement,
            IncludeHeadSeat = true,
            BiteReduction = 0.2
        };

        var result = SmartBindingReconciler.Reconcile(
            [existing],
            [
                new SmartHostAssignment(front, ShaftFitRole.Clearance, 0, 4),
                new SmartHostAssignment(rear, ShaftFitRole.ThreadEngagement, 8, 12)
            ],
            front,
            Profile);

        Assert.Equal(2, result.Bindings.Count);
        Assert.Equal(1, result.Changes.Added);
        Assert.Equal(0, result.Changes.Removed);
        Assert.Equal(1, result.Changes.RoleChanged);
        var clearance = result.Bindings.Single(item => item.TargetObjectId == front);
        Assert.Equal(originalId, clearance.BindingId);
        Assert.Equal(ShaftFitRole.Clearance, clearance.Role);
        Assert.Equal(ClearanceFitClass.Loose, clearance.ClearanceFit);
        Assert.True(clearance.IncludeHeadSeat);
        Assert.False(clearance.IsBooleanEnabled);
        var engagement = result.Bindings.Single(item => item.TargetObjectId == rear);
        Assert.Equal(DepthMode.FastenerLengthPlusCustom, engagement.DepthMode);
        Assert.False(engagement.IsPreviewVisible);
        Assert.True(engagement.IsBooleanEnabled);
    }

    [Fact]
    public void LengthDecreaseRemovesRearHostAndMakesFrontHostEngagement()
    {
        var front = Guid.NewGuid();
        var rear = Guid.NewGuid();
        var frontId = Guid.NewGuid();
        var existing =
            new[]
            {
                new HoleTargetBinding
                {
                    BindingId = frontId,
                    TargetObjectId = front,
                    Role = ShaftFitRole.Clearance,
                    IncludeHeadSeat = true
                },
                new HoleTargetBinding
                {
                    TargetObjectId = rear,
                    Role = ShaftFitRole.ThreadEngagement
                }
            };

        var result = SmartBindingReconciler.Reconcile(
            existing,
            [new SmartHostAssignment(front, ShaftFitRole.ThreadEngagement, 0, 4)],
            front,
            Profile);

        var binding = Assert.Single(result.Bindings);
        Assert.Equal(frontId, binding.BindingId);
        Assert.Equal(ShaftFitRole.ThreadEngagement, binding.Role);
        Assert.True(binding.IncludeHeadSeat);
        Assert.Equal(1, result.Changes.Removed);
        Assert.Equal(1, result.Changes.RoleChanged);
    }

    [Fact]
    public void SameTargetAndRolePreserveTargetSpecificSettings()
    {
        var target = Guid.NewGuid();
        var bindingId = Guid.NewGuid();
        var existing = new HoleTargetBinding
        {
            BindingId = bindingId,
            TargetObjectId = target,
            Role = ShaftFitRole.Clearance,
            ClearanceFit = ClearanceFitClass.Close,
            BindingOverride = 0.17,
            IsPreviewVisible = false,
            IsBooleanEnabled = true,
            CutterObjectId = Guid.NewGuid()
        };

        var result = SmartBindingReconciler.Reconcile(
            [existing],
            [new SmartHostAssignment(target, ShaftFitRole.Clearance, 0, 5)],
            target,
            Profile);

        var binding = Assert.Single(result.Bindings);
        Assert.Equal(bindingId, binding.BindingId);
        Assert.Equal(ClearanceFitClass.Close, binding.ClearanceFit);
        Assert.Equal(0.17, binding.BindingOverride, 6);
        Assert.False(binding.IsPreviewVisible);
        Assert.True(binding.IsBooleanEnabled);
        Assert.Equal(Guid.Empty, binding.CutterObjectId);
        Assert.False(result.Changes.HasChanges);
    }

    [Fact]
    public void VersionSevenComponentsRemainManualAfterMigration()
    {
        var legacy = new FastenerComponentData
        {
            SchemaVersion = 7,
            AutoRecognizeHosts = true,
            SmartBindingProfile = Profile
        };

        var migrated = ComponentJson.Migrate(legacy);

        Assert.Equal(FastenerComponentData.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.False(migrated.AutoRecognizeHosts);
        Assert.Null(migrated.SmartBindingProfile);
    }

    [Fact]
    public void VersionEightRoundTripPreservesRecognitionModeAndProfile()
    {
        var component = new FastenerComponentData
        {
            AutoRecognizeHosts = true,
            SmartRecognitionMode = SmartPlacementRecognitionMode.AllClearance,
            SmartBindingProfile = Profile
        };

        var restored = ComponentJson.Deserialize(ComponentJson.Serialize(component));

        Assert.True(restored.AutoRecognizeHosts);
        Assert.Equal(SmartPlacementRecognitionMode.AllClearance, restored.SmartRecognitionMode);
        Assert.Equal(Profile, restored.SmartBindingProfile);
    }

    [Fact]
    public void VersionTwentyMigratesWithoutConfirmedEngagementHost()
    {
        var legacy = new FastenerComponentData
        {
            SchemaVersion = 20,
            ConfirmedEngagementHostId = Guid.NewGuid()
        };

        var migrated = ComponentJson.Migrate(legacy);

        Assert.Equal(21, migrated.SchemaVersion);
        Assert.Equal(Guid.Empty, migrated.ConfirmedEngagementHostId);
    }

    [Fact]
    public void VersionTwentyOneRoundTripPreservesConfirmedEngagementHost()
    {
        var hostId = Guid.NewGuid();
        var component = new FastenerComponentData
        {
            AssemblyMode = ScrewAssemblyMode.ThreadEngagement,
            ConfirmedEngagementHostId = hostId
        };

        var restored = ComponentJson.Deserialize(ComponentJson.Serialize(component));

        Assert.Equal(hostId, restored.ConfirmedEngagementHostId);
    }
}
