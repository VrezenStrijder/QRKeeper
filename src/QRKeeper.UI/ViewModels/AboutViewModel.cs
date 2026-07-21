using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRKeeper.Core.Common;
using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;
using QRKeeper.UI.Services;

namespace QRKeeper.UI.ViewModels;

public sealed partial class AboutViewModel : ViewModelBase
{
    private readonly ILocalizationService _localizationService;
    private readonly IUpdateService updateService;
    private readonly IExternalLauncherService externalLauncherService;
    private readonly IMessageService messageService;
    private UpdateCheckResult? updateCheckResult;

    public AboutViewModel(
        ILocalizationService localizationService,
        IUpdateService updateService,
        IExternalLauncherService externalLauncherService,
        IMessageService messageService)
    {
        _localizationService = localizationService;
        this.updateService = updateService;
        this.externalLauncherService = externalLauncherService;
        this.messageService = messageService;
        UpdateStatusText = _localizationService.GetString("Update_NotChecked");
        _localizationService.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(VersionText));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(UpdateTitle));
            OnPropertyChanged(nameof(CheckUpdateText));
            OnPropertyChanged(nameof(OpenUpdateText));
            UpdateStatusText = GetLocalizedUpdateStatus();
        };
    }

    [ObservableProperty]
    private string updateStatusText = string.Empty;

    [ObservableProperty]
    private bool isCheckingUpdates;

    public string AppName => AppConstants.AppName;

    public string VersionText => _localizationService.Format("About_VersionFormat", AppConstants.AppVersion);

    public string Title => _localizationService.GetString("About_Title");

    public string Description => _localizationService.GetString("About_Description");

    public string UpdateTitle => _localizationService.GetString("Update_Title");

    public string CheckUpdateText => _localizationService.GetString("Update_Check");

    public string OpenUpdateText => _localizationService.GetString("Update_OpenDownload");

    public bool CanOpenUpdateDownload => !string.IsNullOrWhiteSpace(updateCheckResult?.DownloadUrl);

    public bool CanCheckForUpdates => !IsCheckingUpdates;

    [RelayCommand(CanExecute = nameof(CanCheckForUpdates))]
    public async Task CheckForUpdatesAsync()
    {
        try
        {
            IsCheckingUpdates = true;
            UpdateStatusText = _localizationService.GetString("Update_Checking");
            updateCheckResult = await updateService.CheckForUpdatesAsync(
                UpdatePlatform.Windows,
                AppConstants.AppVersion);
            UpdateStatusText = GetLocalizedUpdateStatus();
            OnPropertyChanged(nameof(CanOpenUpdateDownload));
            OpenUpdateDownloadCommand.NotifyCanExecuteChanged();

            messageService.Show(
                UpdateTitle,
                UpdateStatusText,
                updateCheckResult.IsUpdateAvailable ? MessageSeverity.Success : MessageSeverity.Info);
        }
        catch (OperationCanceledException)
        {
            UpdateStatusText = _localizationService.GetString("Update_Canceled");
        }
        finally
        {
            IsCheckingUpdates = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenUpdateDownload))]
    public async Task OpenUpdateDownloadAsync()
    {
        string? uri = updateCheckResult?.DownloadUrl ?? updateCheckResult?.ReleaseUrl;
        if (string.IsNullOrWhiteSpace(uri))
        {
            return;
        }

        await externalLauncherService.OpenUriAsync(uri);
    }

    partial void OnIsCheckingUpdatesChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCheckForUpdates));
        CheckForUpdatesCommand.NotifyCanExecuteChanged();
    }

    private string GetLocalizedUpdateStatus()
    {
        if (updateCheckResult is null)
        {
            return _localizationService.GetString("Update_NotChecked");
        }

        if (!string.IsNullOrWhiteSpace(updateCheckResult.Message))
        {
            return updateCheckResult.Message;
        }

        if (updateCheckResult.IsUpdateAvailable)
        {
            return _localizationService.Format(
                "Update_AvailableFormat",
                updateCheckResult.LatestVersion ?? string.Empty);
        }

        return _localizationService.Format(
            "Update_UpToDateFormat",
            updateCheckResult.CurrentVersion);
    }
}
