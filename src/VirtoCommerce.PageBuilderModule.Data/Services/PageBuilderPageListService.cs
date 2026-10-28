using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Extensions;
using VirtoCommerce.PageBuilderModule.Data.Models;
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
    public virtual Task<PageBuilderPageListSearchResult> SearchAsync(PageBuilderPageSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        return SearchCoreAsync(criteria, cancellationToken);
    }

    public virtual Task<IList<PermalinkUsage>> FindPermalinkUsageAsync(PermalinkUsageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return FindPermalinkUsageCoreAsync(request, cancellationToken);
    }

    public virtual Task<PageCopyName> GetCopyNameAsync(GroupedPageBuilderPage page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        return GetCopyNameCoreAsync(page, cancellationToken);
    }

    protected virtual async Task<PageCopyName> GetCopyNameCoreAsync(GroupedPageBuilderPage page, CancellationToken cancellationToken)
    {
        int number;
        using (var repository = repositoryFactory())
        {
            number = await repository.FindFreeCopyNumberAsync(page.StoreId, [page.Permalink], cancellationToken);
        }

        var result = AbstractTypeFactory<PageCopyName>.TryCreateInstance();
        result.Name = PageBuilderPageCopy.GetName(page.Name, number);
        result.Permalink = PageBuilderPageCopy.GetPermalink(page.Permalink, number);
        return result;
    }

    protected virtual async Task<PageBuilderPageListSearchResult> SearchCoreAsync(PageBuilderPageSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var result = AbstractTypeFactory<PageBuilderPageListSearchResult>.TryCreateInstance();
        IList<Row> rows;

        var languagesToHave = criteria.TranslationStatus == TranslationStatuses.Missing
            ? await GetLanguagesToHaveAsync(criteria)
            : [];

        using (var repository = repositoryFactory())
        {
            var query = BuildQuery(repository, criteria, languagesToHave);

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

    protected virtual async Task<IList<PermalinkUsage>> FindPermalinkUsageCoreAsync(PermalinkUsageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.StoreId) || string.IsNullOrWhiteSpace(request.Permalink))
        {
            return [];
        }

        // Stored permalinks start with a slash, older ones may not.
        var permalink = $"/{request.Permalink.Trim().TrimStart('/')}";
        var permalinkWithoutSlash = permalink.TrimStart('/');
        var cultureNames = request.CultureNames ?? [];
        var excludeGroupIds = request.ExcludeGroupIds ?? [];
        var excludeMultilanguagePageId = request.ExcludeMultilanguagePageId;

        using var repository = repositoryFactory();

        var query = repository.GroupedPageBuilderPages
            .Where(x => x.StoreId == request.StoreId)
            .Where(x => x.Permalink == permalink || x.Permalink == permalinkWithoutSlash)
            .Where(x => x.Pages.Any(p => p.Status != PageStatuses.Archived))
            .Where(x => !excludeGroupIds.Contains(x.Id));

        if (cultureNames.Count > 0)
        {
            query = query.Where(x => cultureNames.Contains(x.CultureName));
        }

        if (!string.IsNullOrEmpty(excludeMultilanguagePageId))
        {
            query = query.Where(x => x.MultilanguagePageId == null || x.MultilanguagePageId != excludeMultilanguagePageId);
        }

        return await query
            .OrderBy(x => x.CultureName)
            .ThenBy(x => x.Name)
            .Select(x => new PermalinkUsage
            {
                GroupId = x.Id,
                Name = x.Name,
                CultureName = x.CultureName,
                MultilanguagePageId = x.MultilanguagePageId,
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Landing pages and multilanguage roots as one sortable, pageable query.
    /// </summary>
    /// <param name="languagesToHave">Languages a Multilanguage Page must have a version in, for the Missing translation status.</param>
    protected virtual IQueryable<Row> BuildQuery(IPageBuilderModuleRepository repository, PageBuilderPageSearchCriteria criteria,
        IList<string> languagesToHave)
    {
        var translationStatus = criteria.TranslationStatus;

        // A missing language has no version to match, so versions are matched in any language.
        var versionCriteria = criteria;
        if (translationStatus == TranslationStatuses.Missing)
        {
            versionCriteria = criteria.CloneTyped();
            versionCriteria.LanguageCode = null;
        }

        var groups = GetGroupSearchService().GetFilteredQuery(repository, versionCriteria);

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

        var roots = FilterRoots(repository, criteria, versionCriteria, groups);
        roots = FilterByTranslationStatus(repository, roots, criteria, languagesToHave);

        var multilanguagePages = roots.Select(x => new Row
        {
            IsMultilanguage = true,
            Id = x.Id,
            Name = x.Name,
            Permalink = x.Permalink,
            CreatedDate = x.CreatedDate,
            ModifiedDate = x.ModifiedDate,
        });

        // A Landing Page has no translations to be missing or outdated.
        var pageType = string.IsNullOrEmpty(translationStatus) ? criteria.PageType : PageTypes.Multilanguage;

        return pageType switch
        {
            PageTypes.Landing => landingPages,
            PageTypes.Multilanguage => multilanguagePages,
            _ => landingPages.Concat(multilanguagePages),
        };
    }

    /// <summary>
    /// Roots of the store; with version filters, only the roots with a matching language version.
    /// </summary>
    protected virtual IQueryable<MultilanguagePageEntity> FilterRoots(IPageBuilderModuleRepository repository, PageBuilderPageSearchCriteria criteria,
        PageBuilderPageSearchCriteria versionCriteria, IQueryable<GroupedPageBuilderPageEntity> groups)
    {
        var matchingRootIds = groups
            .Where(x => x.MultilanguagePageId != null)
            .Select(x => x.MultilanguagePageId);

        var roots = repository.MultilanguagePages.AsQueryable();
        if (!string.IsNullOrEmpty(criteria.StoreId))
        {
            roots = roots.Where(x => x.StoreId == criteria.StoreId);
        }

        if (FiltersLanguageVersions(versionCriteria))
        {
            if (!ListsRootsWithoutVersions(versionCriteria))
            {
                return roots.Where(x => matchingRootIds.Contains(x.Id));
            }

            // A root without language versions is still being authored, like a new Landing Page with only a draft.
            var rootIdsWithVersions = repository.GroupedPageBuilderPages
                .Where(x => x.MultilanguagePageId != null)
                .Select(x => x.MultilanguagePageId);
            var rootIdsWithoutVersions = FilterByKeyword(roots, criteria.Keyword)
                .Where(x => !rootIdsWithVersions.Contains(x.Id))
                .Select(x => x.Id);

            return roots.Where(x => matchingRootIds.Contains(x.Id) || rootIdsWithoutVersions.Contains(x.Id));
        }

        // Without version filters a root is listed even before it has any language version.
        if (string.IsNullOrEmpty(criteria.Keyword))
        {
            return roots;
        }

        var keywordRootIds = FilterByKeyword(roots, criteria.Keyword).Select(x => x.Id);

        return roots.Where(x => matchingRootIds.Contains(x.Id) || keywordRootIds.Contains(x.Id));
    }

    // Ignores case like the language versions filter (see GroupedPageSearchService).
    private static IQueryable<MultilanguagePageEntity> FilterByKeyword(IQueryable<MultilanguagePageEntity> roots, string keyword)
    {
        if (string.IsNullOrEmpty(keyword))
        {
            return roots;
        }

        keyword = keyword.ToLowerInvariant();

#pragma warning disable CA1862 // IQueryable: the StringComparison overload does not translate to SQL
        return roots.Where(x => x.Name.ToLower().Contains(keyword) || x.Permalink.ToLower().Contains(keyword));
#pragma warning restore CA1862
    }

    // Only the drafts list: a status, language or date filter asks about versions a root without them does not have.
    private static bool ListsRootsWithoutVersions(PageBuilderPageSearchCriteria criteria)
    {
        return criteria.Lifecycle == PageLifecycleFilters.Drafts && !FiltersVersionFields(criteria);
    }

    private static bool FiltersVersionFields(PageBuilderPageSearchCriteria criteria)
    {
        return !string.IsNullOrEmpty(criteria.Statuses) ||
               !string.IsNullOrEmpty(criteria.LanguageCode) ||
               criteria.ActiveOn.HasValue ||
               criteria.ObjectIds is { Count: > 0 };
    }

    protected virtual IQueryable<MultilanguagePageEntity> FilterByTranslationStatus(IPageBuilderModuleRepository repository,
        IQueryable<MultilanguagePageEntity> roots, PageBuilderPageSearchCriteria criteria, IList<string> languagesToHave)
    {
        var versions = repository.GroupedPageBuilderPages;

        if (criteria.TranslationStatus == TranslationStatuses.Missing)
        {
            // One version per language: fewer versions in these languages than languages means one is missing.
            return roots.Where(x =>
                versions.Count(g => g.MultilanguagePageId == x.Id && languagesToHave.Contains(g.CultureName)) < languagesToHave.Count);
        }

        if (criteria.TranslationStatus != TranslationStatuses.Outdated)
        {
            return roots;
        }

        // A served (not archived) translation based on an older publication of the source language than its latest one.
        var translations = versions.Where(g => g.Pages.Any(p => p.Status != PageStatuses.Archived));
        var languageCode = criteria.LanguageCode;
        if (!string.IsNullOrEmpty(languageCode))
        {
            translations = translations.Where(g => g.CultureName == languageCode);
        }

        var publishedSources = versions.Where(source => source.SourceRevisionDate != null);

        return roots.Where(x => translations
            .Where(g => g.MultilanguagePageId == x.Id && g.CultureName != x.DefaultCultureName)
            .Any(g => publishedSources
                .Where(source => source.MultilanguagePageId == x.Id && source.CultureName == x.DefaultCultureName)
                .Any(source => g.SourceRevisionDate == null || g.SourceRevisionDate < source.SourceRevisionDate)));
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

    private async Task<IList<GroupedPageBuilderPage>> GetLanguageVersionsAsync(List<string> rootIds)
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

    private async Task<IList<string>> GetLanguagesToHaveAsync(PageBuilderPageSearchCriteria criteria)
    {
        if (!string.IsNullOrEmpty(criteria.LanguageCode))
        {
            return [criteria.LanguageCode];
        }

        var store = string.IsNullOrEmpty(criteria.StoreId) ? null : await storeService.GetNoCloneAsync(criteria.StoreId);
        return store?.Languages?.ToList() ?? [];
    }

    private GroupedPageSearchService GetGroupSearchService()
    {
        return groupedPageSearchService as GroupedPageSearchService
            ?? throw new InvalidOperationException($"The pages list requires {nameof(IGroupedPageSearchService)} to derive from {nameof(GroupedPageSearchService)}.");
    }

    private static bool FiltersLanguageVersions(PageBuilderPageSearchCriteria criteria)
    {
        var filtersByStatus = !string.IsNullOrEmpty(criteria.Statuses) || !string.IsNullOrEmpty(criteria.Lifecycle) || criteria.ActiveOn.HasValue;

        return filtersByStatus || !string.IsNullOrEmpty(criteria.LanguageCode) || criteria.ObjectIds is { Count: > 0 };
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
