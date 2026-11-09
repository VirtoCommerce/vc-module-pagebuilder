using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace VirtoCommerce.PageBuilderModule.Web.Extensions;

public static class ValidatorExtensions
{
    /// <summary>Returns <c>null</c> for a valid request.</summary>
    public static async Task<BadRequestObjectResult> ValidateRequestAsync<T>(this IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);

        return validationResult.IsValid
            ? null
            : new BadRequestObjectResult(new { error = string.Join(" ", validationResult.Errors.Select(x => x.ErrorMessage)) });
    }

    /// <summary>Treats what MVC could not bind as the caller's mistake too, where MVC would pass the parameter's default on.</summary>
    public static Task<BadRequestObjectResult> ValidateRequestAsync<T>(this IValidator<T> validator, T request, ModelStateDictionary modelState,
        CancellationToken cancellationToken)
    {
        var bindingErrors = modelState?.Values
            .SelectMany(entry => entry.Errors)
            .Select(error => string.IsNullOrEmpty(error.ErrorMessage) ? "The request could not be read." : error.ErrorMessage)
            .ToList() ?? [];

        return bindingErrors.Count > 0
            ? Task.FromResult(new BadRequestObjectResult(new { error = string.Join(" ", bindingErrors) }))
            : validator.ValidateRequestAsync(request, cancellationToken);
    }
}
