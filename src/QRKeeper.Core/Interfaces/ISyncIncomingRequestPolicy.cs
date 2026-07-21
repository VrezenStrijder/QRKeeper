using QRKeeper.Core.Models;

namespace QRKeeper.Core.Interfaces;

/// <summary>
/// Decides whether an incoming sync request should be accepted.
/// </summary>
public interface ISyncIncomingRequestPolicy
{
    /// <summary>
    /// Returns true when the incoming request may be processed.
    /// </summary>
    Task<bool> ShouldAcceptAsync(
        SyncDeviceInfo sender,
        SyncRequest request,
        CancellationToken cancellationToken = default);
}
