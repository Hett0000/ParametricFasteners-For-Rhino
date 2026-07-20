using Eto.Drawing;
using Eto.Forms;

namespace RhinoMM.Plugin.UI;

internal static class FastenerUiTheme
{
    public const int SpaceSmall = 4;
    public const int SpaceMedium = 8;
    public const int SpaceLarge = 12;
    public const int ControlHeight = 28;
    public const int ActionButtonHeight = 40;

    public static Color Canvas { get; } = Color.FromArgb(245, 245, 247);
    public static Color Card { get; } = Colors.White;
    public static Color Accent { get; } = Color.FromArgb(0, 122, 255);
    public static Color SecondaryFill { get; } = Color.FromArgb(238, 238, 242);
    public static Color PrimaryText { get; } = Color.FromArgb(29, 29, 31);
    public static Color SecondaryText { get; } = Color.FromArgb(110, 110, 115);
    public static Color Divider { get; } = Color.FromArgb(210, 210, 215);

    public static Panel CreateCard(Control content, int padding = SpaceMedium) => new()
    {
        BackgroundColor = Card,
        Padding = new Padding(padding),
        Content = content
    };

    public static Label SectionTitle(string text) => new()
    {
        Text = text,
        Font = new Font(SystemFont.Bold, 10),
        TextColor = PrimaryText
    };

    public static Label SecondaryLabel(string text) => new()
    {
        Text = text,
        TextColor = SecondaryText
    };

    public static void ApplyPrimary(Button button, bool primary)
    {
        button.Height = ControlHeight;
        button.BackgroundColor = primary ? Accent : SecondaryFill;
        button.TextColor = primary ? Colors.White : PrimaryText;
        button.Font = primary ? SystemFonts.Bold() : SystemFonts.Default();
    }

    public static void ApplySecondary(Button button)
    {
        button.Height = ControlHeight;
        button.BackgroundColor = SecondaryFill;
        button.TextColor = PrimaryText;
        button.Font = SystemFonts.Default();
    }
}
