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
    IPageBuilderLinkedComponentAssetReferenceIndexService linkedComponentAssetReferenceIndexService,
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
            linkedComponentAssetReferenceIndexService: null,
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
            var componentMigrationCompleted = settingsManager.GetValue<bool>(Settings.Migration.LinkedComponentAssetReferenceIndexMigrated);
            if (!pageMigrationCompleted ||
                linkedComponentAssetReferenceIndexService != null && !componentMigrationCompleted)
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

        var componentMigrationCompleted = settingsManager.GetValue<bool>(Settings.Migration.LinkedComponentAssetReferenceIndexMigrated);
        if (!componentMigrationCompleted && linkedComponentAssetReferenceIndexService != null)
        {
            await RebuildLinkedComponentAssetReferenceIndex();
            await settingsManager.SetValueAsync(
                Settings.Migration.LinkedComponentAssetReferenceIndexMigrated.Name,
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

    internal async Task RebuildLinkedComponentAssetReferenceIndex()
    {
        string cursor = null;
        var components = await GetLinkedComponentContents(cursor);

        while (components.Count > 0)
        {
            foreach (var component in components)
            {
                await RebuildLinkedComponentIndexAsync(
                    component.Id,
                    component.Content,
                    linkedComponentAssetReferenceIndexService);
            }

            cursor = components[^1].Id;
            components = await GetLinkedComponentContents(cursor);
        }
    }

    internal static Task RebuildLinkedComponentIndexAsync(
        string linkedComponentId,
        string content,
        IPageBuilderLinkedComponentAssetReferenceIndexService assetReferenceIndexService,
        CancellationToken cancellationToken = default)
    {
        return assetReferenceIndexService.RebuildIndexAsync(
            linkedComponentId,
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

    private async Task<IList<LinkedComponentContent>> GetLinkedComponentContents(string cursor)
    {
        using var repository = repositoryFactory();
        if (repository is not IPageBuilderLinkedComponentRepository linkedRepository)
        {
            return [];
        }

        var query = linkedRepository.PageBuilderLinkedComponentContents;
        if (!string.IsNullOrEmpty(cursor))
        {
            query = ApplyLinkedComponentCursor(query, cursor);
        }

        return await query
            .OrderBy(x => x.Id)
            .Take(_batchSize)
            .Select(x => new LinkedComponentContent
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

    internal static IQueryable<PageBuilderLinkedComponentContentEntity> ApplyLinkedComponentCursor(
        IQueryable<PageBuilderLinkedComponentContentEntity> query,
        string id)
    {
        return query.Where(x => string.Compare(x.Id, id) > 0);
    }

    private sealed class LinkedComponentContent
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
