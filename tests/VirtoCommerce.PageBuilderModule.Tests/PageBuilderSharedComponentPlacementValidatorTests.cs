using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.PageBuilderModule.Data.Services;
using VirtoCommerce.StoreModule.Core.Model;
using Xunit;
using Permissions = VirtoCommerce.PageBuilderModule.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderSharedComponentPlacementValidatorTests
{
    private const string Placement = """{ "settings": {}, "content": [{ "id": "p", "type": "componentRef", "componentRef": "c1" }] }""";
    private const string NoPlacement = """{ "settings": {}, "content": [{ "id": "a", "type": "text" }] }""";

    private readonly RecordingReferenceIndexService _index = new();

    [Fact]
    public async Task ValidateAsync_PlacementWithoutSharedComponentsRead_IsForbiddenWithoutValidatingTheReferences()
    {
        var result = await Validator(permission: Permissions.Update).ValidateAsync(new ClaimsPrincipal(), "s1", Placement, TestContext.Current.CancellationToken);

        Assert.True(result.IsForbidden);
        Assert.False(result.Succeeded);
        Assert.Empty(_index.Stores);
    }

    [Fact]
    public async Task ValidateAsync_SharedComponentsReadInAnotherStore_IsForbidden()
    {
        var result = await Validator(storeId: "s2").ValidateAsync(new ClaimsPrincipal(), "s1", Placement, TestContext.Current.CancellationToken);

        Assert.True(result.IsForbidden);
        Assert.Empty(_index.Stores);
    }

    [Fact]
    public async Task ValidateAsync_SharedComponentsReadScopedToTheStore_ValidatesTheReferencesForTheStore()
    {
        var result = await Validator().ValidateAsync(new ClaimsPrincipal(), "s1", Placement, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(["s1"], _index.Stores);
    }

    [Fact]
    public async Task ValidateAsync_NoPlacement_NeedsNoSharedComponentsRead()
    {
        var result = await Validator(permission: Permissions.Update).ValidateAsync(new ClaimsPrincipal(), "s1", NoPlacement, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ValidateAsync_ReferenceOfAnotherStore_ReturnsItsError()
    {
        _index.Error = "Shared Component 'c1' was not found.";

        var result = await Validator().ValidateAsync(new ClaimsPrincipal(), "s1", Placement, TestContext.Current.CancellationToken);

        Assert.False(result.IsForbidden);
        Assert.Equal("Shared Component 'c1' was not found.", result.Error);
    }

    private PageBuilderSharedComponentPlacementValidator Validator(string storeId = "s1", string permission = Permissions.SharedComponentsRead) =>
        new(_index, new StoreScopedAuthorization(storeId, permission));

    /// <summary>A role holding one permission for one store: a named policy, which sees only global permissions, refuses it.</summary>
    private sealed class StoreScopedAuthorization(string storeId, string permission) : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            var isGranted = resource is Store store && store.Id == storeId &&
                            requirements.OfType<PageBuilderAuthorizationRequirement>().Single().Permission == permission;

            return Task.FromResult(isGranted ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName) =>
            Task.FromResult(AuthorizationResult.Failed());
    }

    private sealed class RecordingReferenceIndexService : NoopSharedComponentReferenceIndexService
    {
        public List<string> Stores { get; } = [];

        public string Error { get; set; }

        public override Task ValidateReferencesForStoreAsync(string storeId, IEnumerable<string> contents, CancellationToken cancellationToken = default)
        {
            Stores.Add(storeId);
            return Error == null ? Task.CompletedTask : throw new InvalidDataException(Error);
        }
    }
}
