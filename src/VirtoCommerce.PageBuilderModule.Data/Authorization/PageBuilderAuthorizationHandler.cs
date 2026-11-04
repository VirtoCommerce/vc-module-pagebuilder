using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.PageBuilderModule.Data.Authorization
{
    public sealed class PageBuilderAuthorizationHandler(Func<UserManager<ApplicationUser>> userManagerFactory)
        : AuthorizationHandler<PageBuilderAuthorizationRequirement>
    {
        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PageBuilderAuthorizationRequirement requirement)
        {
            var result = context.User.IsInRole(PlatformConstants.Security.SystemRoles.Administrator);

            if (!result)
            {
                var user = await GetUser(context);
                if (user != null)
                {
                    // A back-office manager usually has no store on the account; the action's permission still guards them.
                    result = string.IsNullOrEmpty(user.StoreId) || context.Resource switch
                    {
                        IHasStoreId hasStoreId => hasStoreId.StoreId.EqualsIgnoreCase(user.StoreId),
                        IEnumerable<IHasStoreId> hasStoreIds => hasStoreIds.All(x => x.StoreId.EqualsIgnoreCase(user.StoreId)),
                        _ => false,
                    };
                }
            }

            // A refusal stays silent: failing here would override any other handler registered for this requirement.
            if (result)
            {
                context.Succeed(requirement);
            }
        }

        private async Task<ApplicationUser> GetUser(AuthorizationHandlerContext context)
        {
            var userId = context.User.GetUserId();

            using var userManager = userManagerFactory();
            var user = await userManager.FindByIdAsync(userId);
            return user;
        }
    }
}

