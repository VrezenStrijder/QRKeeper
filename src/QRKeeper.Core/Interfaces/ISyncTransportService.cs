using QRKeeper.Core.Models;

namespace QRKeeper.Core.Interfaces;

/// <summary>
/// Sends a sync request to a peer and returns its response.
/// </summary>
public interface ISyncTransportService
{
    /// <summary>
    /// Sends the request to the selected peer.
    /// </summary>
    Task<SyncResponse> SendAsync(
        SyncDeviceInfo peer,
        SyncRequest request,
        CancellationToken cancellationToken = default);
}
