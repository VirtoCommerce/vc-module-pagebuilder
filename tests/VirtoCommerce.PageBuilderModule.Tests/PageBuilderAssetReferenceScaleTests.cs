using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.PageBuilderModule.Data.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderAssetReferenceScaleTests
{
    [Fact]
    public async Task References_500Assets_PageBatchAndDeepFolderUseBoundedSqlQueries()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var commands = new CommandCounter();
        var options = new DbContextOptionsBuilder<PageBuilderModuleDbContext>()
            .UseSqlite(connection).AddInterceptors(commands).Options;
        await using (var context = new PageBuilderModuleDbContext(options))
        {
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            context.Add(new GroupedPageBuilderPageEntity { Id = "group", StoreId = "store", Name = "Page" });
            context.Add(new PageBuilderPageEntity { Id = "page", GroupId = "group", StoreId = "store", Status = "Draft" });
            context.AddRange(Enumerable.Range(0, 500).Select(index =>
            {
                var url = $"/stores/store/deep/level1/level2/level3/level4/asset-{index:D3}.png";
                return new PageBuilderAssetReferenceEntity
                {
                    Id = $"reference-{index}", PageId = "page", NormalizedAssetUrl = url,
                    NormalizedAssetUrlHash = PageBuilderAssetReferenceMatcher.GetAssetUrlHash(url),
                };
            }));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var service = new PageBuilderAssetReferenceService(() => new PageBuilderModuleRepository(new PageBuilderModuleDbContext(options)));
        foreach (var pageSize in new[] { 20, 50, 100 })
        {
            commands.Reads = 0;
            var result = await service.SearchReferencesAsync(new PageBuilderAssetReferencesSearchCriteria
            {
                StoreId = "store", IncludePages = false,
                AssetUrls = Enumerable.Range(0, pageSize)
                    .Select(index => $"/stores/store/deep/level1/level2/level3/level4/asset-{index:D3}.png").ToArray(),
            }, TestContext.Current.CancellationToken);
            Assert.Equal(pageSize, result.Results.Count);
            Assert.All(result.Results, item => Assert.Equal(1, item.ReferencesCount));
            Assert.Equal(2, commands.Reads);
        }

        commands.Reads = 0;
        var folder = await service.SearchReferencesAsync(new PageBuilderAssetReferencesSearchCriteria
        {
            StoreId = "store", FolderUrl = "/stores/store/deep", IncludePages = true,
        }, TestContext.Current.CancellationToken);
        var reference = Assert.Single(folder.Results);
        Assert.Equal(1, reference.ReferencesCount);
        Assert.Single(reference.Pages);
        Assert.Equal(4, commands.Reads);
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Reads { get; set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Reads++;
            return ValueTask.FromResult(result);
        }
    }
}
