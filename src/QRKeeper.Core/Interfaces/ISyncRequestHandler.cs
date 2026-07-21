using QRKeeper.Core.Models;

namespace QRKeeper.Core.Interfaces;

/// <summary>
/// Handles an incoming sync request on the local device.
/// </summary>
public interface ISyncRequestHandler
{
    /// <summary>
    /// Processes the incoming request and returns a sync response.
    /// </summary>
    Task<SyncResponse> HandleAsync(
        SyncDeviceInfo responder,
        SyncRequest request,
        CancellationToken cancellationToken = default);
}
