using System.Collections.ObjectModel;
using System.Net.Sockets;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using QRKeeper.Core.Common;
using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;
using QRKeeper.UI.Services;

namespace QRKeeper.UI.ViewModels;

public sealed partial class SyncViewModel : ViewModelBase
{
    private readonly ISyncHostService _syncHostService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMessageService _messageService;
    private readonly ILocalizationService _localizationService;
    private readonly ISyncNetworkInfoService _networkInfoService;

    [ObservableProperty]
    private SyncDeviceInfo? _selectedPeer;

    [ObservableProperty]
    private string _statusText;

    [ObservableProperty]
    private bool _isBusy;

    public SyncViewModel(
        ISyncHostService syncHostService,
        IServiceScopeFactory scopeFactory,
        IMessageService messageService,
        ILocalizationService localizationService,
        ISyncNetworkInfoService networkInfoService)
    {
        _syncHostService = syncHostService;
        _scopeFactory = scopeFactory;
        _messageService = messageService;
        _localizationService = localizationService;
        _networkInfoService = networkInfoService;
        _statusText = T("Sync_Ready");
        _syncHostService.PeersChanged += OnPeersChanged;
        _localizationService.LanguageChanged += OnLanguageChanged;
        RefreshPeers();
    }

    public ObservableCollection<SyncDeviceInfo> Peers { get; } = new();

    public bool HasPeers => Peers.Count > 0;

    public bool HasNoPeers => !HasPeers;

    public string LocalDeviceText => _syncHostService.LocalDevice is { } device
        ? F("Sync_LocalDeviceFormat", device.DeviceName, device.TransportPort)
        : T("Sync_NotRunning");

    public string CurrentNetworkText => F("Sync_NetworkFormat", GetCurrentNetworkName());

    [RelayCommand]
    public void RefreshPeers()
    {
        string? selectedDeviceId = SelectedPeer?.DeviceId;
        Peers.Clear();
        foreach (SyncDeviceInfo peer in _syncHostService.GetPeers())
        {
            Peers.Add(peer);
        }

        SelectedPeer = string.IsNullOrWhiteSpace(selectedDeviceId)
            ? Peers.FirstOrDefault()
            : Peers.FirstOrDefault(peer => peer.DeviceId == selectedDeviceId) ?? Peers.FirstOrDefault();
        NotifyPeerPropertiesChanged();
    }

    [RelayCommand(CanExecute = nameof(CanStartSync))]
    public async Task StartSyncAsync()
    {
        SyncDeviceInfo? localDevice = _syncHostService.LocalDevice;
        SyncDeviceInfo? peer = SelectedPeer;
        if (localDevice is null || peer is null)
        {
            StatusText = T("Sync_SelectPeerFirst");
            _messageService.Show(T("Toast_SyncFailed"), StatusText, MessageSeverity.Warning);
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = F("Sync_InProgressFormat", peer.DeviceName);
            using IServiceScope scope = _scopeFactory.CreateScope();
            ISyncCoordinator coordinator = scope.ServiceProvider.GetRequiredService<ISyncCoordinator>();
            SyncSessionResult result = await coordinator.SyncAsync(localDevice, peer);
            if (!result.PeerResponse.Accepted)
            {
                StatusText = result.PeerResponse.Message ?? T("Sync_Rejected");
                _messageService.Show(T("Toast_SyncRejected"), StatusText, MessageSeverity.Warning);
                return;
            }

            StatusText = F("Sync_ResultFormat", result.ImportedCount, result.SkippedCount);
            _messageService.Show(T("Toast_SyncCompleted"), StatusText, MessageSeverity.Success);
        }
        catch (AppException ex)
        {
            SetSyncError(ex.Message);
        }
        catch (IOException ex)
        {
            SetSyncError(ex.Message);
        }
        catch (SocketException ex)
        {
            SetSyncError(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            SetSyncError(ex.Message);
        }
        finally
        {
            IsBusy = false;
            RefreshPeers();
        }
    }

    private bool CanStartSync()
    {
        return !IsBusy && SelectedPeer is not null && _syncHostService.LocalDevice is not null;
    }

    private void SetSyncError(string message)
    {
        StatusText = F("Sync_ErrorFormat", message);
        _messageService.Show(T("Toast_SyncFailed"), StatusText, MessageSeverity.Error);
    }

    private void OnPeersChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(RefreshPeers);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (!IsBusy)
        {
            StatusText = T("Sync_Ready");
        }

        NotifyPeerPropertiesChanged();
    }

    private void NotifyPeerPropertiesChanged()
    {
        OnPropertyChanged(nameof(HasPeers));
        OnPropertyChanged(nameof(HasNoPeers));
        OnPropertyChanged(nameof(LocalDeviceText));
        OnPropertyChanged(nameof(CurrentNetworkText));
        StartSyncCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedPeerChanged(SyncDeviceInfo? value)
    {
        StartSyncCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        StartSyncCommand.NotifyCanExecuteChanged();
    }

    private string T(string key)
    {
        return _localizationService.GetString(key);
    }

    private string F(string key, params object[] args)
    {
        return _localizationService.Format(key, args);
    }

    private string GetCurrentNetworkName()
    {
        string networkName = _networkInfoService.GetCurrentNetworkName();
        return string.IsNullOrWhiteSpace(networkName) ? T("Sync_NetworkUnknown") : networkName;
    }
}
