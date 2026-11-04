using System.Security.Claims;

namespace VirtoCommerce.PageBuilderModule.Core.Theme;

/// <summary>Puts a store's theme drafts live at once, as one publication.</summary>
public interface IThemePublisher
{
    /// <summary>
    /// Validates the theme against the catalog <paramref name="catalogHash"/> names, writes the snapshots, moves the
    /// pointer, then writes the drafts live. Throws <see cref="ThemeValidationException"/> before writing anything when
    /// it names no catalog, or a file does not fit it.
    /// </summary>
    /// <returns>The publication number.</returns>
    Task<int> PublishAsync(string storeId, string catalogHash, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}
