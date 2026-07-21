namespace QRKeeper.Core.Models;

public sealed class UpdateAsset
{
    public string Platform { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public int? VersionCode { get; set; }

    public string DownloadUrl { get; set; } = string.Empty;

    public string? Sha256 { get; set; }

    public string? FileName { get; set; }
}
