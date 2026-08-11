using System.Data.Common;
using MySqlConnector;
using VirtoCommerce.PageBuilderModule.Data.Repositories;

namespace VirtoCommerce.PageBuilderModule.Data.MySql;

public class MySqlContentStreamRepository(PageBuilderModuleDbContext dbContext)
    : ContentStreamRepository(dbContext)
{
    protected override string QuoteOpen => "`";
    protected override string QuoteClose => "`";

    // MySQL refuses "UPDATE t ... (SELECT ... FROM t)" with error 1093. Wrapping the read in a derived table
    // materialises it first, which is the standard way around the restriction.
    protected override string CopyContentSql =>
        $"UPDATE {Table} SET {ContentColumn} = " +
        $"(SELECT src.c FROM (SELECT {ContentColumn} AS c FROM {Table} WHERE {IdColumn} = @sourceId) AS src) " +
        $"WHERE {IdColumn} = @id";

    protected override string AppendContentChunkSql =>
        $"UPDATE {Table} SET {ContentColumn} = CONCAT({ContentColumn}, @chunk) WHERE {IdColumn} = @id";

    protected override void SetIdParameter(DbCommand cmd, string value)
    {
        cmd.Parameters.Add(new MySqlParameter("@id", value));
    }

    protected override void SetContentChunk(DbCommand cmd, string chunk)
    {
        cmd.Parameters.Add(new MySqlParameter("@chunk", MySqlDbType.VarChar) { Value = chunk });
    }
}
