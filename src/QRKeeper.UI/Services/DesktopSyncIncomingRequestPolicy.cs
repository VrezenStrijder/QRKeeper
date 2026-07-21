using Avalonia.Threading;
using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;

namespace QRKeeper.UI.Services;

/// <summary>
/// Applies desktop settings and confirmation UI to incoming LAN sync requests.
/// </summary>
public sealed class DesktopSyncIncomingRequestPolicy : ISyncIncomingRequestPolicy
{
    private readonly DesktopSettingsService _settingsService;
    private readonly IConfirmationService _confirmationService;
    private readonly ILocalizationService _localizationService;

    public DesktopSyncIncomingRequestPolicy(
        DesktopSettingsService settingsService,
        IConfirmationService confirmationService,
        ILocalizationService localizationService)
    {
        _settingsService = settingsService;
        _confirmationService = confirmationService;
        _localizationService = localizationService;
    }

    /// <inheritdoc />
    public async Task<bool> ShouldAcceptAsync(
        SyncDeviceInfo localDevice,
        SyncRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_settingsService.AutoAcceptLanSyncRequests)
        {
            return true;
        }

        string senderName = string.IsNullOrWhiteSpace(request.Sender.DeviceName)
            ? _localizationService.GetString("Sync_UnknownDevice")
            : request.Sender.DeviceName;
        Task<bool> dialogTask = await Dispatcher.UIThread.InvokeAsync(
            () => _confirmationService.ConfirmAsync(
                _localizationService.GetString("Sync_IncomingRequestTitle"),
                _localizationService.Format("Sync_IncomingRequestMessage", senderName, request.Records.Count),
                _localizationService.GetString("Sync_Accept"),
                _localizationService.GetString("Common_Cancel")),
            DispatcherPriority.Normal,
            cancellationToken);

        return await dialogTask.WaitAsync(cancellationToken);
    }
}
