using System.Collections.Generic;
using System.IO;
using VirtoCommerce.PageBuilderModule.Data.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderLinkedComponentReferenceIndexServiceTests
{
    [Fact]
    public void ValidateComponentReferences_RejectsMetadataOrphanWithoutContentRow()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            PageBuilderLinkedComponentReferenceIndexService.ValidateComponentReferences(
                ["component"],
                "store",
                new Dictionary<string, string> { ["component"] = "store" },
                new HashSet<string>()));

        Assert.Contains("has no content", exception.Message);
    }
}
