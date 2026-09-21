using Hangfire;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Settings;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderAssetReferenceMigrationService(
    Func<IPageBuilderModuleRepository> repositoryFactory,
    ISettingsManager settingsManager)
    : IPageBuilderAssetReferenceMigrationService
{
    private const int _batchSize = 50;
    private const int _concurrentExecutionTimeoutInSeconds = 24 * 60 * 60;
    private static readonly object LockObject = new();

    public void StartMigration()
    {
        lock (LockObject)
        {
            var pageMigrationCompleted = settingsManager.GetValue<bool>(Settings.Migration.AssetReferenceIndexMigrated);
            if (!pageMigrationCompleted)
            {
                BackgroundJob.Enqueue(() => RebuildAssetReferenceIndex());
            }
        }
    }

    [DisableConcurrentExecution(_concurrentExecutionTimeoutInSeconds)]
    public async Task RebuildAssetReferenceIndex()
    {
        var pageMigrationCompleted = settingsManager.GetValue<bool>(Settings.Migration.AssetReferenceIndexMigrated);
        if (!pageMigrationCompleted)
        {
            await RebuildPageAssetReferenceIndex();
            await settingsManager.SetValueAsync(Settings.Migration.AssetReferenceIndexMigrated.Name, true);
        }
    }

    internal async Task RebuildPageAssetReferenceIndex()
    {
        PageCursor cursor = null;
        var pages = await GetPages(cursor);

        while (pages.Count > 0)
        {
            foreach (var pageId in pages.Select(page => page.Id))
            {
                await RebuildPageAssetReferenceIndexAsync(pageId);
            }

            cursor = pages[^1];
            pages = await GetPages(cursor);
        }
    }

    private async Task RebuildPageAssetReferenceIndexAsync(string pageId)
    {
        using var repository = repositoryFactory();
        await repository.RebuildPageAssetReferenceIndexAsync(pageId);
    }

    private async Task<IList<PageCursor>> GetPages(PageCursor cursor)
    {
        using var repository = repositoryFactory();

        var query = repository.PageBuilderPages;
        if (cursor != null)
        {
            query = ApplyPageCursor(query, cursor.CreatedDate, cursor.Id);
        }

        return await query
            .OrderBy(x => x.CreatedDate)
            .ThenBy(x => x.Id)
            .Take(_batchSize)
            .Select(x => new PageCursor
            {
                Id = x.Id,
                CreatedDate = x.CreatedDate,
            })
            .ToListAsync();
    }

    internal static IQueryable<PageBuilderPageEntity> ApplyPageCursor(
        IQueryable<PageBuilderPageEntity> query,
        DateTime createdDate,
        string id)
    {
        return query.Where(x =>
            x.CreatedDate > createdDate ||
            x.CreatedDate == createdDate && string.Compare(x.Id, id) > 0);
    }

    private sealed class PageCursor
    {
        public string Id { get; init; }

        public DateTime CreatedDate { get; init; }
    }
}
