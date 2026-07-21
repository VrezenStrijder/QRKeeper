namespace QRKeeper.Core.Common;

/// <summary>
/// Provides default network settings for LAN sync.
/// </summary>
public static class SyncNetworkDefaults
{
    /// <summary>
    /// Gets the default UDP discovery port.
    /// </summary>
    public const int DiscoveryPort = 45454;

    /// <summary>
    /// Gets the default TCP transport port.
    /// </summary>
    public const int TransportPort = 45455;

    /// <summary>
    /// Gets the peer expiration window.
    /// </summary>
    public static TimeSpan PeerTimeout => TimeSpan.FromSeconds(8);

    /// <summary>
    /// Gets the discovery heartbeat interval.
    /// </summary>
    public static TimeSpan DiscoveryInterval => TimeSpan.FromSeconds(2);
}
