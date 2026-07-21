using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using QRKeeper.Core.Common;
using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;

namespace QRKeeper.Infrastructure.Services;

public sealed class GitHubUpdateService : IUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly HttpClient httpClient;
    private readonly string repositoryOwner;
    private readonly string repositoryName;
    private readonly string updateManifestAssetName;

    public GitHubUpdateService(HttpClient httpClient)
        : this(
            httpClient,
            AppConstants.UpdateRepositoryOwner,
            AppConstants.UpdateRepositoryName,
            AppConstants.UpdateManifestAssetName)
    {
    }

    public GitHubUpdateService(
        HttpClient httpClient,
        string repositoryOwner,
        string repositoryName,
        string updateManifestAssetName)
    {
        this.httpClient = httpClient;
        this.repositoryOwner = repositoryOwner;
        this.repositoryName = repositoryName;
        this.updateManifestAssetName = updateManifestAssetName;
        this.httpClient.DefaultRequestHeaders.UserAgent.Clear();
        this.httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue(AppConstants.AppName, AppConstants.AppVersion));
        this.httpClient.DefaultRequestHeaders.Accept.Clear();
        this.httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(
        UpdatePlatform platform,
        string currentVersion,
        CancellationToken cancellationToken = default)
    {
        if (IsUpdateSourcePlaceholder(repositoryOwner))
        {
            return UpdateCheckResult.Unavailable(
                currentVersion,
                "Update source is not configured.");
        }

        try
        {
            GitHubRelease? release = await GetLatestReleaseAsync(cancellationToken).ConfigureAwait(false);
            if (release is null)
            {
                return UpdateCheckResult.Unavailable(currentVersion, "Latest GitHub release was not found.");
            }

            GitHubReleaseAsset? manifestAsset = release.Assets.FirstOrDefault(asset =>
                string.Equals(asset.Name, updateManifestAssetName, StringComparison.OrdinalIgnoreCase));
            if (manifestAsset is null)
            {
                return UpdateCheckResult.Unavailable(
                    currentVersion,
                    $"Release does not contain {updateManifestAssetName}.");
            }

            UpdateManifest? manifest = await GetUpdateManifestAsync(
                manifestAsset.BrowserDownloadUrl,
                cancellationToken).ConfigureAwait(false);
            if (manifest is null)
            {
                return UpdateCheckResult.Unavailable(currentVersion, "Update manifest could not be parsed.");
            }

            UpdateAsset? asset = FindPlatformAsset(platform, manifest);
            if (asset is null)
            {
                return UpdateCheckResult.Unavailable(
                    currentVersion,
                    $"Update manifest has no {platform} asset.");
            }

            string latestVersion = string.IsNullOrWhiteSpace(asset.Version)
                ? manifest.Version
                : asset.Version;
            if (string.IsNullOrWhiteSpace(latestVersion))
            {
                return UpdateCheckResult.Unavailable(currentVersion, "Update manifest version is empty.");
            }

            string? downloadUrl = ResolveDownloadUrl(asset, release.Assets);
            string releaseUrl = string.IsNullOrWhiteSpace(manifest.ReleaseUrl)
                ? release.HtmlUrl
                : manifest.ReleaseUrl!;
            string releaseNotes = string.IsNullOrWhiteSpace(manifest.ReleaseNotes)
                ? release.Body ?? string.Empty
                : manifest.ReleaseNotes!;

            if (!IsNewerVersion(latestVersion, currentVersion))
            {
                return UpdateCheckResult.NotAvailable(currentVersion, latestVersion, releaseUrl, releaseNotes);
            }

            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                return UpdateCheckResult.Unavailable(
                    currentVersion,
                    $"Update manifest has no download URL for {platform}.");
            }

            return UpdateCheckResult.Available(
                currentVersion,
                latestVersion,
                downloadUrl,
                releaseUrl,
                releaseNotes);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
        {
            return UpdateCheckResult.Unavailable(currentVersion, $"Update check failed: {ex.Message}");
        }
    }

    private static UpdateAsset? FindPlatformAsset(UpdatePlatform platform, UpdateManifest manifest)
    {
        string platformName = platform.ToString();
        return manifest.Assets.FirstOrDefault(asset =>
            string.Equals(asset.Platform, platformName, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ResolveDownloadUrl(
        UpdateAsset asset,
        IReadOnlyList<GitHubReleaseAsset> releaseAssets)
    {
        if (!string.IsNullOrWhiteSpace(asset.DownloadUrl))
        {
            return asset.DownloadUrl;
        }

        if (string.IsNullOrWhiteSpace(asset.FileName))
        {
            return null;
        }

        return releaseAssets.FirstOrDefault(releaseAsset =>
            string.Equals(releaseAsset.Name, asset.FileName, StringComparison.OrdinalIgnoreCase))
            ?.BrowserDownloadUrl;
    }

    private static bool IsNewerVersion(string latestVersion, string currentVersion)
    {
        if (Version.TryParse(NormalizeVersion(latestVersion), out Version? latest) &&
            Version.TryParse(NormalizeVersion(currentVersion), out Version? current))
        {
            return latest > current;
        }

        return !string.Equals(latestVersion, currentVersion, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeVersion(string version)
    {
        string normalized = version.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        int suffixIndex = normalized.IndexOfAny(['-', '+']);
        return suffixIndex >= 0 ? normalized[..suffixIndex] : normalized;
    }

    private static bool IsUpdateSourcePlaceholder(string repositoryOwner)
    {
        return string.IsNullOrWhiteSpace(repositoryOwner) ||
            repositoryOwner.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<GitHubRelease?> GetLatestReleaseAsync(CancellationToken cancellationToken)
    {
        string uri =
            $"https://api.github.com/repos/{repositoryOwner}/{repositoryName}/releases/latest";
        using Stream stream = await httpClient.GetStreamAsync(uri, cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<GitHubRelease>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<UpdateManifest?> GetUpdateManifestAsync(
        string uri,
        CancellationToken cancellationToken)
    {
        using Stream stream = await httpClient.GetStreamAsync(uri, cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<UpdateManifest>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubReleaseAsset> Assets { get; set; } = new();
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}
