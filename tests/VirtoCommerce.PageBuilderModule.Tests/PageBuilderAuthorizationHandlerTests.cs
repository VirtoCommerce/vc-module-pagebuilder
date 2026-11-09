using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.JsonConverters;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Security.Authorization;
using VirtoCommerce.StoreModule.Core.Security;
using Xunit;
using Permissions = VirtoCommerce.PageBuilderModule.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    public class PageBuilderAuthorizationHandlerTests
    {
        private static readonly MvcNewtonsoftJsonOptions JsonOptions = CreateJsonOptions();

        [Fact]
        public async Task UserWithoutStore_ReadEverywhere_ReachesEveryStore()
        {
            Assert.True(await IsAuthorizedAsync(new ApplicationUser { StoreId = null }, "s1", Read()));
            Assert.True(await IsAuthorizedAsync(new ApplicationUser { StoreId = "" }, "s2", Read()));
        }

        [Fact]
        public async Task UserWithoutStore_NoRead_IsRefused()
        {
            Assert.False(await IsAuthorizedAsync(new ApplicationUser(), "s1", Permission(Permissions.Update)));
        }

        [Fact]
        public async Task ReadScopedToStores_ReachesOnlyThoseStores()
        {
            var claims = Read("s1", "s3");

            Assert.True(await IsAuthorizedAsync(new ApplicationUser(), "S1", claims));
            Assert.False(await IsAuthorizedAsync(new ApplicationUser(), "s2", claims));
            Assert.True(await IsAuthorizedAsync(new ApplicationUser(), Pages("s1", "s3"), claims));
            Assert.False(await IsAuthorizedAsync(new ApplicationUser(), Pages("s1", "s2"), claims));
        }

        [Fact]
        public async Task ReadScopedToStores_ResourceWithoutStore_IsRefused()
        {
            Assert.False(await IsAuthorizedAsync(new ApplicationUser(), new PageBuilderPageSearchCriteria(), Read("s1")));
            Assert.False(await IsAuthorizedAsync(new ApplicationUser(), new object(), Read("s1")));
        }

        [Fact]
        public async Task ReadEverywhere_ResourceWithoutStore_IsLetThrough()
        {
            Assert.True(await IsAuthorizedAsync(new ApplicationUser(), new object(), Read()));
        }

        [Fact]
        public async Task ScopeOfAnotherPermission_DoesNotCount()
        {
            Assert.False(await IsAuthorizedAsync(new ApplicationUser(), "s1", Permission(Permissions.Update, "s1")));
        }

        [Fact]
        public async Task RequirementOfAnotherPermission_TakesItsOwnScope()
        {
            var requirement = new PageBuilderAuthorizationRequirement(Permissions.SharedComponentsRead);
            var claims = Permission(Permissions.SharedComponentsRead, "s1");

            Assert.True(await IsAuthorizedAsync(new ApplicationUser(), "s1", claims, requirement: requirement));
            Assert.False(await IsAuthorizedAsync(new ApplicationUser(), "s1", claims));
        }

        [Fact]
        public async Task UserBoundToAStore_ReachesOnlyThatStore()
        {
            var user = new ApplicationUser { StoreId = "s1" };

            Assert.True(await IsAuthorizedAsync(user, "S1", Read()));
            Assert.False(await IsAuthorizedAsync(user, "s2", Read()));
            Assert.False(await IsAuthorizedAsync(user, "s2", Read("s2")));
        }

        [Fact]
        public async Task UserBoundToAStore_PlatformDefaultHandlerRunsToo_AnotherStoreStaysRefused()
        {
            var user = new ApplicationUser { StoreId = "s1" };

            Assert.False(await IsAuthorizedAsync(user, "s2", Read(), extraHandler: new DefaultPermissionAuthorizationHandler()));
        }

        [Fact]
        public async Task UserBoundToAStore_NoRead_IsRefused()
        {
            Assert.False(await IsAuthorizedAsync(new ApplicationUser { StoreId = "s1" }, "s1", []));
        }

        [Fact]
        public async Task UnknownUser_IsRefused()
        {
            Assert.False(await IsAuthorizedAsync(null, "s1", Read()));
        }

        [Fact]
        public async Task Administrator_ReachesEveryStore()
        {
            Assert.True(await IsAuthorizedAsync(new ApplicationUser { StoreId = "s1" }, "s2", [], administrator: true));
        }

        [Fact]
        public async Task AnotherHandler_CanGrantWhatThisOneRefuses()
        {
            Assert.True(await IsAuthorizedAsync(new ApplicationUser { StoreId = "s1" }, "s2", Read(), extraHandler: new GrantingHandler()));
        }

        private static Claim[] Read(params string[] storeIds) => Permission(Permissions.Read, storeIds);

        private static Claim[] Permission(string name, params string[] storeIds)
        {
            var permission = new Permission
            {
                Name = name,
                AssignedScopes = storeIds.Select(x => (PermissionScope)new StoreSelectedScope { Scope = x }).ToList(),
            };

            return [permission.ToClaim(JsonOptions.SerializerSettings)];
        }

        private static GroupedPageBuilderPage[] Pages(params string[] storeIds) =>
            storeIds.Select(x => new GroupedPageBuilderPage { StoreId = x }).ToArray();

        private static Task<bool> IsAuthorizedAsync(ApplicationUser user, string storeId, Claim[] claims, bool administrator = false,
            IAuthorizationHandler extraHandler = null, PageBuilderAuthorizationRequirement requirement = null) =>
            IsAuthorizedAsync(user, new PageBuilderPageSearchCriteria { StoreId = storeId }, claims, administrator, extraHandler, requirement);

        private static async Task<bool> IsAuthorizedAsync(ApplicationUser user, object resource, Claim[] claims, bool administrator = false,
            IAuthorizationHandler extraHandler = null, PageBuilderAuthorizationRequirement requirement = null)
        {
            requirement ??= new PageBuilderAuthorizationRequirement();
            Claim[] roles = administrator ? [new Claim(ClaimTypes.Role, PlatformConstants.Security.SystemRoles.Administrator)] : [];
            var principal = new ClaimsPrincipal(new ClaimsIdentity([.. claims, .. roles], "test"));
            var context = new AuthorizationHandlerContext([requirement], principal, resource);

            var handler = new PageBuilderAuthorizationHandler(
                () => new UserManager<ApplicationUser>(new SingleUserStore(user), null, null, null, null, null, null, null, null),
                Options.Create(JsonOptions));
            await handler.HandleAsync(context);
            if (extraHandler != null)
            {
                await extraHandler.HandleAsync(context);
            }

            return context.HasSucceeded;
        }

        // as the platform reads permission scopes from the claims
        private static MvcNewtonsoftJsonOptions CreateJsonOptions()
        {
            AbstractTypeFactory<PermissionScope>.RegisterType<StoreSelectedScope>();
            PolymorphJsonConverter.RegisterTypeForDiscriminator(typeof(PermissionScope), nameof(PermissionScope.Type));

            var options = new MvcNewtonsoftJsonOptions();
            options.SerializerSettings.Converters.Add(new PolymorphJsonConverter());
            options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
            return options;
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
