namespace QRKeeper.Core.Models;

public sealed class ImportPreview
{
    public string BackupPath { get; init; } = string.Empty;

    public BackupManifest Manifest { get; init; } = new();

    public List<ImportPreviewItem> Items { get; init; } = new();

    public int NewCount => Items.Count(item => !item.IsDuplicate);

    public int DuplicateCount => Items.Count(item => item.IsDuplicate);
}
