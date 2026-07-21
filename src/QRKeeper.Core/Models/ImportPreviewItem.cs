namespace QRKeeper.Core.Models;

public sealed class ImportPreviewItem
{
    public QRRecord Record { get; init; } = new();

    public bool IsDuplicate { get; init; }

    public bool IsSelected { get; set; }

    public QRRecord? ExistingRecord { get; init; }
}
