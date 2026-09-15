using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public sealed class PageBuilderPageChangeService(
    Func<IPageBuilderModuleRepository> repositoryFactory)
    : IPageBuilderPageChangeService
{
    private const int QueryBatchSize = 500;

    public async Task<IReadOnlyDictionary<string, DateTime>> GetEffectiveChangeDatesAsync(
        IEnumerable<PageBuilderPage> pages,
        CancellationToken cancellationToken = default)
    {
        using var repository = repositoryFactory();
        var result = pages
            .Where(page => !string.IsNullOrWhiteSpace(page.Id))
            .ToDictionary(
                page => page.Id,
                page => page.ModifiedDate ?? page.CreatedDate,
                StringComparer.OrdinalIgnoreCase);

        foreach (var batch in result.Keys.Chunk(QueryBatchSize))
        {
            var componentDates = await repository.PageBuilderSharedComponentReferences
                .Where(reference => batch.Contains(reference.PageId))
                .Join(
                    repository.PageBuilderSharedComponents,
                    reference => reference.SharedComponentId,
                    component => component.Id,
                    (reference, component) => new
                    {
                        reference.PageId,
                        ChangeDate = component.ModifiedDate ?? component.CreatedDate,
                    })
                .GroupBy(x => x.PageId)
                .Select(group => new
                {
                    PageId = group.Key,
                    ChangeDate = group.Max(x => x.ChangeDate),
                })
                .ToListAsync(cancellationToken);

            foreach (var componentDate in componentDates)
            {
                if (result.TryGetValue(componentDate.PageId, out var pageDate) &&
                    componentDate.ChangeDate > pageDate)
                {
                    result[componentDate.PageId] = componentDate.ChangeDate;
                }
            }
        }

        return result;
    }
}
