using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Authorization;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

/// <summary>
/// The order a request about a store is checked in: access to the store first, so that someone outside it learns
/// nothing about what it holds, then the request itself. <c>null</c> lets the request through.
/// </summary>
internal static class StoreRequestCheck
{
    public static async Task<ActionResult> CheckStoreRequestAsync<TRequest>(this ControllerBase controller,
        IAuthorizationService authorizationService, IValidator<TRequest> validator, TRequest request)
        where TRequest : IHasStoreId =>
        await controller.CheckStoreAccessAsync(authorizationService, request) ?? await ValidateAsync(controller, validator, request);

    public static async Task<ActionResult> CheckStoreAccessAsync(this ControllerBase controller,
        IAuthorizationService authorizationService, IHasStoreId request)
    {
        var access = await authorizationService.AuthorizeAsync(controller.User, request, new PageBuilderAuthorizationRequirement());
        return access.Succeeded ? null : controller.Forbid();
    }

    private static async Task<ActionResult> ValidateAsync<TRequest>(ControllerBase controller, IValidator<TRequest> validator, TRequest request)
    {
        var validation = await validator.ValidateAsync(request, controller.HttpContext.RequestAborted);
        return validation.IsValid
            ? null
            : controller.BadRequest(new { error = string.Join(" ", validation.Errors.Select(x => x.ErrorMessage)) });
    }
}
