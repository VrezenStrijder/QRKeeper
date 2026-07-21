namespace QRKeeper.Core.Models;

public sealed class UpdateManifest
{
    public string Version { get; set; } = string.Empty;

    public int? VersionCode { get; set; }

    public string? ReleaseUrl { get; set; }

    public string? ReleaseNotes { get; set; }

    public bool IsPrerelease { get; set; }

    public List<UpdateAsset> Assets { get; set; } = new();
}
