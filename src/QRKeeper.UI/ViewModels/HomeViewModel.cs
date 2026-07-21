using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRKeeper.Core.Common;
using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;
using QRKeeper.UI.Services;

namespace QRKeeper.UI.ViewModels;

public sealed partial class HomeViewModel : ViewModelBase
{
    private readonly IQRRecordRepository _repository;
    private readonly IImageStorageService _imageStorage;
    private readonly IFilePickerService _filePicker;
    private readonly IContentTypeDetector _contentTypeDetector;
    private readonly IQRCodeService _qrCodeService;
    private readonly IScreenCaptureService _screenCapture;
    private readonly IMessageService _messageService;
    private readonly ILocalizationService _localizationService;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private QRRecord? _selectedRecord;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string _editNote = string.Empty;

    [ObservableProperty]
    private string _newName = GenerateDefaultName();

    [ObservableProperty]
    private string _newContent = string.Empty;

    [ObservableProperty]
    private string _newNote = string.Empty;

    [ObservableProperty]
    private string _captureStatusText = string.Empty;

    [ObservableProperty]
    private Bitmap? _selectedImage;

    public HomeViewModel(
        IQRRecordRepository repository,
        IImageStorageService imageStorage,
        IFilePickerService filePicker,
        IContentTypeDetector contentTypeDetector,
        IQRCodeService qrCodeService,
        IScreenCaptureService screenCapture,
        IMessageService messageService,
        ILocalizationService localizationService)
    {
        _repository = repository;
        _imageStorage = imageStorage;
        _filePicker = filePicker;
        _contentTypeDetector = contentTypeDetector;
        _qrCodeService = qrCodeService;
        _screenCapture = screenCapture;
        _messageService = messageService;
        _localizationService = localizationService;
        _localizationService.LanguageChanged += OnLanguageChanged;
        StatusText = T("Status_Ready");
        CaptureStatusText = T("Status_CapturePrompt");
    }

    public ObservableCollection<QRRecord> Records { get; } = new();

    public bool HasRecords => Records.Count > 0;

    public bool HasSelection => SelectedRecord is not null;

    public string SelectedContent => SelectedRecord?.Content ?? string.Empty;

