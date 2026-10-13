using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.GenericCrud;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

/// <summary>
/// Saving a root copies its shared name and inherited settings into the linked language versions.
/// Deleting a root detaches its language versions; they stay as Landing Pages with their current values.
/// </summary>
public interface IMultilanguagePageService : ICrudService<MultilanguagePage>
{
    /// <summary>
    /// Makes an existing page a language version of the root. Fields whose value differs from the root become overridden,
    /// so linking never changes how the page is served.
    /// </summary>
    Task<GroupedPageBuilderPage> LinkAsync(string multilanguagePageId, string groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns a language version back into a Landing Page with its current values.
    /// </summary>
    Task<GroupedPageBuilderPage> UnlinkAsync(string groupId, CancellationToken cancellationToken = default);
}
