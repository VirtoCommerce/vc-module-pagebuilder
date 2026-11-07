using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderSharedComponentPlacementValidator(
    IPageBuilderSharedComponentReferenceIndexService referenceIndexService,
    IAuthorizationService authorizationService)
    : IPageBuilderSharedComponentPlacementValidator
{
    public virtual async Task<SharedComponentPlacementValidationResult> ValidateAsync(ClaimsPrincipal user, string storeId, string content,
        CancellationToken cancellationToken = default)
    {
        var result = AbstractTypeFactory<SharedComponentPlacementValidationResult>.TryCreateInstance();

        try
        {
            // a user who may not read shared components learns nothing about the store's ones
            if (PageBuilderSharedComponentReferenceMatcher.HasReferences(content) && !await CanReadSharedComponentsAsync(user))
            {
                result.IsForbidden = true;
                return result;
            }

            await referenceIndexService.ValidateReferencesForStoreAsync(storeId, [content], cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    protected virtual async Task<bool> CanReadSharedComponentsAsync(ClaimsPrincipal user)
    {
        var authorizationResult = await authorizationService.AuthorizeAsync(user, null, ModuleConstants.Security.Permissions.SharedComponentsRead);
        return authorizationResult.Succeeded;
    }
}
