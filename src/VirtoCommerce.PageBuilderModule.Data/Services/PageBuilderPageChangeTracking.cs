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
        return repository is IPageBuilderSharedComponentRepository sharedComponentRepository
            ? ApplyDateRangeWithSharedComponents(pages, sharedComponentRepository, modifiedSince, modifiedBefore)
            : ApplyPageDateRange(pages, modifiedSince, modifiedBefore);
    }

    private static IQueryable<PageBuilderPageEntity> ApplyPageDateRange(
        IQueryable<PageBuilderPageEntity> pages,
        DateTime? modifiedSince,
        DateTime? modifiedBefore)
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

    private static IQueryable<PageBuilderPageEntity> ApplyDateRangeWithSharedComponents(
        IQueryable<PageBuilderPageEntity> pages,
        IPageBuilderSharedComponentRepository sharedComponentRepository,
        DateTime? modifiedSince,
        DateTime? modifiedBefore)
    {
        pages = ApplyModifiedSince(pages, sharedComponentRepository, modifiedSince);
        return ApplyModifiedBefore(pages, sharedComponentRepository, modifiedBefore);
    }

    private static IQueryable<PageBuilderPageEntity> ApplyModifiedSince(
        IQueryable<PageBuilderPageEntity> pages,
        IPageBuilderSharedComponentRepository sharedComponentRepository,
        DateTime? modifiedSince)
    {
        if (!modifiedSince.HasValue)
        {
            return pages;
        }

        var start = modifiedSince.Value;
        return pages.Where(page =>
            (page.ModifiedDate ?? page.CreatedDate) >= start ||
            sharedComponentRepository.PageBuilderSharedComponentReferences
                .Where(reference => reference.PageId == page.Id)
                .Join(
                    sharedComponentRepository.PageBuilderSharedComponents,
                    reference => reference.SharedComponentId,
                    component => component.Id,
                    (_, component) => component.ModifiedDate ?? component.CreatedDate)
                .Any(changeDate => changeDate >= start));
    }

    private static IQueryable<PageBuilderPageEntity> ApplyModifiedBefore(
        IQueryable<PageBuilderPageEntity> pages,
        IPageBuilderSharedComponentRepository sharedComponentRepository,
        DateTime? modifiedBefore)
    {
        if (modifiedBefore.HasValue)
        {
            var end = modifiedBefore.Value;
            return pages.Where(page =>
                (page.ModifiedDate ?? page.CreatedDate) <= end &&
                !sharedComponentRepository.PageBuilderSharedComponentReferences
                    .Where(reference => reference.PageId == page.Id)
                    .Join(
                        sharedComponentRepository.PageBuilderSharedComponents,
                        reference => reference.SharedComponentId,
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

        if (repository is not IPageBuilderSharedComponentRepository sharedComponentRepository)
        {
            return result;
        }

        foreach (var batch in result.Keys.Chunk(QueryBatchSize))
        {
            var componentDates = await sharedComponentRepository.PageBuilderSharedComponentReferences
                .Where(reference => batch.Contains(reference.PageId))
                .Join(
                    sharedComponentRepository.PageBuilderSharedComponents,
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
