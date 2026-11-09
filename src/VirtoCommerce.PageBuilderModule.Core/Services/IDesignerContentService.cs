using System.Security.Claims;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

/// <summary>Writes the drafts of a page and of theme files together: all or nothing, one version of the history.</summary>
public interface IDesignerContentService
{
    /// <summary>Checks theme files a save writes against the store's theme rules and the schema catalog named.</summary>
    /// <param name="files">Contents by the path with the theme folder.</param>
    /// <exception cref="InvalidDataException">A live theme write on a store with the new Page Builder.</exception>
    /// <exception cref="Theme.ThemeValidationException">A file does not fit the catalog.</exception>
    Task ValidateThemeFilesAsync(string storeId, IDictionary<string, JToken> files, bool draft, string catalogHash,
        CancellationToken cancellationToken = default);

    /// <exception cref="PageBuilderContentConflictException">The page or a theme file was saved by someone else since it was read: nothing is written.</exception>
    Task<DesignerSaveResult> SaveAsync(DesignerSaveCommand command, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>Discards the drafts; a theme file never published keeps its draft.</summary>
    /// <exception cref="InvalidDataException">The database page has never been published.</exception>
    Task<ContentSaveResult> DiscardAsync(DesignerDiscardCommand command, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>Writes the items as of a version as their drafts.</summary>
    Task<ContentSaveResult> RestoreAsync(ContentRestoreCommand command, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}
