namespace QRKeeper.Core.Models;

/// <summary>
/// Represents the result of a completed sync session.
/// </summary>
public sealed class SyncSessionResult
{
    /// <summary>
    /// Gets or sets the response returned by the peer.
    /// </summary>
    public SyncResponse PeerResponse { get; set; } = new();

    /// <summary>
    /// Gets or sets the result of importing the peer's records locally.
    /// </summary>
    public SyncMergeResult LocalMergeResult { get; set; } = new();

    /// <summary>
    /// Gets the total imported count across both sides.
    /// </summary>
    public int ImportedCount => PeerResponse.MergeResult.ImportedCount + LocalMergeResult.ImportedCount;

    /// <summary>
    /// Gets the total skipped count across both sides.
    /// </summary>
    public int SkippedCount => PeerResponse.MergeResult.SkippedCount + LocalMergeResult.SkippedCount;
}
