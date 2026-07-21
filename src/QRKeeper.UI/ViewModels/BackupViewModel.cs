using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRKeeper.Core.Common;
using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;
using QRKeeper.UI.Services;

namespace QRKeeper.UI.ViewModels;

public sealed partial class BackupViewModel : ViewModelBase
{
    private readonly IBackupService _backupService;
    private readonly IFilePickerService _filePicker;
    private readonly IMessageService _messageService;
    private readonly ILocalizationService _localizationService;
    private readonly IConfirmationService _confirmationService;
    private readonly string _settingsPath;
    private ImportPreview? _currentPreview;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _manifestText = string.Empty;

    [ObservableProperty]
    private string _defaultBackupDirectory = string.Empty;

    public BackupViewModel(
        IBackupService backupService,
        IFilePickerService filePicker,
        IMessageService messageService,
        ILocalizationService localizationService,
        IConfirmationService confirmationService)
    {
        _backupService = backupService;
        _filePicker = filePicker;
        _messageService = messageService;
        _localizationService = localizationService;
        _confirmationService = confirmationService;
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QRKeeper",
            "backup-directory.txt");
        DefaultBackupDirectory = LoadDefaultBackupDirectory();
        _localizationService.LanguageChanged += OnLanguageChanged;
        StatusText = T("Status_ReadyPeriod");
        ManifestText = T("Status_NoImportPreview");
    }

    public ObservableCollection<ImportPreviewItemViewModel> PreviewItems { get; } = new();

    public bool HasImportPreview => _currentPreview is not null;

    public bool HasNoImportPreview => !HasImportPreview;

    [RelayCommand]
    public async Task CreateBackupAsync()
    {
        try
        {
            string path = GetDefaultBackupPath();
            await _backupService.CreateBackupAsync(path);
            StatusText = F("Status_BackupSavedFormat", path);
            _messageService.Show(T("Toast_BackupSaved"), path, MessageSeverity.Success);
        }
        catch (AppException ex)
        {
            StatusText = ex.Message;
            _messageService.Show(T("Toast_BackupFailed"), ex.Message, MessageSeverity.Error);
        }
    }

    [RelayCommand]
    public async Task ChooseBackupDirectoryAsync()
    {
        string? path = await _filePicker.PickFolderPathAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        DefaultBackupDirectory = path;
        SaveDefaultBackupDirectory(path);
        StatusText = F("Status_DefaultBackupDirectoryFormat", path);
        _messageService.Show(T("Toast_BackupDirectoryUpdated"), path, MessageSeverity.Success);
    }

    [RelayCommand]
    public async Task RestoreAsync()
    {
        try
        {
            string? path = await _filePicker.PickBackupOpenPathAsync();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            bool confirmed = await _confirmationService.ConfirmAsync(
                T("Backup_RestoreConfirmTitle"),
                T("Backup_RestoreConfirmMessage"),
                T("Backup_Restore"),
                T("Common_Cancel"));
            if (!confirmed)
            {
                return;
            }

            string safetyBackupPath = GetDefaultBackupPath("BeforeRestore");
            await _backupService.CreateBackupAsync(safetyBackupPath);
            await _backupService.RestoreAsync(path);
            StatusText = F("Status_RestoreCompletedWithBackupFormat", safetyBackupPath);
            _messageService.Show(T("Toast_RestoreCompleted"), StatusText, MessageSeverity.Success);
        }
        catch (AppException ex)
        {
            StatusText = ex.Message;
            _messageService.Show(T("Toast_RestoreFailed"), ex.Message, MessageSeverity.Error);
        }
    }

    private string GetDefaultBackupPath(string suffix = "")
    {
        Directory.CreateDirectory(DefaultBackupDirectory);
        string nameSuffix = string.IsNullOrWhiteSpace(suffix) ? string.Empty : $"_{suffix}";
        return Path.Combine(
            DefaultBackupDirectory,
            $"QRKeeper_{DateTime.Now:yyyyMMdd_HHmmss}{nameSuffix}{AppConstants.BackupExtension}");
    }

    private string LoadDefaultBackupDirectory()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                string path = File.ReadAllText(_settingsPath).Trim();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    return path;
                }
            }
        }
        catch (IOException)
        {
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QRKeeper",
            "Backups");
    }

    private void SaveDefaultBackupDirectory(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        File.WriteAllText(_settingsPath, path);
    }

    [RelayCommand]
    public async Task PreviewImportAsync()
    {
        if (_currentPreview is not null)
        {
            ClearImportPreview();
            StatusText = T("Status_ImportPreviewCanceled");
            _messageService.Show(T("Toast_ImportPreviewCanceled"), StatusText, MessageSeverity.Info);
            return;
        }

        try
        {
            string? path = await _filePicker.PickBackupOpenPathAsync();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            _currentPreview = await _backupService.PreviewImportAsync(path);
            PreviewItems.Clear();
            foreach (ImportPreviewItem item in _currentPreview.Items)
            {
                PreviewItems.Add(new ImportPreviewItemViewModel(item));
            }

            ManifestText = F("Status_ImportPreviewManifestFormat", _currentPreview.Manifest.RecordCount, _currentPreview.NewCount, _currentPreview.DuplicateCount);
            StatusText = T("Status_ImportPreviewLoaded");
            OnPropertyChanged(nameof(HasImportPreview));
            OnPropertyChanged(nameof(HasNoImportPreview));
            _messageService.Show(T("Toast_ImportPreviewLoaded"), ManifestText, MessageSeverity.Info);
        }
        catch (AppException ex)
        {
            StatusText = ex.Message;
            _messageService.Show(T("Toast_ImportPreviewFailed"), ex.Message, MessageSeverity.Error);
        }
    }

    [RelayCommand]
    public async Task ImportSelectedAsync()
    {
        if (_currentPreview is null)
        {
            StatusText = T("Status_LoadImportPreviewFirst");
            _messageService.Show(T("Toast_ImportBlocked"), StatusText, MessageSeverity.Warning);
            return;
        }

        try
        {
            ImportResult result = await _backupService.ImportAsync(_currentPreview);
            StatusText = F("Status_ImportResultFormat", result.ImportedCount, result.SkippedCount);
            _messageService.Show(T("Toast_ImportCompleted"), StatusText, MessageSeverity.Success);
        }
        catch (AppException ex)
        {
            StatusText = ex.Message;
            _messageService.Show(T("Toast_ImportFailed"), ex.Message, MessageSeverity.Error);
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (_currentPreview is null)
        {
            ManifestText = T("Status_NoImportPreview");
        }
        else
        {
            ManifestText = F("Status_ImportPreviewManifestFormat", _currentPreview.Manifest.RecordCount, _currentPreview.NewCount, _currentPreview.DuplicateCount);
        }
    }

    private void ClearImportPreview()
    {
        _currentPreview = null;
        PreviewItems.Clear();
        ManifestText = T("Status_NoImportPreview");
        OnPropertyChanged(nameof(HasImportPreview));
        OnPropertyChanged(nameof(HasNoImportPreview));
    }

    private string T(string key)
    {
        return _localizationService.GetString(key);
    }

    private string F(string key, params object[] args)
    {
        return _localizationService.Format(key, args);
    }
}
