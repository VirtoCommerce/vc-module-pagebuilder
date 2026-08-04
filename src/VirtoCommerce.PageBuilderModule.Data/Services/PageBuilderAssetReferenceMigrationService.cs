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
    IPageBuilderSharedComponentAssetReferenceIndexService sharedComponentAssetReferenceIndexService,
    ISettingsManager settingsManager,
    IGroupedPageService groupedPageService = null,
    IPageBuilderAssetReferenceIndexService assetReferenceIndexService = null)
    : IPageBuilderAssetReferenceMigrationService
{
    public PageBuilderAssetReferenceMigrationService(
        Func<IPageBuilderModuleRepository> repositoryFactory,
        IGroupedPageService groupedPageService,
        IPageBuilderAssetReferenceIndexService assetReferenceIndexService,
        ISettingsManager settingsManager)
        : this(
            repositoryFactory,
            sharedComponentAssetReferenceIndexService: null,
            settingsManager,
            groupedPageService,
            assetReferenceIndexService)
    {
    }

    private const int _batchSize = 50;
    private const int _concurrentExecutionTimeoutInSeconds = 24 * 60 * 60;
    private static readonly object LockObject = new();

    public void StartMigration()
    {
        lock (LockObject)
        {
            var pageMigrationCompleted = settingsManager.GetValue<bool>(Settings.Migration.AssetReferenceIndexMigrated);
            var componentMigrationCompleted = settingsManager.GetValue<bool>(Settings.Migration.SharedComponentAssetReferenceIndexMigrated);
            if (!pageMigrationCompleted ||
                sharedComponentAssetReferenceIndexService != null && !componentMigrationCompleted)
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

        var componentMigrationCompleted = settingsManager.GetValue<bool>(Settings.Migration.SharedComponentAssetReferenceIndexMigrated);
        if (!componentMigrationCompleted && sharedComponentAssetReferenceIndexService != null)
        {
            await RebuildSharedComponentAssetReferenceIndex();
            await settingsManager.SetValueAsync(
                Settings.Migration.SharedComponentAssetReferenceIndexMigrated.Name,
                true);
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
        if (repository is IPageBuilderWriteLockRepository writeLockRepository)
        {
            await writeLockRepository.ExecuteUnderPageWriteLocksAsync(
                [pageId],
                (dbContext, cancellationToken) =>
                    PageBuilderPageIndexing.RebuildCurrentRawPageAssetIndexAsync(
                        dbContext,
                        pageId,
                        cancellationToken));
            return;
        }

        if (groupedPageService == null || assetReferenceIndexService == null)
        {
            throw new NotSupportedException(
                "Page asset migration requires either repository write-lock support or the legacy page index services.");
        }

        var content = await groupedPageService.LoadContent(pageId);
        await assetReferenceIndexService.RebuildPageIndexAsync(pageId, content);
    }

    internal async Task RebuildSharedComponentAssetReferenceIndex()
    {
        string cursor = null;
        var components = await GetSharedComponentContents(cursor);

        while (components.Count > 0)
        {
            foreach (var component in components)
            {
                await RebuildSharedComponentIndexAsync(
                    component.Id,
                    component.Content,
                    sharedComponentAssetReferenceIndexService);
            }

            cursor = components[^1].Id;
            components = await GetSharedComponentContents(cursor);
        }
    }

    internal static Task RebuildSharedComponentIndexAsync(
        string sharedComponentId,
        string content,
        IPageBuilderSharedComponentAssetReferenceIndexService assetReferenceIndexService,
        CancellationToken cancellationToken = default)
    {
        return assetReferenceIndexService.RebuildIndexAsync(
            sharedComponentId,
            content,
            cancellationToken);
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

    private async Task<IList<SharedComponentContent>> GetSharedComponentContents(string cursor)
    {
        using var repository = repositoryFactory();
        if (repository is not IPageBuilderSharedComponentRepository sharedComponentRepository)
        {
            return [];
        }

        var query = sharedComponentRepository.PageBuilderSharedComponentContents;
        if (!string.IsNullOrEmpty(cursor))
        {
            query = ApplySharedComponentCursor(query, cursor);
        }

        return await query
            .OrderBy(x => x.Id)
            .Take(_batchSize)
            .Select(x => new SharedComponentContent
            {
                Id = x.Id,
                Content = x.ComponentContent,
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

    internal static IQueryable<PageBuilderSharedComponentContentEntity> ApplySharedComponentCursor(
        IQueryable<PageBuilderSharedComponentContentEntity> query,
        string id)
    {
        return query.Where(x => string.Compare(x.Id, id) > 0);
    }

    private sealed class SharedComponentContent
    {
        public string Id { get; init; }

        public string Content { get; init; }
    }

    private sealed class PageCursor
    {
        public string Id { get; init; }

        public DateTime CreatedDate { get; init; }
    }
}
