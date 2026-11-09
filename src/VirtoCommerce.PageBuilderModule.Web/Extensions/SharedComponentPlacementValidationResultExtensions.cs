using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Web.Extensions;

public static class SharedComponentPlacementValidationResultExtensions
{
    public static ActionResult ToActionResult(this SharedComponentPlacementValidationResult validation)
    {
        if (validation.IsForbidden)
        {
            return new ForbidResult();
        }

        return validation.Error != null ? new BadRequestObjectResult(new { error = validation.Error }) : null;
    }
}
