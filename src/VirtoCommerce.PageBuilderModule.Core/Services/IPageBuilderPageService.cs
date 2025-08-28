using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.GenericCrud;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderPageService : ICrudService<PageBuilderPage>
{
    Task<string> GetPageContentAsync(string pageId, CancellationToken cancellationToken = default);
}
