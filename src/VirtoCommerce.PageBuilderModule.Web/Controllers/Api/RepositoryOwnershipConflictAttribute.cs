using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api
{
    /// <summary>
    /// A store working in a repository the module created for another store is a setting to fix, not a server error:
    /// answered with 409 and what to do, wherever the store's repository is asked for.
    /// </summary>
    public sealed class RepositoryOwnershipConflictAttribute : ExceptionFilterAttribute
    {
        public override void OnException(ExceptionContext context)
        {
            if (context.Exception is GitRepositoryOwnershipException conflict)
            {
                context.Result = new ConflictObjectResult(new { error = conflict.Message });
                context.ExceptionHandled = true;
            }
        }
    }
}
