using QRKeeper.Core.Models;

namespace QRKeeper.Core.Interfaces;

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckForUpdatesAsync(
        UpdatePlatform platform,
        string currentVersion,
        CancellationToken cancellationToken = default);
}
