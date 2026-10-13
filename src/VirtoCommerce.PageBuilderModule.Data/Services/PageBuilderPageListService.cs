using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Services;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderPageListService(
    Func<IPageBuilderModuleRepository> repositoryFactory,
    IGroupedPageSearchService groupedPageSearchService,
    IGroupedPageService groupedPageService,
    IMultilanguagePageService multilanguagePageService,
    IStoreService storeService)
    : IPageBuilderPageListService
{
    public virtual async Task<PageBuilderPageListSearchResult> SearchAsync(PageBuilderPageSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var result = AbstractTypeFactory<PageBuilderPageListSearchResult>.TryCreateInstance();
        IList<Row> rows;

        using (var repository = repositoryFactory())
        {
            var query = BuildQuery(repository, criteria);

            result.TotalCount = await query.CountAsync(cancellationToken);
            if (criteria.Take <= 0)
            {
                return result;
            }

            rows = await query
                .OrderBySortInfos(BuildSortExpression(criteria))
                .ThenBy(x => x.Id)
                .Skip(criteria.Skip)
                .Take(criteria.Take)
                .ToListAsync(cancellationToken);
        }

        result.Results = await ToListItemsAsync(rows, cancellationToken);

        return result;
    }

    /// <summary>
    /// Landing pages and multilanguage roots as one sortable, pageable query.
    /// </summary>
    protected virtual IQueryable<Row> BuildQuery(IPageBuilderModuleRepository repository, PageBuilderPageSearchCriteria criteria)
    {
        var groups = GetGroupSearchService().GetFilteredQuery(repository, criteria);

        var landingPages = groups
            .Where(x => x.MultilanguagePageId == null)
            .Select(x => new Row
            {
                IsMultilanguage = false,
                Id = x.Id,
                Name = x.Name,
                Permalink = x.Permalink,
                CreatedDate = x.CreatedDate,
                ModifiedDate = x.ModifiedDate,
            });

        var matchingRootIds = groups
            .Where(x => x.MultilanguagePageId != null)
            .Select(x => x.MultilanguagePageId);

        var roots = repository.MultilanguagePages.AsQueryable();
        if (!string.IsNullOrEmpty(criteria.StoreId))
        {
            roots = roots.Where(x => x.StoreId == criteria.StoreId);
        }

        if (FiltersLanguageVersions(criteria))
        {
            roots = roots.Where(x => matchingRootIds.Contains(x.Id));
        }
        else
        {
            // Without version filters a root is listed even before it has any language version.
            var keyword = criteria.Keyword;
            roots = roots.Where(x => matchingRootIds.Contains(x.Id) ||
                                     string.IsNullOrEmpty(keyword) ||
                                     x.Name.Contains(keyword) ||
                                     x.Permalink.Contains(keyword));
        }

        var multilanguagePages = roots.Select(x => new Row
        {
            IsMultilanguage = true,
            Id = x.Id,
            Name = x.Name,
            Permalink = x.Permalink,
            CreatedDate = x.CreatedDate,
            ModifiedDate = x.ModifiedDate,
        });

        return criteria.PageType switch
        {
            PageTypes.Landing => landingPages,
            PageTypes.Multilanguage => multilanguagePages,
            _ => landingPages.Concat(multilanguagePages),
        };
    }

    protected virtual IList<SortInfo> BuildSortExpression(PageBuilderPageSearchCriteria criteria)
    {
        // Only columns both page types have can be sorted on.
        var sortInfos = criteria.SortInfos?
            .Where(x => typeof(Row).GetProperty(x.SortColumn ?? string.Empty,
                System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) != null)
            .ToList();

        return sortInfos.IsNullOrEmpty()
            ? [new SortInfo { SortColumn = nameof(Row.CreatedDate), SortDirection = SortDirection.Descending }]
            : sortInfos;
    }

    protected virtual async Task<IList<PageBuilderPageListItem>> ToListItemsAsync(IList<Row> rows, CancellationToken cancellationToken)
    {
        var landingIds = rows.Where(x => !x.IsMultilanguage).Select(x => x.Id).ToList();
        var rootIds = rows.Where(x => x.IsMultilanguage).Select(x => x.Id).ToList();

        var landingPages = (await groupedPageService.GetAsync(landingIds)).ToDictionary(x => x.Id);
        var roots = (await multilanguagePageService.GetAsync(rootIds)).ToDictionary(x => x.Id);
        var versions = await GetLanguageVersionsAsync(rootIds);
        var storeLanguages = await GetStoreLanguagesAsync(roots.Values.Select(x => x.StoreId));

        return rows.Select(row =>
            {
                var item = AbstractTypeFactory<PageBuilderPageListItem>.TryCreateInstance();
                item.Type = row.IsMultilanguage ? PageTypes.Multilanguage : PageTypes.Landing;
                item.Id = row.Id;
                item.Name = row.Name;
                item.Permalink = row.Permalink;
                item.CreatedDate = row.CreatedDate;
                item.ModifiedDate = row.ModifiedDate;

                if (row.IsMultilanguage && roots.TryGetValue(row.Id, out var root))
                {
                    item.MultilanguagePage = root;
                    item.Languages = MultilanguagePageLanguages.Build(
                        root,
                        storeLanguages.GetValueOrDefault(root.StoreId),
                        versions.Where(x => x.MultilanguagePageId == root.Id));
                }
                else if (!row.IsMultilanguage)
                {
                    item.Page = landingPages.GetValueOrDefault(row.Id);
                }

                return item;
            })
            .ToList();
    }

    private async Task<IList<GroupedPageBuilderPage>> GetLanguageVersionsAsync(IList<string> rootIds)
    {
        if (rootIds.Count == 0)
        {
            return [];
        }

        IList<string> groupIds;
        using (var repository = repositoryFactory())
        {
            groupIds = await repository.GroupedPageBuilderPages
                .Where(x => rootIds.Contains(x.MultilanguagePageId))
                .Select(x => x.Id)
                .ToListAsync();
        }

        return await groupedPageService.GetAsync(groupIds);
    }

    private async Task<Dictionary<string, ICollection<string>>> GetStoreLanguagesAsync(IEnumerable<string> storeIds)
    {
        var ids = storeIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var stores = ids.Count == 0 ? [] : await storeService.GetNoCloneAsync(ids);

        return stores.ToDictionary(x => x.Id, x => x.Languages, StringComparer.OrdinalIgnoreCase);
    }

    private GroupedPageSearchService GetGroupSearchService()
    {
        return groupedPageSearchService as GroupedPageSearchService
            ?? throw new InvalidOperationException($"The pages list requires {nameof(IGroupedPageSearchService)} to derive from {nameof(GroupedPageSearchService)}.");
    }

    private static bool FiltersLanguageVersions(PageBuilderPageSearchCriteria criteria)
    {
        return !string.IsNullOrEmpty(criteria.Statuses) ||
               !string.IsNullOrEmpty(criteria.Lifecycle) ||
               criteria.ActiveOn.HasValue ||
               !string.IsNullOrEmpty(criteria.LanguageCode);
    }

    /// <summary>
    /// Shape shared by both sides of the union. Both sides must project identical column types.
    /// </summary>
    protected class Row
    {
        public bool IsMultilanguage { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Permalink { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}
