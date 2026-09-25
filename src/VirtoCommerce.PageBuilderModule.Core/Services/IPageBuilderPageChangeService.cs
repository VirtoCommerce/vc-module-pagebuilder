using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderPageChangeService
{
    Task<IReadOnlyDictionary<string, DateTime>> GetEffectiveChangeDatesAsync(
        IEnumerable<PageBuilderPage> pages,
        CancellationToken cancellationToken = default);
}
