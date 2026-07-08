using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VirtoCommerce.PageBuilderModule.Data.Models;

namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

public abstract class ContentStreamRepository(PageBuilderModuleDbContext dbContext) : IContentStreamRepository
{
    private const int DefaultContentBufferSize = 8192;
    protected virtual int ContentBufferSize => DefaultContentBufferSize;

    // Identifier delimiters differ per provider: " for PostgreSQL, [ ] for SQL Server, ` for MySQL.
    // The raw SQL below must delimit every identifier; PostgreSQL/MySQL fold unquoted PascalCase
    // identifiers to a different case than EF created them, so unquoted names break (42P01 / 42703).
    protected abstract string QuoteOpen { get; }
    protected abstract string QuoteClose { get; }

    private string Quote(string identifier) => $"{QuoteOpen}{identifier}{QuoteClose}";
    protected string Table => Quote(PageBuilderModuleDbContext.PageBuilderPageTableName);
    protected string ContentColumn => Quote(nameof(PageBuilderContentEntity.PageContent));
    protected string IdColumn => Quote("Id");

    // Empty-content literal: SQL Server uses the N'' Unicode literal; other providers use ''.
    protected virtual string EmptyContentLiteral => "''";

    protected virtual string LoadContentSql =>
        $"SELECT {ContentColumn} FROM {Table} WHERE {IdColumn} = @id";

    protected virtual string InitContentSql =>
        $"UPDATE {Table} SET {ContentColumn} = {EmptyContentLiteral} WHERE {IdColumn} = @id";

    // Chunked append differs structurally per dialect, so each provider supplies it in full.
    protected abstract string AppendContentChunkSql { get; }

    public async Task SaveBinaryAsync(string pageId, TextReader reader, CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        var ambientTransaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        await WriteContentAsync(connection, ambientTransaction, pageId, reader, cancellationToken);
    }

    // The content is saved as an "empty first, then append chunks" sequence (writing empty first
    // avoids an out-of-memory exception for large content). That sequence must be ATOMIC: without a
    // transaction each statement auto-commits, so the empty intermediate value becomes globally
    // visible and a concurrent reader (a different connection issuing GET .../content while a save
    // is in flight) reads it as empty content — the transient empty-body read behind VCST-5429,
    // which self-heals once the appends land. Wrapping the init + appends in one transaction means a
    // concurrent reader only ever sees the previous committed content or the new one, never the empty
    // intermediate. When the caller already runs inside a transaction, that ambient one is reused and
    // left for the caller to commit.
    protected virtual async Task WriteContentAsync(
        DbConnection connection,
        DbTransaction ambientTransaction,
        string pageId,
        TextReader reader,
        CancellationToken cancellationToken)
    {
        var ownsTransaction = ambientTransaction == null;
        var transaction = ambientTransaction ?? await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            // write empty value first to avoid "out of memory" exception in case of large content
            await using (var init = connection.CreateCommand())
            {
                init.CommandText = InitContentSql;
                SetIdParameter(init, pageId);
                init.Transaction = transaction;
                await init.ExecuteNonQueryAsync(cancellationToken);
            }

            var buffer = new char[ContentBufferSize];
            int read;
            while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = AppendContentChunkSql;
                SetIdParameter(cmd, pageId);

                var chunk = new string(buffer, 0, read);
                SetContentChunk(cmd, chunk);

                cmd.Transaction = transaction;

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            if (ownsTransaction)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (ownsTransaction)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (ownsTransaction)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task LoadBinaryAsync(string pageId, TextWriter writer, CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = LoadContentSql;

        SetIdParameter(cmd, pageId);

        var tx = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        if (tx != null)
        {
            cmd.Transaction = tx;
        }

        await using var reader = await cmd.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess | CommandBehavior.SingleRow, cancellationToken);

        if (await reader.ReadAsync(cancellationToken))
        {
            await CopyContentToWriterAsync(reader, writer, ContentBufferSize, cancellationToken);
        }
    }

    // PageContent is NULL for a freshly created draft page (no content written yet) and may be NULL
    // for imported/legacy rows. Npgsql's GetTextReader throws InvalidCastException on a NULL column,
    // so guard with IsDBNull first and treat NULL as empty content.
    protected static async Task CopyContentToWriterAsync(
        DbDataReader reader, TextWriter writer, int bufferSize, CancellationToken cancellationToken)
    {
        if (await reader.IsDBNullAsync(0, cancellationToken))
        {
            return;
        }

        using var textReader = reader.GetTextReader(0);
        var buf = new char[bufferSize];
        int read;
        while ((read = await textReader.ReadAsync(buf, 0, buf.Length)) > 0)
        {
            await writer.WriteAsync(buf.AsMemory(0, read), cancellationToken);
        }
    }

    protected abstract void SetIdParameter(DbCommand cmd, string value);
    protected abstract void SetContentChunk(DbCommand cmd, string chunk);
}
