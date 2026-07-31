using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

internal static class PageBuilderPageChangeTracking
{
    private const int QueryBatchSize = 500;

    internal static IQueryable<PageBuilderPageEntity> ApplyDateRange(
        IQueryable<PageBuilderPageEntity> pages,
        IPageBuilderModuleRepository repository,
        DateTime? modifiedSince,
        DateTime? modifiedBefore)
    {
        if (repository is not IPageBuilderLinkedComponentRepository linkedRepository)
        {
            if (modifiedSince.HasValue)
            {
                var start = modifiedSince.Value;
                pages = pages.Where(page => (page.ModifiedDate ?? page.CreatedDate) >= start);
            }

            if (modifiedBefore.HasValue)
            {
                var end = modifiedBefore.Value;
                pages = pages.Where(page => (page.ModifiedDate ?? page.CreatedDate) <= end);
            }

            return pages;
        }

        if (modifiedSince.HasValue)
        {
            var start = modifiedSince.Value;
            pages = pages.Where(page =>
                (page.ModifiedDate ?? page.CreatedDate) >= start ||
                linkedRepository.PageBuilderLinkedComponentReferences
                    .Where(reference => reference.PageId == page.Id)
                    .Join(
                        linkedRepository.PageBuilderLinkedComponents,
                        reference => reference.LinkedComponentId,
                        component => component.Id,
                        (_, component) => component.ModifiedDate ?? component.CreatedDate)
                    .Any(changeDate => changeDate >= start));
        }

        if (modifiedBefore.HasValue)
        {
            var end = modifiedBefore.Value;
            pages = pages.Where(page =>
                (page.ModifiedDate ?? page.CreatedDate) <= end &&
                !linkedRepository.PageBuilderLinkedComponentReferences
                    .Where(reference => reference.PageId == page.Id)
                    .Join(
                        linkedRepository.PageBuilderLinkedComponents,
                        reference => reference.LinkedComponentId,
                        component => component.Id,
                        (_, component) => component.ModifiedDate ?? component.CreatedDate)
                    .Any(changeDate => changeDate > end));
        }

        return pages;
    }

    internal static async Task<IReadOnlyDictionary<string, DateTime>> GetEffectiveChangeDatesAsync(
        IPageBuilderModuleRepository repository,
        IEnumerable<PageBuilderPage> pages,
        CancellationToken cancellationToken = default)
    {
        var result = pages
            .Where(page => !string.IsNullOrWhiteSpace(page.Id))
            .ToDictionary(
                page => page.Id,
                page => page.ModifiedDate ?? page.CreatedDate,
                StringComparer.OrdinalIgnoreCase);

        if (repository is not IPageBuilderLinkedComponentRepository linkedRepository)
        {
            return result;
        }

        foreach (var batch in result.Keys.Chunk(QueryBatchSize))
        {
            var componentDates = await linkedRepository.PageBuilderLinkedComponentReferences
                .Where(reference => batch.Contains(reference.PageId))
                .Join(
                    linkedRepository.PageBuilderLinkedComponents,
                    reference => reference.LinkedComponentId,
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
