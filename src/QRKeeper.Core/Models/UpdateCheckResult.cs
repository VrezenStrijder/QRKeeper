namespace QRKeeper.Core.Models;

public sealed class UpdateCheckResult
{
    private UpdateCheckResult(
        bool isUpdateAvailable,
        string currentVersion,
        string? latestVersion,
        string? downloadUrl,
        string? releaseUrl,
        string? releaseNotes,
        string? message)
    {
        IsUpdateAvailable = isUpdateAvailable;
        CurrentVersion = currentVersion;
        LatestVersion = latestVersion;
        DownloadUrl = downloadUrl;
        ReleaseUrl = releaseUrl;
        ReleaseNotes = releaseNotes;
        Message = message;
    }

    public bool IsUpdateAvailable { get; }

    public string CurrentVersion { get; }

    public string? LatestVersion { get; }

    public string? DownloadUrl { get; }

    public string? ReleaseUrl { get; }

    public string? ReleaseNotes { get; }

    public string? Message { get; }

    public static UpdateCheckResult Available(
        string currentVersion,
        string latestVersion,
        string downloadUrl,
        string? releaseUrl,
        string? releaseNotes)
    {
        return new UpdateCheckResult(true, currentVersion, latestVersion, downloadUrl, releaseUrl, releaseNotes, null);
    }

    public static UpdateCheckResult NotAvailable(
        string currentVersion,
        string latestVersion,
        string? releaseUrl,
        string? releaseNotes)
    {
        return new UpdateCheckResult(false, currentVersion, latestVersion, null, releaseUrl, releaseNotes, null);
    }

    public static UpdateCheckResult Unavailable(string currentVersion, string message)
    {
        return new UpdateCheckResult(false, currentVersion, null, null, null, null, message);
    }
}
