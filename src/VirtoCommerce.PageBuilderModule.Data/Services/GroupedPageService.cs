using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Data.GenericCrud;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Data.Services
{
    public class GroupedPageService : CrudService<GroupedPageBuilderPage, GroupedPageBuilderPageEntity, GroupedPageBuilderPageChangingEvent, GroupedPageBuilderPageChangedEvent>, IGroupedPageService
    {
        public GroupedPageService(
            Func<IPageBuilderModuleRepository> repositoryFactory,
            IPlatformMemoryCache platformMemoryCache,
            IEventPublisher eventPublisher)
            : base(repositoryFactory, platformMemoryCache, eventPublisher)
        {
        }

        protected override async Task BeforeSaveChanges(IList<GroupedPageBuilderPage> models)
        {
            foreach (var model in models)
            {
                if (!model.Pages.IsNullOrEmpty())
                {
                    // Update status of the grouped page based on the status of the pages it contains
                    model.Status = model.Pages.Any(p => p.Status == Archived) ? Archived : model.Pages.Any(p => p.Status == Published) ? Published : Draft;
                }
            }

            await base.BeforeSaveChanges(models);
        }

        protected override async Task<IList<GroupedPageBuilderPageEntity>> LoadEntities(IRepository repository, IList<string> ids, string responseGroup)
        {
            var result = await ((IPageBuilderModuleRepository)repository).GetGroupedPageBuilderPagesByIdsAsync(ids, responseGroup);
            return result;
        }
    }
}
