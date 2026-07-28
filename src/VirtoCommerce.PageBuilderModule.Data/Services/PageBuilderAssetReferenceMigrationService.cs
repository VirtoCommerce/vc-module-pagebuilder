using Hangfire;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Settings;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderAssetReferenceMigrationService(
    Func<IPageBuilderModuleRepository> repositoryFactory,
    IGroupedPageService groupedPageService,
    IPageBuilderLinkedComponentResolver linkedComponentResolver,
    IPageBuilderAssetReferenceIndexService assetReferenceIndexService,
    IPageBuilderLinkedComponentAssetReferenceIndexService linkedComponentAssetReferenceIndexService,
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
            var componentMigrationCompleted = settingsManager.GetValue<bool>(Settings.Migration.LinkedComponentAssetReferenceIndexMigrated);
            if (!pageMigrationCompleted || !componentMigrationCompleted)
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
        if (!componentMigrationCompleted)
        {
            await RebuildLinkedComponentAssetReferenceIndex();
            await settingsManager.SetValueAsync(
                Settings.Migration.LinkedComponentAssetReferenceIndexMigrated.Name,
                true);
        }
    }

    private async Task RebuildPageAssetReferenceIndex()
    {
        var skip = 0;
        var pageIds = await GetPageIds(skip);

        while (pageIds.Count > 0)
        {
            foreach (var pageId in pageIds)
            {
                var rawContent = await groupedPageService.LoadContent(pageId);
                await RebuildResolvedPageIndexAsync(
                    pageId,
                    rawContent,
                    linkedComponentResolver,
                    assetReferenceIndexService);
            }

            skip += pageIds.Count;
            pageIds = await GetPageIds(skip);
        }
    }

    private async Task RebuildLinkedComponentAssetReferenceIndex()
    {
        var skip = 0;
        var components = await GetLinkedComponentContents(skip);

        while (components.Count > 0)
        {
            foreach (var component in components)
            {
                await RebuildLinkedComponentIndexAsync(
                    component.Id,
                    component.Content,
                    linkedComponentAssetReferenceIndexService);
            }

            skip += components.Count;
            components = await GetLinkedComponentContents(skip);
        }
    }

    internal static async Task RebuildResolvedPageIndexAsync(
        string pageId,
        string rawContent,
        IPageBuilderLinkedComponentResolver linkedComponentResolver,
        IPageBuilderAssetReferenceIndexService assetReferenceIndexService,
        CancellationToken cancellationToken = default)
    {
        var resolvedContent = await linkedComponentResolver.ResolveAsync(rawContent, cancellationToken);
        await assetReferenceIndexService.RebuildPageIndexAsync(pageId, resolvedContent, cancellationToken);
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

    private async Task<IList<string>> GetPageIds(int skip)
    {
        using var repository = repositoryFactory();

        return await repository.PageBuilderPages
            .OrderBy(x => x.CreatedDate)
            .ThenBy(x => x.Id)
            .Skip(skip)
            .Take(_batchSize)
            .Select(x => x.Id)
            .ToListAsync();
    }

    private async Task<IList<LinkedComponentContent>> GetLinkedComponentContents(int skip)
    {
        using var repository = repositoryFactory();

        return await repository.PageBuilderLinkedComponentContents
            .OrderBy(x => x.Id)
            .Skip(skip)
            .Take(_batchSize)
            .Select(x => new LinkedComponentContent
            {
                Id = x.Id,
                Content = x.ComponentContent,
            })
            .ToListAsync();
    }

    private sealed class LinkedComponentContent
    {
        public string Id { get; init; }

        public string Content { get; init; }
    }
}
