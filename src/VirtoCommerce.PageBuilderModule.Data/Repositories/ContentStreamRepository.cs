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

        if (await reader.ReadAsync(cancellationToken) && !await reader.IsDBNullAsync(0, cancellationToken))
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
