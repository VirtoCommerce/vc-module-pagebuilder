using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderLinkedComponentAssetReferenceIndexService(
    Func<IPageBuilderModuleRepository> repositoryFactory)
    : IPageBuilderLinkedComponentAssetReferenceIndexService
{
    public async Task RebuildIndexAsync(
        string linkedComponentId,
        string content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(linkedComponentId))
        {
            return;
        }

        using var repository = repositoryFactory();
        if (repository is not IPageBuilderWriteLockRepository writeLockRepository ||
            repository is not IPageBuilderLinkedComponentRepository linkedRepository)
        {
            throw new NotSupportedException("Shared Component writes require repository write-lock support.");
        }

        await writeLockRepository.ExecuteUnderLinkedComponentWriteLockAsync(
            linkedComponentId,
            async transactionCancellationToken =>
            {
                // Migration/backfill can pass content read before a concurrent live edit. Once the row lock
                // is held, prefer the current persisted version so an older snapshot cannot replace the index.
                var currentContent = await linkedRepository.PageBuilderLinkedComponentContents
                    .Where(x => x.Id == linkedComponentId)
                    .Select(x => x.ComponentContent)
                    .FirstOrDefaultAsync(transactionCancellationToken);
                await RebuildIndexInCurrentUnitOfWorkAsync(
                    repository,
                    linkedComponentId,
                    currentContent ?? content,
                    transactionCancellationToken);
                await repository.UnitOfWork.CommitAsync();
            },
            cancellationToken);
    }

    internal static Task RebuildIndexInCurrentUnitOfWorkAsync(
        IPageBuilderModuleRepository repository,
        string linkedComponentId,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);

        return RebuildIndexInCurrentUnitOfWorkCoreAsync(
            repository,
            linkedComponentId,
            content,
            cancellationToken);
    }

    private static async Task RebuildIndexInCurrentUnitOfWorkCoreAsync(
        IPageBuilderModuleRepository repository,
        string linkedComponentId,
        string content,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(linkedComponentId))
        {
            return;
        }

        var linkedRepository = repository.RequireLinkedComponents();
        var existingReferences = await linkedRepository.PageBuilderLinkedComponentAssetReferences
            .Where(x => x.LinkedComponentId == linkedComponentId)
            .ToListAsync(cancellationToken);

        foreach (var reference in existingReferences)
        {
            repository.Remove(reference);
        }

        foreach (var normalizedAssetUrl in PageBuilderAssetReferenceMatcher.ExtractReferences(content))
        {
            repository.Add(new PageBuilderLinkedComponentAssetReferenceEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                LinkedComponentId = linkedComponentId,
                NormalizedAssetUrl = normalizedAssetUrl,
                NormalizedAssetUrlHash = PageBuilderAssetReferenceMatcher.GetAssetUrlHash(normalizedAssetUrl),
            });
        }
    }
}
