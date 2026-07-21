namespace QRKeeper.UI.Services;

public interface IMessageService
{
    event EventHandler<AppMessage>? MessageRequested;

    void Show(
        string title,
        string body,
        MessageSeverity severity = MessageSeverity.Info,
        MessagePosition position = MessagePosition.BottomRight,
        TimeSpan? duration = null);
}
