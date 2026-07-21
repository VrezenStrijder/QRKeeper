using QRKeeper.Core.Models;

namespace QRKeeper.Core.Interfaces;

public interface IBackupService
{
    Task CreateBackupAsync(string backupPath, CancellationToken cancellationToken = default);

    Task RestoreAsync(string backupPath, CancellationToken cancellationToken = default);

    Task<ImportPreview> PreviewImportAsync(string backupPath, CancellationToken cancellationToken = default);

    Task<ImportResult> ImportAsync(ImportPreview preview, CancellationToken cancellationToken = default);
}
