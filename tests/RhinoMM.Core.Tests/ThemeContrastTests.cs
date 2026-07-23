using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class ThemeContrastTests
{
    [Theory]
    [InlineData(242, 242, 243, 46, 46, 49, 4.5)]
    [InlineData(181, 181, 184, 46, 46, 49, 4.5)]
    [InlineData(255, 255, 255, 11, 107, 203, 4.5)]
    [InlineData(100, 168, 255, 36, 36, 38, 3.0)]
    [InlineData(97, 201, 122, 46, 46, 49, 4.5)]
    [InlineData(255, 179, 64, 46, 46, 49, 4.5)]
    [InlineData(255, 107, 107, 46, 46, 49, 4.5)]
    public void DarkFallbackPalette_MeetsContrastTargets(
        int foregroundRed,
        int foregroundGreen,
        int foregroundBlue,
        int backgroundRed,
        int backgroundGreen,
        int backgroundBlue,
        double minimumRatio)
    {
        var foreground = (foregroundRed, foregroundGreen, foregroundBlue);
        var background = (backgroundRed, backgroundGreen, backgroundBlue);

        Assert.True(
            ContrastRatio(foreground, background) >= minimumRatio,
            $"Contrast {ContrastRatio(foreground, background):0.00}:1 is below {minimumRatio:0.0}:1.");
    }

    private static double ContrastRatio(
        (int Red, int Green, int Blue) left,
        (int Red, int Green, int Blue) right)
    {
        var leftLuminance = RelativeLuminance(left);
        var rightLuminance = RelativeLuminance(right);
        return (Math.Max(leftLuminance, rightLuminance) + 0.05)
            / (Math.Min(leftLuminance, rightLuminance) + 0.05);
    }

    private static double RelativeLuminance((int Red, int Green, int Blue) color) =>
        0.2126 * Linearize(color.Red / 255.0)
        + 0.7152 * Linearize(color.Green / 255.0)
        + 0.0722 * Linearize(color.Blue / 255.0);

    private static double Linearize(double channel) =>
        channel <= 0.04045
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);
}
