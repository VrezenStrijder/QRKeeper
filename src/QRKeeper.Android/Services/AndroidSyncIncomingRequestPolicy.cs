using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;

namespace QRKeeper.Android.Services;

/// <summary>
/// Applies Android settings and confirmation UI to incoming LAN sync requests.
/// </summary>
public sealed class AndroidSyncIncomingRequestPolicy : ISyncIncomingRequestPolicy
{
    private readonly AndroidSettingsService _settingsService;
    private readonly AndroidDialogService _dialogService;
    private readonly AndroidTextService _textService;

    public AndroidSyncIncomingRequestPolicy(
        AndroidSettingsService settingsService,
        AndroidDialogService dialogService,
        AndroidTextService textService)
    {
        _settingsService = settingsService;
        _dialogService = dialogService;
        _textService = textService;
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
            ? T("SyncUnknownDevice")
            : request.Sender.DeviceName;
        Task<bool> confirmTask = _dialogService.ConfirmAsync(
            T("SyncIncomingRequestTitle"),
            _textService.Format(_settingsService.Language, "SyncIncomingRequestMessage", senderName, request.Records.Count),
            T("SyncAccept"),
            T("Cancel"));

        return await confirmTask.WaitAsync(cancellationToken);
    }

    private string T(string key)
    {
        return _textService.Get(_settingsService.Language, key);
    }
}
