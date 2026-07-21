using QRKeeper.Core.Models;

namespace QRKeeper.Core.Interfaces;

/// <summary>
/// Starts and exposes the LAN sync runtime.
/// </summary>
public interface ISyncHostService : IAsyncDisposable
{
    /// <summary>
    /// Raised when the discovered peer list changes.
    /// </summary>
    event EventHandler? PeersChanged;

    /// <summary>
    /// Gets the local device description used for sync.
    /// </summary>
    SyncDeviceInfo? LocalDevice { get; }

    /// <summary>
    /// Gets the current discovered peers.
    /// </summary>
    IReadOnlyList<SyncDeviceInfo> GetPeers();

    /// <summary>
    /// Starts the sync runtime.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the advertised local device description.
    /// </summary>
    Task RefreshLocalDeviceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the sync runtime.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
