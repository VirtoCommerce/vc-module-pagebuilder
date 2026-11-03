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
    /// <summary>
    /// Store access: an administrator reaches every store, a user bound to a store reaches only that store,
    /// and a user with no store on the account is let through only where the caller allows it.
    /// </summary>
    public class PageBuilderAuthorizationHandlerTests
    {
        private static readonly PageBuilderAuthorizationRequirement Default = new();
        private static readonly PageBuilderAuthorizationRequirement AllowingUsersWithoutStore = new() { AllowUsersWithoutStore = true };

        [Fact]
        public async Task UserWithoutStore_IsLetThrough_WhereAllowed()
        {
            Assert.True(await IsAuthorizedAsync(new ApplicationUser { StoreId = null }, "s1", AllowingUsersWithoutStore));
            Assert.True(await IsAuthorizedAsync(new ApplicationUser { StoreId = "" }, "s1", AllowingUsersWithoutStore));
        }

        [Fact]
        public async Task UserWithoutStore_IsRefused_ByDefault()
        {
            Assert.False(await IsAuthorizedAsync(new ApplicationUser { StoreId = null }, "s1", Default));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task UserBoundToAStore_ReachesOnlyThatStore(bool allowUsersWithoutStore)
        {
            var requirement = new PageBuilderAuthorizationRequirement { AllowUsersWithoutStore = allowUsersWithoutStore };
            var user = new ApplicationUser { StoreId = "s1" };

            Assert.True(await IsAuthorizedAsync(user, "S1", requirement));
            Assert.False(await IsAuthorizedAsync(user, "s2", requirement));
        }

        [Fact]
        public async Task UnknownUser_IsRefused()
        {
            Assert.False(await IsAuthorizedAsync(null, "s1", AllowingUsersWithoutStore));
        }

        [Fact]
        public async Task Administrator_ReachesEveryStore()
        {
            Assert.True(await IsAuthorizedAsync(new ApplicationUser { StoreId = "s1" }, "s2", Default, administrator: true));
        }

        private static async Task<bool> IsAuthorizedAsync(ApplicationUser user, string storeId, PageBuilderAuthorizationRequirement requirement, bool administrator = false)
        {
            Claim[] claims = administrator ? [new Claim(ClaimTypes.Role, PlatformConstants.Security.SystemRoles.Administrator)] : [];
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
            var context = new AuthorizationHandlerContext([requirement], principal, new PageBuilderPageSearchCriteria { StoreId = storeId });

            var handler = new PageBuilderAuthorizationHandler(() => new UserManager<ApplicationUser>(new SingleUserStore(user), null, null, null, null, null, null, null, null));
            await handler.HandleAsync(context);

            return context.HasSucceeded;
        }

        /// <summary>Answers every lookup with the one user the test is about.</summary>
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
