using QRKeeper.Core.Common;
using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;

namespace QRKeeper.Infrastructure.Services;

/// <summary>
/// Performs additive LAN sync merge operations against the local repository.
/// </summary>
public sealed class SyncMergeService : ISyncMergeService
{
    private readonly IContentTypeDetector _contentTypeDetector;
    private readonly IImageStorageService _imageStorage;
    private readonly IQRCodeService _qrCodeService;
    private readonly IQRRecordRepository _repository;

    public SyncMergeService(
        IQRRecordRepository repository,
        IQRCodeService qrCodeService,
        IImageStorageService imageStorage,
        IContentTypeDetector contentTypeDetector)
    {
        _repository = repository;
        _qrCodeService = qrCodeService;
        _imageStorage = imageStorage;
        _contentTypeDetector = contentTypeDetector;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SyncRecordDto>> GetLocalRecordsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<QRRecord> records = await _repository.GetAllAsync(cancellationToken);
        return records.Select(ToDto).ToArray();
    }

    /// <inheritdoc />
    public async Task<SyncMergeResult> ImportMissingAsync(
        IEnumerable<SyncRecordDto> records,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        IReadOnlyList<QRRecord> existingRecords = await _repository.GetAllAsync(cancellationToken);
        HashSet<(string Name, string Content)> existingKeys = existingRecords
            .Select(record => (record.Name, record.Content))
            .ToHashSet();

        List<SyncRecordDto> pendingImports = new();
        int skippedCount = 0;

        foreach (SyncRecordDto? record in records)
        {
            if (record is null)
            {
                skippedCount++;
                continue;
            }

            ValidateIncomingRecord(record);
            (string name, string content) key = GetKey(record);
            if (!existingKeys.Add(key))
            {
                skippedCount++;
                continue;
            }

            pendingImports.Add(record);
        }

        List<int> importedIds = new();
        foreach (SyncRecordDto record in pendingImports.AsEnumerable().Reverse())
        {
            QRRecord entity = ToEntity(record);
            byte[] pngBytes = _qrCodeService.GeneratePng(entity.Content);
            entity.ImageFileName = await _imageStorage.SavePngAsync(pngBytes, cancellationToken);

            QRRecord saved = await _repository.AddAsync(entity, cancellationToken);
            importedIds.Add(saved.Id);
        }

        return new SyncMergeResult
        {
            ImportedCount = importedIds.Count,
            SkippedCount = skippedCount,
            ImportedRecordIds = importedIds
        };
    }

    /// <summary>
    /// Converts a local record to a sync DTO.
    /// </summary>
    private static SyncRecordDto ToDto(QRRecord record)
    {
        return new SyncRecordDto
        {
            Name = record.Name,
            Content = record.Content,
            ContentType = record.ContentType,
            Note = record.Note,
            Source = record.Source,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt
        };
    }

    /// <summary>
    /// Converts an incoming sync DTO to a local record.
    /// </summary>
    private QRRecord ToEntity(SyncRecordDto record)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new QRRecord
        {
            Name = record.Name,
            Content = record.Content,
            ContentType = record.ContentType == QRContentType.Unknown
                ? _contentTypeDetector.Detect(record.Content)
                : record.ContentType,
            ImageFileName = string.Empty,
            Note = record.Note,
            Source = record.Source,
            CreatedAt = record.CreatedAt == default ? now : record.CreatedAt,
            UpdatedAt = record.UpdatedAt == default ? now : record.UpdatedAt
        };
    }

    /// <summary>
    /// Builds the duplicate key used by sync merge.
    /// </summary>
    private static (string Name, string Content) GetKey(SyncRecordDto record)
    {
        return (record.Name, record.Content);
    }

    /// <summary>
    /// Validates an incoming sync record before import.
    /// </summary>
    private static void ValidateIncomingRecord(SyncRecordDto record)
    {
        if (string.IsNullOrWhiteSpace(record.Name))
        {
            throw new AppException("同步记录名称不能为空。");
        }

        if (string.IsNullOrWhiteSpace(record.Content))
        {
            throw new AppException("同步记录内容不能为空。");
        }

        if (record.Name.Length > AppConstants.MaxNameLength)
        {
            throw new AppException($"同步记录名称不能超过 {AppConstants.MaxNameLength} 个字符。");
        }

        if (record.Note is { Length: > AppConstants.MaxNoteLength })
        {
            throw new AppException($"同步记录备注不能超过 {AppConstants.MaxNoteLength} 个字符。");
        }
    }
}
