using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using QRKeeper.Core.Common;
using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;

namespace QRKeeper.Infrastructure.Services;

/// <summary>
/// Broadcasts local device presence and tracks peers on the LAN.
/// </summary>
public sealed class UdpBroadcastSyncDiscoveryService : ISyncDiscoveryService
{
    private readonly SyncMessageSerializer _serializer;
    private readonly object _gate = new();
    private readonly Dictionary<string, SyncDeviceInfo> _peers = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;
    private UdpClient? _client;
    private Task? _sendLoopTask;
    private Task? _receiveLoopTask;
    private Task? _cleanupLoopTask;
    private SyncDeviceInfo? _localDevice;

    public UdpBroadcastSyncDiscoveryService(SyncMessageSerializer serializer)
    {
        _serializer = serializer;
    }

    public event EventHandler? PeersChanged;

    /// <inheritdoc />
    public IReadOnlyList<SyncDeviceInfo> GetPeers()
    {
        lock (_gate)
        {
            return _peers.Values
                .OrderByDescending(peer => peer.LastSeenAt)
                .ThenBy(peer => peer.DeviceName)
                .Select(ClonePeer)
                .ToArray();
        }
    }

    /// <inheritdoc />
    public Task StartAsync(SyncDeviceInfo localDevice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(localDevice);
        if (_client is not null)
        {
            return Task.CompletedTask;
        }

        _localDevice = localDevice;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _client = new UdpClient(AddressFamily.InterNetwork)
        {
            EnableBroadcast = true
        };
        _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _client.Client.Bind(new IPEndPoint(IPAddress.Any, SyncNetworkDefaults.DiscoveryPort));

        _sendLoopTask = SendLoopAsync(_cts.Token);
        _receiveLoopTask = ReceiveLoopAsync(_cts.Token);
        _cleanupLoopTask = CleanupLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateLocalDeviceAsync(SyncDeviceInfo localDevice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(localDevice);
        lock (_gate)
        {
            _localDevice = localDevice;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _cts?.Cancel();
        _client?.Dispose();

        List<Task> tasks = new();
        if (_sendLoopTask is not null)
        {
            tasks.Add(_sendLoopTask);
        }

        if (_receiveLoopTask is not null)
        {
            tasks.Add(_receiveLoopTask);
        }

        if (_cleanupLoopTask is not null)
        {
            tasks.Add(_cleanupLoopTask);
        }

        if (tasks.Count > 0)
        {
            try
            {
                await Task.WhenAll(tasks).WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException)
            {
            }
        }

        lock (_gate)
        {
            _peers.Clear();
        }

        _sendLoopTask = null;
        _receiveLoopTask = null;
        _cleanupLoopTask = null;
        _client = null;
        _cts?.Dispose();
        _cts = null;
        _localDevice = null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                SyncDeviceInfo? localDevice;
                lock (_gate)
                {
                    localDevice = _localDevice;
                }

                if (localDevice is null)
                {
                    break;
                }

                byte[] payload = _serializer.SerializeDeviceInfo(localDevice);
                foreach (IPEndPoint endpoint in GetDiscoveryEndpoints())
                {
                    try
                    {
                        await _client.SendAsync(payload, endpoint, cancellationToken);
                    }
                    catch (SocketException)
                    {
                    }
                }

                await Task.Delay(SyncNetworkDefaults.DiscoveryInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException)
            {
                await Task.Delay(SyncNetworkDefaults.DiscoveryInterval, cancellationToken);
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult result = await _client.ReceiveAsync().WaitAsync(cancellationToken);
                SyncDeviceInfo packet = _serializer.DeserializeDeviceInfo(result.Buffer);
                if (!IsValidPeer(packet))
                {
                    continue;
                }

                SyncDeviceInfo peer = ClonePeer(packet);
                peer.Endpoint = result.RemoteEndPoint.Address.ToString();
                peer.LastSeenAt = DateTimeOffset.UtcNow;

                bool changed = UpsertPeer(peer);
                if (changed)
                {
                    PeersChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(200, cancellationToken);
                }
            }
        }
    }

    private async Task CleanupLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            bool removed = RemoveExpiredPeers();
            if (removed)
            {
                PeersChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private bool UpsertPeer(SyncDeviceInfo peer)
    {
        lock (_gate)
        {
            if (_localDevice is not null &&
                string.Equals(peer.DeviceId, _localDevice.DeviceId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (_peers.TryGetValue(peer.DeviceId, out SyncDeviceInfo? existing))
            {
                if (existing.DeviceName == peer.DeviceName &&
                    existing.Platform == peer.Platform &&
                    existing.Endpoint == peer.Endpoint &&
                    existing.TransportPort == peer.TransportPort &&
                    existing.NetworkName == peer.NetworkName &&
                    existing.AppVersion == peer.AppVersion)
                {
                    existing.LastSeenAt = peer.LastSeenAt;
                    return false;
                }
            }

            _peers[peer.DeviceId] = peer;
            return true;
        }
    }

    private bool RemoveExpiredPeers()
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow - SyncNetworkDefaults.PeerTimeout;
        bool removed = false;

        lock (_gate)
        {
            List<string> staleIds = _peers
                .Where(pair => pair.Value.LastSeenAt < cutoff)
                .Select(pair => pair.Key)
                .ToList();

            foreach (string staleId in staleIds)
            {
                removed |= _peers.Remove(staleId);
            }
        }

        return removed;
    }

    private static bool IsValidPeer(SyncDeviceInfo peer)
    {
        return !string.IsNullOrWhiteSpace(peer.DeviceId) &&
               !string.IsNullOrWhiteSpace(peer.DeviceName) &&
               string.Equals(peer.AppName, AppConstants.AppName, StringComparison.Ordinal) &&
               peer.ProtocolVersion == AppConstants.SyncProtocolVersion &&
               peer.TransportPort > 0;
    }

    private static SyncDeviceInfo ClonePeer(SyncDeviceInfo peer)
    {
        return new SyncDeviceInfo
        {
            DeviceId = peer.DeviceId,
            DeviceName = peer.DeviceName,
            Platform = peer.Platform,
            AppName = peer.AppName,
            AppVersion = peer.AppVersion,
            NetworkName = peer.NetworkName,
            ProtocolVersion = peer.ProtocolVersion,
            Endpoint = peer.Endpoint,
            TransportPort = peer.TransportPort,
            LastSeenAt = peer.LastSeenAt
        };
    }

    private static IReadOnlyList<IPEndPoint> GetDiscoveryEndpoints()
    {
        List<IPAddress> addresses = [IPAddress.Broadcast];

        try
        {
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                    networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                foreach (UnicastIPAddressInformation unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
                {
                    if (unicastAddress.Address.AddressFamily != AddressFamily.InterNetwork ||
                        unicastAddress.IPv4Mask is null)
                    {
                        continue;
                    }

                    IPAddress? broadcastAddress = GetSubnetBroadcastAddress(
                        unicastAddress.Address,
                        unicastAddress.IPv4Mask);
                    if (broadcastAddress is not null)
                    {
                        addresses.Add(broadcastAddress);
                    }
                }
            }
        }
        catch (NetworkInformationException)
        {
        }
        catch (SocketException)
        {
        }

        return addresses
            .Distinct()
            .Select(address => new IPEndPoint(address, SyncNetworkDefaults.DiscoveryPort))
            .ToArray();
    }

    private static IPAddress? GetSubnetBroadcastAddress(IPAddress address, IPAddress subnetMask)
    {
        byte[] addressBytes = address.GetAddressBytes();
        byte[] maskBytes = subnetMask.GetAddressBytes();
        if (addressBytes.Length != maskBytes.Length)
        {
            return null;
        }

        byte[] broadcastBytes = new byte[addressBytes.Length];
        for (int index = 0; index < addressBytes.Length; index++)
        {
            broadcastBytes[index] = (byte)(addressBytes[index] | ~maskBytes[index]);
        }

        return new IPAddress(broadcastBytes);
    }
}
