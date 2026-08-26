using RhinoMM.Core.Services;
using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class AssemblyLengthChoiceServiceTests
{
    [Fact]
    public void BuildOptions_MergesSortsAndLabelsCurrentSuggestedAndCommonValues()
    {
        var options = AssemblyLengthChoiceService.BuildOptions(12.5, 25);

        Assert.Equal(options.Select(option => option.Value).OrderBy(value => value), options.Select(option => option.Value));
        Assert.Equal(options.Count, options.Select(option => Math.Round(option.Value, 6)).Distinct().Count());
        Assert.True(options.Single(option => Math.Abs(option.Value - 12.5) < 1e-6).IsCurrent);
        Assert.True(options.Single(option => Math.Abs(option.Value - 25) < 1e-6).IsSuggested);
        Assert.Contains(options, option => Math.Abs(option.Value - 8) < 1e-6);
        Assert.Contains(options, option => Math.Abs(option.Value - 60) < 1e-6);
    }

    [Fact]
    public void BuildOptions_UsesOneEntryWhenCurrentAndSuggestedMatchACommonLength()
    {
        var option = AssemblyLengthChoiceService.BuildOptions(20, 20)
            .Single(item => Math.Abs(item.Value - 20) < 1e-6);

        Assert.True(option.IsCurrent);
        Assert.True(option.IsSuggested);
    }

    [Theory]
    [InlineData("12", 12)]
    [InlineData("12.5", 12.5)]
    [InlineData("0.01", 0.01)]
    [InlineData("1000", 1000)]
    public void TryParse_AcceptsValidCustomLengths(string text, double expected)
    {
        Assert.True(AssemblyLengthChoiceService.TryParse(text, out var value));
        Assert.Equal(expected, value, 6);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1000.01")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public void TryParse_RejectsInvalidCustomLengths(string text)
    {
        Assert.False(AssemblyLengthChoiceService.TryParse(text, out _));
    }
}
