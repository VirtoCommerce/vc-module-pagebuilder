using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Tests;

internal sealed class FakeAuthorizationService(bool allowed) : IAuthorizationService
{
    private readonly string _storeId;
    private readonly string[] _permissions = [];

    public FakeAuthorizationService(string storeId, params string[] permissions)
        : this(allowed: false)
    {
        _storeId = storeId;
        _permissions = permissions;
    }

    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
        CreateResult(_storeId == null ? allowed : resource is IHasStoreId store && store.StoreId == _storeId);

    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName) =>
        CreateResult(_storeId == null ? allowed : _permissions.Contains(policyName));

    private static Task<AuthorizationResult> CreateResult(bool succeeded) =>
        Task.FromResult(succeeded ? AuthorizationResult.Success() : AuthorizationResult.Failed());
}
