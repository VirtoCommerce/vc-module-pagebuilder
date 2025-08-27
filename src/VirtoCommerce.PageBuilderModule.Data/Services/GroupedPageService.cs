using System.Text;
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
        private readonly Func<IPageBuilderModuleRepository> _repositoryFactory;
        private readonly Func<IContentStreamRepository> _contentStreamRepositoryFactory;

        public GroupedPageService(
            Func<IPageBuilderModuleRepository> repositoryFactory,
            Func<IContentStreamRepository> contentStreamRepositoryFactory,
            IPlatformMemoryCache platformMemoryCache,
            IEventPublisher eventPublisher)
            : base(repositoryFactory, platformMemoryCache, eventPublisher)
        {
            _repositoryFactory = repositoryFactory;
            _contentStreamRepositoryFactory = contentStreamRepositoryFactory;
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

        public async Task LoadContentToStreamAsync(string pageId, Stream stream,
            CancellationToken cancellationToken = default)
        {
            var repository = _contentStreamRepositoryFactory();
            await using var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 8192, leaveOpen: true);
            await repository.LoadBinaryAsync(pageId, writer, cancellationToken);
        }

        public async Task SaveStreamAsContentAsync(string pageId, Stream stream,
            CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            var repository = _contentStreamRepositoryFactory();
            await repository.SaveBinaryAsync(pageId, reader, cancellationToken);
        }
    }
}
