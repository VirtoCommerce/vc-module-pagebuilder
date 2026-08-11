using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderSharedComponentContentService(
    Func<IPageBuilderModuleRepository> repositoryFactory,
    IEventPublisher eventPublisher,
    PageBuilderSharedComponentAssetReferenceIndexService assetReferenceIndexService,
    ILogger<PageBuilderSharedComponentContentService> logger = null)
    : IPageBuilderSharedComponentContentService
{
    private const int QueryBatchSize = 500;

    public async Task<string> LoadContentAsync(
        string sharedComponentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sharedComponentId))
        {
            return null;
        }

        using var repository = repositoryFactory();
        return await repository.PageBuilderSharedComponentContents
            .Where(x => x.Id == sharedComponentId)
            .Select(x => x.ComponentContent)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string> TryLoadContentAsync(
        PageBuilderSharedComponent expectedComponent,
        CancellationToken cancellationToken = default)
    {
        ValidateExpectedComponent(expectedComponent);

        using var repository = repositoryFactory();
        var result = await repository.PageBuilderSharedComponentContents
            .Where(x =>
                x.Id == expectedComponent.Id &&
                x.Component.CreatedDate == expectedComponent.CreatedDate)
            .Select(x => new
            {
                x.Component.StoreId,
                x.ComponentContent,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return result != null &&
               string.Equals(result.StoreId, expectedComponent.StoreId, StringComparison.OrdinalIgnoreCase)
            ? result.ComponentContent
            : null;
    }

    public async Task<IReadOnlyDictionary<string, string>> LoadContentsAsync(
        IEnumerable<string> sharedComponentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = sharedComponentIds
            ?.Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        if (ids.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        using var repository = repositoryFactory();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var batch in ids.Chunk(QueryBatchSize))
        {
            var contents = await repository.PageBuilderSharedComponentContents
                .Where(x => batch.Contains(x.Id))
                .Select(x => new { x.Id, x.ComponentContent })
                .ToListAsync(cancellationToken);

            foreach (var item in contents)
            {
                result[item.Id] = item.ComponentContent;
            }
        }

        return result;
    }

    public async Task SaveContentAsync(
        string sharedComponentId,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedComponentId);
        PageBuilderSharedComponentReferenceMatcher.ValidateComponentContent(content);

        if (!await SaveContentInternalAsync(
                sharedComponentId,
                expectedComponent: null,
                content,
                cancellationToken))
        {
            throw new KeyNotFoundException($"Shared Component '{sharedComponentId}' was not found.");
        }
    }

    public Task<bool> TrySaveContentAsync(
        PageBuilderSharedComponent expectedComponent,
        string content,
        CancellationToken cancellationToken = default)
    {
        ValidateExpectedComponent(expectedComponent);

        return SaveContentInternalAsync(
            expectedComponent.Id,
            expectedComponent,
            content,
            cancellationToken);
    }

    private async Task<bool> SaveContentInternalAsync(
        string sharedComponentId,
        PageBuilderSharedComponent expectedComponent,
        string content,
        CancellationToken cancellationToken)
    {
        if (!await TryPersistContentAsync(
                sharedComponentId,
                expectedComponent,
                content,
                cancellationToken))
        {
            return false;
        }

        ExpireCaches(sharedComponentId);
        await PublishContentChangedEventAsync(sharedComponentId);

        return true;
    }

    private async Task<bool> TryPersistContentAsync(
        string sharedComponentId,
        PageBuilderSharedComponent expectedComponent,
        string content,
        CancellationToken cancellationToken)
    {
        using (var repository = repositoryFactory())
        {
            var contentSaved = false;
            var componentExists = await repository.ExecuteUnderSharedComponentWriteLockAsync(
                sharedComponentId,
                async transactionCancellationToken =>
                    contentSaved = await TryPersistContentInCurrentUnitOfWorkAsync(
                        repository,
                        sharedComponentId,
                        expectedComponent,
                        content,
                        transactionCancellationToken),
                cancellationToken);

            return componentExists && contentSaved;
        }
    }

    private async Task<bool> TryPersistContentInCurrentUnitOfWorkAsync(
        IPageBuilderModuleRepository repository,
        string sharedComponentId,
        PageBuilderSharedComponent expectedComponent,
        string content,
        CancellationToken cancellationToken)
    {
        var component = await repository.PageBuilderSharedComponents
            .FirstAsync(x => x.Id == sharedComponentId, cancellationToken);
        var contentEntity = await repository.PageBuilderSharedComponentContents
            .FirstOrDefaultAsync(x => x.Id == sharedComponentId, cancellationToken);

        if (expectedComponent != null &&
            (!HasExpectedIdentity(component, expectedComponent) || contentEntity == null))
        {
            return false;
        }

        // Validate only after the locked identity check so a stale authorized snapshot fails closed.
        if (expectedComponent != null)
        {
            PageBuilderSharedComponentReferenceMatcher.ValidateComponentContent(content);
        }

        UpsertContent(repository, sharedComponentId, content, contentEntity);
        TouchMetadata(component);
        await assetReferenceIndexService.RebuildIndexInCurrentUnitOfWorkAsync(
            repository,
            sharedComponentId,
            content,
            cancellationToken);
        await repository.UnitOfWork.CommitAsync();

        return true;
    }

    private static void UpsertContent(
        IPageBuilderModuleRepository repository,
        string sharedComponentId,
        string content,
        PageBuilderSharedComponentContentEntity contentEntity)
    {
        if (contentEntity == null)
        {
            repository.Add(new PageBuilderSharedComponentContentEntity
            {
                Id = sharedComponentId,
                ComponentContent = content,
            });
        }
        else
        {
            contentEntity.ComponentContent = content;
            repository.Update(contentEntity);
        }
    }

    private static void ExpireCaches(string sharedComponentId)
    {
        GenericCachingRegion<PageBuilderSharedComponent>.ExpireTokenForKey(sharedComponentId);
        GenericSearchCachingRegion<PageBuilderSharedComponent>.ExpireRegion();

        GenericSearchCachingRegion<PageBuilderPage>.ExpireRegion();
    }

    private async Task PublishContentChangedEventAsync(string sharedComponentId)
    {
        try
        {
            await eventPublisher.Publish(
                new PageBuilderSharedComponentContentChangedEvent([sharedComponentId]),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger?.LogError(
                ex,
                "Failed to schedule post-commit propagation for Shared Component {SharedComponentId}",
                sharedComponentId);
        }
    }

    private static void ValidateExpectedComponent(PageBuilderSharedComponent expectedComponent)
    {
        ArgumentNullException.ThrowIfNull(expectedComponent);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedComponent.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedComponent.StoreId);
    }

    private static bool HasExpectedIdentity(
        PageBuilderSharedComponentEntity component,
        PageBuilderSharedComponent expectedComponent)
    {
        return string.Equals(component.StoreId, expectedComponent.StoreId, StringComparison.OrdinalIgnoreCase) &&
               component.CreatedDate == expectedComponent.CreatedDate;
    }

    internal static void TouchMetadata(PageBuilderSharedComponentEntity component)
    {
        component.ModifiedDate = DateTime.UtcNow;
    }
}
