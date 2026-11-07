using System.Security.Claims;
using Newtonsoft.Json.Linq;

namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public interface IThemePublisher
{
    /// <summary>Publishes the theme drafts of the store. Returns the publication number.</summary>
    /// <exception cref="ThemeValidationException">The theme does not match the catalog.</exception>
    Task<int> PublishAsync(string storeId, string catalogHash, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>Publishes the live theme files when the store has no publication yet; otherwise returns the current publication.</summary>
    Task<int> PublishFirstAsync(string storeId, CancellationToken cancellationToken = default);

    /// <summary>Gets the current publication and the theme files with a draft.</summary>
    Task<ThemeStatus> GetStatusAsync(string storeId, CancellationToken cancellationToken = default);

    /// <summary>Validates theme files against the catalog as a publication does. The files are keyed by their path in the theme.</summary>
    /// <exception cref="ThemeValidationException">There is no such catalog, or a file does not match it.</exception>
    Task ValidateFilesAsync(string storeId, string catalogHash, IDictionary<string, JToken> files, CancellationToken cancellationToken = default);
}
