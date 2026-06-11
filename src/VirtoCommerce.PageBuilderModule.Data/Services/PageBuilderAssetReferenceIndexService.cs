using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderAssetReferenceIndexService(
    Func<IPageBuilderModuleRepository> repositoryFactory,
    ILogger<PageBuilderAssetReferenceIndexService> logger)
    : IPageBuilderAssetReferenceIndexService
{
    public async Task RebuildPageIndexAsync(string pageId, string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pageId))
        {
            return;
        }

        using var repository = repositoryFactory();
        var pageMetadata = await GetPageMetadataAsync(repository, pageId, cancellationToken);

        await DeletePageIndexInternalAsync(repository, [pageId], cancellationToken);

        if (pageMetadata == null)
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
                PageId = pageMetadata.PageId,
                GroupId = pageMetadata.GroupId,
                StoreId = pageMetadata.StoreId,
                CultureName = pageMetadata.CultureName,
                Status = pageMetadata.Status,
                NormalizedAssetUrl = reference,
                NormalizedAssetUrlHash = PageBuilderAssetReferenceMatcher.GetAssetUrlHash(reference),
            });
        }

        await repository.UnitOfWork.CommitAsync();
    }

    public async Task RefreshPageMetadataAsync(IEnumerable<string> pageIds, CancellationToken cancellationToken = default)
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
        var metadata = await GetPageMetadataAsync(repository, ids, cancellationToken);
        var metadataByPageId = metadata.ToDictionary(x => x.PageId, StringComparer.OrdinalIgnoreCase);
        var references = await repository.PageBuilderAssetReferences
            .Where(x => ids.Contains(x.PageId))
            .ToListAsync(cancellationToken);

        foreach (var reference in references)
        {
            if (!metadataByPageId.TryGetValue(reference.PageId, out var pageMetadata))
            {
                repository.Remove(reference);
                continue;
            }

            reference.GroupId = pageMetadata.GroupId;
            reference.StoreId = pageMetadata.StoreId;
            reference.CultureName = pageMetadata.CultureName;
            reference.Status = pageMetadata.Status;

            repository.Update(reference);
        }

        await repository.UnitOfWork.CommitAsync();
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
        var references = await repository.PageBuilderAssetReferences
            .Where(x => ids.Contains(x.GroupId))
            .ToListAsync(cancellationToken);

        foreach (var reference in references)
        {
            repository.Remove(reference);
        }

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

    private async Task<PageReferenceMetadata> GetPageMetadataAsync(IPageBuilderModuleRepository repository, string pageId, CancellationToken cancellationToken)
    {
        return (await GetPageMetadataAsync(repository, [pageId], cancellationToken)).FirstOrDefault();
    }

    private async Task<List<PageReferenceMetadata>> GetPageMetadataAsync(IPageBuilderModuleRepository repository, string[] pageIds, CancellationToken cancellationToken)
    {
        var rawMetadata = await repository.PageBuilderPages
            .Where(page => pageIds.Contains(page.Id))
            .Join(
                repository.GroupedPageBuilderPages,
                page => page.GroupId,
                group => group.Id,
                (page, group) => new RawPageReferenceMetadata
                {
                    PageId = page.Id,
                    GroupId = page.GroupId,
                    StoreId = page.StoreId ?? group.StoreId,
                    CultureName = group.CultureName,
                    Status = page.Status,
                })
            .ToListAsync(cancellationToken);

        var result = new List<PageReferenceMetadata>();

        foreach (var metadata in rawMetadata)
        {
            if (!TryGetPageStatus(metadata.Status, out var status))
            {
                logger.LogWarning(
                    "Skipping page asset reference index metadata for page '{PageId}' with unsupported status '{Status}'.",
                    metadata.PageId,
                    metadata.Status);
                continue;
            }

            result.Add(new PageReferenceMetadata
            {
                PageId = metadata.PageId,
                GroupId = metadata.GroupId,
                StoreId = metadata.StoreId,
                CultureName = metadata.CultureName,
                Status = status,
            });
        }

        return result;
    }

    private static bool TryGetPageStatus(string status, out PageBuilderPageStatus result)
    {
        return Enum.TryParse(status, ignoreCase: true, out result);
    }

    private sealed class PageReferenceMetadata
    {
        public string PageId { get; init; }
        public string GroupId { get; init; }
        public string StoreId { get; init; }
        public string CultureName { get; init; }
        public PageBuilderPageStatus Status { get; init; }
    }

    private sealed class RawPageReferenceMetadata
    {
        public string PageId { get; init; }
        public string GroupId { get; init; }
        public string StoreId { get; init; }
        public string CultureName { get; init; }
        public string Status { get; init; }
    }
}
