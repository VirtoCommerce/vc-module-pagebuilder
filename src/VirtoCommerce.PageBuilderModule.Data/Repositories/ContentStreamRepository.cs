using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

public abstract class ContentStreamRepository(PageBuilderModuleDbContext dbContext) : IContentStreamRepository
{
    private const int DefaultContentBufferSize = 8192;
    protected virtual int ContentBufferSize => DefaultContentBufferSize;
    public async Task SaveBinaryAsync(string pageId, TextReader reader, CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        // write empty value first to avoid "out of memory" exception in case of large content
        await using (var init = connection.CreateCommand())
        {
            init.CommandText =
                $"UPDATE {PageBuilderModuleDbContext.PageBuilderPageTableName} SET PageContent = N'' WHERE Id = @id";
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
            cmd.CommandText =
                $"UPDATE {PageBuilderModuleDbContext.PageBuilderPageTableName} SET PageContent .WRITE(@chunk, NULL, 0) WHERE Id = @id";
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
        cmd.CommandText = $"SELECT PageContent FROM {PageBuilderModuleDbContext.PageBuilderPageTableName} WHERE Id = @id";

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
            using var textReader = reader.GetTextReader(0);
            var buf = new char[ContentBufferSize];
            int read;
            while ((read = await textReader.ReadAsync(buf, 0, buf.Length)) > 0)
            {
                await writer.WriteAsync(buf.AsMemory(0, read), cancellationToken);
            }
        }
    }

    protected abstract void SetIdParameter(DbCommand cmd, string value);
    protected abstract void SetContentChunk(DbCommand cmd, string chunk);
}
