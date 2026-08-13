using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderAssetReferenceIndexService(
    Func<IPageBuilderModuleRepository> repositoryFactory)
    : IPageBuilderAssetReferenceIndexService
{
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
