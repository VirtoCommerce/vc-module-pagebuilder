using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.GenericCrud;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderLinkedComponentService : ICrudService<PageBuilderLinkedComponent>
{
    Task SaveWithContentAsync(
        PageBuilderLinkedComponent model,
        string content,
        CancellationToken cancellationToken = default);
}
