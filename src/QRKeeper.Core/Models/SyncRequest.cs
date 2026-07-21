namespace QRKeeper.Core.Models;

/// <summary>
/// Represents a sync request sent from one device to another.
/// </summary>
public sealed class SyncRequest
{
    /// <summary>
    /// Gets or sets the sender device information.
    /// </summary>
    public SyncDeviceInfo Sender { get; set; } = new();

    /// <summary>
    /// Gets or sets the sender's QR records.
    /// </summary>
    public List<SyncRecordDto> Records { get; set; } = new();
}
