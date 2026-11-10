using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Data.Authorization;

namespace VirtoCommerce.PageBuilderModule.Web.Extensions;

public static class AuthorizationServiceExtensions
{
    /// <summary>Returns <c>null</c> when the user holds the permission in the store.</summary>
    public static async Task<ActionResult> CheckStoreAccessAsync(this IAuthorizationService authorizationService, ClaimsPrincipal user, object resource,
        string permission = ModuleConstants.Security.Permissions.Read)
    {
        var authorizationResult = await authorizationService.AuthorizeAsync(user, resource, new PageBuilderAuthorizationRequirement(permission));
        return authorizationResult.Succeeded ? null : new ForbidResult();
    }

    /// <summary>Checks store access before the request, so a 400 does not reveal which stores exist. Returns <c>null</c> for a request to go on with.</summary>
    public static async Task<ActionResult> CheckStoreRequestAsync<T>(this IAuthorizationService authorizationService, ClaimsPrincipal user,
        IValidator<T> validator, T request, ModelStateDictionary modelState, CancellationToken cancellationToken)
    {
        return await authorizationService.CheckStoreAccessAsync(user, request)
               ?? await validator.ValidateRequestAsync(request, modelState, cancellationToken);
    }
}
