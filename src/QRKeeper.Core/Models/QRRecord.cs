using QRKeeper.Core.Common;

namespace QRKeeper.Core.Models;

public sealed class QRRecord
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public QRContentType ContentType { get; set; }

    public string ImageFileName { get; set; } = string.Empty;

    public string? Note { get; set; }

    public QRRecordSource Source { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new AppException("名称不能为空。");
        }

        if (Name.Length > AppConstants.MaxNameLength)
        {
            throw new AppException($"名称不能超过 {AppConstants.MaxNameLength} 个字符。");
        }

        if (string.IsNullOrWhiteSpace(Content))
        {
            throw new AppException("二维码内容不能为空。");
        }

        if (Note is { Length: > AppConstants.MaxNoteLength })
        {
            throw new AppException($"注释不能超过 {AppConstants.MaxNoteLength} 个字符。");
        }
    }
}
