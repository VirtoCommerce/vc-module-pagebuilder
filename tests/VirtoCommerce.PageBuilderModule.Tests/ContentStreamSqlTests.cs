using System;
using System.Collections;
using System.Data.Common;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Data.MySql;
using VirtoCommerce.PageBuilderModule.Data.PostgreSql;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.PageBuilderModule.Data.SqlServer;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    // The raw ADO.NET content-stream SQL must use provider-correct, delimited identifiers and
    // provider-correct dialect for the chunked append. SQL Server's bare/PascalCase names happen
    // to work on its case-insensitive default collation, but PostgreSQL folds unquoted identifiers
    // to lower case (causing 42P01 "relation does not exist") and has no T-SQL .WRITE()/N'' syntax.
    public class ContentStreamSqlTests
    {
        // Test seams expose the protected SQL members without needing a live database connection.
        private sealed class SqlServerProbe() : SqlServerContentStreamRepository(null!)
        {
            public string Load => LoadContentSql;
            public string Init => InitContentSql;
            public string Append => AppendContentChunkSql;
        }

        private sealed class PostgreSqlProbe() : PostgreSqlContentStreamRepository(null!)
        {
            public string Load => LoadContentSql;
            public string Init => InitContentSql;
            public string Append => AppendContentChunkSql;
        }

        private sealed class MySqlProbe() : MySqlContentStreamRepository(null!)
        {
            public string Load => LoadContentSql;
            public string Init => InitContentSql;
            public string Append => AppendContentChunkSql;
        }

        [Fact]
        public void PostgreSql_quotes_identifiers_and_uses_concatenation_append()
        {
            var sql = new PostgreSqlProbe();

            Assert.Equal("SELECT \"PageContent\" FROM \"PageBuilderPage\" WHERE \"Id\" = @id", sql.Load);
            Assert.Equal("UPDATE \"PageBuilderPage\" SET \"PageContent\" = '' WHERE \"Id\" = @id", sql.Init);
            Assert.Equal("UPDATE \"PageBuilderPage\" SET \"PageContent\" = \"PageContent\" || @chunk WHERE \"Id\" = @id", sql.Append);
        }

        [Fact]
        public void PostgreSql_sql_has_no_sql_server_only_syntax()
        {
            var sql = new PostgreSqlProbe();

            Assert.DoesNotContain(".WRITE", sql.Append);
            Assert.DoesNotContain("N''", sql.Init);
        }

        [Fact]
        public void MySql_backquotes_identifiers_and_uses_concat_append()
        {
            var sql = new MySqlProbe();

            Assert.Equal("SELECT `PageContent` FROM `PageBuilderPage` WHERE `Id` = @id", sql.Load);
            Assert.Equal("UPDATE `PageBuilderPage` SET `PageContent` = '' WHERE `Id` = @id", sql.Init);
            Assert.Equal("UPDATE `PageBuilderPage` SET `PageContent` = CONCAT(`PageContent`, @chunk) WHERE `Id` = @id", sql.Append);
        }

        [Fact]
        public void SqlServer_brackets_identifiers_and_keeps_write_mutator()
        {
            var sql = new SqlServerProbe();

            Assert.Equal("SELECT [PageContent] FROM [PageBuilderPage] WHERE [Id] = @id", sql.Load);
            Assert.Equal("UPDATE [PageBuilderPage] SET [PageContent] = N'' WHERE [Id] = @id", sql.Init);
            Assert.Equal("UPDATE [PageBuilderPage] SET [PageContent].WRITE(@chunk, NULL, 0) WHERE [Id] = @id", sql.Append);
        }

        // Exposes the protected static copy helper. A freshly created draft page has PageContent = NULL,
        // and Npgsql's GetTextReader throws InvalidCastException on a NULL column (the reported crash).
        private sealed class CopyProbe() : PostgreSqlContentStreamRepository(null!)
        {
            public static Task Copy(DbDataReader reader, TextWriter writer) =>
                CopyContentToWriterAsync(reader, writer, 8192, CancellationToken.None);
        }

        [Fact]
        public async Task LoadContent_returns_empty_for_null_page_content()
        {
            var reader = new SingleColumnReader(value: null);
            var sb = new StringBuilder();
            await using var writer = new StringWriter(sb);

            await CopyProbe.Copy(reader, writer);

            Assert.Equal(string.Empty, sb.ToString());
            Assert.False(reader.TextReaderRequested); // must not touch GetTextReader on a NULL column
        }

        [Fact]
        public async Task LoadContent_copies_non_null_page_content()
        {
            var reader = new SingleColumnReader(value: "hello world");
            var sb = new StringBuilder();
            await using var writer = new StringWriter(sb);

            await CopyProbe.Copy(reader, writer);

            Assert.Equal("hello world", sb.ToString());
        }

        // Minimal DbDataReader over a single string column that mimics Npgsql: GetTextReader throws on NULL.
        private sealed class SingleColumnReader(string value) : DbDataReader
        {
            public bool TextReaderRequested { get; private set; }

            public override bool IsDBNull(int ordinal) => value == null;
            public override string GetString(int ordinal) => value;

            public override TextReader GetTextReader(int ordinal)
            {
                TextReaderRequested = true;
                if (value == null)
                {
                    throw new InvalidCastException($"Column '{ordinal}' is null.");
                }

                return new StringReader(value);
            }

            // Unused members.
            public override object this[int ordinal] => throw new NotSupportedException();
            public override object this[string name] => throw new NotSupportedException();
            public override int Depth => throw new NotSupportedException();
            public override int FieldCount => 1;
            public override bool HasRows => true;
            public override bool IsClosed => false;
            public override int RecordsAffected => -1;
            public override bool GetBoolean(int ordinal) => throw new NotSupportedException();
            public override byte GetByte(int ordinal) => throw new NotSupportedException();
            public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length) => throw new NotSupportedException();
            public override char GetChar(int ordinal) => throw new NotSupportedException();
            public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length) => throw new NotSupportedException();
            public override string GetDataTypeName(int ordinal) => throw new NotSupportedException();
            public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();
            public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();
            public override double GetDouble(int ordinal) => throw new NotSupportedException();
            public override Type GetFieldType(int ordinal) => typeof(string);
            public override float GetFloat(int ordinal) => throw new NotSupportedException();
            public override Guid GetGuid(int ordinal) => throw new NotSupportedException();
            public override short GetInt16(int ordinal) => throw new NotSupportedException();
            public override int GetInt32(int ordinal) => throw new NotSupportedException();
            public override long GetInt64(int ordinal) => throw new NotSupportedException();
            public override string GetName(int ordinal) => "PageContent";
            public override int GetOrdinal(string name) => 0;
            public override object GetValue(int ordinal) => throw new NotSupportedException();
            public override int GetValues(object[] values) => throw new NotSupportedException();
            public override bool NextResult() => false;
            public override bool Read() => true;
            public override IEnumerator GetEnumerator() => throw new NotSupportedException();
        }
    }
}
