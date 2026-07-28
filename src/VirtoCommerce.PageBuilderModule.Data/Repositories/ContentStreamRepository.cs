using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VirtoCommerce.PageBuilderModule.Data.Models;

namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

// "owner" is the DI scope the DbContext was resolved from, when the caller created one just to build this
// repository. Disposing it is what returns the pooled connection; without it the context lives until GC and the
// connection opened below stays checked out of the pool. It is optional so a caller that owns the context itself
// (tests, or an ambient request scope) can pass none and keep responsibility for its lifetime.
public abstract class ContentStreamRepository(PageBuilderModuleDbContext dbContext, IDisposable owner = null)
    : ITransactionalContentStreamRepository
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

    // Server-side copy: the payload never leaves the database. A missing or NULL source yields NULL, which is
    // the correct result — the target then reads as "never seeded", exactly like the source.
    // MySQL rejects reading the table being updated in a plain subquery (error 1093) and overrides this.
    protected virtual string CopyContentSql =>
        $"UPDATE {Table} SET {ContentColumn} = (SELECT {ContentColumn} FROM {Table} WHERE {IdColumn} = @sourceId) " +
        $"WHERE {IdColumn} = @id";

    public async Task SaveBinaryAsync(string pageId, TextReader reader, CancellationToken cancellationToken = default)
    {
        await SaveBinaryAsync(pageId, reader, updateIndexesAsync: null, cancellationToken);
    }

    public async Task SaveBinaryAsync(
        string pageId,
        TextReader reader,
        Func<PageBuilderModuleDbContext, CancellationToken, Task> updateIndexesAsync,
        CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.CurrentTransaction != null)
        {
            await SaveBinaryInternalAsync(pageId, reader, cancellationToken);
            if (updateIndexesAsync != null)
            {
                await updateIndexesAsync(dbContext, cancellationToken);
            }

            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        await SaveBinaryInternalAsync(pageId, reader, cancellationToken);
        if (updateIndexesAsync != null)
        {
            await updateIndexesAsync(dbContext, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task SaveBinaryInternalAsync(
        string pageId,
        TextReader reader,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            // InitContentSql blanks the column and the chunks are appended one statement at a time, so without a
            // transaction every save is briefly visible to readers as empty and then as truncated content. Own a
            // transaction here unless the caller already started one, in which case theirs wins and the commands
            // below enlist in it. Not committing on the way out rolls back on dispose.
            var ownsTransaction = dbContext.Database.CurrentTransaction == null;
            await using var transaction = ownsTransaction
                ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
                : null;

            // write empty value first to avoid "out of memory" exception in case of large content
            await using (var init = connection.CreateCommand())
            {
                init.CommandText = InitContentSql;
                SetIdParameter(init, pageId);
                var tx = dbContext.Database.CurrentTransaction?.GetDbTransaction();
                if (tx != null)
                {
                    init.Transaction = tx;
                }

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

                var tx = dbContext.Database.CurrentTransaction?.GetDbTransaction();
                if (tx != null)
                {
                    cmd.Transaction = tx;
                }

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    [Obsolete("Cannot distinguish missing content from empty content. Use TryLoadBinaryAsync instead.")]
    public Task LoadBinaryAsync(string pageId, TextWriter writer, CancellationToken cancellationToken = default)
    {
        return TryLoadBinaryAsync(pageId, writer, cancellationToken);
    }

    public async Task<bool> TryLoadBinaryAsync(string pageId, TextWriter writer, CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
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

            // No row: the page was deleted. Either way the caller must not mistake this for empty content.
            if (!await reader.ReadAsync(cancellationToken))
            {
                return false;
            }

            return await CopyContentToWriterAsync(reader, writer, ContentBufferSize, cancellationToken);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public Task CopyContentAsync(string sourcePageId, string targetPageId, CancellationToken cancellationToken = default)
    {
        return CopyContentInternalAsync(sourcePageId, targetPageId, cancellationToken);
    }

    public async Task CopyContentAsync(
        string sourcePageId,
        string targetPageId,
        Func<PageBuilderModuleDbContext, CancellationToken, Task> updateIndexesAsync,
        CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.CurrentTransaction != null)
        {
            await CopyContentInternalAsync(sourcePageId, targetPageId, cancellationToken);
            if (updateIndexesAsync != null)
            {
                await updateIndexesAsync(dbContext, cancellationToken);
            }

            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        await CopyContentInternalAsync(sourcePageId, targetPageId, cancellationToken);
        if (updateIndexesAsync != null)
        {
            await updateIndexesAsync(dbContext, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task CopyContentInternalAsync(
        string sourcePageId,
        string targetPageId,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = CopyContentSql;

            SetIdParameter(cmd, targetPageId);

            // DbCommand.CreateParameter yields the provider's own parameter type, so the source id needs no
            // per-provider hook of its own.
            var sourceParameter = cmd.CreateParameter();
            sourceParameter.ParameterName = "@sourceId";
            sourceParameter.Value = sourcePageId;
            cmd.Parameters.Add(sourceParameter);

            var tx = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            if (tx != null)
            {
                cmd.Transaction = tx;
            }

            // A single statement is atomic on its own, so no transaction is started here.
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public ValueTask DisposeAsync()
    {
        owner?.Dispose();
        return ValueTask.CompletedTask;
    }

    // PageContent is NULL for a freshly created draft page (no content written yet) and may be NULL
    // for imported/legacy rows. Npgsql's GetTextReader throws InvalidCastException on a NULL column,
    // so guard with IsDBNull first. NULL is reported as "no content" rather than empty content — an empty
    // string is a value SaveBinaryAsync writes deliberately, NULL means nothing was ever written.
    // Returns whether content was found.
    protected static async Task<bool> CopyContentToWriterAsync(
        DbDataReader reader, TextWriter writer, int bufferSize, CancellationToken cancellationToken)
    {
        if (await reader.IsDBNullAsync(0, cancellationToken))
        {
            return false;
        }

        using var textReader = reader.GetTextReader(0);
        var buf = new char[bufferSize];
        int read;
        while ((read = await textReader.ReadAsync(buf, 0, buf.Length)) > 0)
        {
            await writer.WriteAsync(buf.AsMemory(0, read), cancellationToken);
        }

        return true;
    }

    protected abstract void SetIdParameter(DbCommand cmd, string value);
    protected abstract void SetContentChunk(DbCommand cmd, string chunk);
}
