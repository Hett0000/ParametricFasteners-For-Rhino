namespace RhinoMM.Plugin.UI;

internal readonly record struct ResponsiveLayoutProfile(
    int KindColumns,
    int SizeColumns,
    int LengthColumns,
    bool PairDimensionFields,
    int HoleFieldColumns,
    int NumericFieldWidth,
    int DimensionFieldWidth,
    int SelectFieldWidth,
    int DepthFieldWidth)
{
    public const int CompactBreakpoint = 340;

    public static ResponsiveLayoutProfile ForWidth(int clientWidth) => clientWidth >= CompactBreakpoint
        ? new ResponsiveLayoutProfile(5, 5, 6, true, 3, 88, 108, 104, 156)
        : new ResponsiveLayoutProfile(5, 5, 4, false, clientWidth >= 300 ? 2 : 1, 88, 100, 104, 148);
}
