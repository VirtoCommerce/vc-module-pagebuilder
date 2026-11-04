using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Security;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    public class PageBuilderAuthorizationHandlerTests
    {
        [Fact]
        public async Task UserWithoutStore_IsLetThrough()
        {
            Assert.True(await IsAuthorizedAsync(new ApplicationUser { StoreId = null }, "s1"));
            Assert.True(await IsAuthorizedAsync(new ApplicationUser { StoreId = "" }, "s1"));
        }

        [Fact]
        public async Task UserBoundToAStore_ReachesOnlyThatStore()
        {
            var user = new ApplicationUser { StoreId = "s1" };

            Assert.True(await IsAuthorizedAsync(user, "S1"));
            Assert.False(await IsAuthorizedAsync(user, "s2"));
        }

        [Fact]
        public async Task UnknownUser_IsRefused()
        {
            Assert.False(await IsAuthorizedAsync(null, "s1"));
        }

        [Fact]
        public async Task Administrator_ReachesEveryStore()
        {
            Assert.True(await IsAuthorizedAsync(new ApplicationUser { StoreId = "s1" }, "s2", administrator: true));
        }

        [Fact]
        public async Task AnotherHandler_CanGrantWhatThisOneRefuses()
        {
            Assert.True(await IsAuthorizedAsync(new ApplicationUser { StoreId = "s1" }, "s2", extraHandler: new GrantingHandler()));
        }

        private static async Task<bool> IsAuthorizedAsync(ApplicationUser user, string storeId, bool administrator = false, IAuthorizationHandler extraHandler = null)
        {
            var requirement = new PageBuilderAuthorizationRequirement();
            Claim[] claims = administrator ? [new Claim(ClaimTypes.Role, PlatformConstants.Security.SystemRoles.Administrator)] : [];
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
            var context = new AuthorizationHandlerContext([requirement], principal, new PageBuilderPageSearchCriteria { StoreId = storeId });

            var handler = new PageBuilderAuthorizationHandler(() => new UserManager<ApplicationUser>(new SingleUserStore(user), null, null, null, null, null, null, null, null));
            await handler.HandleAsync(context);
            if (extraHandler != null)
            {
                await extraHandler.HandleAsync(context);
            }

            return context.HasSucceeded;
        }

        private sealed class GrantingHandler : AuthorizationHandler<PageBuilderAuthorizationRequirement>
        {
            protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PageBuilderAuthorizationRequirement requirement)
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
        }

        private sealed class SingleUserStore(ApplicationUser storedUser) : IUserStore<ApplicationUser>
        {
            public Task<ApplicationUser> FindByIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(storedUser);

            public Task<ApplicationUser> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => Task.FromResult(storedUser);

            public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id);

            public Task<string> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);

            public Task<string> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedUserName);

            public Task SetUserNameAsync(ApplicationUser user, string userName, CancellationToken cancellationToken) => Task.CompletedTask;

            public Task SetNormalizedUserNameAsync(ApplicationUser user, string normalizedName, CancellationToken cancellationToken) => Task.CompletedTask;

            public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);

            public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);

            public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);

            public void Dispose()
            {
            }
        }
    }
}
