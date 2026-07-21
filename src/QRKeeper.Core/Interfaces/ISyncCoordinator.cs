using QRKeeper.Core.Models;

namespace QRKeeper.Core.Interfaces;

/// <summary>
/// Coordinates bidirectional sync for the local device.
/// </summary>
public interface ISyncCoordinator
{
    /// <summary>
    /// Synchronizes the local device with the selected peer.
    /// </summary>
    Task<SyncSessionResult> SyncAsync(
        SyncDeviceInfo localDevice,
        SyncDeviceInfo peer,
        CancellationToken cancellationToken = default);
}
