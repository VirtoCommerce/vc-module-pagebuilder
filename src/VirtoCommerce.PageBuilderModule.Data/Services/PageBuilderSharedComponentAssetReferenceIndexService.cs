using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public interface IPageBuilderSharedComponentAssetReferenceIndexService
{
    Task RebuildIndexInCurrentUnitOfWorkAsync(
        IPageBuilderModuleRepository repository,
        string sharedComponentId,
        string content,
        CancellationToken cancellationToken = default);
}

public sealed class PageBuilderSharedComponentAssetReferenceIndexService
    : IPageBuilderSharedComponentAssetReferenceIndexService
{
    public Task RebuildIndexInCurrentUnitOfWorkAsync(
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

        var existingReferences = await repository.PageBuilderSharedComponentAssetReferences
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
