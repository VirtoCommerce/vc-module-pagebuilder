using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.GenericCrud;

namespace VirtoCommerce.PageBuilderModule.Core.Services
{
    public interface IGroupedPageService : ICrudService<GroupedPageBuilderPage>
    {
        Task LoadContentToStreamAsync(string pageId, Stream stream, CancellationToken cancellationToken = default);
        Task SaveStreamAsContentAsync(string pageId, Stream stream, CancellationToken cancellationToken = default);
    }
}
