using System.Text;
using Microsoft.Extensions.Logging;
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
    public class GroupedPageService(
        Func<IPageBuilderModuleRepository> repositoryFactory,
        Func<IContentStreamRepository> contentStreamRepositoryFactory,
        IPlatformMemoryCache platformMemoryCache,
        IEventPublisher eventPublisher,
        ILogger<GroupedPageService> logger)
        : CrudService<GroupedPageBuilderPage, GroupedPageBuilderPageEntity, GroupedPageBuilderPageChangingEvent,
                GroupedPageBuilderPageChangedEvent>(repositoryFactory, platformMemoryCache, eventPublisher),
            IGroupedPageService
    {
        protected override async Task<IList<GroupedPageBuilderPageEntity>> LoadEntities(IRepository repository, IList<string> ids, string responseGroup)
        {
            var result = await ((IPageBuilderModuleRepository)repository).GetGroupedPageBuilderPagesByIdsAsync(ids, responseGroup);
            return result;
        }

        protected override Task BeforeSaveChanges(IList<GroupedPageBuilderPage> models)
        {
            foreach (var group in models)
            {
                NormalizePublishedPages(group);
            }

            return base.BeforeSaveChanges(models);
        }

        // Enforces invariant: at most one Published page per group.
        // If multiple Published exist, keeps the newest by CreatedDate and demotes the rest to Archived.
        // The change goes through the regular save flow, so the affected pages get re-indexed via the GroupedPageBuilderPageChangedEvent handler.
        private void NormalizePublishedPages(GroupedPageBuilderPage group)
        {
            if (group?.Pages == null)
            {
                return;
            }

            var publishedPages = group.Pages.Where(x => x.Status == Published).ToList();
            if (publishedPages.Count <= 1)
            {
                return;
            }

            var keep = publishedPages.OrderByDescending(x => x.CreatedDate).First();
            foreach (var page in publishedPages)
            {
                if (page.Id == keep.Id)
                {
                    continue;
                }

                page.Status = Archived;
                logger.LogWarning("Page '{PageId}' in group '{GroupId}' had Published status while another Published page exists. Demoted to Archived.",
                    page.Id, group.Id);
            }
        }

        public async Task<string> LoadContent(string pageId, CancellationToken cancellationToken = default)
        {
            await using var memoryStream = new MemoryStream();
            await LoadContentToStreamAsync(pageId, memoryStream, cancellationToken);
            memoryStream.Position = 0;
            using var reader = new StreamReader(memoryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        public async Task SaveContent(string pageId, string content, CancellationToken cancellationToken = default)
        {
            await using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            await SaveStreamAsContentAsync(pageId, memoryStream, cancellationToken);
        }

        public async Task LoadContentToStreamAsync(string pageId, Stream stream, CancellationToken cancellationToken = default)
        {
            var repository = contentStreamRepositoryFactory();
            await using var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 8192, leaveOpen: true);
            await repository.LoadBinaryAsync(pageId, writer, cancellationToken);
        }

        public async Task SaveStreamAsContentAsync(string pageId, Stream stream, CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            var repository = contentStreamRepositoryFactory();
            await repository.SaveBinaryAsync(pageId, reader, cancellationToken);
        }

        public async Task CopyPageContentAsync(string sourcePageId, string targetPageId, CancellationToken cancellationToken = default)
        {
            await using var memoryStream = new MemoryStream();
            await LoadContentToStreamAsync(sourcePageId, memoryStream, cancellationToken);
            memoryStream.Position = 0;
            await SaveStreamAsContentAsync(targetPageId, memoryStream, cancellationToken);
        }
    }
}
