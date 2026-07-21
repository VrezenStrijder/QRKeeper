using Avalonia.Controls;
using Avalonia.Platform.Storage;
using QRKeeper.Core.Interfaces;

namespace QRKeeper.UI.Services;

public sealed class DesktopFilePickerService : IFilePickerService
{
    private readonly Func<Window> _getOwner;

    public DesktopFilePickerService(Func<Window> getOwner)
    {
        _getOwner = getOwner;
    }

    public async Task<Stream?> PickImageFileAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IStorageFile? file = await PickSingleFileAsync(
            "Choose image",
            [new FilePickerFileType("Images")
            {
                Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp"]
            }]);

        cancellationToken.ThrowIfCancellationRequested();
        return file is null ? null : await file.OpenReadAsync();
    }

    public async Task<string?> PickBackupOpenPathAsync(CancellationToken cancellationToken = default)
    {
        IStorageFile? file = await PickSingleFileAsync(
            "Open backup",
            [new FilePickerFileType("QRKeeper backup")
            {
                Patterns = ["*.qrbak"]
            }]);

        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickBackupSavePathAsync(string defaultFileName, CancellationToken cancellationToken = default)
    {
        return await PickSavePathAsync("Save backup", defaultFileName, "QRKeeper backup", ["*.qrbak"], cancellationToken);
    }

    public async Task<string?> PickFolderPathAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<IStorageFolder> folders = await _getOwner().StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose folder",
            AllowMultiple = false
        });

        cancellationToken.ThrowIfCancellationRequested();
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickImageSavePathAsync(string defaultFileName, CancellationToken cancellationToken = default)
    {
        return await PickSavePathAsync("Save image", defaultFileName, "PNG image", ["*.png"], cancellationToken);
    }

    private async Task<IStorageFile?> PickSingleFileAsync(string title, IReadOnlyList<FilePickerFileType> fileTypes)
    {
        TopLevel topLevel = _getOwner();
        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        });

        return files.FirstOrDefault();
    }

    private async Task<string?> PickSavePathAsync(
        string title,
        string defaultFileName,
        string fileTypeName,
        IReadOnlyList<string> patterns,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IStorageFile? file = await _getOwner().StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName,
            FileTypeChoices =
            [
                new FilePickerFileType(fileTypeName)
                {
                    Patterns = patterns
                }
            ]
        });

        cancellationToken.ThrowIfCancellationRequested();
        return file?.TryGetLocalPath();
    }
}
