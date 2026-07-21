using Avalonia.Controls;
using Avalonia.Platform.Storage;
using QRKeeper.Core.Common;

namespace QRKeeper.Android.Services;

public sealed class AndroidBackupFileService
{
    private readonly Func<TopLevel?> _getTopLevel;
    private readonly string _backupDirectory;

    public AndroidBackupFileService(Func<TopLevel?> getTopLevel, string appDataDirectory)
    {
        _getTopLevel = getTopLevel;
        _backupDirectory = Path.Combine(appDataDirectory, "Backups");
        Directory.CreateDirectory(_backupDirectory);
    }

    public string CreateLocalBackupPath(string suffix = "")
    {
        Directory.CreateDirectory(_backupDirectory);
        string normalizedSuffix = string.IsNullOrWhiteSpace(suffix) ? string.Empty : $"_{suffix}";
        return Path.Combine(
            _backupDirectory,
            $"QRKeeper_{DateTime.Now:yyyyMMdd_HHmmss}{normalizedSuffix}{AppConstants.BackupExtension}");
    }

    public async Task<string?> PickBackupOpenCopyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TopLevel? topLevel = _getTopLevel();
        if (topLevel is null || !topLevel.StorageProvider.CanOpen)
        {
            throw new InvalidOperationException("Backup file picker is not available.");
        }

        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open backup",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("QRKeeper backup")
                {
                    Patterns = ["*.qrbak"],
                    MimeTypes = ["application/zip", "application/octet-stream"]
                }
            ]
        });

        cancellationToken.ThrowIfCancellationRequested();
        IStorageFile? file = files.FirstOrDefault();
        if (file is null)
        {
            return null;
        }

        string localPath = CreateLocalBackupPath("Selected");
        await using Stream source = await file.OpenReadAsync();
        await using FileStream target = File.Create(localPath);
        await source.CopyToAsync(target, cancellationToken);
        return localPath;
    }

    public async Task<string?> ExportBackupAsync(
        string sourcePath,
        string defaultFileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TopLevel? topLevel = _getTopLevel();
        if (topLevel is null || !topLevel.StorageProvider.CanSave)
        {
            throw new InvalidOperationException("Backup export picker is not available.");
        }

        IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save backup",
            SuggestedFileName = defaultFileName,
            FileTypeChoices =
            [
                new FilePickerFileType("QRKeeper backup")
                {
                    Patterns = ["*.qrbak"],
                    MimeTypes = ["application/zip", "application/octet-stream"]
                }
            ]
        });

        cancellationToken.ThrowIfCancellationRequested();
        if (file is null)
        {
            return null;
        }

        await using FileStream source = File.OpenRead(sourcePath);
        await using Stream target = await file.OpenWriteAsync();
        if (target.CanSeek)
        {
            target.SetLength(0);
        }

        await source.CopyToAsync(target, cancellationToken);
        return file.TryGetLocalPath() ?? file.Name;
    }
}
