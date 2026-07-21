using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace QRKeeper.Android.ViewModels;

public enum AndroidMessageSeverity
{
    Info,
    Success,
    Warning,
    Error
}

public sealed partial class AndroidToastMessageViewModel : ViewModelBase
{
    public AndroidToastMessageViewModel(
        string title,
        string body,
        AndroidMessageSeverity severity)
    {
        Title = title;
        Body = body;
        Severity = severity;
    }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _body;

    public AndroidMessageSeverity Severity { get; }

    public IBrush AccentBrush => Severity switch
    {
        AndroidMessageSeverity.Success => GetBrush("AppSuccessBrush", Brushes.SeaGreen),
        AndroidMessageSeverity.Warning => GetBrush("AppWarningBrush", Brushes.DarkOrange),
        AndroidMessageSeverity.Error => GetBrush("AppDangerBrush", Brushes.Firebrick),
        _ => GetBrush("AppAccentBrush", Brushes.SteelBlue)
    };

    public IBrush BackgroundBrush => Severity switch
    {
        AndroidMessageSeverity.Success => GetBrush("AppToastSuccessBackgroundBrush", new SolidColorBrush(Color.FromArgb(245, 235, 250, 241))),
        AndroidMessageSeverity.Warning => GetBrush("AppToastWarningBackgroundBrush", new SolidColorBrush(Color.FromArgb(245, 255, 247, 230))),
        AndroidMessageSeverity.Error => GetBrush("AppToastErrorBackgroundBrush", new SolidColorBrush(Color.FromArgb(245, 255, 238, 238))),
        _ => GetBrush("AppToastInfoBackgroundBrush", new SolidColorBrush(Color.FromArgb(245, 235, 243, 255)))
    };

    public IBrush ForegroundBrush => GetBrush("AppTextBrush", new SolidColorBrush(Color.Parse("#1F2937")));

    private static IBrush GetBrush(string key, IBrush fallback)
    {
        if (global::Avalonia.Application.Current?.Resources.TryGetValue(key, out object? value) == true &&
            value is IBrush brush)
        {
            return brush;
        }

        return fallback;
    }
}
