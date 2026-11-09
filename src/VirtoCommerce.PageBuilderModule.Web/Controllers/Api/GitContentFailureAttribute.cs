using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

/// <summary>Returns 502 Bad Gateway with the reason for <see cref="GitContentException"/>, as the history endpoints do.</summary>
public sealed class GitContentFailureAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is GitContentException failure)
        {
            context.Result = new ObjectResult(new { error = failure.Message }) { StatusCode = StatusCodes.Status502BadGateway };
            context.ExceptionHandled = true;
        }
    }
}
