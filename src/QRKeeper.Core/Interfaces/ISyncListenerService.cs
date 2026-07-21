using QRKeeper.Core.Models;

namespace QRKeeper.Core.Interfaces;

/// <summary>
/// Hosts the local sync request listener.
/// </summary>
public interface ISyncListenerService : IAsyncDisposable
{
    /// <summary>
    /// Gets the active transport port.
    /// </summary>
    int Port { get; }

    /// <summary>
    /// Starts the listener.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the local device description used in incoming sync responses.
    /// </summary>
    Task UpdateLocalDeviceAsync(SyncDeviceInfo localDevice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the listener.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
