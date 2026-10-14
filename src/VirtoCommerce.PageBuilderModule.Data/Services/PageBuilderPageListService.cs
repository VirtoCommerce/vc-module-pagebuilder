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

    public virtual async Task<IList<PermalinkUsage>> FindPermalinkUsageAsync(PermalinkUsageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

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
        var languageCode = criteria.LanguageCode;

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

        if (translationStatus == TranslationStatuses.Missing)
        {
            // One version per language: fewer versions in these languages than languages means one is missing.
            var versions = repository.GroupedPageBuilderPages;
            roots = roots.Where(x =>
                versions.Count(g => g.MultilanguagePageId == x.Id && languagesToHave.Contains(g.CultureName)) < languagesToHave.Count);
        }
        else if (translationStatus == TranslationStatuses.Outdated)
        {
            var versions = repository.GroupedPageBuilderPages;
            roots = roots.Where(x => versions.Any(g =>
                g.MultilanguagePageId == x.Id &&
                g.CultureName != x.DefaultCultureName &&
                (languageCode == null || g.CultureName == languageCode) &&
                versions.Any(source =>
                    source.MultilanguagePageId == x.Id &&
                    source.CultureName == x.DefaultCultureName &&
                    source.SourceRevisionDate != null &&
                    (g.SourceRevisionDate == null || g.SourceRevisionDate < source.SourceRevisionDate))));
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

        // A Landing Page has no translations to be missing or outdated.
        var pageType = string.IsNullOrEmpty(translationStatus) ? criteria.PageType : PageTypes.Multilanguage;

        return pageType switch
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
