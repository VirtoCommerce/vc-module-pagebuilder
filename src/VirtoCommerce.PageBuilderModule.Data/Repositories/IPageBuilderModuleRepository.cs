using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.PageBuilderModule.Data.Models;

namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

public interface IPageBuilderModuleRepository : IRepository
{
    IQueryable<PageBuilderPageEntity> PageBuilderPages { get; }

    Task<IList<PageBuilderPageEntity>> GetPageBuilderPagesByIdsAsync(IList<string> ids, string responseGroup);
}
