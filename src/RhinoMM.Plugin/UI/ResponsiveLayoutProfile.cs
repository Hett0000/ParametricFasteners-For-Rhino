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
    public const int NarrowBreakpoint = 300;
    public const int CompactBreakpoint = 340;
    public const int WideBreakpoint = 420;

    public static ResponsiveLayoutProfile ForWidth(int clientWidth)
    {
        if (clientWidth >= WideBreakpoint)
            return new ResponsiveLayoutProfile(5, 5, 6, true, 3, 82, 102, 96, 148);
        if (clientWidth >= CompactBreakpoint)
            return new ResponsiveLayoutProfile(5, 5, 6, true, 3, 78, 96, 92, 140);
        if (clientWidth >= NarrowBreakpoint)
            return new ResponsiveLayoutProfile(4, 5, 4, false, 2, 74, 90, 88, 132);
        return new ResponsiveLayoutProfile(3, 5, 4, false, 2, 68, 84, 82, 120);
    }
}
