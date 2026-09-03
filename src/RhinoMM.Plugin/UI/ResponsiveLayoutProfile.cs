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
    public const int NarrowBreakpoint = 280;
    public const int CompactBreakpoint = 320;
    public const int WideBreakpoint = 420;

    public static ResponsiveLayoutProfile ForWidth(int clientWidth)
    {
        if (clientWidth >= WideBreakpoint)
            return new ResponsiveLayoutProfile(5, 5, 6, true, 3, 80, 98, 94, 144);
        if (clientWidth >= CompactBreakpoint)
            return new ResponsiveLayoutProfile(5, 5, 6, true, 2, 74, 90, 88, 136);
        return new ResponsiveLayoutProfile(5, 5, 6, false, 1, 68, 84, 82, 120);
    }
}
