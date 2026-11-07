using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace VirtoCommerce.PageBuilderModule.Web.Extensions;

public static class ValidatorExtensions
{
    /// <summary>Validates the request; returns a 400 with the errors, or null for a valid request.</summary>
    public static async Task<BadRequestObjectResult> ValidateRequestAsync<T>(this IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);

        return validationResult.IsValid
            ? null
            : new BadRequestObjectResult(new { error = string.Join(" ", validationResult.Errors.Select(x => x.ErrorMessage)) });
    }
}
