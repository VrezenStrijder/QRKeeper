using System.Data;
using Microsoft.EntityFrameworkCore;
using QRKeeper.Core.Models;

namespace QRKeeper.Infrastructure.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<QRRecord> QRRecords => Set<QRRecord>();

    public void EnsureCreatedAndMigrated()
    {
        Database.EnsureCreated();
        EnsureSortOrderColumn();
    }

    public async Task EnsureCreatedAndMigratedAsync(CancellationToken cancellationToken = default)
    {
        await Database.EnsureCreatedAsync(cancellationToken);
        await EnsureSortOrderColumnAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QRRecord>(entity =>
        {
            entity.ToTable("qr_records");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Name).HasMaxLength(100).IsRequired();
            entity.Property(record => record.Content).IsRequired();
            entity.Property(record => record.ContentType).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(record => record.ImageFileName).HasMaxLength(160).IsRequired();
            entity.Property(record => record.Note).HasMaxLength(500);
            entity.Property(record => record.Source).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(record => record.SortOrder).IsRequired();
            entity.Property(record => record.CreatedAt)
                .HasConversion(value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero))
                .IsRequired();
            entity.Property(record => record.UpdatedAt)
                .HasConversion(value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero))
                .IsRequired();
            entity.HasIndex(record => record.Content);
            entity.HasIndex(record => record.CreatedAt);
            entity.HasIndex(record => record.Name);
            entity.HasIndex(record => record.SortOrder);
        });
    }

    private void EnsureSortOrderColumn()
    {
        System.Data.Common.DbConnection connection = Database.GetDbConnection();
        bool shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            connection.Open();
        }

        try
        {
            if (!HasColumn(connection, "qr_records", nameof(QRRecord.SortOrder)))
            {
                using System.Data.Common.DbCommand command = connection.CreateCommand();
                command.CommandText = $"ALTER TABLE qr_records ADD COLUMN {nameof(QRRecord.SortOrder)} INTEGER NOT NULL DEFAULT 0;";
                command.ExecuteNonQuery();
            }
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }

    private async Task EnsureSortOrderColumnAsync(CancellationToken cancellationToken)
    {
        System.Data.Common.DbConnection connection = Database.GetDbConnection();
        bool shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (!HasColumn(connection, "qr_records", nameof(QRRecord.SortOrder)))
            {
                await using System.Data.Common.DbCommand command = connection.CreateCommand();
                command.CommandText = $"ALTER TABLE qr_records ADD COLUMN {nameof(QRRecord.SortOrder)} INTEGER NOT NULL DEFAULT 0;";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static bool HasColumn(System.Data.Common.DbConnection connection, string tableName, string columnName)
    {
        using System.Data.Common.DbCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        using System.Data.Common.DbDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
