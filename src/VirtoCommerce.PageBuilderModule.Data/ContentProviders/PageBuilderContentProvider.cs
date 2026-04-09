using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Extensions;
using VirtoCommerce.Pages.Core.ContentProviders;
using VirtoCommerce.Pages.Core.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.PageBuilderModule.Data.ContentProviders;

public class PageBuilderContentProvider(
    IPageBuilderPageSearchService pageSearchService,
    IGroupedPageService groupedPageService)
    : IPageContentProvider
{
    public string ProviderName => "PageBuilder";
    public bool SupportsReindexation => true;

    public async Task<long> GetTotalChangesCountAsync(DateTime? startDate, DateTime? endDate)
    {
        var criteria = CreateSearchCriteria(startDate, endDate, 0, 0);
        var result = await pageSearchService.SearchAsync(criteria);
        return result.TotalCount;
    }

    public async Task<IList<IndexDocumentChange>> GetChangesAsync(DateTime? startDate, DateTime? endDate, long skip, long take)
    {
        var criteria = CreateSearchCriteria(startDate, endDate, skip, take);
        var result = await pageSearchService.SearchAsync(criteria);

        return result.Results.Select(page => new IndexDocumentChange
        {
            DocumentId = page.Id,
            ChangeDate = page.ModifiedDate ?? page.CreatedDate,
            ChangeType = IndexDocumentChangeType.Modified,
        }).ToList();
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

            var content = await groupedPageService.LoadContent(page.Id);
            var pageDocument = page.ToPageDocument(group, content);
            result.Add(pageDocument);
        }

        return result;
    }

#pragma warning disable S1172
    private static PageBuilderPageSearchCriteria CreateSearchCriteria(DateTime? startDate, DateTime? endDate, long skip, long take)
#pragma warning restore S1172
    {
        var criteria = AbstractTypeFactory<PageBuilderPageSearchCriteria>.TryCreateInstance();
        criteria.Statuses = "Published";
        criteria.Skip = Convert.ToInt32(skip);
        criteria.Take = Convert.ToInt32(take);
        criteria.ModifiedSince = startDate;

        return criteria;
    }
}
