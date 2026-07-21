using System.Collections.ObjectModel;
using System.Net.Sockets;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRKeeper.Android.Services;
using QRKeeper.Core.Common;
using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;

namespace QRKeeper.Android.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    private readonly IQRRecordRepository _repository;
    private readonly IImageStorageService _imageStorage;
    private readonly IFilePickerService _filePicker;
    private readonly ICameraService _cameraService;
    private readonly IContentTypeDetector _contentTypeDetector;
    private readonly IQRCodeService _qrCodeService;
    private readonly IBackupService _backupService;
    private readonly AndroidBackupFileService _backupFiles;
    private readonly AndroidDialogService _dialogService;
    private readonly AndroidSettingsService _settingsService;
    private readonly AndroidTextService _textService;
    private readonly AndroidQrImageShareService _qrImageShareService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISyncHostService _syncHostService;
    private readonly ISyncNetworkInfoService _networkInfoService;
    private readonly IUpdateService updateService;
    private readonly IExternalLauncherService externalLauncherService;
    private ImportPreview? _currentImportPreview;
    private UpdateCheckResult? updateCheckResult;
    private bool _isPreparingSyncNetworkName; // Prevents duplicate Android permission prompts.

    [ObservableProperty]
    private string _selectedSection = "Records";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private QRRecord? _selectedRecord;

    [ObservableProperty]
    private bool _isDetailOpen;

    [ObservableProperty]
    private bool _useWideLayout;

    [ObservableProperty]
    private string _newContent = string.Empty;

    [ObservableProperty]
    private string _newName = GenerateDefaultName();

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string _editNote = string.Empty;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private Bitmap? _selectedImage;

    [ObservableProperty]
    private string _backupStatusText = "Backup is ready.";

    [ObservableProperty]
    private string _backupManifestText = "No import preview loaded.";

    [ObservableProperty]
    private AndroidThemeMode _selectedTheme;

    [ObservableProperty]
    private AndroidAppLanguage _selectedLanguage;

    [ObservableProperty]
    private AndroidColorStyle _selectedColorStyle;

    [ObservableProperty]
    private SyncDeviceInfo? _selectedSyncPeer;

    [ObservableProperty]
    private string _syncStatusText = string.Empty;

    [ObservableProperty]
    private bool _isSyncBusy;

    [ObservableProperty]
    private bool _autoAcceptLanSyncRequests;

    [ObservableProperty]
    private string updateStatusText = string.Empty;

    [ObservableProperty]
    private bool isCheckingUpdates;

    public MainViewModel(
        IQRRecordRepository repository,
        IImageStorageService imageStorage,
        IFilePickerService filePicker,
        ICameraService cameraService,
        IContentTypeDetector contentTypeDetector,
        IQRCodeService qrCodeService,
        IBackupService backupService,
        AndroidBackupFileService backupFiles,
        AndroidDialogService dialogService,
        AndroidSettingsService settingsService,
        AndroidTextService textService,
        AndroidQrImageShareService qrImageShareService,
        IServiceScopeFactory scopeFactory,
        ISyncHostService syncHostService,
        ISyncNetworkInfoService networkInfoService,
        IUpdateService updateService,
        IExternalLauncherService externalLauncherService)
    {
        _repository = repository;
        _imageStorage = imageStorage;
        _filePicker = filePicker;
        _cameraService = cameraService;
        _contentTypeDetector = contentTypeDetector;
        _qrCodeService = qrCodeService;
        _backupService = backupService;
        _backupFiles = backupFiles;
        _dialogService = dialogService;
        _settingsService = settingsService;
        _textService = textService;
        _qrImageShareService = qrImageShareService;
        _scopeFactory = scopeFactory;
        _syncHostService = syncHostService;
        _networkInfoService = networkInfoService;
        this.updateService = updateService;
        this.externalLauncherService = externalLauncherService;
        _syncHostService.PeersChanged += OnSyncPeersChanged;
        _selectedTheme = _settingsService.Theme;
        _selectedLanguage = _settingsService.Language;
        _selectedColorStyle = _settingsService.ColorStyle;
        _autoAcceptLanSyncRequests = _settingsService.AutoAcceptLanSyncRequests;
        SyncStatusText = T("SyncReady");
        StatusText = T("StatusReady");
        BackupStatusText = T("BackupReady");
        BackupManifestText = T("NoImportPreview");
        UpdateStatusText = T("UpdateNotChecked");
    }

    public ObservableCollection<QRRecord> Records { get; } = new();

    public ObservableCollection<AndroidRecordListItemViewModel> RecordItems { get; } = new();

    public ObservableCollection<ImportPreviewItem> ImportPreviewItems { get; } = new();

    public ObservableCollection<SyncDeviceInfo> SyncPeers { get; } = new();

    public ObservableCollection<AndroidToastMessageViewModel> ToastMessages { get; } = new();

    public string AppName => AppConstants.AppName;

    public string RefreshText => T("Refresh");

    public string RecordsText => T("Records");

    public string ScanText => T("Scan");

    public string BackupText => T("Backup");

    public string SyncText => T("Sync");

    public string SettingsText => T("Settings");

    public string SearchRecordsText => T("SearchRecords");

    public string DetailText => T("Detail");

    public string BackText => T("Back");

    public string NameText => T("Name");

    public string NoteText => T("Note");

    public string SaveText => T("Save");

    public string DeleteText => T("Delete");

    public string ShareText => T("Share");

    public string ShareQrImageText => T("ShareQrImage");

    public string SaveQrImageText => T("SaveQrImage");

    public string MoveUpText => T("MoveUp");

    public string MoveDownText => T("MoveDown");

    public string AddQrContentText => T("AddQrContent");

    public string CreateQrText => T("CreateQr");

    public string PasteQrContentText => T("PasteQrContent");

    public string SaveNewQrText => T("SaveNewQr");

    public string ImportText => T("Import");

    public string ImportImageText => T("ImportImage");

    public string ScanWithCameraText => T("ScanWithCamera");

    public string ScanTipText => T("ScanTip");

    public string SelectRecordText => T("SelectRecord");

    public string BackupDescriptionText => T("BackupDescription");

    public string CreateBackupText => T("CreateBackup");

    public string RestoreBackupText => T("RestoreBackup");

    public string PreviewImportText => T("PreviewImport");

    public string ImportSelectedText => T("ImportSelected");

    public string RestoreSafetyNoteText => T("RestoreSafetyNote");

    public string ThemeText => T("Theme");

    public string ThemeSystemText => T("ThemeSystem");

    public string ThemeLightText => T("ThemeLight");

    public string ThemeDarkText => T("ThemeDark");

    public string LanguageText => T("Language");

    public string LanguageChineseText => T("LanguageChinese");

    public string LanguageEnglishText => T("LanguageEnglish");

    public string AboutText => T("About");

    public string AboutDescriptionText => T("AboutDescription");

    public string UpdateText => T("Update");

    public string CheckUpdateText => T("CheckUpdate");

    public string OpenUpdateText => T("OpenUpdate");

    public string ColorStyleText => T("ColorStyle");

    public string ColorStyleOceanText => T("ColorStyleOcean");

    public string ColorStyleForestText => T("ColorStyleForest");

    public string ColorStyleRoseText => T("ColorStyleRose");

    public string CurrentSettingsText => _textService.Format(
        SelectedLanguage,
        "CurrentSettings",
        CurrentThemeLabel,
        CurrentLanguageLabel,
        CurrentColorStyleLabel);

    public string TypeText => T("Type");

    public string CancelPreviewText => T("CancelPreview");

    public string SyncDevicesText => T("SyncDevices");

    public string SyncLocalDeviceText => T("SyncLocalDevice");

    public string SyncStartText => T("SyncStart");

    public string SyncNoPeersText => T("SyncNoPeers");

    public string SyncTroubleshootingText => T("SyncTroubleshooting");

    public string AutoAcceptLanSyncText => T("AutoAcceptLanSync");

    public string AutoAcceptLanSyncDescriptionText => T("AutoAcceptLanSyncDescription");

    public string AutoAcceptLanSyncStateText => AutoAcceptLanSyncRequests ? T("Enabled") : T("Disabled");

    public bool IsRecordsSelected => SelectedSection == "Records";

    public bool IsRecordsListVisible => IsRecordsSelected && (!IsDetailOpen || UseWideLayout);

    public bool IsRecordDetailVisible => IsRecordsSelected && HasSelection && (IsDetailOpen || UseWideLayout);

    public bool ShowRecordPlaceholder => IsRecordsSelected && UseWideLayout && !HasSelection;

    public bool IsRecordAddVisible => IsScanSelected || (IsRecordsSelected && IsDetailOpen && SelectedRecord is null);

    public bool IsNarrowLayout => !UseWideLayout;

    public bool IsScanSelected => SelectedSection == "Scan";

    public bool IsBackupSelected => SelectedSection == "Backup";

    public bool IsSyncSelected => SelectedSection == "Sync";

    public bool IsSettingsSelected => SelectedSection == "Settings";

    public bool HasSelection => SelectedRecord is not null;

    public string SelectedContent => SelectedRecord?.Content ?? string.Empty;

    public string SelectedContentType => SelectedRecord?.ContentType.ToString() ?? string.Empty;

    public string SelectedContentTypeLabel => $"{TypeText}: {SelectedContentType}";

    public string RecordCountText => Records.Count == 0 ? "No records" : $"{Records.Count} records";

    public string VersionText => _textService.Format(SelectedLanguage, "VersionFormat", AppConstants.AppVersion);

    public bool CanCheckForUpdates => !IsCheckingUpdates;

    public bool CanOpenUpdateDownload => !string.IsNullOrWhiteSpace(updateCheckResult?.DownloadUrl);

    public bool HasImportPreview => _currentImportPreview is not null;

    public bool HasSyncPeers => SyncPeers.Count > 0;

    public bool HasNoSyncPeers => !HasSyncPeers;

    public string LocalSyncDeviceText => _syncHostService.LocalDevice is { } device
        ? _textService.Format(SelectedLanguage, "SyncLocalDeviceFormat", device.DeviceName, device.TransportPort)
        : T("SyncNotRunning");

    public string CurrentSyncNetworkText => _textService.Format(
        SelectedLanguage,
        "SyncNetworkFormat",
        GetCurrentNetworkName());

    public string PreviewImportActionText => HasImportPreview ? CancelPreviewText : PreviewImportText;

    public IBrush PreviewImportButtonBackground => HasImportPreview
        ? ResourceBrush("AppDangerBrush")
        : Brushes.Transparent;

    public IBrush PreviewImportButtonForeground => HasImportPreview
        ? ResourceBrush("AppOnAccentBrush")
        : ResourceBrush("AppAccentBrush");

    public IBrush RecordsTabBackground => IsRecordsSelected ? ResourceBrush("AppSelectedBrush") : Brushes.Transparent;

    public IBrush ScanTabBackground => IsScanSelected ? ResourceBrush("AppSelectedBrush") : Brushes.Transparent;

    public IBrush BackupTabBackground => IsBackupSelected ? ResourceBrush("AppSelectedBrush") : Brushes.Transparent;

    public IBrush SyncTabBackground => IsSyncSelected ? ResourceBrush("AppSelectedBrush") : Brushes.Transparent;

    public IBrush SettingsTabBackground => IsSettingsSelected ? ResourceBrush("AppSelectedBrush") : Brushes.Transparent;

    public IBrush RecordsTabBorderBrush => IsRecordsSelected ? ResourceBrush("AppAccentBrush") : Brushes.Transparent;

    public IBrush ScanTabBorderBrush => IsScanSelected ? ResourceBrush("AppAccentBrush") : Brushes.Transparent;

    public IBrush BackupTabBorderBrush => IsBackupSelected ? ResourceBrush("AppAccentBrush") : Brushes.Transparent;

    public IBrush SyncTabBorderBrush => IsSyncSelected ? ResourceBrush("AppAccentBrush") : Brushes.Transparent;

    public IBrush SettingsTabBorderBrush => IsSettingsSelected ? ResourceBrush("AppAccentBrush") : Brushes.Transparent;

    public IBrush RecordsTabForeground => IsRecordsSelected ? ResourceBrush("AppAccentBrush") : ResourceBrush("AppTextMutedBrush");

    public IBrush ScanTabForeground => IsScanSelected ? ResourceBrush("AppAccentBrush") : ResourceBrush("AppTextMutedBrush");

    public IBrush BackupTabForeground => IsBackupSelected ? ResourceBrush("AppAccentBrush") : ResourceBrush("AppTextMutedBrush");

    public IBrush SyncTabForeground => IsSyncSelected ? ResourceBrush("AppAccentBrush") : ResourceBrush("AppTextMutedBrush");

    public IBrush SettingsTabForeground => IsSettingsSelected ? ResourceBrush("AppAccentBrush") : ResourceBrush("AppTextMutedBrush");

    public IBrush ThemeSystemOptionBackground => OptionBackground(IsSystemTheme);

    public IBrush ThemeLightOptionBackground => OptionBackground(IsLightTheme);

    public IBrush ThemeDarkOptionBackground => OptionBackground(IsDarkTheme);

    public IBrush ThemeSystemOptionBorderBrush => OptionBorderBrush(IsSystemTheme);

    public IBrush ThemeLightOptionBorderBrush => OptionBorderBrush(IsLightTheme);

    public IBrush ThemeDarkOptionBorderBrush => OptionBorderBrush(IsDarkTheme);

    public IBrush ColorStyleOceanOptionBackground => OptionBackground(IsOceanColorStyle);

    public IBrush ColorStyleForestOptionBackground => OptionBackground(IsForestColorStyle);

    public IBrush ColorStyleRoseOptionBackground => OptionBackground(IsRoseColorStyle);

    public IBrush ColorStyleOceanOptionBorderBrush => OptionBorderBrush(IsOceanColorStyle);

    public IBrush ColorStyleForestOptionBorderBrush => OptionBorderBrush(IsForestColorStyle);

    public IBrush ColorStyleRoseOptionBorderBrush => OptionBorderBrush(IsRoseColorStyle);

    public IBrush LanguageChineseOptionBackground => OptionBackground(IsChineseLanguage);

    public IBrush LanguageEnglishOptionBackground => OptionBackground(IsEnglishLanguage);

    public IBrush LanguageChineseOptionBorderBrush => OptionBorderBrush(IsChineseLanguage);

    public IBrush LanguageEnglishOptionBorderBrush => OptionBorderBrush(IsEnglishLanguage);

    public IBrush AutoAcceptLanSyncOptionBackground => OptionBackground(AutoAcceptLanSyncRequests);

    public IBrush AutoAcceptLanSyncOptionBorderBrush => OptionBorderBrush(AutoAcceptLanSyncRequests);

    public bool IsSystemTheme
    {
        get => SelectedTheme == AndroidThemeMode.System;
        set
        {
            if (value)
            {
                UseSystemTheme();
            }
        }
    }

    public bool IsLightTheme
    {
        get => SelectedTheme == AndroidThemeMode.Light;
        set
        {
            if (value)
            {
                UseLightTheme();
            }
        }
    }

    public bool IsDarkTheme
    {
        get => SelectedTheme == AndroidThemeMode.Dark;
        set
        {
            if (value)
            {
                UseDarkTheme();
            }
        }
    }

    public bool IsChineseLanguage
    {
        get => SelectedLanguage == AndroidAppLanguage.Chinese;
        set
        {
            if (value)
            {
                UseChineseLanguage();
            }
        }
    }

    public bool IsEnglishLanguage
    {
        get => SelectedLanguage == AndroidAppLanguage.English;
        set
        {
            if (value)
            {
                UseEnglishLanguage();
            }
        }
    }

    public bool IsOceanColorStyle
    {
        get => SelectedColorStyle == AndroidColorStyle.Ocean;
        set
        {
            if (value)
            {
                UseOceanColorStyle();
            }
        }
    }

    public bool IsForestColorStyle
    {
        get => SelectedColorStyle == AndroidColorStyle.Forest;
        set
        {
            if (value)
            {
                UseForestColorStyle();
            }
        }
    }

    public bool IsRoseColorStyle
    {
        get => SelectedColorStyle == AndroidColorStyle.Rose;
        set
        {
            if (value)
            {
                UseRoseColorStyle();
            }
        }
    }

    public string CurrentThemeLabel => SelectedTheme switch
    {
        AndroidThemeMode.Light => ThemeLightText,
        AndroidThemeMode.Dark => ThemeDarkText,
        _ => ThemeSystemText
    };

    public string CurrentLanguageLabel => SelectedLanguage == AndroidAppLanguage.Chinese
        ? LanguageChineseText
        : LanguageEnglishText;

    public string CurrentColorStyleLabel => SelectedColorStyle switch
    {
        AndroidColorStyle.Forest => ColorStyleForestText,
        AndroidColorStyle.Rose => ColorStyleRoseText,
        _ => ColorStyleOceanText
    };

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RefreshAsync();
        RefreshSyncPeers();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        int? selectedId = SelectedRecord?.Id;
        IReadOnlyList<QRRecord> records = await _repository.SearchAsync(SearchText, null, null);

        Records.Clear();
        RecordItems.Clear();
        foreach (QRRecord record in records)
        {
            Records.Add(record);
            RecordItems.Add(new AndroidRecordListItemViewModel(record));
        }

        SelectedRecord = selectedId.HasValue
            ? Records.FirstOrDefault(record => record.Id == selectedId.Value)
            : UseWideLayout ? Records.FirstOrDefault() : null;
        SyncRecordItemSelection();
        StatusText = RecordCountText;
        OnPropertyChanged(nameof(RecordCountText));
    }

    [RelayCommand(CanExecute = nameof(CanCheckForUpdates))]
    public async Task CheckForUpdatesAsync()
    {
        try
        {
            IsCheckingUpdates = true;
            UpdateStatusText = T("UpdateChecking");
            updateCheckResult = await updateService.CheckForUpdatesAsync(
                UpdatePlatform.Android,
                AppConstants.AppVersion);
            UpdateStatusText = GetUpdateStatusText();
            OnPropertyChanged(nameof(CanOpenUpdateDownload));
            OpenUpdateDownloadCommand.NotifyCanExecuteChanged();
            ShowMessage(T("Toast_Update"), UpdateStatusText, updateCheckResult.IsUpdateAvailable
                ? AndroidMessageSeverity.Success
                : AndroidMessageSeverity.Info);
        }
        catch (OperationCanceledException)
        {
            UpdateStatusText = T("UpdateCanceled");
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

    [RelayCommand]
    public void SelectSection(string section)
    {
        SelectedSection = section;
        if (section != "Records")
        {
            IsDetailOpen = false;
        }

        if (section == "Sync")
        {
            RefreshSyncPeers();
            _ = PrepareSyncNetworkNameAsync();
        }
    }

    [RelayCommand]
    public void RefreshSyncPeers()
    {
        string? selectedDeviceId = SelectedSyncPeer?.DeviceId;
        SyncPeers.Clear();
        foreach (SyncDeviceInfo peer in _syncHostService.GetPeers())
        {
            SyncPeers.Add(peer);
        }

        SelectedSyncPeer = string.IsNullOrWhiteSpace(selectedDeviceId)
            ? SyncPeers.FirstOrDefault()
            : SyncPeers.FirstOrDefault(peer => peer.DeviceId == selectedDeviceId) ?? SyncPeers.FirstOrDefault();

        if (!IsSyncBusy)
        {
            SyncStatusText = HasSyncPeers ? T("SyncReady") : T("SyncNoPeers");
        }

        NotifySyncPeerPropertiesChanged();
    }

    [RelayCommand(CanExecute = nameof(CanStartSync))]
    public async Task StartSyncAsync()
    {
        SyncDeviceInfo? localDevice = _syncHostService.LocalDevice;
        SyncDeviceInfo? peer = SelectedSyncPeer;
        if (localDevice is null || peer is null)
        {
            SyncStatusText = T("SyncSelectPeerFirst");
            ShowMessage(T("Toast_SyncFailed"), SyncStatusText, AndroidMessageSeverity.Warning);
            return;
        }

        try
        {
            IsSyncBusy = true;
            SyncStatusText = _textService.Format(SelectedLanguage, "SyncInProgress", peer.DeviceName);
            using IServiceScope scope = _scopeFactory.CreateScope();
            ISyncCoordinator coordinator = scope.ServiceProvider.GetRequiredService<ISyncCoordinator>();
            SyncSessionResult result = await coordinator.SyncAsync(localDevice, peer);
            if (!result.PeerResponse.Accepted)
            {
                SyncStatusText = result.PeerResponse.Message ?? T("SyncRejected");
                ShowMessage(T("Toast_SyncRejected"), SyncStatusText, AndroidMessageSeverity.Warning);
                return;
            }

            await RefreshAsync();
            SyncStatusText = _textService.Format(SelectedLanguage, "SyncResult", result.ImportedCount, result.SkippedCount);
            ShowMessage(T("Toast_SyncCompleted"), SyncStatusText, AndroidMessageSeverity.Success);
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
            string finalStatus = SyncStatusText;
            IsSyncBusy = false;
            RefreshSyncPeers();
            SyncStatusText = finalStatus;
        }
    }

    [RelayCommand]
    public void ToggleAutoAcceptLanSyncRequests()
    {
        AutoAcceptLanSyncRequests = !AutoAcceptLanSyncRequests;
        ShowMessage(AutoAcceptLanSyncText, AutoAcceptLanSyncStateText);
    }

    [RelayCommand]
    public void CreateRecord()
    {
        SelectedRecord = null;
        NewName = GenerateDefaultName();
        IsDetailOpen = true;
        SelectedSection = "Records";
    }

    [RelayCommand]
    public void OpenSelectedRecord()
    {
        if (SelectedRecord is not null && !UseWideLayout)
        {
            IsDetailOpen = true;
        }
    }

    [RelayCommand]
    public void BackToRecords()
    {
        IsDetailOpen = false;
    }

    [RelayCommand]
    public async Task ScanCameraAsync()
    {
        try
        {
            if (!_cameraService.IsSupported)
            {
                StatusText = "Camera is not available.";
                ShowMessage(T("Toast_CameraUnavailable"), StatusText, AndroidMessageSeverity.Warning);
                return;
            }

            StatusText = "Opening camera...";
            ScanResult? result = await _cameraService.ScanAsync();
            if (result is null || string.IsNullOrWhiteSpace(result.Content))
            {
                StatusText = "No QR code found in the captured image.";
                ShowMessage(T("Toast_NoQrCodeFound"), StatusText, AndroidMessageSeverity.Warning);
                return;
            }

            QRRecord record = await AddRecordAsync(result.Content, result.Source);
            await RefreshAsync();
            SelectedRecord = Records.FirstOrDefault(existing => existing.Id == record.Id) ?? Records.FirstOrDefault();
            SelectedSection = "Records";
            IsDetailOpen = UseWideLayout;
            StatusText = "QR code scanned.";
            ShowMessage(T("Toast_QrScanned"), record.Name, AndroidMessageSeverity.Success);
        }
        catch (AppException ex)
        {
            StatusText = ex.Message;
            ShowMessage(T("Toast_CameraScanFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
        catch (IOException ex)
        {
            StatusText = ex.Message;
            ShowMessage(T("Toast_CameraScanFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
        catch (InvalidOperationException ex)
        {
            StatusText = ex.Message;
            ShowMessage(T("Toast_CameraScanFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
    }

    [RelayCommand]
    public async Task ImportImageAsync()
    {
        try
        {
            await using Stream? stream = await _filePicker.PickImageFileAsync();
            if (stream is null)
            {
                StatusText = "Image selection canceled.";
                ShowMessage(T("Toast_ImageImport"), StatusText);
                return;
            }

            await ImportImageStreamAsync(stream, T("Toast_ImportedQr"));
        }
        catch (AppException ex)
        {
            StatusText = ex.Message;
            ShowMessage(T("Toast_ImageImportFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
        catch (IOException ex)
        {
            StatusText = ex.Message;
            ShowMessage(T("Toast_ImageImportFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
        catch (InvalidOperationException ex)
        {
            StatusText = ex.Message;
            ShowMessage(T("Toast_ImageImportFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
    }

    public async Task ImportSharedImageAsync(global::Android.Net.Uri uri)
    {
        try
        {
            await using Stream? stream = global::Android.App.Application.Context.ContentResolver?.OpenInputStream(uri);
            if (stream is null)
            {
                StatusText = "Shared image could not be opened.";
                ShowMessage(T("Toast_ImageImportFailed"), StatusText, AndroidMessageSeverity.Error);
                return;
            }

            await ImportImageStreamAsync(stream, T("Toast_SharedImageImported"));
        }
        catch (AppException ex)
        {
            StatusText = ex.Message;
            ShowMessage(T("Toast_ImageImportFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
        catch (IOException ex)
        {
            StatusText = ex.Message;
            ShowMessage(T("Toast_ImageImportFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
        catch (InvalidOperationException ex)
        {
            StatusText = ex.Message;
            ShowMessage(T("Toast_ImageImportFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    public async Task ShareQrImageAsync()
    {
        await ShareRecordQrImageAsync(SelectedRecord);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    public async Task SaveQrImageAsync()
    {
        await SaveRecordQrImageAsync(SelectedRecord);
    }

    [RelayCommand]
    public async Task ShareRecordQrImageAsync(QRRecord? record)
    {
        try
        {
            if (record is null)
            {
                return;
            }

            string path = _imageStorage.GetImagePath(record.ImageFileName);
            await _qrImageShareService.ShareAsync(path, T("ShareQrImage"));
            StatusText = "QR image share sheet opened.";
            ShowMessage(T("Toast_QrImageShared"), record.Name, AndroidMessageSeverity.Success);
        }
        catch (IOException ex)
        {
            StatusText = ex.Message;
            ShowMessage(T("Toast_QrImageShareFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
        catch (InvalidOperationException ex)
        {
            StatusText = ex.Message;
            ShowMessage(T("Toast_QrImageShareFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
    }

    [RelayCommand]
    public async Task SaveRecordQrImageAsync(QRRecord? record)
    {
        try
        {
            if (record is null)
            {
                return;
            }

            string path = _imageStorage.GetImagePath(record.ImageFileName);
            string? savedPath = await _qrImageShareService.SaveAsync(
                path,
                GetQrImageFileName(record));
            if (string.IsNullOrWhiteSpace(savedPath))
            {
                StatusText = "QR image save canceled.";
                ShowMessage(T("Toast_QrImageSave"), StatusText);
                return;
            }

            StatusText = $"QR image saved: {savedPath}";
            ShowMessage(T("Toast_QrImageSaved"), savedPath, AndroidMessageSeverity.Success);
        }
        catch (IOException ex)
        {
            StatusText = ex.Message;
            ShowMessage(T("Toast_QrImageSaveFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
        catch (InvalidOperationException ex)
        {
            StatusText = ex.Message;
            ShowMessage(T("Toast_QrImageSaveFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
    }

    [RelayCommand]
    public async Task SaveNewAsync()
    {
        try
        {
            string content = NewContent.Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                StatusText = "Enter QR content first.";
                ShowMessage(T("Toast_RecordSaveFailed"), StatusText, AndroidMessageSeverity.Warning);
                return;
            }

            string name = string.IsNullOrWhiteSpace(NewName) ? GenerateDefaultName() : NewName.Trim();
            if (name.Length > AppConstants.MaxNameLength)
            {
                StatusText = $"Name cannot exceed {AppConstants.MaxNameLength} characters.";
                ShowMessage(T("Toast_RecordSaveFailed"), StatusText, AndroidMessageSeverity.Warning);
                return;
            }

            QRRecord record = await AddRecordAsync(content, QRRecordSource.Manual, name);
            NewContent = string.Empty;
            NewName = GenerateDefaultName();
            await RefreshAsync();
            SelectedRecord = Records.FirstOrDefault(existing => existing.Id == record.Id) ?? Records.FirstOrDefault();
            SelectedSection = "Records";
            IsDetailOpen = UseWideLayout;
            StatusText = "Record saved.";
            ShowMessage(T("Toast_RecordSaved"), record.Name, AndroidMessageSeverity.Success);
        }
        catch (AppException ex)
        {
            StatusText = ex.Message;
            ShowMessage(T("Toast_RecordSaveFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    public async Task SaveEditAsync()
    {
        try
        {
            if (SelectedRecord is null)
            {
                return;
            }

            QRRecord updated = new()
            {
                Id = SelectedRecord.Id,
                Name = EditName.Trim(),
                Content = SelectedRecord.Content,
                ContentType = SelectedRecord.ContentType,
                ImageFileName = SelectedRecord.ImageFileName,
                Note = string.IsNullOrWhiteSpace(EditNote) ? null : EditNote.Trim(),
                Source = SelectedRecord.Source,
                SortOrder = SelectedRecord.SortOrder,
                CreatedAt = SelectedRecord.CreatedAt,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await _repository.UpdateAsync(updated);
            await RefreshAsync();
            SelectedRecord = Records.FirstOrDefault(record => record.Id == updated.Id) ?? Records.FirstOrDefault();
            StatusText = "Record updated.";
            ShowMessage(T("Toast_RecordUpdated"), updated.Name, AndroidMessageSeverity.Success);
        }
        catch (AppException ex)
        {
            StatusText = ex.Message;
            ShowMessage(T("Toast_RecordUpdateFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    public async Task DeleteSelectedAsync()
    {
        await DeleteRecordAsync(SelectedRecord);
    }

    [RelayCommand]
    public async Task DeleteRecordAsync(QRRecord? record)
    {
        if (record is null)
        {
            return;
        }

        bool confirmed = await _dialogService.ConfirmAsync(
            T("DeleteConfirmTitle"),
            _textService.Format(SelectedLanguage, "DeleteConfirmMessage", record.Name),
            DeleteText,
            T("Cancel"));
        if (!confirmed)
        {
            return;
        }

        int selectedIndex = Records.IndexOf(record);
        bool wasSelected = SelectedRecord?.Id == record.Id;
        await _repository.DeleteAsync(record.Id);
        await _imageStorage.DeleteAsync(record.ImageFileName);
        await RefreshAsync();

        if (wasSelected && Records.Count > 0)
        {
            SelectedRecord = Records[Math.Min(selectedIndex, Records.Count - 1)];
        }

        StatusText = "Record deleted.";
        IsDetailOpen = UseWideLayout && SelectedRecord is not null;
        ShowMessage(T("Toast_RecordDeleted"), record.Name, AndroidMessageSeverity.Success);
    }

    public void PreviewReorderTargetIndex(QRRecord source, int targetIndex)
    {
        ClearReorderTarget();
        AndroidRecordListItemViewModel? sourceItem = RecordItems.FirstOrDefault(item => item.Record.Id == source.Id);
        if (sourceItem is not null)
        {
            sourceItem.IsReordering = true;
        }

        if (targetIndex < 0 || RecordItems.Count == 0)
        {
            return;
        }

        int oldIndex = Records.IndexOf(source);
        int previewIndex = GetMoveTargetIndex(source, targetIndex);
        if (oldIndex < 0 || previewIndex < 0 || oldIndex == previewIndex)
        {
            return;
        }

        int insertIndex = Math.Clamp(targetIndex, 0, RecordItems.Count);
        if (insertIndex >= RecordItems.Count)
        {
            RecordItems[^1].IsReorderAfter = true;
        }
        else
        {
            RecordItems[insertIndex].IsReorderBefore = true;
        }
    }

    public void ClearReorderTarget()
    {
        foreach (AndroidRecordListItemViewModel item in RecordItems)
        {
            item.IsReordering = false;
            item.IsReorderBefore = false;
            item.IsReorderAfter = false;
        }
    }

    public async Task MoveRecordToIndexAsync(QRRecord source, int targetIndex)
    {
        int oldIndex = Records.IndexOf(source);
        int newIndex = GetMoveTargetIndex(source, targetIndex);
        ClearReorderTarget();
        if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
        {
            return;
        }

        Records.Move(oldIndex, newIndex);
        RecordItems.Move(oldIndex, newIndex);
        SelectedRecord = source;
        SyncRecordItemSelection();
        await PersistRecordOrderAsync(source);
    }

    [RelayCommand]
    public async Task MoveRecordUpAsync(QRRecord? record)
    {
        await MoveRecordByOffsetAsync(record, -1);
    }

    [RelayCommand]
    public async Task MoveRecordDownAsync(QRRecord? record)
    {
        await MoveRecordByOffsetAsync(record, 1);
    }

    public async Task PersistRecordOrderAsync(QRRecord? selectedRecord)
    {
        if (Records.Count == 0)
        {
            return;
        }

        await _repository.ReorderAsync(Records.Select(record => record.Id).ToList());
        await RefreshAsync();
        SelectedRecord = Records.FirstOrDefault(record => record.Id == selectedRecord?.Id) ?? Records.FirstOrDefault();
    }

    private async Task MoveRecordByOffsetAsync(QRRecord? record, int offset)
    {
        if (record is null)
        {
            return;
        }

        int oldIndex = Records.IndexOf(record);
        int newIndex = oldIndex + offset;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= Records.Count)
        {
            return;
        }

        Records.Move(oldIndex, newIndex);
        RecordItems.Move(oldIndex, newIndex);
        SelectedRecord = record;
        SyncRecordItemSelection();
        await PersistRecordOrderAsync(record);
    }

    private int GetMoveTargetIndex(QRRecord source, int targetIndex)
    {
        int oldIndex = Records.IndexOf(source);
        if (oldIndex < 0 || targetIndex < 0 || Records.Count == 0)
        {
            return -1;
        }

        int newIndex = targetIndex;
        if (oldIndex < targetIndex)
        {
            newIndex--;
        }

        return Math.Clamp(newIndex, 0, Records.Count - 1);
    }


    [RelayCommand]
    public async Task CreateBackupAsync()
    {
        try
        {
            string localPath = _backupFiles.CreateLocalBackupPath();
            await _backupService.CreateBackupAsync(localPath);

            string? exportedPath = await _backupFiles.ExportBackupAsync(localPath, Path.GetFileName(localPath));
            BackupStatusText = string.IsNullOrWhiteSpace(exportedPath)
                ? $"Backup created in app storage: {localPath}"
                : $"Backup exported: {exportedPath}";
            StatusText = BackupStatusText;
            ShowMessage(T("Toast_BackupSaved"), BackupStatusText, AndroidMessageSeverity.Success);
        }
        catch (AppException ex)
        {
            BackupStatusText = ex.Message;
            StatusText = ex.Message;
            ShowMessage(T("Toast_BackupFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
        catch (IOException ex)
        {
            BackupStatusText = ex.Message;
            StatusText = ex.Message;
            ShowMessage(T("Toast_BackupFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
        catch (InvalidOperationException ex)
        {
            BackupStatusText = ex.Message;
            StatusText = ex.Message;
            ShowMessage(T("Toast_BackupFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
    }

    [RelayCommand]
    public async Task RestoreBackupAsync()
    {
        try
        {
            string? path = await _backupFiles.PickBackupOpenCopyAsync();
            if (string.IsNullOrWhiteSpace(path))
            {
                BackupStatusText = "Restore canceled.";
                ShowMessage(T("Toast_Restore"), BackupStatusText);
                return;
            }

            bool confirmed = await _dialogService.ConfirmAsync(
                "Restore backup",
                "Restore will replace all current records. A safety backup will be created first.",
                "Restore",
                "Cancel");
            if (!confirmed)
            {
                BackupStatusText = "Restore canceled.";
                ShowMessage(T("Toast_Restore"), BackupStatusText);
                return;
            }

            string safetyBackupPath = _backupFiles.CreateLocalBackupPath("BeforeRestore");
            await _backupService.CreateBackupAsync(safetyBackupPath);
            await _backupService.RestoreAsync(path);
            ClearImportPreview();
            await RefreshAsync();
            BackupStatusText = $"Restore completed. Safety backup: {safetyBackupPath}";
            StatusText = "Backup restored.";
            ShowMessage(T("Toast_RestoreCompleted"), BackupStatusText, AndroidMessageSeverity.Success);
        }
        catch (AppException ex)
        {
            BackupStatusText = ex.Message;
            StatusText = ex.Message;
            ShowMessage(T("Toast_RestoreFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
        catch (IOException ex)
        {
            BackupStatusText = ex.Message;
            StatusText = ex.Message;
            ShowMessage(T("Toast_RestoreFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
        catch (InvalidOperationException ex)
        {
            BackupStatusText = ex.Message;
            StatusText = ex.Message;
            ShowMessage(T("Toast_RestoreFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
    }

    [RelayCommand]
    public async Task PreviewImportAsync()
    {
        if (_currentImportPreview is not null)
        {
            ClearImportPreview();
            BackupStatusText = T("ImportPreviewCanceled");
            StatusText = BackupStatusText;
            ShowMessage(T("Toast_ImportPreviewCanceled"), BackupStatusText);
            return;
        }

        try
        {
            string? path = await _backupFiles.PickBackupOpenCopyAsync();
            if (string.IsNullOrWhiteSpace(path))
            {
                BackupStatusText = "Import preview canceled.";
                ShowMessage(T("Toast_ImportPreview"), BackupStatusText);
                return;
            }

            _currentImportPreview = await _backupService.PreviewImportAsync(path);
            ImportPreviewItems.Clear();
            foreach (ImportPreviewItem item in _currentImportPreview.Items)
            {
                ImportPreviewItems.Add(item);
            }

            BackupManifestText =
                $"{_currentImportPreview.Manifest.RecordCount} records, " +
                $"{_currentImportPreview.NewCount} new, " +
                $"{_currentImportPreview.DuplicateCount} duplicates.";
            BackupStatusText = "Import preview loaded.";
            StatusText = BackupStatusText;
            OnPropertyChanged(nameof(HasImportPreview));
            OnPropertyChanged(nameof(PreviewImportActionText));
            OnPropertyChanged(nameof(PreviewImportButtonBackground));
            OnPropertyChanged(nameof(PreviewImportButtonForeground));
            ShowMessage(T("Toast_ImportPreviewLoaded"), BackupManifestText);
        }
        catch (AppException ex)
        {
            BackupStatusText = ex.Message;
            StatusText = ex.Message;
            ShowMessage(T("Toast_ImportPreviewFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
        catch (IOException ex)
        {
            BackupStatusText = ex.Message;
            StatusText = ex.Message;
            ShowMessage(T("Toast_ImportPreviewFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
        catch (InvalidOperationException ex)
        {
            BackupStatusText = ex.Message;
            StatusText = ex.Message;
            ShowMessage(T("Toast_ImportPreviewFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
    }

    [RelayCommand]
    public async Task ImportSelectedAsync()
    {
        if (_currentImportPreview is null)
        {
            BackupStatusText = "Load an import preview first.";
            StatusText = BackupStatusText;
            ShowMessage(T("Toast_ImportBlocked"), BackupStatusText, AndroidMessageSeverity.Warning);
            return;
        }

        try
        {
            ImportResult result = await _backupService.ImportAsync(_currentImportPreview);
            await RefreshAsync();
            BackupStatusText = $"Imported {result.ImportedCount}; skipped {result.SkippedCount}.";
            StatusText = "Backup import completed.";
            ShowMessage(T("Toast_ImportCompleted"), BackupStatusText, AndroidMessageSeverity.Success);
        }
        catch (AppException ex)
        {
            BackupStatusText = ex.Message;
            StatusText = ex.Message;
            ShowMessage(T("Toast_ImportFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
        catch (IOException ex)
        {
            BackupStatusText = ex.Message;
            StatusText = ex.Message;
            ShowMessage(T("Toast_ImportFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
        catch (InvalidOperationException ex)
        {
            BackupStatusText = ex.Message;
            StatusText = ex.Message;
            ShowMessage(T("Toast_ImportFailed"), ex.Message, AndroidMessageSeverity.Error);
        }
    }

    [RelayCommand]
    public void UseSystemTheme()
    {
        SelectedTheme = AndroidThemeMode.System;
        ApplyTheme();
    }

    [RelayCommand]
    public void UseLightTheme()
    {
        SelectedTheme = AndroidThemeMode.Light;
        ApplyTheme();
    }

    [RelayCommand]
    public void UseDarkTheme()
    {
        SelectedTheme = AndroidThemeMode.Dark;
        ApplyTheme();
    }

    [RelayCommand]
    public void UseChineseLanguage()
    {
        SelectedLanguage = AndroidAppLanguage.Chinese;
        _settingsService.SetLanguage(AndroidAppLanguage.Chinese);
        NotifyLocalizedPropertiesChanged();
        StatusText = T("StatusReady");
        BackupStatusText = T("BackupReady");
        RefreshSyncPeers();
        if (_currentImportPreview is null)
        {
            BackupManifestText = T("NoImportPreview");
        }
    }

    [RelayCommand]
    public void UseEnglishLanguage()
    {
        SelectedLanguage = AndroidAppLanguage.English;
        _settingsService.SetLanguage(AndroidAppLanguage.English);
        NotifyLocalizedPropertiesChanged();
        StatusText = T("StatusReady");
        BackupStatusText = T("BackupReady");
        RefreshSyncPeers();
        if (_currentImportPreview is null)
        {
            BackupManifestText = T("NoImportPreview");
        }
    }

    [RelayCommand]
    public void UseOceanColorStyle()
    {
        ApplyColorStyle(AndroidColorStyle.Ocean);
    }

    [RelayCommand]
    public void UseForestColorStyle()
    {
        ApplyColorStyle(AndroidColorStyle.Forest);
    }

    [RelayCommand]
    public void UseRoseColorStyle()
    {
        ApplyColorStyle(AndroidColorStyle.Rose);
    }

    private void ApplyTheme()
    {
        _settingsService.SetTheme(SelectedTheme);
        if (Avalonia.Application.Current is not null)
        {
            Avalonia.Application.Current.RequestedThemeVariant = SelectedTheme switch
            {
                AndroidThemeMode.Light => ThemeVariant.Light,
                AndroidThemeMode.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };
        }

        AndroidVisualStyleService.Apply(SelectedColorStyle, GetThemeVariant());
        NotifyTabVisualPropertiesChanged();
        NotifyAllOptionVisualPropertiesChanged();
        OnPropertyChanged(nameof(PreviewImportButtonBackground));
        OnPropertyChanged(nameof(PreviewImportButtonForeground));
    }

    private void ApplyColorStyle(AndroidColorStyle colorStyle)
    {
        SelectedColorStyle = colorStyle;
        _settingsService.SetColorStyle(colorStyle);
        AndroidVisualStyleService.Apply(colorStyle, GetThemeVariant());
        NotifyTabVisualPropertiesChanged();
        NotifyAllOptionVisualPropertiesChanged();
        OnPropertyChanged(nameof(PreviewImportButtonBackground));
        OnPropertyChanged(nameof(PreviewImportButtonForeground));
    }

    private ThemeVariant GetThemeVariant()
    {
        return SelectedTheme switch
        {
            AndroidThemeMode.Light => ThemeVariant.Light,
            AndroidThemeMode.Dark => ThemeVariant.Dark,
            _ => Avalonia.Application.Current?.ActualThemeVariant ?? ThemeVariant.Default
        };
    }

    partial void OnSelectedSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsRecordsSelected));
        OnPropertyChanged(nameof(IsRecordsListVisible));
        OnPropertyChanged(nameof(IsRecordDetailVisible));
        OnPropertyChanged(nameof(ShowRecordPlaceholder));
        OnPropertyChanged(nameof(IsRecordAddVisible));
        OnPropertyChanged(nameof(IsScanSelected));
        OnPropertyChanged(nameof(IsBackupSelected));
        OnPropertyChanged(nameof(IsSyncSelected));
        OnPropertyChanged(nameof(IsSettingsSelected));
        NotifyTabVisualPropertiesChanged();
    }

    partial void OnSelectedThemeChanged(AndroidThemeMode value)
    {
        OnPropertyChanged(nameof(IsSystemTheme));
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(CurrentThemeLabel));
        OnPropertyChanged(nameof(CurrentSettingsText));
        NotifyThemeOptionPropertiesChanged();
    }

    partial void OnSelectedLanguageChanged(AndroidAppLanguage value)
    {
        OnPropertyChanged(nameof(IsChineseLanguage));
        OnPropertyChanged(nameof(IsEnglishLanguage));
        OnPropertyChanged(nameof(CurrentLanguageLabel));
        OnPropertyChanged(nameof(CurrentSettingsText));
        NotifyLanguageOptionPropertiesChanged();
    }

    partial void OnSelectedColorStyleChanged(AndroidColorStyle value)
    {
        OnPropertyChanged(nameof(IsOceanColorStyle));
        OnPropertyChanged(nameof(IsForestColorStyle));
        OnPropertyChanged(nameof(IsRoseColorStyle));
        OnPropertyChanged(nameof(CurrentColorStyleLabel));
        OnPropertyChanged(nameof(CurrentSettingsText));
        NotifyColorStyleOptionPropertiesChanged();
    }

    partial void OnSelectedSyncPeerChanged(SyncDeviceInfo? value)
    {
        StartSyncCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsSyncBusyChanged(bool value)
    {
        StartSyncCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsCheckingUpdatesChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCheckForUpdates));
        CheckForUpdatesCommand.NotifyCanExecuteChanged();
    }

    partial void OnAutoAcceptLanSyncRequestsChanged(bool value)
    {
        _settingsService.SetAutoAcceptLanSyncRequests(value);
        OnPropertyChanged(nameof(AutoAcceptLanSyncStateText));
        NotifySyncOptionPropertiesChanged();
    }

    partial void OnIsDetailOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRecordsListVisible));
        OnPropertyChanged(nameof(IsRecordDetailVisible));
        OnPropertyChanged(nameof(ShowRecordPlaceholder));
        OnPropertyChanged(nameof(IsRecordAddVisible));
    }

    partial void OnUseWideLayoutChanged(bool value)
    {
        if (value && SelectedRecord is null && Records.Count > 0)
        {
            SelectedRecord = Records[0];
        }

        OnPropertyChanged(nameof(IsRecordsListVisible));
        OnPropertyChanged(nameof(IsRecordDetailVisible));
        OnPropertyChanged(nameof(ShowRecordPlaceholder));
        OnPropertyChanged(nameof(IsRecordAddVisible));
        OnPropertyChanged(nameof(IsNarrowLayout));
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = RefreshAsync();
    }

    partial void OnSelectedRecordChanged(QRRecord? value)
    {
        EditName = value?.Name ?? string.Empty;
        EditNote = value?.Note ?? string.Empty;
        LoadSelectedImage(value);
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedContent));
        OnPropertyChanged(nameof(SelectedContentType));
        OnPropertyChanged(nameof(SelectedContentTypeLabel));
        OnPropertyChanged(nameof(IsRecordDetailVisible));
        OnPropertyChanged(nameof(ShowRecordPlaceholder));
        OnPropertyChanged(nameof(IsRecordAddVisible));
        SaveEditCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        ShareQrImageCommand.NotifyCanExecuteChanged();
        SaveQrImageCommand.NotifyCanExecuteChanged();
        SyncRecordItemSelection();
    }

    private bool CanStartSync()
    {
        return !IsSyncBusy && SelectedSyncPeer is not null && _syncHostService.LocalDevice is not null;
    }

    private void SetSyncError(string message)
    {
        SyncStatusText = _textService.Format(SelectedLanguage, "SyncError", message);
        ShowMessage(T("Toast_SyncFailed"), SyncStatusText, AndroidMessageSeverity.Error);
    }

    private void OnSyncPeersChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(RefreshSyncPeers);
    }

    public async Task<int> GetRecordCountAsync()
    {
        IReadOnlyList<Core.Models.QRRecord> records = await _repository.GetAllAsync();
        return records.Count;
    }

    private static string GenerateDefaultName()
    {
        return $"QR_{DateTime.Now:yyyyMMdd_HHmmss}";
    }

    private static string GetQrImageFileName(QRRecord record)
    {
        string name = string.IsNullOrWhiteSpace(record.Name) ? GenerateDefaultName() : record.Name.Trim();
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidChar, '_');
        }

        return name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? name : $"{name}.png";
    }

    private async Task<QRRecord> AddRecordAsync(string content, QRRecordSource source, string? name = null)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        byte[] png = _qrCodeService.GeneratePng(content);
        string imageFileName = await _imageStorage.SavePngAsync(png);
        QRRecord record = new()
        {
            Name = string.IsNullOrWhiteSpace(name) ? GenerateDefaultName() : name.Trim(),
            Content = content,
            ContentType = _contentTypeDetector.Detect(content),
            ImageFileName = imageFileName,
            Source = source,
            CreatedAt = now,
            UpdatedAt = now
        };

        return await _repository.AddAsync(record);
    }

    private async Task ImportImageStreamAsync(Stream stream, string successTitle)
    {
        if (stream.CanSeek && stream.Length > AppConstants.MaxImageBytes)
        {
            StatusText = "Image is larger than 20 MB.";
            ShowMessage(T("Toast_ImageTooLarge"), StatusText, AndroidMessageSeverity.Warning);
            return;
        }

        StatusText = "Decoding image...";
        string? content = await _qrCodeService.DecodeAsync(stream);
        if (string.IsNullOrWhiteSpace(content))
        {
            StatusText = "No QR code found in the selected image.";
            ShowMessage(T("Toast_NoQrCodeFound"), StatusText, AndroidMessageSeverity.Warning);
            return;
        }

        QRRecord record = await AddRecordAsync(content, QRRecordSource.ImageImport);
        await RefreshAsync();
        SelectedRecord = Records.FirstOrDefault(existing => existing.Id == record.Id) ?? Records.FirstOrDefault();
        SelectedSection = "Records";
        IsDetailOpen = UseWideLayout;
        StatusText = "QR code imported.";
        ShowMessage(successTitle, record.Name, AndroidMessageSeverity.Success);
    }

    private void LoadSelectedImage(QRRecord? record)
    {
        SelectedImage?.Dispose();
        SelectedImage = null;

        if (record is null || string.IsNullOrWhiteSpace(record.ImageFileName))
        {
            return;
        }

        string path = _imageStorage.GetImagePath(record.ImageFileName);
        if (!File.Exists(path))
        {
            return;
        }

        using FileStream stream = File.OpenRead(path);
        SelectedImage = new Bitmap(stream);
    }

    private string T(string key)
    {
        return _textService.Get(SelectedLanguage, key);
    }

    private string GetUpdateStatusText()
    {
        if (updateCheckResult is null)
        {
            return T("UpdateNotChecked");
        }

        if (!string.IsNullOrWhiteSpace(updateCheckResult.Message))
        {
            return updateCheckResult.Message;
        }

        if (updateCheckResult.IsUpdateAvailable)
        {
            return _textService.Format(
                SelectedLanguage,
                "UpdateAvailable",
                updateCheckResult.LatestVersion ?? string.Empty);
        }

        return _textService.Format(
            SelectedLanguage,
            "UpdateUpToDate",
            updateCheckResult.CurrentVersion);
    }

    private string GetCurrentNetworkName()
    {
        string? androidNetworkName = null;
        if (_networkInfoService is AndroidSyncNetworkService androidNetworkInfoService &&
            !androidNetworkInfoService.TryGetCurrentNetworkName(
                out androidNetworkName,
                out AndroidSyncNetworkService.NetworkNameState state))
        {
            return state switch
            {
                AndroidSyncNetworkService.NetworkNameState.NotOnWifi => T("SyncNetworkNotWifi"),
                AndroidSyncNetworkService.NetworkNameState.MissingPermission => T("SyncNetworkMissingPermission"),
                AndroidSyncNetworkService.NetworkNameState.LocationServiceOff => T("SyncNetworkLocationOff"),
                AndroidSyncNetworkService.NetworkNameState.Unknown => T("SyncNetworkHiddenBySystem"),
                _ => T("SyncNetworkUnknown")
            };
        }

        if (!string.IsNullOrWhiteSpace(androidNetworkName))
        {
            return androidNetworkName;
        }

        string networkName = _networkInfoService.GetCurrentNetworkName();
        return string.IsNullOrWhiteSpace(networkName) ? T("SyncNetworkUnknown") : networkName;
    }

    private async Task PrepareSyncNetworkNameAsync()
    {
        if (_isPreparingSyncNetworkName ||
            _networkInfoService is not AndroidSyncNetworkService androidNetworkInfoService)
        {
            return;
        }

        _isPreparingSyncNetworkName = true;
        try
        {
            bool permissionGranted = await androidNetworkInfoService.EnsureNetworkNameAccessAsync();
            if (permissionGranted)
            {
                await _syncHostService.RefreshLocalDeviceAsync();
            }

            Dispatcher.UIThread.Post(NotifySyncPeerPropertiesChanged);
        }
        catch (InvalidOperationException)
        {
            Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(CurrentSyncNetworkText)));
        }
        finally
        {
            _isPreparingSyncNetworkName = false;
        }
    }

    private void ClearImportPreview()
    {
        _currentImportPreview = null;
        ImportPreviewItems.Clear();
        BackupManifestText = T("NoImportPreview");
        OnPropertyChanged(nameof(HasImportPreview));
        OnPropertyChanged(nameof(PreviewImportActionText));
        OnPropertyChanged(nameof(PreviewImportButtonBackground));
        OnPropertyChanged(nameof(PreviewImportButtonForeground));
    }

    private void ShowMessage(
        string title,
        string body,
        AndroidMessageSeverity severity = AndroidMessageSeverity.Info)
    {
        AndroidToastMessageViewModel message = new(title, body, severity);
        ToastMessages.Add(message);
        _ = DismissMessageAsync(message, severity == AndroidMessageSeverity.Error
            ? TimeSpan.FromSeconds(5)
            : TimeSpan.FromSeconds(3));
    }

    private async Task DismissMessageAsync(AndroidToastMessageViewModel message, TimeSpan delay)
    {
        await Task.Delay(delay);
        ToastMessages.Remove(message);
    }

    private void NotifyTabVisualPropertiesChanged()
    {
        OnPropertyChanged(nameof(RecordsTabBackground));
        OnPropertyChanged(nameof(ScanTabBackground));
        OnPropertyChanged(nameof(BackupTabBackground));
        OnPropertyChanged(nameof(SyncTabBackground));
        OnPropertyChanged(nameof(SettingsTabBackground));
        OnPropertyChanged(nameof(RecordsTabBorderBrush));
        OnPropertyChanged(nameof(ScanTabBorderBrush));
        OnPropertyChanged(nameof(BackupTabBorderBrush));
        OnPropertyChanged(nameof(SyncTabBorderBrush));
        OnPropertyChanged(nameof(SettingsTabBorderBrush));
        OnPropertyChanged(nameof(RecordsTabForeground));
        OnPropertyChanged(nameof(ScanTabForeground));
        OnPropertyChanged(nameof(BackupTabForeground));
        OnPropertyChanged(nameof(SyncTabForeground));
        OnPropertyChanged(nameof(SettingsTabForeground));
    }

    private void NotifyThemeOptionPropertiesChanged()
    {
        OnPropertyChanged(nameof(ThemeSystemOptionBackground));
        OnPropertyChanged(nameof(ThemeLightOptionBackground));
        OnPropertyChanged(nameof(ThemeDarkOptionBackground));
        OnPropertyChanged(nameof(ThemeSystemOptionBorderBrush));
        OnPropertyChanged(nameof(ThemeLightOptionBorderBrush));
        OnPropertyChanged(nameof(ThemeDarkOptionBorderBrush));
    }

    private void NotifyColorStyleOptionPropertiesChanged()
    {
        OnPropertyChanged(nameof(ColorStyleOceanOptionBackground));
        OnPropertyChanged(nameof(ColorStyleForestOptionBackground));
        OnPropertyChanged(nameof(ColorStyleRoseOptionBackground));
        OnPropertyChanged(nameof(ColorStyleOceanOptionBorderBrush));
        OnPropertyChanged(nameof(ColorStyleForestOptionBorderBrush));
        OnPropertyChanged(nameof(ColorStyleRoseOptionBorderBrush));
    }

    private void NotifyLanguageOptionPropertiesChanged()
    {
        OnPropertyChanged(nameof(LanguageChineseOptionBackground));
        OnPropertyChanged(nameof(LanguageEnglishOptionBackground));
        OnPropertyChanged(nameof(LanguageChineseOptionBorderBrush));
        OnPropertyChanged(nameof(LanguageEnglishOptionBorderBrush));
    }

    private void NotifySyncOptionPropertiesChanged()
    {
        OnPropertyChanged(nameof(AutoAcceptLanSyncOptionBackground));
        OnPropertyChanged(nameof(AutoAcceptLanSyncOptionBorderBrush));
    }

    private void NotifyAllOptionVisualPropertiesChanged()
    {
        NotifyThemeOptionPropertiesChanged();
        NotifyColorStyleOptionPropertiesChanged();
        NotifyLanguageOptionPropertiesChanged();
        NotifySyncOptionPropertiesChanged();
    }

    private void NotifySyncPeerPropertiesChanged()
    {
        OnPropertyChanged(nameof(HasSyncPeers));
        OnPropertyChanged(nameof(HasNoSyncPeers));
        OnPropertyChanged(nameof(LocalSyncDeviceText));
        OnPropertyChanged(nameof(CurrentSyncNetworkText));
        StartSyncCommand.NotifyCanExecuteChanged();
    }

    private void SyncRecordItemSelection()
    {
        int? selectedId = SelectedRecord?.Id;
        foreach (AndroidRecordListItemViewModel item in RecordItems)
        {
            item.IsSelected = selectedId.HasValue && item.Record.Id == selectedId.Value;
        }
    }

    private static IBrush ResourceBrush(string key)
    {
        if (Avalonia.Application.Current?.Resources.TryGetResource(
            key,
            Avalonia.Application.Current.ActualThemeVariant,
            out object? value) == true &&
            value is IBrush brush)
        {
            return brush;
        }

        return Brushes.Transparent;
    }

    private static IBrush OptionBackground(bool isSelected)
    {
        return isSelected ? ResourceBrush("AppSelectedBrush") : Brushes.Transparent;
    }

    private static IBrush OptionBorderBrush(bool isSelected)
    {
        return isSelected ? ResourceBrush("AppAccentBrush") : ResourceBrush("AppDividerBrush");
    }

    private void NotifyLocalizedPropertiesChanged()
    {
        OnPropertyChanged(nameof(RefreshText));
        OnPropertyChanged(nameof(RecordsText));
        OnPropertyChanged(nameof(ScanText));
        OnPropertyChanged(nameof(BackupText));
        OnPropertyChanged(nameof(SyncText));
        OnPropertyChanged(nameof(SettingsText));
        OnPropertyChanged(nameof(SearchRecordsText));
        OnPropertyChanged(nameof(DetailText));
        OnPropertyChanged(nameof(BackText));
        OnPropertyChanged(nameof(NameText));
        OnPropertyChanged(nameof(NoteText));
        OnPropertyChanged(nameof(SaveText));
        OnPropertyChanged(nameof(DeleteText));
        OnPropertyChanged(nameof(ShareText));
        OnPropertyChanged(nameof(ShareQrImageText));
        OnPropertyChanged(nameof(SaveQrImageText));
        OnPropertyChanged(nameof(MoveUpText));
        OnPropertyChanged(nameof(MoveDownText));
        OnPropertyChanged(nameof(AddQrContentText));
        OnPropertyChanged(nameof(CreateQrText));
        OnPropertyChanged(nameof(PasteQrContentText));
        OnPropertyChanged(nameof(SaveNewQrText));
        OnPropertyChanged(nameof(ImportText));
        OnPropertyChanged(nameof(ImportImageText));
        OnPropertyChanged(nameof(ScanWithCameraText));
        OnPropertyChanged(nameof(ScanTipText));
        OnPropertyChanged(nameof(SelectRecordText));
        OnPropertyChanged(nameof(BackupDescriptionText));
        OnPropertyChanged(nameof(CreateBackupText));
        OnPropertyChanged(nameof(RestoreBackupText));
        OnPropertyChanged(nameof(PreviewImportText));
        OnPropertyChanged(nameof(ImportSelectedText));
        OnPropertyChanged(nameof(TypeText));
        OnPropertyChanged(nameof(CancelPreviewText));
        OnPropertyChanged(nameof(PreviewImportActionText));
        OnPropertyChanged(nameof(PreviewImportButtonBackground));
        OnPropertyChanged(nameof(PreviewImportButtonForeground));
        OnPropertyChanged(nameof(SelectedContentTypeLabel));
        OnPropertyChanged(nameof(RestoreSafetyNoteText));
        OnPropertyChanged(nameof(SyncDevicesText));
        OnPropertyChanged(nameof(SyncLocalDeviceText));
        OnPropertyChanged(nameof(SyncStartText));
        OnPropertyChanged(nameof(SyncNoPeersText));
        OnPropertyChanged(nameof(SyncTroubleshootingText));
        OnPropertyChanged(nameof(LocalSyncDeviceText));
        OnPropertyChanged(nameof(CurrentSyncNetworkText));
        OnPropertyChanged(nameof(AutoAcceptLanSyncText));
        OnPropertyChanged(nameof(AutoAcceptLanSyncDescriptionText));
        OnPropertyChanged(nameof(AutoAcceptLanSyncStateText));
        OnPropertyChanged(nameof(ThemeText));
        OnPropertyChanged(nameof(ThemeSystemText));
        OnPropertyChanged(nameof(ThemeLightText));
        OnPropertyChanged(nameof(ThemeDarkText));
        OnPropertyChanged(nameof(ColorStyleText));
        OnPropertyChanged(nameof(ColorStyleOceanText));
        OnPropertyChanged(nameof(ColorStyleForestText));
        OnPropertyChanged(nameof(ColorStyleRoseText));
        OnPropertyChanged(nameof(CurrentThemeLabel));
        OnPropertyChanged(nameof(CurrentLanguageLabel));
        OnPropertyChanged(nameof(CurrentColorStyleLabel));
        OnPropertyChanged(nameof(LanguageText));
        OnPropertyChanged(nameof(LanguageChineseText));
        OnPropertyChanged(nameof(LanguageEnglishText));
        OnPropertyChanged(nameof(AboutText));
        OnPropertyChanged(nameof(VersionText));
        OnPropertyChanged(nameof(AboutDescriptionText));
        OnPropertyChanged(nameof(UpdateText));
        OnPropertyChanged(nameof(CheckUpdateText));
        OnPropertyChanged(nameof(OpenUpdateText));
        UpdateStatusText = GetUpdateStatusText();
        OnPropertyChanged(nameof(CurrentSettingsText));
    }
}

