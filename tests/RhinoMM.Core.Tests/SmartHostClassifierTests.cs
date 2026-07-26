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
