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

public class PageBuilderLinkedComponentContentService(
    Func<IPageBuilderModuleRepository> repositoryFactory,
    IEventPublisher eventPublisher,
    ILogger<PageBuilderLinkedComponentContentService> logger = null)
    : IPageBuilderLinkedComponentContentService
{
    private const int QueryBatchSize = 500;

    public async Task<string> LoadContentAsync(
        string linkedComponentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(linkedComponentId))
        {
            return null;
        }

        using var repository = repositoryFactory();
        var linkedRepository = repository.RequireLinkedComponents();
        return await linkedRepository.PageBuilderLinkedComponentContents
            .Where(x => x.Id == linkedComponentId)
            .Select(x => x.ComponentContent)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string> TryLoadContentAsync(
        PageBuilderLinkedComponent expectedComponent,
        CancellationToken cancellationToken = default)
    {
        ValidateExpectedComponent(expectedComponent);

        var normalizedStoreId = expectedComponent.StoreId.ToUpperInvariant();
        using var repository = repositoryFactory();
        var linkedRepository = repository.RequireLinkedComponents();
        return await linkedRepository.PageBuilderLinkedComponentContents
            .Where(x =>
                x.Id == expectedComponent.Id &&
                x.Component.StoreId.ToUpper() == normalizedStoreId &&
                x.Component.CreatedDate == expectedComponent.CreatedDate)
            .Select(x => x.ComponentContent)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> LoadContentsAsync(
        IEnumerable<string> linkedComponentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = linkedComponentIds
            ?.Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        if (ids.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        using var repository = repositoryFactory();
        var linkedRepository = repository.RequireLinkedComponents();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var batch in ids.Chunk(QueryBatchSize))
        {
            var contents = await linkedRepository.PageBuilderLinkedComponentContents
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
        string linkedComponentId,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(linkedComponentId);
        PageBuilderLinkedComponentReferenceMatcher.ValidateComponentContent(content);

        if (!await SaveContentInternalAsync(
                linkedComponentId,
                expectedComponent: null,
                content,
                cancellationToken))
        {
            throw new KeyNotFoundException($"Linked Component '{linkedComponentId}' was not found.");
        }
    }

    public Task<bool> TrySaveContentAsync(
        PageBuilderLinkedComponent expectedComponent,
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
        string linkedComponentId,
        PageBuilderLinkedComponent expectedComponent,
        string content,
        CancellationToken cancellationToken)
    {
        using (var repository = repositoryFactory())
        {
            if (repository is not IPageBuilderWriteLockRepository writeLockRepository ||
                repository is not IPageBuilderLinkedComponentRepository linkedRepository)
            {
                throw new NotSupportedException("Shared Component writes require repository write-lock support.");
            }

            var contentSaved = false;
            var componentExists = await writeLockRepository.ExecuteUnderLinkedComponentWriteLockAsync(
                linkedComponentId,
                async transactionCancellationToken =>
                {
                    var component = await linkedRepository.PageBuilderLinkedComponents
                        .FirstAsync(x => x.Id == linkedComponentId, transactionCancellationToken);
                    var contentEntity = await linkedRepository.PageBuilderLinkedComponentContents
                        .FirstOrDefaultAsync(x => x.Id == linkedComponentId, transactionCancellationToken);

                    if (expectedComponent != null &&
                        (!HasExpectedIdentity(component, expectedComponent) || contentEntity == null))
                    {
                        return;
                    }

                    // Validate only after the locked identity check so a stale authorized snapshot fails closed.
                    if (expectedComponent != null)
                    {
                        PageBuilderLinkedComponentReferenceMatcher.ValidateComponentContent(content);
                    }

                    if (contentEntity == null)
                    {
                        repository.Add(new PageBuilderLinkedComponentContentEntity
                        {
                            Id = linkedComponentId,
                            ComponentContent = content,
                        });
                    }
                    else
                    {
                        contentEntity.ComponentContent = content;
                        repository.Update(contentEntity);
                    }

                    TouchMetadata(component);
                    await PageBuilderLinkedComponentAssetReferenceIndexService.RebuildIndexInCurrentUnitOfWorkAsync(
                        repository,
                        linkedComponentId,
                        content,
                        transactionCancellationToken);
                    await repository.UnitOfWork.CommitAsync();
                    contentSaved = true;
                },
                cancellationToken);

            if (!componentExists || !contentSaved)
            {
                return false;
            }
        }

        GenericCachingRegion<PageBuilderLinkedComponent>.ExpireTokenForKey(linkedComponentId);
        GenericSearchCachingRegion<PageBuilderLinkedComponent>.ExpireRegion();

        GenericSearchCachingRegion<PageBuilderPage>.ExpireRegion();

        try
        {
            await eventPublisher.Publish(
                new PageBuilderLinkedComponentContentChangedEvent([linkedComponentId]),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger?.LogError(
                ex,
                "Failed to schedule post-commit propagation for Shared Component {LinkedComponentId}",
                linkedComponentId);
        }

        return true;
    }

    private static void ValidateExpectedComponent(PageBuilderLinkedComponent expectedComponent)
    {
        ArgumentNullException.ThrowIfNull(expectedComponent);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedComponent.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedComponent.StoreId);
    }

    private static bool HasExpectedIdentity(
        PageBuilderLinkedComponentEntity component,
        PageBuilderLinkedComponent expectedComponent)
    {
        return string.Equals(component.StoreId, expectedComponent.StoreId, StringComparison.OrdinalIgnoreCase) &&
               component.CreatedDate == expectedComponent.CreatedDate;
    }

    internal static void TouchMetadata(PageBuilderLinkedComponentEntity component)
    {
        component.ModifiedDate = DateTime.UtcNow;
    }
}
