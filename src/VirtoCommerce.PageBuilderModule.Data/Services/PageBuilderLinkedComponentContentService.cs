using Microsoft.EntityFrameworkCore;
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
    IEventPublisher eventPublisher)
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
        return await repository.PageBuilderLinkedComponentContents
            .Where(x => x.Id == linkedComponentId)
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
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var batch in ids.Chunk(QueryBatchSize))
        {
            var contents = await repository.PageBuilderLinkedComponentContents
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

        using (var repository = repositoryFactory())
        {
            var componentExists = await repository.ExecuteUnderLinkedComponentWriteLockAsync(
                linkedComponentId,
                async transactionCancellationToken =>
                {
                    var component = await repository.PageBuilderLinkedComponents
                        .FirstAsync(x => x.Id == linkedComponentId, transactionCancellationToken);
                    var contentEntity = await repository.PageBuilderLinkedComponentContents
                        .FirstOrDefaultAsync(x => x.Id == linkedComponentId, transactionCancellationToken);

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
                },
                cancellationToken);

            if (!componentExists)
            {
                throw new KeyNotFoundException($"Linked Component '{linkedComponentId}' was not found.");
            }
        }

        GenericCachingRegion<PageBuilderLinkedComponent>.ExpireTokenForKey(linkedComponentId);
        GenericSearchCachingRegion<PageBuilderLinkedComponent>.ExpireRegion();

        await eventPublisher.Publish(
            new PageBuilderLinkedComponentContentChangedEvent([linkedComponentId]),
            cancellationToken);
    }

    internal static void TouchMetadata(PageBuilderLinkedComponentEntity component)
    {
        component.ModifiedDate = DateTime.UtcNow;
    }
}
