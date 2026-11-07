using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Web.Extensions;

public static class SharedComponentPlacementValidationResultExtensions
{
    /// <summary>Gets the 403 or the 400 <c>{ error }</c> of a refused placement; null for an accepted one.</summary>
    public static ActionResult ToActionResult(this SharedComponentPlacementValidationResult validation)
    {
        if (validation.IsForbidden)
        {
            return new ForbidResult();
        }

        return validation.Error != null ? new BadRequestObjectResult(new { error = validation.Error }) : null;
    }
}
