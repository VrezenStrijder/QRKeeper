using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;

namespace QRKeeper.Infrastructure.Services;

/// <summary>
/// Default first-version policy that accepts all incoming sync requests.
/// </summary>
public sealed class AllowAllSyncIncomingRequestPolicy : ISyncIncomingRequestPolicy
{
    /// <inheritdoc />
    public Task<bool> ShouldAcceptAsync(
        SyncDeviceInfo sender,
        SyncRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}
