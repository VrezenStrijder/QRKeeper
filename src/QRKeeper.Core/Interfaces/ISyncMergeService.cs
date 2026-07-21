using QRKeeper.Core.Models;

namespace QRKeeper.Core.Interfaces;

/// <summary>
/// Provides record snapshot and additive merge operations for LAN sync.
/// </summary>
public interface ISyncMergeService
{
    /// <summary>
    /// Gets the current local records in sync-transfer form.
    /// </summary>
    Task<IReadOnlyList<SyncRecordDto>> GetLocalRecordsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports missing records from the peer payload.
    /// </summary>
    Task<SyncMergeResult> ImportMissingAsync(
        IEnumerable<SyncRecordDto> records,
        CancellationToken cancellationToken = default);
}
