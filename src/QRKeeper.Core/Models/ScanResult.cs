namespace QRKeeper.Core.Models;

public sealed class ScanResult
{
    public string Content { get; init; } = string.Empty;

    public QRRecordSource Source { get; init; }
}
