using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Data.Services;
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
        var result = await Validator(Permissions.Update).ValidateAsync(new ClaimsPrincipal(), "s1", Placement, TestContext.Current.CancellationToken);

        Assert.True(result.IsForbidden);
        Assert.False(result.Succeeded);
        Assert.Empty(_index.Stores);
    }

    [Fact]
    public async Task ValidateAsync_PlacementWithSharedComponentsRead_ValidatesTheReferencesForTheStore()
    {
        var result = await Validator(Permissions.SharedComponentsRead).ValidateAsync(new ClaimsPrincipal(), "s1", Placement, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(["s1"], _index.Stores);
    }

    [Fact]
    public async Task ValidateAsync_NoPlacement_NeedsNoSharedComponentsRead()
    {
        var result = await Validator().ValidateAsync(new ClaimsPrincipal(), "s1", NoPlacement, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ValidateAsync_ReferenceOfAnotherStore_ReturnsItsError()
    {
        _index.Error = "Shared Component 'c1' was not found.";

        var result = await Validator(Permissions.SharedComponentsRead).ValidateAsync(new ClaimsPrincipal(), "s1", Placement, TestContext.Current.CancellationToken);

        Assert.False(result.IsForbidden);
        Assert.Equal("Shared Component 'c1' was not found.", result.Error);
    }

    private PageBuilderSharedComponentPlacementValidator Validator(params string[] permissions) =>
        new(_index, new FakeAuthorizationService("s1", permissions));

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
