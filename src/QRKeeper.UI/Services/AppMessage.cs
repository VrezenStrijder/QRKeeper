namespace QRKeeper.UI.Services;

public sealed class AppMessage
{
    public AppMessage(
        string title,
        string body,
        MessageSeverity severity,
        MessagePosition position,
        TimeSpan duration)
    {
        Title = title;
        Body = body;
        Severity = severity;
        Position = position;
        Duration = duration;
    }

    public string Title { get; }

    public string Body { get; }

    public MessageSeverity Severity { get; }

    public MessagePosition Position { get; }

    public TimeSpan Duration { get; }
}
