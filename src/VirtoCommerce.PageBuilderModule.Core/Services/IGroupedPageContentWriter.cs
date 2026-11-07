using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

/// <summary>Writes the draft content of a database page and raises the page events.</summary>
public interface IGroupedPageContentWriter
{
    /// <exception cref="PageBuilderContentConflictException">The page was saved by someone else since it was read.</exception>
    Task<PageBuilderConditionalContentWriteResult> SaveContentAsync(GroupedPageBuilderPage group, string content, string expectedETag,
        CancellationToken cancellationToken = default);
}
