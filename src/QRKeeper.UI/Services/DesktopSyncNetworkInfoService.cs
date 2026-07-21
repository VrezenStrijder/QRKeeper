using System.Diagnostics;
using System.Net.NetworkInformation;
using QRKeeper.Core.Interfaces;

namespace QRKeeper.UI.Services;

/// <summary>
/// Provides desktop network display information for LAN sync screens.
/// </summary>
public sealed class DesktopSyncNetworkInfoService : ISyncNetworkInfoService
{
    /// <inheritdoc />
    public string GetCurrentNetworkName()
    {
        return GetWindowsWifiSsid()
            ?? GetActiveInterfaceName()
            ?? string.Empty;
    }

    private static string? GetWindowsWifiSsid()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "wlan show interfaces",
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(1500))
            {
                process.Kill(true);
                return null;
            }

            foreach (string line in output.Split(Environment.NewLine))
            {
                string trimmed = line.Trim();
                if (!trimmed.StartsWith("SSID", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("BSSID", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int separatorIndex = trimmed.IndexOf(':', StringComparison.Ordinal);
                if (separatorIndex < 0)
                {
                    continue;
                }

                string ssid = trimmed[(separatorIndex + 1)..].Trim();
                if (!string.IsNullOrWhiteSpace(ssid))
                {
                    return ssid;
                }
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }

        return null;
    }

    private static string? GetActiveInterfaceName()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(networkInterface =>
                    networkInterface.OperationalStatus == OperationalStatus.Up &&
                    networkInterface.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel &&
                    networkInterface.GetIPProperties().GatewayAddresses.Count > 0)
                .Select(networkInterface => networkInterface.Name)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
        }
        catch (NetworkInformationException)
        {
            return null;
        }
    }
}
