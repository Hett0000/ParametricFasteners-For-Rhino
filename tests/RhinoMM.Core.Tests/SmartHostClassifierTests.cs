using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class SmartHostClassifierTests
{
    [Fact]
    public void Automatic_AssignsSingleHostToThreadEngagement()
    {
        var host = Guid.NewGuid();

        var result = SmartHostClassifier.Classify(
            [new SmartHostInterval(host, 0, 8)],
            SmartPlacementRecognitionMode.Automatic,
            0.001);

        Assert.True(result.IsValid);
        var assignment = Assert.Single(result.Assignments);
        Assert.Equal(host, assignment.ObjectId);
        Assert.Equal(ShaftFitRole.ThreadEngagement, assignment.Role);
    }

    [Fact]
    public void Automatic_AssignsDeepestHostToEngagementAndEarlierHostsToClearance()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();

        var result = SmartHostClassifier.Classify(
            [
                new SmartHostInterval(third, 14, 20),
                new SmartHostInterval(first, 0, 4),
                new SmartHostInterval(second, 6, 10)
            ],
            SmartPlacementRecognitionMode.Automatic,
            0.001);

        Assert.True(result.IsValid);
        Assert.Collection(
            result.Assignments,
            item =>
            {
                Assert.Equal(first, item.ObjectId);
                Assert.Equal(ShaftFitRole.Clearance, item.Role);
            },
            item =>
            {
                Assert.Equal(second, item.ObjectId);
                Assert.Equal(ShaftFitRole.Clearance, item.Role);
            },
            item =>
            {
                Assert.Equal(third, item.ObjectId);
                Assert.Equal(ShaftFitRole.ThreadEngagement, item.Role);
            });
    }

    [Fact]
    public void ProductionAutomatic_RejectsShaftThatDoesNotExitPlacementHost()
    {
        var first = Guid.NewGuid();

        var result = SmartHostClassifier.Classify(
            [new SmartHostInterval(first, 0, 8)],
            SmartPlacementRecognitionMode.Automatic,
            0.001,
            first,
            headEmbedDepth: 1,
            fastenerLength: 6);

        Assert.False(result.IsValid);
        Assert.Contains("至少需要长度 7", result.Message);
        Assert.Contains("只咬合", result.Message);
    }

    [Fact]
    public void ProductionAutomatic_RejectsNoRearEngagementHost()
    {
        var first = Guid.NewGuid();

        var result = SmartHostClassifier.Classify(
            [new SmartHostInterval(first, 0, 8)],
            SmartPlacementRecognitionMode.Automatic,
            0.001,
            first,
            headEmbedDepth: 0,
            fastenerLength: 12);

        Assert.False(result.IsValid);
        Assert.Contains("未检测到后方咬合宿主", result.Message);
    }

    [Fact]
    public void ProductionAutomatic_AssignsFirstToClearanceAndRearToEngagement()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var result = SmartHostClassifier.Classify(
            [
                new SmartHostInterval(first, 0, 8),
                new SmartHostInterval(second, 10, 14)
            ],
            SmartPlacementRecognitionMode.Automatic,
            0.001,
            first,
            headEmbedDepth: 0,
            fastenerLength: 16);

        Assert.True(result.IsValid);
        Assert.Equal(ShaftFitRole.Clearance, result.Assignments[0].Role);
        Assert.Equal(ShaftFitRole.ThreadEngagement, result.Assignments[1].Role);
    }

    [Theory]
    [InlineData(SmartPlacementRecognitionMode.AllClearance, ShaftFitRole.Clearance)]
    [InlineData(SmartPlacementRecognitionMode.AllEngagement, ShaftFitRole.ThreadEngagement)]
    public void ExplicitMode_AssignsEveryHostToRequestedRole(
        SmartPlacementRecognitionMode mode,
        ShaftFitRole expectedRole)
    {
        var result = SmartHostClassifier.Classify(
            [
                new SmartHostInterval(Guid.NewGuid(), 0, 4),
                new SmartHostInterval(Guid.NewGuid(), 5, 9)
            ],
            mode,
            0.001);

        Assert.True(result.IsValid);
        Assert.All(result.Assignments, item => Assert.Equal(expectedRole, item.Role));
    }

    [Fact]
    public void OverlappingHosts_AreRejectedAsAmbiguous()
    {
        var result = SmartHostClassifier.Classify(
            [
                new SmartHostInterval(Guid.NewGuid(), 0, 8),
                new SmartHostInterval(Guid.NewGuid(), 6, 12)
            ],
            SmartPlacementRecognitionMode.Automatic,
            0.001);

        Assert.False(result.IsValid);
        Assert.Empty(result.Assignments);
        Assert.Contains("重叠", result.Message);
    }

    [Fact]
    public void EmptyOrZeroLengthIntervals_AreRejected()
    {
        var result = SmartHostClassifier.Classify(
            [new SmartHostInterval(Guid.NewGuid(), 3, 3)],
            SmartPlacementRecognitionMode.Automatic,
            0.001);

        Assert.False(result.IsValid);
        Assert.Empty(result.Assignments);
    }
}
