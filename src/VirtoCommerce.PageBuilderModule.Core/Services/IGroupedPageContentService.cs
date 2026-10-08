using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

/// <summary>Versioned authoring capability, separate from the existing grouped CRUD contract.</summary>
public interface IGroupedPageContentService
{
    Task<PageBuilderContentSnapshot> LoadGroupContentAsync(
        GroupedPageBuilderPage authorizedGroup, bool draft = true, CancellationToken cancellationToken = default);

    Task<PageBuilderConditionalContentWriteResult> SaveGroupContentAsync(
        GroupedPageBuilderPage authorizedGroup, string content, string expectedETag, CancellationToken cancellationToken = default);

    Task SaveGroupSettingsAsync(GroupedPageBuilderPage group, CancellationToken cancellationToken = default);
}
