using QRKeeper.Core.Common;
using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;

namespace QRKeeper.UI.Services;

/// <summary>
/// Creates the desktop device description advertised for LAN sync.
/// </summary>
public sealed class DesktopSyncLocalDeviceProvider : ISyncLocalDeviceProvider
{
    private readonly DesktopSettingsService _settingsService;
    private readonly ISyncNetworkInfoService _networkInfoService;

    public DesktopSyncLocalDeviceProvider(
        DesktopSettingsService settingsService,
        ISyncNetworkInfoService networkInfoService)
    {
        _settingsService = settingsService;
        _networkInfoService = networkInfoService;
    }

    /// <inheritdoc />
    public SyncDeviceInfo CreateLocalDeviceInfo(int transportPort)
    {
        return new SyncDeviceInfo
        {
            DeviceId = _settingsService.DeviceId,
            DeviceName = string.IsNullOrWhiteSpace(Environment.MachineName) ? "Desktop" : Environment.MachineName,
            Platform = SyncDevicePlatform.Desktop,
            AppName = AppConstants.AppName,
            AppVersion = AppConstants.AppVersion,
            NetworkName = _networkInfoService.GetCurrentNetworkName(),
            ProtocolVersion = AppConstants.SyncProtocolVersion,
            TransportPort = transportPort,
            LastSeenAt = DateTimeOffset.UtcNow
        };
    }
}
