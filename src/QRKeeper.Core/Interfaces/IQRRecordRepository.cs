using QRKeeper.Core.Models;

namespace QRKeeper.Core.Interfaces;

public interface IQRRecordRepository
{
    Task<IReadOnlyList<QRRecord>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QRRecord>> SearchAsync(
        string? nameKeyword,
        DateTimeOffset? createdFrom,
        DateTimeOffset? createdTo,
        CancellationToken cancellationToken = default);

    Task<QRRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<QRRecord?> FindByContentAsync(string content, CancellationToken cancellationToken = default);

    Task<QRRecord> AddAsync(QRRecord record, CancellationToken cancellationToken = default);

    Task UpdateAsync(QRRecord record, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task ReorderAsync(IReadOnlyList<int> orderedIds, CancellationToken cancellationToken = default);

    Task ReplaceAllAsync(IEnumerable<QRRecord> records, CancellationToken cancellationToken = default);
}
