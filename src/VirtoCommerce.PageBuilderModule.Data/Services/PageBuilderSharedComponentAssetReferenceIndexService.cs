using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderSharedComponentAssetReferenceIndexService(
    Func<IPageBuilderModuleRepository> repositoryFactory)
    : IPageBuilderSharedComponentAssetReferenceIndexService
{
    public async Task RebuildIndexAsync(
        string sharedComponentId,
        string content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sharedComponentId))
        {
            return;
        }

        using var repository = repositoryFactory();
        if (repository is not IPageBuilderWriteLockRepository writeLockRepository ||
            repository is not IPageBuilderSharedComponentRepository sharedComponentRepository)
        {
            throw new NotSupportedException("Shared Component writes require repository write-lock support.");
        }

        await writeLockRepository.ExecuteUnderSharedComponentWriteLockAsync(
            sharedComponentId,
            async transactionCancellationToken =>
            {
                // Migration/backfill can pass content read before a concurrent live edit. Once the row lock
                // is held, prefer the current persisted version so an older snapshot cannot replace the index.
                var currentContent = await sharedComponentRepository.PageBuilderSharedComponentContents
                    .Where(x => x.Id == sharedComponentId)
                    .Select(x => x.ComponentContent)
                    .FirstOrDefaultAsync(transactionCancellationToken);
                await RebuildIndexInCurrentUnitOfWorkAsync(
                    repository,
                    sharedComponentId,
                    currentContent ?? content,
                    transactionCancellationToken);
                await repository.UnitOfWork.CommitAsync();
            },
            cancellationToken);
    }

    internal static Task RebuildIndexInCurrentUnitOfWorkAsync(
        IPageBuilderModuleRepository repository,
        string sharedComponentId,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);

        return RebuildIndexInCurrentUnitOfWorkCoreAsync(
            repository,
            sharedComponentId,
            content,
            cancellationToken);
    }

    private static async Task RebuildIndexInCurrentUnitOfWorkCoreAsync(
        IPageBuilderModuleRepository repository,
        string sharedComponentId,
        string content,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sharedComponentId))
        {
            return;
        }

        var sharedComponentRepository = repository.RequireSharedComponents();
        var existingReferences = await sharedComponentRepository.PageBuilderSharedComponentAssetReferences
            .Where(x => x.SharedComponentId == sharedComponentId)
            .ToListAsync(cancellationToken);

        foreach (var reference in existingReferences)
        {
            repository.Remove(reference);
        }

        foreach (var normalizedAssetUrl in PageBuilderAssetReferenceMatcher.ExtractReferences(content))
        {
            repository.Add(new PageBuilderSharedComponentAssetReferenceEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                SharedComponentId = sharedComponentId,
                NormalizedAssetUrl = normalizedAssetUrl,
                NormalizedAssetUrlHash = PageBuilderAssetReferenceMatcher.GetAssetUrlHash(normalizedAssetUrl),
            });
        }
    }
}