    public string SelectedContentType => SelectedRecord?.ContentType.ToString() ?? string.Empty;

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await RefreshCoreAsync();
    }

    [RelayCommand]
    public async Task ImportImageAsync()
    {
        try
        {
            await using Stream? stream = await _filePicker.PickImageFileAsync();
            if (stream is null)
            {
                return;
            }

            await DecodeAndSaveAsync(stream, QRRecordSource.ImageImport);
        }
        catch (AppException ex)
        {
            ShowError(T("Toast_ImageImportFailed"), ex.Message);
        }
    }

    [RelayCommand]
    public async Task ImportScreenAsync()
    {
        try
        {
            ShowCaptureFeedback(T("Status_CapturingScreen"));
            ScreenCaptureResult result = await _screenCapture.CaptureScreenAsync();
            if (result.ImageStream is null)
            {
                if (!string.IsNullOrWhiteSpace(result.DecodedText))
                {
                    await SaveDecodedAsync(result.DecodedText, QRRecordSource.ImageImport);
                    return;
                }

                ShowWarning(T("Toast_ScreenRecognition"), result.Message ?? T("Status_ScreenRecognitionCanceled"));
                return;
            }

            await using Stream stream = result.ImageStream;
            await DecodeAndSaveAsync(stream, QRRecordSource.ImageImport);
        }
        catch (AppException ex)
        {
            ShowError(T("Toast_ScreenRecognitionFailed"), ex.Message);
        }
    }

    public async Task ImportImagePathAsync(string imagePath)
    {
        try
        {
            FileInfo file = new(imagePath);
            if (!file.Exists)
            {
                ShowCaptureFeedback(T("Status_DroppedFileMissing"));
                return;
            }

            if (file.Length > AppConstants.MaxImageBytes)
            {
                ShowWarning(T("Toast_ImageTooLarge"), T("Status_ImageTooLarge"));
                return;
            }

            await using FileStream stream = file.OpenRead();
            await DecodeAndSaveAsync(stream, QRRecordSource.ImageImport);
        }
        catch (AppException ex)
        {
            ShowError(T("Toast_ImageImportFailed"), ex.Message);
        }
        catch (IOException ex)
        {
            ShowError(T("Toast_ImageImportFailed"), ex.Message);
        }
    }

    [RelayCommand]
    public async Task SaveNewAsync()
    {
        try
        {
            QRRecord record = await SaveRecordAsync(
                NewName,
                NewContent,
                NewNote,
                QRRecordSource.Manual);

            NewName = GenerateDefaultName();
            NewContent = string.Empty;
            NewNote = string.Empty;
            await RefreshAndSelectAsync(record.Id);
            ShowSuccess(T("Toast_RecordSaved"), record.Name);
        }
        catch (AppException ex)
        {
            ShowError(T("Toast_RecordSaveFailed"), ex.Message);
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
            StatusText = T("Status_RecordUpdated");
            _messageService.Show(T("Toast_RecordUpdated"), updated.Name, MessageSeverity.Success);
            await RefreshAndSelectAsync(updated.Id);
        }
        catch (AppException ex)
        {
            StatusText = ex.Message;
            ShowError(T("Toast_RecordUpdateFailed"), ex.Message);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            ShowError(T("Toast_RecordUpdateFailed"), ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    public async Task ExportImageAsync()
    {
        if (SelectedRecord is null)
        {
            return;
        }

        byte[]? imageBytes = await _imageStorage.ReadAsync(SelectedRecord.ImageFileName);
        if (imageBytes is null)
        {
            StatusText = T("Status_QrImageMissing");
            ShowError(T("Toast_ExportFailed"), StatusText);
            return;
        }

        string defaultFileName = $"{SanitizeFileName(SelectedRecord.Name)}.png";
        string? path = await _filePicker.PickImageSavePathAsync(defaultFileName);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await File.WriteAllBytesAsync(path, imageBytes);
        StatusText = F("Status_QrImageSavedFormat", path);
        _messageService.Show(T("Toast_QrImageExported"), path, MessageSeverity.Success);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    public async Task DeleteSelectedAsync()
    {
        if (SelectedRecord is null)
        {
            return;
        }

        int selectedIndex = Records.IndexOf(SelectedRecord);
        QRRecord record = SelectedRecord;
        await _repository.DeleteAsync(record.Id);
        await _imageStorage.DeleteAsync(record.ImageFileName);
        StatusText = T("Status_RecordDeleted");
        _messageService.Show(T("Toast_RecordDeleted"), record.Name, MessageSeverity.Success);
        await RefreshCoreAsync(clearMissingSelection: true);

        if (Records.Count > 0)
        {
            SelectedRecord = Records[Math.Min(selectedIndex, Records.Count - 1)];
        }
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
        SaveEditCommand.NotifyCanExecuteChanged();
        ExportImageCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
    }

    private static string SanitizeFileName(string value)
    {
        string fileName = string.IsNullOrWhiteSpace(value) ? "QRKeeper" : value.Trim();
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidChar, '_');
        }

        return fileName;
    }

    private async Task RefreshCoreAsync(bool clearMissingSelection = false)
    {
        int? selectedId = SelectedRecord?.Id;
        IReadOnlyList<QRRecord> records = await _repository.SearchAsync(SearchText, null, null);
        Records.Clear();
        foreach (QRRecord record in records)
        {
            Records.Add(record);
        }

        if (selectedId.HasValue)
        {
            SelectedRecord = Records.FirstOrDefault(record => record.Id == selectedId.Value);
        }

        if (SelectedRecord is null && Records.Count > 0)
        {
            SelectedRecord = Records[0];
        }

        if (clearMissingSelection && SelectedRecord is null)
        {
            OnSelectedRecordChanged(null);
        }

        StatusText = records.Count == 0 ? T("Status_NoRecords") : F("Status_RecordCountFormat", records.Count);
        OnPropertyChanged(nameof(HasRecords));
    }

    private async Task RefreshAndSelectAsync(int recordId)
    {
        await RefreshCoreAsync();
        SelectedRecord = Records.FirstOrDefault(record => record.Id == recordId) ?? Records.FirstOrDefault();
    }

    public void MoveRecordToIndex(QRRecord source, int targetIndex)
    {
        int oldIndex = Records.IndexOf(source);
        int newIndex = GetMoveTargetIndex(source, targetIndex);
        if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
        {
            return;
        }

        Records.Move(oldIndex, newIndex);
        SelectedRecord = source;
    }

    public int GetMoveTargetIndex(QRRecord source, int targetIndex)
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

    public async Task PersistRecordOrderAsync(QRRecord? selectedRecord)
    {
        if (Records.Count == 0)
        {
            return;
        }

        await _repository.ReorderAsync(Records.Select(record => record.Id).ToList());
        await RefreshAndSelectAsync(selectedRecord?.Id ?? SelectedRecord?.Id ?? Records[0].Id);
    }

    private async Task DecodeAndSaveAsync(Stream stream, QRRecordSource source)
    {
        if (stream.CanSeek && stream.Length > AppConstants.MaxImageBytes)
        {
            ShowCaptureFeedback(T("Status_ImageTooLarge"));
            _messageService.Show(T("Toast_ImageTooLarge"), T("Status_ImageTooLarge"), MessageSeverity.Warning);
            return;
        }

        ShowCaptureFeedback(T("Status_DecodingQr"));
        string? decoded = await _qrCodeService.DecodeAsync(stream);
        if (string.IsNullOrWhiteSpace(decoded))
        {
            ShowWarning(T("Toast_NoQrCodeFound"), T("Status_NoQrFoundBody"));
            return;
        }

        await SaveDecodedAsync(decoded, source);
    }

    private async Task SaveDecodedAsync(string decoded, QRRecordSource source)
    {
        QRRecord record = await SaveRecordAsync(GenerateDefaultName(), decoded, string.Empty, source);
        NewContent = decoded;
        NewName = GenerateDefaultName();
        NewNote = string.Empty;
        await RefreshAndSelectAsync(record.Id);
        ShowSuccess(T("Toast_ImportedQr"), record.Name);
    }

    private async Task<QRRecord> SaveRecordAsync(
        string name,
        string content,
        string note,
        QRRecordSource source)
    {
        string trimmedContent = content.Trim();
        if (string.IsNullOrWhiteSpace(trimmedContent))
        {
            throw new AppException(T("Error_EnterQrContent"));
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        byte[] png = _qrCodeService.GeneratePng(trimmedContent);
        string imageFileName = await _imageStorage.SavePngAsync(png);
        QRRecord record = new()
        {
            Name = string.IsNullOrWhiteSpace(name) ? GenerateDefaultName() : name.Trim(),
            Content = trimmedContent,
            ContentType = _contentTypeDetector.Detect(trimmedContent),
            ImageFileName = imageFileName,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            Source = source,
            CreatedAt = now,
            UpdatedAt = now
        };

        return await _repository.AddAsync(record);
    }

    private static string GenerateDefaultName()
    {
        return $"QR_{DateTime.Now:yyyyMMdd_HHmmss}";
    }

    private void ShowCaptureFeedback(string message)
    {
        CaptureStatusText = message;
        StatusText = message;
    }

    private void ShowSuccess(string title, string body)
    {
        ShowCaptureFeedback(body);
        _messageService.Show(title, body, MessageSeverity.Success);
    }

    private void ShowWarning(string title, string body)
    {
        ShowCaptureFeedback(body);
        _messageService.Show(title, body, MessageSeverity.Warning);
    }

    private void ShowError(string title, string body)
    {
        ShowCaptureFeedback(body);
        _messageService.Show(title, body, MessageSeverity.Error);
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

        try
        {
            using FileStream stream = File.OpenRead(path);
            SelectedImage = new Bitmap(stream);
        }
        catch (IOException)
        {
            StatusText = T("Status_QrImageCannotOpen");
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        CaptureStatusText = T("Status_CapturePrompt");
        _ = RefreshAsync();
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
