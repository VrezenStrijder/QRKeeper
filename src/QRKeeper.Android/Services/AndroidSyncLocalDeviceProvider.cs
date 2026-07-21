using QRKeeper.Core.Common;
using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;

namespace QRKeeper.Android.Services;

/// <summary>
/// Creates the Android device description advertised for LAN sync.
/// </summary>
public sealed class AndroidSyncLocalDeviceProvider : ISyncLocalDeviceProvider
{
    private readonly AndroidSettingsService _settingsService;
    private readonly ISyncNetworkInfoService _networkInfoService;

    public AndroidSyncLocalDeviceProvider(
        AndroidSettingsService settingsService,
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
            DeviceName = GetDeviceName(),
            Platform = SyncDevicePlatform.Android,
            AppName = AppConstants.AppName,
            AppVersion = AppConstants.AppVersion,
            NetworkName = _networkInfoService.GetCurrentNetworkName(),
            ProtocolVersion = AppConstants.SyncProtocolVersion,
            TransportPort = transportPort,
            LastSeenAt = DateTimeOffset.UtcNow
        };
    }

    private static string GetDeviceName()
    {
        string manufacturer = global::Android.OS.Build.Manufacturer ?? string.Empty;
        string model = global::Android.OS.Build.Model ?? string.Empty;
        if (string.IsNullOrWhiteSpace(manufacturer) && string.IsNullOrWhiteSpace(model))
        {
            return "Android";
        }

        if (!string.IsNullOrWhiteSpace(model) &&
            model.StartsWith(manufacturer, StringComparison.OrdinalIgnoreCase))
        {
            return model;
        }

        return $"{manufacturer} {model}".Trim();
    }
}
