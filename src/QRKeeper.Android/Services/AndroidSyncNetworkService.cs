using Android;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using QRKeeper.Android;
using QRKeeper.Core.Interfaces;

namespace QRKeeper.Android.Services;

/// <summary>
/// Provides Android LAN sync network capabilities and display information.
/// </summary>
public sealed class AndroidSyncNetworkService : ISyncNetworkInfoService
{
    private const string LogTag = "QRKeeper.Wifi";
    private global::Android.Net.Wifi.WifiManager.MulticastLock? multicastLock;
    private bool multicastLockHeld; // Tracks the non-reference-counted Android lock state.

    public enum NetworkNameState
    {
        Available,
        NotOnWifi,
        MissingPermission,
        LocationServiceOff,
        Unknown,
        Error
    }

    /// <summary>
    /// Keeps Wi-Fi broadcast/multicast packets deliverable while LAN sync is running.
    /// </summary>
    public void AcquireMulticastLock()
    {
        if (multicastLockHeld)
        {
            return;
        }

        try
        {
            global::Android.Net.Wifi.WifiManager? wifiManager = GetWifiManager();
            if (wifiManager is null)
            {
                return;
            }

            global::Android.Net.Wifi.WifiManager.MulticastLock? newLock =
                wifiManager.CreateMulticastLock("QRKeeperLanSync");
            if (newLock is null)
            {
                return;
            }

            newLock.SetReferenceCounted(false);
            newLock.Acquire();
            multicastLock = newLock;
            multicastLockHeld = true;
        }
        catch (Java.Lang.SecurityException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>
    /// Releases the Wi-Fi broadcast/multicast lock if it is currently held.
    /// </summary>
    public void ReleaseMulticastLock()
    {
        if (!multicastLockHeld)
        {
            return;
        }

        try
        {
            multicastLock?.Release();
        }
        catch (Java.Lang.RuntimeException)
        {
        }
        finally
        {
            multicastLockHeld = false;
            multicastLock?.Dispose();
            multicastLock = null;
        }
    }

    /// <inheritdoc />
    public string GetCurrentNetworkName()
    {
        return TryGetCurrentNetworkName(out string? networkName) ? networkName ?? string.Empty : string.Empty;
    }

    /// <summary>
    /// Gets the current network name and a coarse state that explains why the name may be unavailable.
    /// </summary>
    public bool TryGetCurrentNetworkName(out string? networkName, out NetworkNameState state)
    {
        try
        {
            global::Android.Net.ConnectivityManager? connectivityManager =
                global::Android.App.Application.Context.GetSystemService(global::Android.Content.Context.ConnectivityService)
                    as global::Android.Net.ConnectivityManager;
            global::Android.Net.Network? activeNetwork = connectivityManager?.ActiveNetwork;
            global::Android.Net.NetworkCapabilities? capabilities = activeNetwork is null
                ? null
                : connectivityManager?.GetNetworkCapabilities(activeNetwork);
            if (capabilities?.HasTransport(global::Android.Net.TransportType.Wifi) != true)
            {
                networkName = null;
                state = NetworkNameState.NotOnWifi;
                return false;
            }

            string? ssid = GetWifiSsid();
            if (!string.IsNullOrWhiteSpace(ssid))
            {
                networkName = ssid;
                state = NetworkNameState.Available;
                return true;
            }

            networkName = null;
            if (!HasWifiPermission())
            {
                state = NetworkNameState.MissingPermission;
                return false;
            }

            state = IsSystemLocationEnabled() ? NetworkNameState.Unknown : NetworkNameState.LocationServiceOff;
            return false;
        }
        catch (Java.Lang.SecurityException)
        {
            networkName = null;
            state = NetworkNameState.MissingPermission;
            return false;
        }
        catch (InvalidOperationException)
        {
            networkName = null;
            state = NetworkNameState.Error;
            return false;
        }
    }

    public bool TryGetCurrentNetworkName(out string? networkName)
    {
        return TryGetCurrentNetworkName(out networkName, out _);
    }

    /// <summary>
    /// Requests the runtime permission Android needs before it can expose the current Wi-Fi SSID.
    /// </summary>
    public Task<bool> EnsureNetworkNameAccessAsync()
    {
        return MainActivity.Current?.EnsureWifiNetworkNamePermissionAsync() ?? Task.FromResult(false);
    }

    private static bool HasWifiPermission()
    {
        try
        {
            return HasFineLocationPermission() && HasNearbyWifiPermission();
        }
        catch (Java.Lang.SecurityException)
        {
            return false;
        }
    }

    private static bool IsSystemLocationEnabled()
    {
        try
        {
            global::Android.Locations.LocationManager? locationManager =
                global::Android.App.Application.Context.GetSystemService(global::Android.Content.Context.LocationService)
                    as global::Android.Locations.LocationManager;
            if (locationManager is null)
            {
                return false;
            }

            if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
            {
#pragma warning disable CA1416
                return locationManager.IsLocationEnabled;
#pragma warning restore CA1416
            }

            return locationManager.IsProviderEnabled(global::Android.Locations.LocationManager.GpsProvider) ||
                locationManager.IsProviderEnabled(global::Android.Locations.LocationManager.NetworkProvider);
        }
        catch (Java.Lang.RuntimeException)
        {
            return false;
        }
    }

    private static string? GetWifiSsid()
    {
        string? modernSsid = GetModernWifiSsid();
        string? legacySsid = GetLegacyWifiSsid();
        string? ssid = NormalizeWifiSsid(modernSsid) ?? NormalizeWifiSsid(legacySsid);
        if (ssid is null)
        {
            LogWifiSsidDiagnostic(modernSsid, legacySsid);
        }

        return ssid;
    }

    private static string? NormalizeWifiSsid(string? ssid)
    {
        if (string.IsNullOrWhiteSpace(ssid) ||
            ssid.Equals("<unknown ssid>", StringComparison.OrdinalIgnoreCase) ||
            ssid.Equals("0x", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string trimmedSsid = ssid.Trim();
        if (trimmedSsid.Length >= 2 && trimmedSsid[0] == '"' && trimmedSsid[^1] == '"')
        {
            return trimmedSsid[1..^1];
        }

        if (trimmedSsid.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            trimmedSsid.Length > 2)
        {
            return TryDecodeHexSsid(trimmedSsid[2..]) ?? trimmedSsid;
        }

        return trimmedSsid;
    }

    private static string? GetModernWifiSsid()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            return null;
        }

        global::Android.Net.ConnectivityManager? connectivityManager =
            global::Android.App.Application.Context.GetSystemService(global::Android.Content.Context.ConnectivityService)
                as global::Android.Net.ConnectivityManager;
        global::Android.Net.Network? activeNetwork = connectivityManager?.ActiveNetwork;
        global::Android.Net.NetworkCapabilities? capabilities = activeNetwork is null
            ? null
            : connectivityManager?.GetNetworkCapabilities(activeNetwork);
        object? transportInfo = capabilities?.TransportInfo;
        if (transportInfo is global::Android.Net.Wifi.WifiInfo wifiInfo)
        {
            return wifiInfo.SSID;
        }

        if (transportInfo is Java.Lang.Object javaObject)
        {
            try
            {
                return javaObject.JavaCast<global::Android.Net.Wifi.WifiInfo>()?.SSID;
            }
            catch (Exception ex) when (ex is InvalidCastException or Java.Lang.ClassCastException)
            {
                return null;
            }
        }

        return null;
    }

    private static string? GetLegacyWifiSsid()
    {
        try
        {
#pragma warning disable CS0618, CA1422
            return GetWifiManager()?.ConnectionInfo?.SSID;
#pragma warning restore CS0618, CA1422
        }
        catch (Java.Lang.SecurityException)
        {
            return null;
        }
    }

    private static global::Android.Net.Wifi.WifiManager? GetWifiManager()
    {
        return global::Android.App.Application.Context.ApplicationContext?
            .GetSystemService(global::Android.Content.Context.WifiService)
            as global::Android.Net.Wifi.WifiManager;
    }

    private static bool HasFineLocationPermission()
    {
        return global::Android.App.Application.Context.CheckSelfPermission(Manifest.Permission.AccessFineLocation) ==
            Permission.Granted;
    }

    private static bool HasNearbyWifiPermission()
    {
        return !OperatingSystem.IsAndroidVersionAtLeast(33) ||
            global::Android.App.Application.Context.CheckSelfPermission("android.permission.NEARBY_WIFI_DEVICES") ==
            Permission.Granted;
    }

    private static string? TryDecodeHexSsid(string hexSsid)
    {
        if (hexSsid.Length == 0 || hexSsid.Length % 2 != 0)
        {
            return null;
        }

        try
        {
            byte[] bytes = Convert.FromHexString(hexSsid);
            string decodedSsid = System.Text.Encoding.UTF8.GetString(bytes);
            return string.IsNullOrWhiteSpace(decodedSsid) ? null : decodedSsid;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static void LogWifiSsidDiagnostic(string? modernSsid, string? legacySsid)
    {
        global::Android.Util.Log.Info(
            LogTag,
            "SSID unavailable. " +
            $"sdk={Build.VERSION.SdkInt}; " +
            $"fineLocation={HasFineLocationPermission()}; " +
            $"nearbyWifi={HasNearbyWifiPermission()}; " +
            $"locationEnabled={IsSystemLocationEnabled()}; " +
            $"modern={DescribeSsidForLog(modernSsid)}; " +
            $"legacy={DescribeSsidForLog(legacySsid)}");
    }

    private static string DescribeSsidForLog(string? ssid)
    {
        if (ssid is null)
        {
            return "null";
        }

        if (ssid.Length == 0)
        {
            return "empty";
        }

        return ssid.Equals("<unknown ssid>", StringComparison.OrdinalIgnoreCase) ||
            ssid.Equals("0x", StringComparison.OrdinalIgnoreCase)
            ? ssid
            : "available";
    }
}
