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

namespace VirtoCommerce.PageBuilderModule.Data.Services
{
    public class GroupedPageService(
        Func<IPageBuilderModuleRepository> repositoryFactory,
        Func<IContentStreamRepository> contentStreamRepositoryFactory,
        IPlatformMemoryCache platformMemoryCache,
        IEventPublisher eventPublisher)
        : CrudService<GroupedPageBuilderPage, GroupedPageBuilderPageEntity, GroupedPageBuilderPageChangingEvent,
                GroupedPageBuilderPageChangedEvent>(repositoryFactory, platformMemoryCache, eventPublisher),
            IGroupedPageService
    {
        protected override async Task<IList<GroupedPageBuilderPageEntity>> LoadEntities(IRepository repository, IList<string> ids, string responseGroup)
        {
            var result = await ((IPageBuilderModuleRepository)repository).GetGroupedPageBuilderPagesByIdsAsync(ids, responseGroup);
            return result;
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

        public async Task<bool> LoadContentToStreamAsync(string pageId, Stream stream, CancellationToken cancellationToken = default)
        {
            await using var repository = contentStreamRepositoryFactory();

            // Deliberately not disposed: disposing flushes, and flushing an HTTP response body commits the
            // status line, which would make the caller's fall-through to the next candidate — or to 404 —
            // impossible. It would also emit the UTF8 preamble for a page that turned out to have no content.
            // The writer holds no unmanaged resources and the stream is left open either way.
            var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 8192, leaveOpen: true);
            var found = await repository.TryLoadBinaryAsync(pageId, writer, cancellationToken);
            if (found)
            {
                await writer.FlushAsync(cancellationToken);
            }

            return found;
        }

        public async Task SaveStreamAsContentAsync(string pageId, Stream stream, CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);

            // Drain the source before opening the write transaction. When the source is an HTTP request body,
            // reading it is the step that can stall or fail on a dropped connection; doing it first means such a
            // failure happens before the content column has been touched at all.
            var content = await reader.ReadToEndAsync(cancellationToken);
            await using var repository = contentStreamRepositoryFactory();
            using var contentReader = new StringReader(content);
            await repository.SaveBinaryAsync(pageId, contentReader, cancellationToken);
        }

        public async Task CopyPageContentAsync(string sourcePageId, string targetPageId, CancellationToken cancellationToken = default)
        {
            // One server-side statement: the target takes on exactly the source's state, NULL included, and the
            // payload never round-trips through here. A load followed by a save would leave a gap in which the
            // source could change, and would flip a NULL source into '' on the target — turning "never seeded"
            // into "deliberately empty", which is the distinction readers use to fall through to the live page.
            await using var repository = contentStreamRepositoryFactory();
            await repository.CopyContentAsync(sourcePageId, targetPageId, cancellationToken);
        }
    }
}
