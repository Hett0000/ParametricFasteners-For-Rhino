namespace RhinoMM.Plugin.UI;

/// <summary>
/// Shared visual metrics for the Rhino-native compact interface. Keeping these
/// values in one place prevents panels and auxiliary windows from slowly
/// diverging as controls are added.
/// </summary>
internal static class FastenerUiMetrics
{
    public const int SpaceTight = 3;
    public const int SpaceSmall = 4;
    public const int SpaceCompact = 6;
    public const int SpaceMedium = 8;
    public const int SpaceLarge = 12;

    public const int ControlHeight = 26;
    public const int ActionButtonHeight = 36;
    public const int QuickControlHeight = 22;
    public const int QuickButtonHeight = 26;
    public const int DialogButtonWidth = 88;

    public const int SummaryFontSize = 11;
    public const int SectionFontSize = 10;
    public const int FieldFontSize = 9;
    public const int AuxiliaryFontSize = 9;

    public const int CardBorder = 1;
}
