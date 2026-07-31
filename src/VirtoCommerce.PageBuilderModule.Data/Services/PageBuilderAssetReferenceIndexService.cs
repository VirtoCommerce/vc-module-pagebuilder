using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderAssetReferenceIndexService(
    Func<IPageBuilderModuleRepository> repositoryFactory)
    : IPageBuilderAssetReferenceIndexService
{
    public async Task RebuildPageIndexAsync(string pageId, string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pageId))
        {
            return;
        }

        using var repository = repositoryFactory();
        var pageExists = await repository.PageBuilderPages.AnyAsync(x => x.Id == pageId, cancellationToken);

        await DeletePageIndexInternalAsync(repository, [pageId], cancellationToken);

        if (!pageExists)
        {
            await repository.UnitOfWork.CommitAsync();
            return;
        }

        var normalizedReferences = PageBuilderAssetReferenceMatcher.ExtractReferences(content);

        foreach (var reference in normalizedReferences)
        {
            repository.Add(new PageBuilderAssetReferenceEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                PageId = pageId,
                NormalizedAssetUrl = reference,
                NormalizedAssetUrlHash = PageBuilderAssetReferenceMatcher.GetAssetUrlHash(reference),
            });
        }

        await repository.UnitOfWork.CommitAsync();
    }

    internal static async Task ReplacePageIndexInCurrentUnitOfWorkAsync(
        PageBuilderModuleDbContext dbContext,
        string pageId,
        string content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pageId))
        {
            return;
        }

        var pageExists = await dbContext.Set<PageBuilderPageEntity>()
            .AnyAsync(x => x.Id == pageId, cancellationToken);
        var existingReferences = await dbContext.Set<PageBuilderAssetReferenceEntity>()
            .Where(x => x.PageId == pageId)
            .ToListAsync(cancellationToken);

        dbContext.RemoveRange(existingReferences);

        if (pageExists)
        {
            await dbContext.AddRangeAsync(
                PageBuilderAssetReferenceMatcher.ExtractReferences(content)
                    .Select(reference => new PageBuilderAssetReferenceEntity
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        PageId = pageId,
                        NormalizedAssetUrl = reference,
                        NormalizedAssetUrlHash = PageBuilderAssetReferenceMatcher.GetAssetUrlHash(reference),
                    }),
                cancellationToken);
        }
    }

    public async Task DeletePageIndexAsync(IEnumerable<string> pageIds, CancellationToken cancellationToken = default)
    {
        var ids = pageIds
            ?.Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (ids.IsNullOrEmpty())
        {
            return;
        }

        using var repository = repositoryFactory();
        await DeletePageIndexInternalAsync(repository, ids, cancellationToken);
        await repository.UnitOfWork.CommitAsync();
    }

    public async Task DeleteGroupIndexAsync(IEnumerable<string> groupIds, CancellationToken cancellationToken = default)
    {
        var ids = groupIds
            ?.Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (ids.IsNullOrEmpty())
        {
            return;
        }

        using var repository = repositoryFactory();
        var pageIds = await repository.PageBuilderPages
            .Where(x => ids.Contains(x.GroupId))
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);

        await DeletePageIndexInternalAsync(repository, pageIds, cancellationToken);
        await repository.UnitOfWork.CommitAsync();
    }

    private static async Task DeletePageIndexInternalAsync(IPageBuilderModuleRepository repository, string[] pageIds, CancellationToken cancellationToken)
    {
        var references = await repository.PageBuilderAssetReferences
            .Where(x => pageIds.Contains(x.PageId))
            .ToListAsync(cancellationToken);

        foreach (var reference in references)
        {
            repository.Remove(reference);
        }
    }
}
