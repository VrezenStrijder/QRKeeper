namespace QRKeeper.Core.Models;

/// <summary>
/// Describes a QRKeeper device that participates in LAN sync.
/// </summary>
public sealed class SyncDeviceInfo
{
    /// <summary>
    /// Gets or sets the stable device identifier.
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name shown in the peer list.
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the platform type.
    /// </summary>
    public SyncDevicePlatform Platform { get; set; } = SyncDevicePlatform.Unknown;

    /// <summary>
    /// Gets or sets the app name used for validation.
    /// </summary>
    public string AppName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the app version.
    /// </summary>
    public string AppVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the local network when available.
    /// </summary>
    public string NetworkName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sync protocol version.
    /// </summary>
    public int ProtocolVersion { get; set; }

    /// <summary>
    /// Gets or sets the advertised endpoint.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the TCP port used for sync requests.
    /// </summary>
    public int TransportPort { get; set; }

    /// <summary>
    /// Gets or sets the time the device was last seen.
    /// </summary>
    public DateTimeOffset LastSeenAt { get; set; }
}
