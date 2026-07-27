using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Data.GenericCrud;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderPageService(
    Func<IPageBuilderModuleRepository> repositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    IEventPublisher eventPublisher)
    : CrudService<PageBuilderPage, PageBuilderPageEntity, PageBuilderPageChangingEvent, PageBuilderPageChangedEvent>(
        repositoryFactory, platformMemoryCache, eventPublisher), IPageBuilderPageService
{
    protected override Task<IList<PageBuilderPageEntity>> LoadEntities(IRepository repository, IList<string> ids, string responseGroup)
    {
        return ((IPageBuilderModuleRepository)repository).GetPageBuilderPagesByIdsAsync(ids, responseGroup);
    }

    // A page is part of its group's cached aggregate: GroupedPageBuilderPage carries the whole Pages collection.
    // The base implementation only expires this entity's own region, so saving or deleting a page would leave the
    // group cached with a stale page list — and nothing would ever correct it, because the changed-event handlers
    // re-read the group through the very cache that was not invalidated. PublishGroup deletes the superseded pages
    // right after saving the group, so without this cascade the group keeps serving deleted page ids until the
    // process restarts.
    protected override void ClearCache(IList<PageBuilderPage> models)
    {
        base.ClearCache(models);

        foreach (var groupId in models.Select(x => x.GroupId).Where(x => !string.IsNullOrEmpty(x)).Distinct())
        {
            GenericCachingRegion<GroupedPageBuilderPage>.ExpireTokenForKey(groupId);
        }
    }
}
