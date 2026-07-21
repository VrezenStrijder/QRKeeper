using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;
using QRKeeper.UI.Services;

namespace QRKeeper.UI.ViewModels;

public sealed partial class ToastMessageViewModel : ObservableObject
{
    public ToastMessageViewModel(AppMessage message)
    {
        Title = message.Title;
        Body = message.Body;
        Severity = message.Severity;
        Position = message.Position;
        Duration = message.Duration;
    }

    public string Title { get; }

    public string Body { get; }

    public MessageSeverity Severity { get; }

    public MessagePosition Position { get; }

    public TimeSpan Duration { get; }

    public IBrush AccentBrush => Severity switch
    {
        MessageSeverity.Success => Brushes.SeaGreen,
        MessageSeverity.Warning => Brushes.DarkOrange,
        MessageSeverity.Error => Brushes.Firebrick,
        _ => Brushes.DodgerBlue
    };

    public IBrush BackgroundBrush => Severity switch
    {
        MessageSeverity.Success => new SolidColorBrush(Color.FromArgb(245, 235, 250, 241)),
        MessageSeverity.Warning => new SolidColorBrush(Color.FromArgb(245, 255, 247, 230)),
        MessageSeverity.Error => new SolidColorBrush(Color.FromArgb(245, 255, 238, 238)),
        _ => new SolidColorBrush(Color.FromArgb(245, 238, 246, 255))
    };

    public IBrush ForegroundBrush => new SolidColorBrush(Color.Parse("#1F2937"));
}
