using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.Services
{
    public interface IGroupedPageService
    {
        Task<GroupedPageBuilderPageSearchResult> SearchAsync(PageBuilderPageSearchCriteria criteria);
        Task<GroupedPageBuilderPage> GetGroupedAsync(string id);
        Task<IList<GroupedPageBuilderPage>> GetGroupedAsync(string[] id);
    }
}
