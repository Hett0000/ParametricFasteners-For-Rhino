using System.Runtime.CompilerServices;
using Eto.Drawing;
using Eto.Forms;
using Rhino.ApplicationSettings;
using Rhino.Runtime;

namespace RhinoMM.Plugin.UI;

internal enum FastenerThemeRole
{
    Canvas,
    Card,
    CardBorder,
    PrimaryText,
    SecondaryText,
    DisabledText,
    SecondaryAction,
    PrimaryAction,
    StatusSuccess,
    StatusWarning,
    StatusError
}

internal readonly record struct FastenerThemePalette(
    bool IsDark,
    Color Canvas,
    Color Card,
    Color CardBorder,
    Color SecondaryFill,
    Color PrimaryText,
    Color SecondaryText,
    Color DisabledText,
    Color Accent,
    Color Focus,
    Color Success,
    Color Warning,
    Color Error);

internal static class FastenerUiTheme
{
    public const int SpaceSmall = 4;
    public const int SpaceMedium = 8;
    public const int SpaceLarge = 12;
    public const int ControlHeight = 28;
    public const int ActionButtonHeight = 40;

    private sealed class ThemeRoleHolder(FastenerThemeRole role)
    {
        public FastenerThemeRole Role { get; set; } = role;
    }

    private static readonly ConditionalWeakTable<Control, ThemeRoleHolder> Roles = new();
    private static FastenerThemePalette _palette = ReadPalette();

    public static FastenerThemePalette Palette => _palette;
    public static bool IsDark => _palette.IsDark;
    public static Color Canvas => _palette.Canvas;
    public static Color Card => _palette.Card;
    public static Color Accent => _palette.Accent;
    public static Color SecondaryFill => _palette.SecondaryFill;
    public static Color PrimaryText => _palette.PrimaryText;
    public static Color SecondaryText => _palette.SecondaryText;
    public static Color DisabledText => _palette.DisabledText;
    public static Color Divider => _palette.CardBorder;
    public static Color Focus => _palette.Focus;
    public static Color Success => _palette.Success;
    public static Color Warning => _palette.Warning;
    public static Color Error => _palette.Error;

    public static bool RefreshPalette()
    {
        var next = ReadPalette();
        if (next == _palette)
            return false;
        _palette = next;
        return true;
    }

    public static T Register<T>(T control, FastenerThemeRole role) where T : Control
    {
        SetRole(control, role);
        return control;
    }

    public static void SetRole(Control control, FastenerThemeRole role)
    {
        if (Roles.TryGetValue(control, out var holder))
            holder.Role = role;
        else
            Roles.Add(control, new ThemeRoleHolder(role));
        ApplyRole(control, role);
    }

    public static void ApplyTree(Control root)
    {
        if (Roles.TryGetValue(root, out var holder))
            ApplyRole(root, holder.Role);
        foreach (var child in root.VisualControls)
            ApplyTree(child);
    }

    public static Panel CreateCard(Control content, int padding = SpaceMedium)
    {
        var surface = Register(new Panel
        {
            Padding = new Padding(padding),
            Content = content
        }, FastenerThemeRole.Card);
        return Register(new Panel
        {
            Padding = new Padding(1),
            Content = surface
        }, FastenerThemeRole.CardBorder);
    }

    public static Label SectionTitle(string text) => Register(new Label
    {
        Text = text,
        Font = new Font(SystemFont.Bold, 10)
    }, FastenerThemeRole.PrimaryText);

    public static Label PrimaryLabel(string text = "") => Register(new Label
    {
        Text = text
    }, FastenerThemeRole.PrimaryText);

    public static Label SecondaryLabel(string text = "") => Register(new Label
    {
        Text = text
    }, FastenerThemeRole.SecondaryText);

    public static void ApplyPrimary(Button button, bool primary)
    {
        button.Height = ControlHeight;
        SetRole(button, primary ? FastenerThemeRole.PrimaryAction : FastenerThemeRole.SecondaryAction);
        button.Font = primary ? SystemFonts.Bold() : SystemFonts.Default();
    }

    public static void ApplySecondary(Button button)
    {
        button.Height = ControlHeight;
        button.Font = SystemFonts.Default();
        SetRole(button, FastenerThemeRole.SecondaryAction);
    }

    public static Color StatusColor(bool success, bool warning = false) =>
        success ? Success : warning ? Warning : Error;

    private static void ApplyRole(Control control, FastenerThemeRole role)
    {
        switch (role)
        {
            case FastenerThemeRole.Canvas:
                control.BackgroundColor = Canvas;
                break;
            case FastenerThemeRole.Card:
                control.BackgroundColor = Card;
                break;
            case FastenerThemeRole.CardBorder:
                control.BackgroundColor = Divider;
                break;
            case FastenerThemeRole.PrimaryText:
                SetTextColor(control, PrimaryText);
                break;
            case FastenerThemeRole.SecondaryText:
                SetTextColor(control, SecondaryText);
                break;
            case FastenerThemeRole.DisabledText:
                SetTextColor(control, DisabledText);
                break;
            case FastenerThemeRole.SecondaryAction:
                control.BackgroundColor = SecondaryFill;
                SetTextColor(control, PrimaryText);
                break;
            case FastenerThemeRole.PrimaryAction:
                control.BackgroundColor = Accent;
                SetTextColor(control, Colors.White);
                break;
            case FastenerThemeRole.StatusSuccess:
                SetTextColor(control, Success);
                break;
            case FastenerThemeRole.StatusWarning:
                SetTextColor(control, Warning);
                break;
            case FastenerThemeRole.StatusError:
                SetTextColor(control, Error);
                break;
        }
    }

