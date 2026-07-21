using QRKeeper.Core.Models;

namespace QRKeeper.Core.Interfaces;

/// <summary>
/// Creates the local device description used by sync services.
/// </summary>
public interface ISyncLocalDeviceProvider
{
    /// <summary>
    /// Builds the local device info for the specified transport port.
    /// </summary>
    SyncDeviceInfo CreateLocalDeviceInfo(int transportPort);
}
