using Avalonia.Controls;
using Avalonia.Platform.Storage;
using QRKeeper.Core.Interfaces;

namespace QRKeeper.Android.Services;

public sealed class AndroidFilePickerService : IFilePickerService
{
    private readonly Func<TopLevel?> _getTopLevel;

    public AndroidFilePickerService(Func<TopLevel?> getTopLevel)
    {
        _getTopLevel = getTopLevel;
    }

    public async Task<Stream?> PickImageFileAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TopLevel? topLevel = _getTopLevel();
        if (topLevel is null || !topLevel.StorageProvider.CanOpen)
        {
            throw new InvalidOperationException("Image picker is not available.");
        }

        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose image",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Images")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp"],
                    MimeTypes = ["image/png", "image/jpeg", "image/bmp", "image/gif", "image/webp"]
                }
            ]
        });

        cancellationToken.ThrowIfCancellationRequested();
        return files.FirstOrDefault() is { } file ? await file.OpenReadAsync() : null;
    }

    public Task<string?> PickBackupOpenPathAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<string?> PickBackupSavePathAsync(string defaultFileName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<string?> PickFolderPathAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<string?> PickImageSavePathAsync(string defaultFileName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }
}
