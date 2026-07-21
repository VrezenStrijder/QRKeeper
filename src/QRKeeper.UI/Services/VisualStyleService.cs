using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace QRKeeper.UI.Services;

public static class VisualStyleService
{
    public static void Apply(AppColorStyle colorStyle, ThemeVariant? themeVariant = null)
    {
        if (Application.Current is not { } application ||
            application.Resources is not { } resources)
        {
            return;
        }

        bool isDark = (themeVariant ?? application.ActualThemeVariant) == ThemeVariant.Dark;
        Palette palette = colorStyle switch
        {
            AppColorStyle.Forest => isDark
                ? new Palette("#6EC69A", "#1D3A2C", "#101B16", "#182720", "#355A45")
                : new Palette("#2F6B4F", "#DDEFE5", "#F4FAF6", "#E8F2EC", "#9BB8A8"),
            AppColorStyle.Rose => isDark
                ? new Palette("#E1849B", "#43212A", "#1F1518", "#2D1D22", "#68404B")
                : new Palette("#B14B63", "#F8DDE4", "#FFF6F8", "#F4E7EA", "#CDA4AF"),
            _ => isDark
                ? new Palette("#8DB3FF", "#1B2D4D", "#101827", "#182235", "#3A4E70")
                : new Palette("#2F6FED", "#DCE7FF", "#F5F8FF", "#E7EEFC", "#A8B9D8")
        };

        resources["AppAccentBrush"] = Brush.Parse(palette.Accent);
        resources["AppAccentSoftBrush"] = Brush.Parse(palette.AccentSoft);
        resources["AppSurfaceBrush"] = Brush.Parse(palette.Surface);
        resources["AppSurfaceAltBrush"] = Brush.Parse(palette.SurfaceAlt);
        resources["AppBorderBrush"] = Brush.Parse(palette.Border);
        resources["AppDangerBrush"] = Brush.Parse("#B3261E");
        resources["AppSuccessBrush"] = Brush.Parse("#237B4B");
    }

    private readonly record struct Palette(
        string Accent,
        string AccentSoft,
        string Surface,
        string SurfaceAlt,
        string Border);
}
