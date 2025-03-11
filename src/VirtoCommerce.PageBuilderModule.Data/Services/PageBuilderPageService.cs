using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Data.GenericCrud;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderPageService : CrudService<PageBuilderPage, PageBuilderPageEntity, PageBuilderPageChangingEvent, PageBuilderPageChangedEvent>, IPageBuilderPageService
{
    public PageBuilderPageService(
        Func<IPageBuilderModuleRepository> repositoryFactory,
        IPlatformMemoryCache platformMemoryCache,
        IEventPublisher eventPublisher)
        : base(repositoryFactory, platformMemoryCache, eventPublisher)
    {
    }

    protected override Task<IList<PageBuilderPageEntity>> LoadEntities(IRepository repository, IList<string> ids, string responseGroup)
    {
        return ((IPageBuilderModuleRepository)repository).GetPageBuilderPagesByIdsAsync(ids, responseGroup);
    }
}
