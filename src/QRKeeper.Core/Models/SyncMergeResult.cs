namespace QRKeeper.Core.Models;

/// <summary>
/// Represents the result of importing missing records from a peer.
/// </summary>
public sealed class SyncMergeResult
{
    /// <summary>
    /// Gets or sets the number of imported records.
    /// </summary>
    public int ImportedCount { get; set; }

    /// <summary>
    /// Gets or sets the number of skipped records.
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// Gets or sets the identifiers of imported local records.
    /// </summary>
    public List<int> ImportedRecordIds { get; set; } = new();
}
