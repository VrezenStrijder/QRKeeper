using System.IO.Compression;
using System.Text.Json;
using QRKeeper.Core.Common;
using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;

namespace QRKeeper.Infrastructure.Services;

public sealed class BackupService : IBackupService
{
    private const string ManifestEntryName = "manifest.json";
    private const string DataEntryName = "data.json";
    private const string ImageEntryPrefix = "images/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly IQRRecordRepository _repository;
    private readonly IImageStorageService _imageStorage;

    public BackupService(IQRRecordRepository repository, IImageStorageService imageStorage)
    {
        _repository = repository;
        _imageStorage = imageStorage;
    }

    public async Task CreateBackupAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<QRRecord> records = await _repository.GetAllAsync(cancellationToken);
        BackupManifest manifest = new()
        {
            AppName = AppConstants.AppName,
            Version = AppConstants.AppVersion,
            BackupTime = DateTimeOffset.UtcNow,
            RecordCount = records.Count,
            Platform = Environment.OSVersion.Platform.ToString()
        };

        string? directory = Path.GetDirectoryName(backupPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream fileStream = File.Create(backupPath);
        using ZipArchive archive = new(fileStream, ZipArchiveMode.Create);

        await WriteJsonEntryAsync(archive, ManifestEntryName, manifest, cancellationToken);
        await WriteJsonEntryAsync(archive, DataEntryName, new BackupData { Records = records.ToList() }, cancellationToken);

        foreach (QRRecord record in records)
        {
            if (string.IsNullOrWhiteSpace(record.ImageFileName))
            {
                continue;
            }

            byte[]? imageBytes = await _imageStorage.ReadAsync(record.ImageFileName, cancellationToken);
            if (imageBytes is null)
            {
                continue;
            }

            ZipArchiveEntry imageEntry = archive.CreateEntry(ImageEntryPrefix + Path.GetFileName(record.ImageFileName));
            await using Stream imageStream = imageEntry.Open();
            await imageStream.WriteAsync(imageBytes, cancellationToken);
        }
    }

    public async Task RestoreAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        (BackupManifest manifest, List<QRRecord> records) = await ReadBackupAsync(backupPath, cancellationToken);
        ValidateManifest(manifest);

        await using FileStream fileStream = File.OpenRead(backupPath);
        using ZipArchive archive = new(fileStream, ZipArchiveMode.Read);

        List<QRRecord> restoredRecords = new(records.Count);
        foreach (QRRecord record in records)
        {
            QRRecord restored = CloneForInsert(record);
            restored.ImageFileName = await RestoreImageAsync(archive, record.ImageFileName, cancellationToken);
            restoredRecords.Add(restored);
        }

        await _repository.ReplaceAllAsync(restoredRecords, cancellationToken);
    }

    public async Task<ImportPreview> PreviewImportAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        (BackupManifest manifest, List<QRRecord> records) = await ReadBackupAsync(backupPath, cancellationToken);
        ValidateManifest(manifest);

        ImportPreview preview = new()
        {
            BackupPath = backupPath,
            Manifest = manifest
        };

        foreach (QRRecord record in records)
        {
            QRRecord? existing = await _repository.FindByContentAsync(record.Content, cancellationToken);
            bool isDuplicate = existing is not null;
            preview.Items.Add(new ImportPreviewItem
            {
                Record = record,
                ExistingRecord = existing,
                IsDuplicate = isDuplicate,
                IsSelected = !isDuplicate
            });
        }

        return preview;
    }

    public async Task<ImportResult> ImportAsync(ImportPreview preview, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(preview.BackupPath))
        {
            throw new AppException("缺少导入来源文件。");
        }

        await using FileStream fileStream = File.OpenRead(preview.BackupPath);
        using ZipArchive archive = new(fileStream, ZipArchiveMode.Read);

        int imported = 0;
        int skipped = 0;
        foreach (ImportPreviewItem item in preview.Items)
        {
            if (!item.IsSelected)
            {
                skipped++;
                continue;
            }

            QRRecord record = CloneForInsert(item.Record);
            record.Source = QRRecordSource.BackupImport;
            record.ImageFileName = await RestoreImageAsync(archive, item.Record.ImageFileName, cancellationToken);
            await _repository.AddAsync(record, cancellationToken);
            imported++;
        }

        return new ImportResult
        {
            ImportedCount = imported,
            SkippedCount = skipped
        };
    }

    private static async Task WriteJsonEntryAsync<T>(
        ZipArchive archive,
        string entryName,
        T value,
        CancellationToken cancellationToken)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryName);
        await using Stream stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
    }

    private static async Task<(BackupManifest Manifest, List<QRRecord> Records)> ReadBackupAsync(
        string backupPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(backupPath))
        {
            throw new AppException("备份文件不存在。");
        }

        await using FileStream fileStream = File.OpenRead(backupPath);
        using ZipArchive archive = new(fileStream, ZipArchiveMode.Read);

        BackupManifest manifest = await ReadJsonEntryAsync<BackupManifest>(archive, ManifestEntryName, cancellationToken);
        BackupData data = await ReadJsonEntryAsync<BackupData>(archive, DataEntryName, cancellationToken);
        return (manifest, data.Records);
    }

    private static async Task<T> ReadJsonEntryAsync<T>(
        ZipArchive archive,
        string entryName,
        CancellationToken cancellationToken)
    {
        ZipArchiveEntry? entry = archive.GetEntry(entryName);
        if (entry is null)
        {
            throw new AppException($"备份文件缺少 {entryName}。");
        }

        await using Stream stream = entry.Open();
        T? value = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
        return value ?? throw new AppException($"备份文件中的 {entryName} 无效。");
    }

    private static void ValidateManifest(BackupManifest manifest)
    {
        if (!string.Equals(manifest.AppName, AppConstants.AppName, StringComparison.Ordinal))
        {
            throw new AppException("不是有效的 QRKeeper 备份文件。");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new AppException("备份文件版本无效。");
        }
    }

    private static QRRecord CloneForInsert(QRRecord source)
    {
        return new QRRecord
        {
            Name = source.Name,
            Content = source.Content,
            ContentType = source.ContentType,
            ImageFileName = source.ImageFileName,
            Note = source.Note,
            Source = source.Source,
            SortOrder = source.SortOrder,
            CreatedAt = source.CreatedAt == default ? DateTimeOffset.UtcNow : source.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private async Task<string> RestoreImageAsync(
        ZipArchive archive,
        string imageFileName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageFileName))
        {
            return string.Empty;
        }

        string sourceFileName = Path.GetFileName(imageFileName);
        ZipArchiveEntry? entry = archive.GetEntry(ImageEntryPrefix + sourceFileName);
        if (entry is null)
        {
            return string.Empty;
        }

        await using Stream stream = entry.Open();
        using MemoryStream memoryStream = new();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        return await _imageStorage.SavePngAsync(memoryStream.ToArray(), cancellationToken);
    }

    private sealed class BackupData
    {
        public List<QRRecord> Records { get; set; } = new();
    }
}
