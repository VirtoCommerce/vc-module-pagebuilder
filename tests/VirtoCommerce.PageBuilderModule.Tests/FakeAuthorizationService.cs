using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace VirtoCommerce.PageBuilderModule.Tests;

internal sealed class FakeAuthorizationService(bool allowed) : IAuthorizationService
{
    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
        Task.FromResult(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());

    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName) =>
        Task.FromResult(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());
}
