using System.Security.Claims;

namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public interface IThemePublisher
{
    /// <summary>Publishes the theme drafts of the store. Returns the publication number.</summary>
    /// <exception cref="ThemeValidationException">The theme does not match the catalog.</exception>
    Task<int> PublishAsync(string storeId, string catalogHash, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>Publishes the live theme files when the store has no publication yet; otherwise returns the current publication.</summary>
    Task<int> PublishFirstAsync(string storeId, CancellationToken cancellationToken = default);
}
