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
    IPageBuilderAssetReferenceIndexService assetReferenceIndexService,
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
            var migrationCompleted = settingsManager.GetValue<bool>(Settings.Migration.AssetReferenceIndexMigrated);
            if (!migrationCompleted)
            {
                BackgroundJob.Enqueue(() => RebuildAssetReferenceIndex());
            }
        }
    }

    [DisableConcurrentExecution(_concurrentExecutionTimeoutInSeconds)]
    public async Task RebuildAssetReferenceIndex()
    {
        var migrationCompleted = settingsManager.GetValue<bool>(Settings.Migration.AssetReferenceIndexMigrated);
        if (migrationCompleted)
        {
            return;
        }

        var skip = 0;
        var pageIds = await GetPageIds(skip);

        while (pageIds.Count > 0)
        {
            foreach (var pageId in pageIds)
            {
                var content = await groupedPageService.LoadContent(pageId);
                await assetReferenceIndexService.RebuildPageIndexAsync(pageId, content);
            }

            skip += pageIds.Count;
            pageIds = await GetPageIds(skip);
        }

        await settingsManager.SetValueAsync(Settings.Migration.AssetReferenceIndexMigrated.Name, true);
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
}
