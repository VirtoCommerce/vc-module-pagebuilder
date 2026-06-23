---
name: content-stream-sql-portability
description: ContentStreamRepository raw SQL was SQL-Server-only; portability fix and the seam used
metadata:
  type: project
---

The PageBuilder content blob is read/written via hand-written ADO.NET in `ContentStreamRepository` (Data/Repositories), NOT EF. Before fix/VCST-5285 the shared base class emitted T-SQL with three SQL-Server-only constructs that broke PostgreSQL/MySQL — the visible symptom was `42P01 relation "pagebuilderpage" does not exist`:

1. Unquoted PascalCase identifiers (table AND columns `PageContent`/`Id`) — Postgres folds to lowercase, mismatching EF's quoted PascalCase tables → 42P01 then 42703.
2. `N''` Unicode literal — SQL Server only.
3. `PageContent.WRITE(@chunk, NULL, 0)` chunked mutator — SQL Server only; no PG/MySQL equivalent.

**Why non-obvious:** the 42P01 masks #2 and #3 (table name fails to resolve first), so "just quote the table name" is an incomplete fix — only the SELECT/read path is salvageable by quoting; the save path needed per-dialect rewrites.

**How it's fixed:** base class builds SQL from provider-supplied quoting primitives (`QuoteOpen`/`QuoteClose`, `EmptyContentLiteral`, abstract `AppendContentChunkSql`). PG uses `"x"` + `|| @chunk`; MySQL uses `` `x` `` + `CONCAT(...)`; SQL Server uses `[x]` + `.WRITE()`. Tests in `tests/.../ContentStreamSqlTests.cs` assert each provider's generated SQL via test-subclass probes (no DB needed).

**Second fix (same path):** `LoadBinaryAsync` called `reader.GetTextReader(0)` unconditionally; a freshly created draft page (and imported/legacy rows) has `PageContent = NULL`, and Npgsql's GetTextReader throws `InvalidCastException: Column 'PageContent' is null`. Fixed by extracting `CopyContentToWriterAsync` with an `IsDBNullAsync` guard that treats NULL as empty content. Regression-tested with a minimal fake `DbDataReader` that mimics Npgsql's throw-on-null.

**Still open (not done in this change):** create is non-atomic — `PageBuilderPageController.CreateGroup` commits group rows via CrudService, then the post-commit `GroupedPageBuilderPageChangedEvent` handler reads content back; a failure there leaves orphan groups. Defense-in-depth follow-up.

**Test runner note:** xunit.v3 tests aren't discovered by `dotnet test` here; run the built `VirtoCommerce.PageBuilderModule.Tests.exe` directly.
