using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Data.PostgreSql;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    // VCST-5429: SaveBinaryAsync persists content as "empty first, then append chunks" (writing empty
    // first keeps memory bounded for large content). If that sequence is not atomic, each statement
    // auto-commits and the empty intermediate becomes globally visible: a concurrent reader on another
    // connection (a different GET .../content request, READ COMMITTED) reads empty content and returns
    // a BOM-only empty body that self-heals on re-fetch — the reported transient empty render.
    //
    // The fake below models a real RDBMS truthfully: writes made inside a transaction stay invisible to
    // other connections until commit; auto-committed statements are visible immediately. The atomicity
    // fix (wrapping init + appends in one transaction) is what keeps every concurrent read non-empty.
    public class ContentStreamAtomicityTests
    {
        [Fact]
        public async Task SaveBinary_never_exposes_empty_content_to_a_concurrent_reader()
        {
            const string existingContent = "{\"blocks\":[\"a\",\"b\"]}";      // content already on the server
            const string newContent = "{\"blocks\":[\"a\",\"b\",\"c\"]}";     // content being saved right now

            var store = new FakeContentStore { Committed = existingContent };
            var connection = new FakeDbConnection(store);

            // What a concurrent reader (different connection, no transaction) would observe after each
            // write statement the save executes.
            var readerObservations = new List<string>();
            connection.AfterWrite = () => readerObservations.Add(store.Committed);

            var repository = new WriteProbe();
            using var reader = new StringReader(newContent);

            // No ambient transaction — mirrors the real DI path (a fresh scope/connection per content op).
            await repository.Write(connection, ambient: null, pageId: "page-1", reader);

            // The defect: a concurrent reader observes empty content mid-save.
            // The fix: every observation stays the previous content until the atomic commit swaps in the new one.
            Assert.All(readerObservations, observed => Assert.False(string.IsNullOrEmpty(observed)));
            Assert.DoesNotContain(string.Empty, readerObservations);

            // And the new content is still fully persisted.
            Assert.Equal(newContent, store.Committed);
        }

        // Exposes the protected WriteContentAsync seam and forces small chunks so the init -> append
        // window spans multiple statements. Reuses PostgreSQL's SQL; the parameter setters are redirected
        // onto the fake command so no real ADO parameter collection is needed.
        private sealed class WriteProbe() : PostgreSqlContentStreamRepository(null!)
        {
            protected override int ContentBufferSize => 4;

            public Task Write(DbConnection connection, DbTransaction ambient, string pageId, TextReader reader) =>
                WriteContentAsync(connection, ambient, pageId, reader, CancellationToken.None);

            protected override void SetIdParameter(DbCommand cmd, string value) => ((FakeDbCommand)cmd).PageId = value;
            protected override void SetContentChunk(DbCommand cmd, string chunk) => ((FakeDbCommand)cmd).Chunk = chunk;
        }

        // The committed (globally visible) content of the single page row.
        private sealed class FakeContentStore
        {
            public string Committed { get; set; } = string.Empty;
        }

        private sealed class FakeDbConnection(FakeContentStore store) : DbConnection
        {
            public FakeContentStore Store => store;
            public Action AfterWrite { get; set; }

            public override string ConnectionString { get; set; } = string.Empty;
            public override string Database => "fake";
            public override string DataSource => "fake";
            public override string ServerVersion => "1.0";
            public override ConnectionState State => ConnectionState.Open;

            public override void ChangeDatabase(string databaseName) { }
            public override void Close() { }
            public override void Open() { }

            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
                new FakeDbTransaction(this, store);

            protected override DbCommand CreateDbCommand() => new FakeDbCommand(this) { Connection = this };
        }

        // Models transactional visibility: writes go to a private snapshot taken at BEGIN and only
        // become visible (store.Committed) on Commit; Rollback discards them.
        private sealed class FakeDbTransaction : DbTransaction
        {
            private readonly FakeDbConnection _connection;
            private readonly FakeContentStore _store;

            public FakeDbTransaction(FakeDbConnection connection, FakeContentStore store)
            {
                _connection = connection;
                _store = store;
                Pending = store.Committed;
            }

            public string Pending { get; set; }

            protected override DbConnection DbConnection => _connection;
            public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

            public override void Commit() => _store.Committed = Pending;
            public override void Rollback() { /* discard Pending */ }
        }

        private sealed class FakeDbCommand(FakeDbConnection connection) : DbCommand
        {
            public string PageId { get; set; }
            public string Chunk { get; set; }

            public override string CommandText { get; set; } = string.Empty;
            public override int CommandTimeout { get; set; }
            public override CommandType CommandType { get; set; }
            public override bool DesignTimeVisible { get; set; }
            public override UpdateRowSource UpdatedRowSource { get; set; }
            protected override DbConnection DbConnection { get; set; }
            protected override DbParameterCollection DbParameterCollection => throw new NotSupportedException();
            protected override DbTransaction DbTransaction { get; set; }

            public override void Cancel() { }
            public override void Prepare() { }

            public override int ExecuteNonQuery()
            {
                var tx = DbTransaction as FakeDbTransaction;

                if (IsInit)
                {
                    // UPDATE ... SET PageContent = '' — truncate to empty.
                    if (tx != null)
                    {
                        tx.Pending = string.Empty;
                    }
                    else
                    {
                        connection.Store.Committed = string.Empty; // auto-commit: immediately visible
                    }
                }
                else
                {
                    // UPDATE ... SET PageContent = PageContent || @chunk — append.
                    if (tx != null)
                    {
                        tx.Pending += Chunk;
                    }
                    else
                    {
                        connection.Store.Committed += Chunk; // auto-commit: immediately visible
                    }
                }

                connection.AfterWrite?.Invoke();
                return 1;
            }

            // The init statement assigns a literal empty value; the append statement concatenates.
            private bool IsInit =>
                !CommandText.Contains("||") &&
                !CommandText.Contains("CONCAT") &&
                !CommandText.Contains(".WRITE");

            public override object ExecuteScalar() => throw new NotSupportedException();
            protected override DbParameter CreateDbParameter() => throw new NotSupportedException();
            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
        }
    }
}
