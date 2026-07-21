namespace QRKeeper.Core.Models;

public sealed class BackupManifest
{
    public string AppName { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public DateTimeOffset BackupTime { get; set; }

    public int RecordCount { get; set; }

    public string Platform { get; set; } = string.Empty;
}
