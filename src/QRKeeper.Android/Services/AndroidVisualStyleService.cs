using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace QRKeeper.Android.Services;

public static class AndroidVisualStyleService
{
    public static void Apply(AndroidColorStyle colorStyle, ThemeVariant? themeVariant = null)
    {
        if (global::Avalonia.Application.Current is not { } application ||
            application.Resources is not { } resources)
        {
            return;
        }

        bool isDark = (themeVariant ?? application.ActualThemeVariant) == ThemeVariant.Dark;
        Palette palette = colorStyle switch
        {
            AndroidColorStyle.Forest => isDark
                ? new Palette("#6EC69A", "#1D3A2C", "#163122", "#F7FFF9", "#F7FFF9", "#101B16", "#182720", "#202F27", "#22382C", "#2B4938", "#355A45", "#284536", "#98B5A5", "#F08A80", "#4B2522", "#6EC69A", "#1D3A2C", "#E7B867", "#43321B", "#1B2E25", "#193327", "#3B2E1D", "#3C2220", "#99000000", "#202F27")
                : new Palette("#2F6B4F", "#DDEFE5", "#DDEFE5", "#17251D", "#17251D", "#F4FAF6", "#E8F2EC", "#FFFFFF", "#EEF6F1", "#DEEEE6", "#9BB8A8", "#D6E4DC", "#5F756A", "#B3261E", "#FCE8E6", "#237B4B", "#E4F4EA", "#A56300", "#FFF2D7", "#EEF7F2", "#E8F6EE", "#FFF4DC", "#FDEBE9", "#80000000", "#FFFFFF"),
            AndroidColorStyle.Rose => isDark
                ? new Palette("#E1849B", "#43212A", "#4A2E36", "#FFF8FA", "#FFF8FA", "#1F1518", "#2D1D22", "#35242A", "#3B2930", "#4A2E36", "#68404B", "#4A2E36", "#C0A0A9", "#F08A80", "#4B2522", "#85D7A4", "#1F3A2B", "#E7B867", "#43321B", "#33222A", "#263629", "#3B2E1D", "#3C2220", "#99000000", "#35242A")
                : new Palette("#B14B63", "#F8DDE4", "#F8DDE4", "#30161D", "#30161D", "#FFF6F8", "#F4E7EA", "#FFFFFF", "#FBEDF1", "#F3DCE3", "#CDA4AF", "#EAD2D8", "#7F6670", "#B3261E", "#FCE8E6", "#237B4B", "#E4F4EA", "#A56300", "#FFF2D7", "#FFF0F4", "#EAF7EF", "#FFF4DC", "#FDEBE9", "#80000000", "#FFFFFF"),
            _ => isDark
                ? new Palette("#8DB3FF", "#1B2D4D", "#243A60", "#F8FBFF", "#F8FBFF", "#101827", "#182235", "#202B42", "#233552", "#2E3D58", "#3A4E70", "#2E3D58", "#9AA8C0", "#F08A80", "#4B2522", "#85D7A4", "#1F3A2B", "#E7B867", "#43321B", "#1C2B43", "#1B3226", "#3B2E1D", "#3C2220", "#99000000", "#202B42")
                : new Palette("#2F6FED", "#DCE7FF", "#DCE7FF", "#172033", "#172033", "#F5F8FF", "#E7EEFC", "#FFFFFF", "#EEF3FF", "#D7E3FA", "#A8B9D8", "#D5DEF1", "#63708A", "#B3261E", "#FCE8E6", "#237B4B", "#E4F4EA", "#A56300", "#FFF2D7", "#EEF5FF", "#EAF7EF", "#FFF5DD", "#FDEBE9", "#80000000", "#FFFFFF")
        };

        resources["AppAccentBrush"] = Brush.Parse(palette.Accent);
        resources["AppAccentSoftBrush"] = Brush.Parse(palette.AccentSoft);
        resources["AppOnAccentBrush"] = Brush.Parse("#FFFFFF");
        resources["AppSelectedBrush"] = Brush.Parse(palette.Selected);
        resources["AppSelectedForegroundBrush"] = Brush.Parse(palette.SelectedForeground);
        resources["AppTextBrush"] = Brush.Parse(palette.Text);
        resources["AppSurfaceBrush"] = Brush.Parse(palette.Surface);
        resources["AppSurfaceAltBrush"] = Brush.Parse(palette.SurfaceAlt);
        resources["AppCardBrush"] = Brush.Parse(palette.Card);
        resources["AppComponentBrush"] = Brush.Parse(palette.Component);
        resources["AppPressedBrush"] = Brush.Parse(palette.Pressed);
        resources["AppBorderBrush"] = Brush.Parse(palette.Border);
        resources["AppDividerBrush"] = Brush.Parse(palette.Divider);
        resources["AppTextMutedBrush"] = Brush.Parse(palette.TextMuted);
        resources["AppDangerBrush"] = Brush.Parse(palette.Danger);
        resources["AppDangerSoftBrush"] = Brush.Parse(palette.DangerSoft);
        resources["AppSuccessBrush"] = Brush.Parse(palette.Success);
        resources["AppSuccessSoftBrush"] = Brush.Parse(palette.SuccessSoft);
        resources["AppWarningBrush"] = Brush.Parse(palette.Warning);
        resources["AppWarningSoftBrush"] = Brush.Parse(palette.WarningSoft);
        resources["AppToastInfoBackgroundBrush"] = Brush.Parse(palette.ToastInfo);
        resources["AppToastSuccessBackgroundBrush"] = Brush.Parse(palette.ToastSuccess);
        resources["AppToastWarningBackgroundBrush"] = Brush.Parse(palette.ToastWarning);
        resources["AppToastErrorBackgroundBrush"] = Brush.Parse(palette.ToastError);
        resources["AppDialogOverlayBrush"] = Brush.Parse(palette.DialogOverlay);
        resources["AppDialogBrush"] = Brush.Parse(palette.Dialog);
    }

    private readonly record struct Palette(
        string Accent,
        string AccentSoft,
        string Selected,
        string SelectedForeground,
        string Text,
        string Surface,
        string SurfaceAlt,
        string Card,
        string Component,
        string Pressed,
        string Border,
        string Divider,
        string TextMuted,
        string Danger,
        string DangerSoft,
        string Success,
        string SuccessSoft,
        string Warning,
        string WarningSoft,
        string ToastInfo,
        string ToastSuccess,
        string ToastWarning,
        string ToastError,
        string DialogOverlay,
        string Dialog);
}
