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

    /// <summary>
    /// Turns a Landing Page into a Multilanguage Page: a new root takes the page's name, permalink and settings,
    /// and the page becomes its default language version, so the storefront keeps serving it unchanged.
    /// </summary>
    Task<MultilanguagePage> ConvertAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a translation as matching the current source language publication, e.g. when the source change needs no translation.
    /// </summary>
    Task<GroupedPageBuilderPage> MarkUpToDateAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Language table: every store language (default language first) with its version status,
    /// followed by versions whose language was removed from the store.
    /// </summary>
    Task<IList<MultilanguagePageLanguage>> GetLanguagesAsync(string multilanguagePageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a draft version for a missing store language. With <paramref name="copyContent"/> the draft starts from the
    /// content of the default language version.
    /// </summary>
    Task<GroupedPageBuilderPage> CreateLanguageAsync(string multilanguagePageId, string cultureName, bool copyContent, CancellationToken cancellationToken = default);

    /// <summary>Publishes the draft of every language version that has one.</summary>
    Task<MultilanguagePageBatchResult> PublishAllAsync(string multilanguagePageId, CancellationToken cancellationToken = default);

    /// <summary>Archives every language version.</summary>
    Task<MultilanguagePageBatchResult> ArchiveAllAsync(string multilanguagePageId, CancellationToken cancellationToken = default);
}
