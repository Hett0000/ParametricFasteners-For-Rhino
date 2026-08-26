using System.Globalization;

namespace RhinoMM.Core.Services;

public sealed record AssemblyLengthChoice(
    double Value,
    bool IsCurrent,
    bool IsSuggested);

public static class AssemblyLengthChoiceService
{
    private static readonly double[] Values = [8, 10, 12, 15, 20, 25, 30, 35, 40, 45, 50, 60];

    public static IReadOnlyList<double> CommonLengths => Values;

    public static IReadOnlyList<AssemblyLengthChoice> BuildOptions(double current, double suggested) =>
        Values
            .Append(current)
            .Append(suggested)
            .Where(value => double.IsFinite(value) && value > 0 && value <= 1000)
            .DistinctBy(value => Math.Round(value, 6))
            .OrderBy(value => value)
            .Select(value => new AssemblyLengthChoice(
                value,
                Math.Abs(value - current) <= 1e-6,
                Math.Abs(value - suggested) <= 1e-6))
            .ToArray();

    public static bool TryParse(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)
            || (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                && !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)))
            return false;
        return double.IsFinite(value) && value > 0 && value <= 1000;
    }

    public static string Format(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);
}
