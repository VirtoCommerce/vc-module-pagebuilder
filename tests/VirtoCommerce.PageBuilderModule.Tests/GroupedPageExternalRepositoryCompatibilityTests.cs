using System;
using VirtoCommerce.PageBuilderModule.Data.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class GroupedPageExternalRepositoryCompatibilityTests
{
    [Fact]
    public void EnsureNonTransactionalContentSupported_AllowsOrdinaryLegacyContent()
    {
        var exception = Record.Exception(() =>
            GroupedPageService.EnsureNonTransactionalContentSupported(
                "{ \"settings\": {}, \"content\": [{ \"id\": \"hero\", \"type\": \"hero\" }] }",
                "{ \"settings\": {}, \"content\": [] }"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureNonTransactionalContentSupported_RejectsSharedComponentContent()
    {
        var content =
            "{ \"settings\": {}, \"content\": [{ \"id\": \"placement\", \"type\": \"componentRef\", \"componentRef\": \"component\" }] }";

        var exception = Assert.Throws<NotSupportedException>(() =>
            GroupedPageService.EnsureNonTransactionalContentSupported(content));

        Assert.Contains(nameof(VirtoCommerce.PageBuilderModule.Data.Repositories.ITransactionalContentStreamRepository), exception.Message);
    }
}
