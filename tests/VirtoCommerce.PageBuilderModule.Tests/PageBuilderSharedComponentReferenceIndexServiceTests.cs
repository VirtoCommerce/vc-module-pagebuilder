using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.PageBuilderModule.Data.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderSharedComponentReferenceIndexServiceTests
{
    [Fact]
    public void ValidateComponentReferences_RejectsMetadataOrphanWithoutContentRow()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            PageBuilderSharedComponentReferenceIndexService.ValidateComponentReferences(
                ["component"],
                "store",
                new Dictionary<string, string> { ["component"] = "store" },
                new HashSet<string>()));

        Assert.Contains("has no content", exception.Message);
    }

    [Fact]
    public async Task ValidateReferencesForStoreAsync_ValidatesVariantUnionWithoutNPlusOneQueries()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var counter = new CommandCounter();
        var options = new DbContextOptionsBuilder<PageBuilderModuleDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(counter)
            .Options;
        await using (var setupContext = new PageBuilderModuleDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            setupContext.AddRange(
                CreateComponent("component-a"),
                CreateComponent("component-b"));
            await setupContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        counter.Reset();
        var service = new PageBuilderSharedComponentReferenceIndexService(
            () => new PageBuilderModuleRepository(new PageBuilderModuleDbContext(options)));
        var contents = Enumerable.Range(0, 100)
            .Select(index =>
                $"{{ \"settings\": {{}}, \"content\": [" +
                $"{{ \"id\": \"placement-a-{index}\", \"type\": \"componentRef\", \"componentRef\": \"component-a\" }}," +
                $"{{ \"id\": \"placement-b-{index}\", \"type\": \"componentRef\", \"componentRef\": \"component-b\" }}] }}")
            .ToArray();

        await service.ValidateReferencesForStoreAsync(
            "store",
            contents,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, counter.ReaderCommandCount);
    }

    private static PageBuilderSharedComponentEntity CreateComponent(string id)
    {
        return new PageBuilderSharedComponentEntity
        {
            Id = id,
            StoreId = "store",
            Name = id,
            CreatedDate = DateTime.UtcNow,
            Content = new PageBuilderSharedComponentContentEntity
            {
                Id = id,
                ComponentContent = "{ \"settings\": {}, \"content\": [] }",
            },
        };
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int ReaderCommandCount { get; private set; }

        public void Reset() => ReaderCommandCount = 0;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            ReaderCommandCount++;
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ReaderCommandCount++;
            return ValueTask.FromResult(result);
        }
    }
}
