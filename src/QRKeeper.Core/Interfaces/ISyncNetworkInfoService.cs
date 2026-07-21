namespace QRKeeper.Core.Interfaces;

/// <summary>
/// Provides user-facing local network information for LAN sync screens.
/// </summary>
public interface ISyncNetworkInfoService
{
    /// <summary>
    /// Gets the current network display name when the platform can provide it.
    /// </summary>
    string GetCurrentNetworkName();
}
