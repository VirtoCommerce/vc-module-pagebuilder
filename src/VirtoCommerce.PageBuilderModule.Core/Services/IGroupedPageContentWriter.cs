using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IGroupedPageContentWriter
{
    Task<PageBuilderConditionalContentWriteResult> SaveContentAsync(GroupedPageBuilderPage group, string content, string expectedETag,
        CancellationToken cancellationToken = default);
}
