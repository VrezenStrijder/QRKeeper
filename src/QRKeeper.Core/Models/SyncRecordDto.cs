using QRKeeper.Core.Common;

namespace QRKeeper.Core.Models;

/// <summary>
/// Represents a QR record exchanged during LAN sync.
/// </summary>
public sealed class SyncRecordDto
{
    /// <summary>
    /// Gets or sets the record title.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the QR content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content type.
    /// </summary>
    public QRContentType ContentType { get; set; }

    /// <summary>
    /// Gets or sets the note text.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Gets or sets the record source.
    /// </summary>
    public QRRecordSource Source { get; set; }

    /// <summary>
    /// Gets or sets the creation time.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update time.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
