using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Extensions;
using VirtoCommerce.Pages.Core.ContentProviders;
using VirtoCommerce.Pages.Core.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Data.ContentProviders;

public class PageBuilderContentProvider(
    IPageBuilderPageSearchService pageSearchService,
    IGroupedPageService groupedPageService,
    IPageBuilderLinkedComponentResolver linkedComponentResolver)
    : IPageContentProvider
{
    public string ProviderName => "PageBuilder";
    public bool SupportsReindexation => true;

    public async Task<PageChangesSearchResult> SearchChangesAsync(PageChangesSearchCriteria criteria)
    {
        var searchCriteria = CreateSearchCriteria(criteria);
        var result = await pageSearchService.SearchAsync(searchCriteria);

        return new PageChangesSearchResult
        {
            TotalCount = result.TotalCount,
            Results = result.Results.Select(page => new IndexDocumentChange
            {
                DocumentId = page.Id,
                ChangeDate = page.ModifiedDate ?? page.CreatedDate,
                ChangeType = IndexDocumentChangeType.Modified,
            }).ToList(),
        };
    }

    public async Task<IList<PageDocument>> GetByIdsAsync(IList<string> ids)
    {
        var criteria = AbstractTypeFactory<PageBuilderPageSearchCriteria>.TryCreateInstance();
        criteria.ObjectIds = ids.ToArray();
        criteria.Take = ids.Count;

        var pages = await pageSearchService.SearchAsync(criteria);

        var groupIds = pages.Results.Select(p => p.GroupId).Distinct().ToArray();
        var groups = await groupedPageService.GetAsync(groupIds);
        var groupsById = groups.ToDictionary(g => g.Id);

        var result = new List<PageDocument>();

        foreach (var page in pages.Results)
        {
            if (!groupsById.TryGetValue(page.GroupId, out var group))
            {
                continue;
            }

            var rawContent = await groupedPageService.LoadContent(page.Id);
            var content = await linkedComponentResolver.ResolveAsync(rawContent);
            var pageDocument = page.ToPageDocument(group, content);
            pageDocument.Status = MapStatus(page.Status);
            result.Add(pageDocument);
        }

        return result;
    }

    private static PageBuilderPageSearchCriteria CreateSearchCriteria(PageChangesSearchCriteria criteria)
    {
        var result = AbstractTypeFactory<PageBuilderPageSearchCriteria>.TryCreateInstance();
        result.Statuses = $"{Draft},{Published},{Archived}";
        result.Skip = criteria.Skip;
        result.Take = criteria.Take;
        result.ModifiedSince = criteria.StartDate;
        result.ModifiedBefore = criteria.EndDate;

        return result;
    }

    private static PageDocumentStatus MapStatus(string status)
    {
        return status switch
        {
            Published => PageDocumentStatus.Published,
            Archived => PageDocumentStatus.Archived,
            Draft => PageDocumentStatus.Draft,
            _ => PageDocumentStatus.Draft,
        };
    }
}
