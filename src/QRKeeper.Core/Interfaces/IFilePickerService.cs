namespace QRKeeper.Core.Interfaces;

public interface IFilePickerService
{
    Task<Stream?> PickImageFileAsync(CancellationToken cancellationToken = default);

    Task<string?> PickBackupOpenPathAsync(CancellationToken cancellationToken = default);

    Task<string?> PickBackupSavePathAsync(string defaultFileName, CancellationToken cancellationToken = default);

    Task<string?> PickFolderPathAsync(CancellationToken cancellationToken = default);

    Task<string?> PickImageSavePathAsync(string defaultFileName, CancellationToken cancellationToken = default);
}