    private static void SetTextColor(Control control, Color color)
    {
        if (control is TextControl textControl)
            textControl.TextColor = color;
    }

    private static FastenerThemePalette ReadPalette()
    {
        var dark = SafeDarkMode();
        var panel = Paint(PaintColor.PanelBackground,
            dark ? System.Drawing.Color.FromArgb(36, 36, 38) : System.Drawing.Color.FromArgb(245, 245, 247));
        var editor = Paint(PaintColor.EditBoxBackground,
            dark ? System.Drawing.Color.FromArgb(58, 58, 61) : System.Drawing.Color.FromArgb(238, 238, 242));
        var enabledText = Paint(PaintColor.TextEnabled,
            dark ? System.Drawing.Color.FromArgb(242, 242, 243) : System.Drawing.Color.FromArgb(29, 29, 31));
        var disabledText = Paint(PaintColor.TextDisabled,
            dark ? System.Drawing.Color.FromArgb(125, 125, 130) : System.Drawing.Color.FromArgb(142, 142, 147));
        var divider = Paint(PaintColor.GridLinesOnPanelBackground,
            dark ? System.Drawing.Color.FromArgb(81, 81, 87) : System.Drawing.Color.FromArgb(210, 210, 215));
        var inactive = Paint(PaintColor.InactiveTabBackground, editor);

        if (dark)
        {
            var card = Mix(panel, enabledText, 0.055);
            var primary = EnsureContrast(enabledText, card, 4.5, System.Drawing.Color.White);
            var secondary = EnsureContrast(Mix(panel, primary, 0.72), card, 4.5, System.Drawing.Color.White);
            var border = EnsureContrast(divider, card, 3.0, System.Drawing.Color.White);
            var secondaryFill = Mix(editor, enabledText, 0.035);
            return new FastenerThemePalette(
                true,
                ToEto(panel),
                ToEto(card),
                ToEto(border),
                ToEto(secondaryFill),
                ToEto(primary),
                ToEto(secondary),
                ToEto(disabledText),
                Color.FromArgb(11, 107, 203),
                Color.FromArgb(100, 168, 255),
                Color.FromArgb(97, 201, 122),
                Color.FromArgb(255, 179, 64),
                Color.FromArgb(255, 107, 107));
        }

        var lightCard = System.Drawing.Color.White;
        var lightPrimary = EnsureContrast(enabledText, lightCard, 4.5, System.Drawing.Color.Black);
        var lightSecondary = EnsureContrast(
            System.Drawing.Color.FromArgb(100, 100, 105),
            lightCard,
            4.5,
            System.Drawing.Color.Black);
        var lightBorder = EnsureContrast(divider, lightCard, 3.0, System.Drawing.Color.Black);
        return new FastenerThemePalette(
            false,
            ToEto(panel),
            Colors.White,
            ToEto(lightBorder),
            ToEto(inactive),
            ToEto(lightPrimary),
            ToEto(lightSecondary),
            ToEto(disabledText),
            Color.FromArgb(0, 103, 197),
            Color.FromArgb(0, 103, 197),
            Color.FromArgb(36, 124, 68),
            Color.FromArgb(154, 85, 0),
            Color.FromArgb(190, 45, 45));
    }

    private static bool SafeDarkMode()
    {
        try
        {
            return HostUtils.RunningInDarkMode;
        }
        catch
        {
            return false;
        }
    }

    private static System.Drawing.Color Paint(PaintColor color, System.Drawing.Color fallback)
    {
        try
        {
            var value = AppearanceSettings.GetPaintColor(color, true);
            return value.IsEmpty ? fallback : value;
        }
        catch
        {
            return fallback;
        }
    }

    private static System.Drawing.Color Mix(
        System.Drawing.Color background,
        System.Drawing.Color foreground,
        double foregroundAmount)
    {
        foregroundAmount = Math.Clamp(foregroundAmount, 0, 1);
        var backgroundAmount = 1 - foregroundAmount;
        return System.Drawing.Color.FromArgb(
            255,
            (int)Math.Round(background.R * backgroundAmount + foreground.R * foregroundAmount),
            (int)Math.Round(background.G * backgroundAmount + foreground.G * foregroundAmount),
            (int)Math.Round(background.B * backgroundAmount + foreground.B * foregroundAmount));
    }

    private static System.Drawing.Color EnsureContrast(
        System.Drawing.Color candidate,
        System.Drawing.Color background,
        double minimumRatio,
        System.Drawing.Color target)
    {
        if (ContrastRatio(candidate, background) >= minimumRatio)
            return candidate;
        for (var step = 1; step <= 20; step++)
        {
            var adjusted = Mix(candidate, target, step / 20.0);
            if (ContrastRatio(adjusted, background) >= minimumRatio)
                return adjusted;
        }
        return target;
    }

    private static double ContrastRatio(System.Drawing.Color left, System.Drawing.Color right)
    {
        var leftLuminance = RelativeLuminance(left);
        var rightLuminance = RelativeLuminance(right);
        return (Math.Max(leftLuminance, rightLuminance) + 0.05)
            / (Math.Min(leftLuminance, rightLuminance) + 0.05);
    }

    private static double RelativeLuminance(System.Drawing.Color color) =>
        0.2126 * Linearize(color.R / 255.0)
        + 0.7152 * Linearize(color.G / 255.0)
        + 0.0722 * Linearize(color.B / 255.0);

    private static double Linearize(double channel) =>
        channel <= 0.04045
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);

    private static Color ToEto(System.Drawing.Color color) =>
        Color.FromArgb(color.R, color.G, color.B, color.A);
}
