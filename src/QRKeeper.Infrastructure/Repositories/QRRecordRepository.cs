using Microsoft.EntityFrameworkCore;
using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;
using QRKeeper.Infrastructure.Data;

namespace QRKeeper.Infrastructure.Repositories;

public sealed class QRRecordRepository : IQRRecordRepository
{
    private readonly AppDbContext _dbContext;

    public QRRecordRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<QRRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.QRRecords
            .AsNoTracking()
            .OrderBy(record => record.SortOrder)
            .ThenByDescending(record => record.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<QRRecord>> SearchAsync(
        string? nameKeyword,
        DateTimeOffset? createdFrom,
        DateTimeOffset? createdTo,
        CancellationToken cancellationToken = default)
    {
        IQueryable<QRRecord> query = _dbContext.QRRecords.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(nameKeyword))
        {
            string keyword = nameKeyword.Trim();
            query = query.Where(record => EF.Functions.Like(record.Name, $"%{keyword}%"));
        }

        if (createdFrom.HasValue)
        {
            query = query.Where(record => record.CreatedAt >= createdFrom.Value);
        }

        if (createdTo.HasValue)
        {
            query = query.Where(record => record.CreatedAt <= createdTo.Value);
        }

        return await query
            .OrderBy(record => record.SortOrder)
            .ThenByDescending(record => record.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<QRRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QRRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(record => record.Id == id, cancellationToken);
    }

    public async Task<QRRecord?> FindByContentAsync(string content, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QRRecords
            .AsNoTracking()
            .OrderBy(record => record.SortOrder)
            .ThenByDescending(record => record.CreatedAt)
            .FirstOrDefaultAsync(record => record.Content == content, cancellationToken);
    }

    public async Task<QRRecord> AddAsync(QRRecord record, CancellationToken cancellationToken = default)
    {
        record.Validate();
        int? minSortOrder = await _dbContext.QRRecords
            .Select(existing => (int?)existing.SortOrder)
            .MinAsync(cancellationToken);
        record.SortOrder = minSortOrder.HasValue ? minSortOrder.Value - 1 : 0;
        _dbContext.QRRecords.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return record;
    }

    public async Task UpdateAsync(QRRecord record, CancellationToken cancellationToken = default)
    {
        record.Validate();

        QRRecord? tracked = _dbContext.QRRecords.Local.FirstOrDefault(existing => existing.Id == record.Id);
        if (tracked is not null)
        {
            _dbContext.Entry(tracked).CurrentValues.SetValues(record);
        }
        else
        {
            _dbContext.QRRecords.Update(record);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        QRRecord? record = await _dbContext.QRRecords.FindAsync([id], cancellationToken);
        if (record is null)
        {
            return;
        }

        _dbContext.QRRecords.Remove(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderAsync(IReadOnlyList<int> orderedIds, CancellationToken cancellationToken = default)
    {
        Dictionary<int, int> sortOrders = orderedIds
            .Distinct()
            .Select((id, index) => new { id, index })
            .ToDictionary(item => item.id, item => item.index);

        if (sortOrders.Count == 0)
        {
            return;
        }

        List<QRRecord> records = await _dbContext.QRRecords
            .Where(record => sortOrders.Keys.Contains(record.Id))
            .ToListAsync(cancellationToken);

        foreach (QRRecord record in records)
        {
            record.SortOrder = sortOrders[record.Id];
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceAllAsync(IEnumerable<QRRecord> records, CancellationToken cancellationToken = default)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        _dbContext.QRRecords.RemoveRange(_dbContext.QRRecords);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (QRRecord record in records)
        {
            record.Id = 0;
            record.Validate();
        }

        await _dbContext.QRRecords.AddRangeAsync(records, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
