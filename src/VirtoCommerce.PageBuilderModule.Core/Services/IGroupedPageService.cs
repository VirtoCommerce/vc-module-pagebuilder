using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.GenericCrud;

namespace VirtoCommerce.PageBuilderModule.Core.Services
{
    public interface IGroupedPageService : ICrudService<GroupedPageBuilderPage>
    {
        Task<string> LoadContent(string pageId, CancellationToken cancellationToken = default);
        Task SaveContent(string pageId, string content, CancellationToken cancellationToken = default);

        /// <returns>
        /// <c>false</c> when the page has no content at all — it was deleted, or it is a draft that was created
        /// but never seeded. Callers must not treat that as empty content; nothing is written to
        /// <paramref name="stream"/> in that case.
        /// </returns>
        Task<bool> LoadContentToStreamAsync(string pageId, Stream stream, CancellationToken cancellationToken = default);
        Task SaveStreamAsContentAsync(string pageId, Stream stream, CancellationToken cancellationToken = default);

        Task CopyPageContentAsync(string sourcePageId, string targetPageId,
            CancellationToken cancellationToken = default);

        Task<bool> TryDeleteEmptyDraftAsync(string pageId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }
    }
}
