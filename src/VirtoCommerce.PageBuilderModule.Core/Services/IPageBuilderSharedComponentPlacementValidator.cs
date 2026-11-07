using System.Security.Claims;
using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderSharedComponentPlacementValidator
{
    /// <summary>Checks that the user may place shared components in page content and that each one it places belongs to the store.</summary>
    Task<SharedComponentPlacementValidationResult> ValidateAsync(ClaimsPrincipal user, string storeId, string content,
        CancellationToken cancellationToken = default);
}
