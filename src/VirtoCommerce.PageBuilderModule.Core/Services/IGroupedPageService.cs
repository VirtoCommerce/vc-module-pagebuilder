using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.GenericCrud;

namespace VirtoCommerce.PageBuilderModule.Core.Services
{
    public interface IGroupedPageService : ICrudService<GroupedPageBuilderPage>
    {
        Task<string> LoadContent(string pageId, CancellationToken cancellationToken = default);
        Task SaveContent(string pageId, string content, CancellationToken cancellationToken = default);

        Task LoadContentToStreamAsync(string pageId, Stream stream, CancellationToken cancellationToken = default);
        Task SaveStreamAsContentAsync(string pageId, Stream stream, CancellationToken cancellationToken = default);

        Task CopyPageContentAsync(string sourcePageId, string targetPageId,
            CancellationToken cancellationToken = default);
    }
}
