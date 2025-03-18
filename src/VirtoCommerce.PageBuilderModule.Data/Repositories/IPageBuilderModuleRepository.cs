using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

public interface IPageBuilderModuleRepository : IRepository
{
    IQueryable<PageBuilderPageEntity> PageBuilderPages { get; }

    Task<IList<PageBuilderPageEntity>> GetPageBuilderPagesByIdsAsync(IList<string> ids, string responseGroup);
}
