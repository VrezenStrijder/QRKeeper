using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;

namespace QRKeeper.Infrastructure.Services;

/// <summary>
/// Starts and exposes the LAN sync runtime for the current app process.
/// </summary>
public sealed class SyncHostService : ISyncHostService
{
    private readonly ISyncDiscoveryService _discoveryService;
    private readonly ISyncListenerService _listenerService;
    private readonly ISyncLocalDeviceProvider _localDeviceProvider;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private bool _isStarted;

    public SyncHostService(
        ISyncDiscoveryService discoveryService,
        ISyncListenerService listenerService,
        ISyncLocalDeviceProvider localDeviceProvider)
    {
        _discoveryService = discoveryService;
        _listenerService = listenerService;
        _localDeviceProvider = localDeviceProvider;
        _discoveryService.PeersChanged += OnPeersChanged;
    }

    /// <inheritdoc />
    public event EventHandler? PeersChanged;

    /// <inheritdoc />
    public SyncDeviceInfo? LocalDevice { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<SyncDeviceInfo> GetPeers()
    {
        return _discoveryService.GetPeers();
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            if (_isStarted)
            {
                return;
            }

            await _listenerService.StartAsync(cancellationToken);
            LocalDevice = _localDeviceProvider.CreateLocalDeviceInfo(_listenerService.Port);
            await _discoveryService.StartAsync(LocalDevice, cancellationToken);
            _isStarted = true;
        }
        catch
        {
            await _discoveryService.StopAsync(CancellationToken.None);
            await _listenerService.StopAsync(CancellationToken.None);
            LocalDevice = null;
            _isStarted = false;
            throw;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task RefreshLocalDeviceAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            if (!_isStarted)
            {
                return;
            }

            SyncDeviceInfo localDevice = _localDeviceProvider.CreateLocalDeviceInfo(_listenerService.Port);
            LocalDevice = localDevice;
            await _listenerService.UpdateLocalDeviceAsync(localDevice, cancellationToken);
            await _discoveryService.UpdateLocalDeviceAsync(localDevice, cancellationToken);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            if (!_isStarted)
            {
                return;
            }

            try
            {
                await Task.WhenAll(
                    _discoveryService.StopAsync(cancellationToken),
                    _listenerService.StopAsync(cancellationToken));
            }
            finally
            {
                LocalDevice = null;
                _isStarted = false;
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _discoveryService.PeersChanged -= OnPeersChanged;
        await StopAsync();
        await _discoveryService.DisposeAsync();
        await _listenerService.DisposeAsync();
        _stateLock.Dispose();
    }

    private void OnPeersChanged(object? sender, EventArgs e)
    {
        PeersChanged?.Invoke(this, EventArgs.Empty);
    }
}
