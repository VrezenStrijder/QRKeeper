namespace QRKeeper.Core.Models;

/// <summary>
/// Represents the response returned after a sync request is processed.
/// </summary>
public sealed class SyncResponse
{
    /// <summary>
    /// Gets or sets the responder device information.
    /// </summary>
    public SyncDeviceInfo Responder { get; set; } = new();

    /// <summary>
    /// Gets or sets whether the request was accepted.
    /// </summary>
    public bool Accepted { get; set; }

    /// <summary>
    /// Gets or sets the responder's QR records.
    /// </summary>
    public List<SyncRecordDto> Records { get; set; } = new();

    /// <summary>
    /// Gets or sets the merge result for the request payload.
    /// </summary>
    public SyncMergeResult MergeResult { get; set; } = new();

    /// <summary>
    /// Gets or sets a user-friendly status or error message.
    /// </summary>
    public string? Message { get; set; }
}
