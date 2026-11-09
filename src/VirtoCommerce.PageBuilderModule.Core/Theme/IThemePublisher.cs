using System.Security.Claims;
using Newtonsoft.Json.Linq;

namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public interface IThemePublisher
{
    Task<int> PublishAsync(string storeId, string catalogHash, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>Publishes the live theme files when the store has no publication yet; otherwise returns the current publication.</summary>
    Task<int> PublishFirstAsync(string storeId, CancellationToken cancellationToken = default);

    Task<ThemeStatus> GetStatusAsync(string storeId, CancellationToken cancellationToken = default);

    /// <summary>The files are keyed by their path in the theme.</summary>
    /// <exception cref="ThemeValidationException">There is no such catalog, or a file does not match it.</exception>
    Task ValidateFilesAsync(string storeId, string catalogHash, IDictionary<string, JToken> files, CancellationToken cancellationToken = default);
}
