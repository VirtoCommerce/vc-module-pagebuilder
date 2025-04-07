using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.PageBuilderModule.Data.Authorization
{
    public sealed class PageBuilderAuthorizationHandler : AuthorizationHandler<PageBuilderAuthorizationRequirement>
    {
        private readonly Func<UserManager<ApplicationUser>> _userManagerFactory;

        public PageBuilderAuthorizationHandler(Func<UserManager<ApplicationUser>> userManagerFactory)
        {
            _userManagerFactory = userManagerFactory;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PageBuilderAuthorizationRequirement requirement)
        {
            var result = context.User.IsInRole(PlatformConstants.Security.SystemRoles.Administrator);

            if (!result)
            {
                var user = await GetUser(context);
                if (user == null)
                {
                    context.Fail();
                }

                switch (context.Resource)
                {
                    case IHasStoreId hasStoreId:
                        result = hasStoreId.StoreId.EqualsInvariant(user.StoreId);
                        break;
                    case IEnumerable<IHasStoreId> hasStoreIds:
                        result = hasStoreIds.All(x => x.StoreId.EqualsInvariant(user.StoreId));
                        break;
                }
            }

            if (result)
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail();
            }
        }

        private async Task<ApplicationUser> GetUser(AuthorizationHandlerContext context)
        {
            var userId = context.User.GetUserId();

            using var userManager = _userManagerFactory();
            var user = await userManager.FindByIdAsync(userId);
            return user;
        }
    }
}

