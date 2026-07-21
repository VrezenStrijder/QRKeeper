using QRKeeper.Core.Models;

namespace QRKeeper.Core.Interfaces;

/// <summary>
/// Discovers LAN peers and keeps a live peer snapshot.
/// </summary>
public interface ISyncDiscoveryService : IAsyncDisposable
{
    /// <summary>
    /// Raised when the peer snapshot changes.
    /// </summary>
    event EventHandler? PeersChanged;

    /// <summary>
    /// Gets the current peer snapshot.
    /// </summary>
    IReadOnlyList<SyncDeviceInfo> GetPeers();

    /// <summary>
    /// Starts advertising and listening for peers.
    /// </summary>
    Task StartAsync(SyncDeviceInfo localDevice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the local device description used for discovery announcements.
    /// </summary>
    Task UpdateLocalDeviceAsync(SyncDeviceInfo localDevice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops discovery.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
