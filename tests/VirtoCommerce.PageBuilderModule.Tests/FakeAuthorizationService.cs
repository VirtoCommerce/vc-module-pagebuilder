using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.StoreModule.Core.Model;
using Permissions = VirtoCommerce.PageBuilderModule.Core.ModuleConstants.Security.Permissions;

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
        CreateResult(_storeId == null ? allowed : IsInStore(resource) && requirements.All(IsGranted));

    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName) =>
        CreateResult(_storeId == null ? allowed : _permissions.Contains(policyName));

    private bool IsInStore(object resource) => resource switch
    {
        Store store => store.Id == _storeId,
        IHasStoreId hasStoreId => hasStoreId.StoreId == _storeId,
        _ => false,
    };

    private bool IsGranted(IAuthorizationRequirement requirement) =>
        requirement is not PageBuilderAuthorizationRequirement storeRequirement ||
        storeRequirement.Permission == Permissions.Read ||
        _permissions.Contains(storeRequirement.Permission);

    private static Task<AuthorizationResult> CreateResult(bool succeeded) =>
        Task.FromResult(succeeded ? AuthorizationResult.Success() : AuthorizationResult.Failed());
}
