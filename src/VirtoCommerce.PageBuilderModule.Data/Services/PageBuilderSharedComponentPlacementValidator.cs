using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Model;

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
            if (PageBuilderSharedComponentReferenceMatcher.HasReferences(content) && !await CanReadSharedComponentsAsync(user, storeId))
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

    protected virtual async Task<bool> CanReadSharedComponentsAsync(ClaimsPrincipal user, string storeId)
    {
        var store = AbstractTypeFactory<Store>.TryCreateInstance();
        store.Id = storeId;

        var authorizationResult = await authorizationService.AuthorizeAsync(user, store,
            new PageBuilderAuthorizationRequirement(ModuleConstants.Security.Permissions.SharedComponentsRead));
        return authorizationResult.Succeeded;
    }
}
