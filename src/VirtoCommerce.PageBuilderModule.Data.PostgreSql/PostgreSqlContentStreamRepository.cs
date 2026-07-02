using System.Data.Common;
using Npgsql;
using VirtoCommerce.PageBuilderModule.Data.Repositories;

namespace VirtoCommerce.PageBuilderModule.Data.PostgreSql;

public class PostgreSqlContentStreamRepository(PageBuilderModuleDbContext dbContext) : ContentStreamRepository(dbContext)
{
    protected override string QuoteOpen => "\"";
    protected override string QuoteClose => "\"";

    protected override string AppendContentChunkSql =>
        $"UPDATE {Table} SET {ContentColumn} = {ContentColumn} || @chunk WHERE {IdColumn} = @id";

    protected override void SetIdParameter(DbCommand cmd, string value)
    {
        cmd.Parameters.Add(new NpgsqlParameter("@id", value));
    }

    protected override void SetContentChunk(DbCommand cmd, string chunk)
    {
        cmd.Parameters.Add(new NpgsqlParameter("@chunk", NpgsqlTypes.NpgsqlDbType.Text) { Value = chunk });
    }
}
