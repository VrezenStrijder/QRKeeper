namespace QRKeeper.UI.Services;

public sealed class MessageService : IMessageService
{
    public event EventHandler<AppMessage>? MessageRequested;

    public void Show(
        string title,
        string body,
        MessageSeverity severity = MessageSeverity.Info,
        MessagePosition position = MessagePosition.BottomRight,
        TimeSpan? duration = null)
    {
        TimeSpan resolvedDuration = duration ?? (severity == MessageSeverity.Error
            ? TimeSpan.FromSeconds(5)
            : TimeSpan.FromSeconds(3));

        MessageRequested?.Invoke(this, new AppMessage(title, body, severity, position, resolvedDuration));
    }
}
