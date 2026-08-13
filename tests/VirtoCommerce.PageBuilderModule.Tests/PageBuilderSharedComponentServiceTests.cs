using System.Collections.Generic;
using System.IO;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderSharedComponentServiceTests
{
    [Fact]
    public void ValidateAndNormalize_TrimsNameAndRejectsNamesOverSharedLimit()
    {
        var valid = new PageBuilderSharedComponent
        {
            StoreId = "store",
            Name = $"  {new string('x', ModuleConstants.SharedComponents.NameMaxLength)}  ",
        };

        PageBuilderSharedComponentService.ValidateAndNormalize(valid);

        Assert.Equal(ModuleConstants.SharedComponents.NameMaxLength, valid.Name.Length);
        Assert.Throws<InvalidDataException>(() =>
            PageBuilderSharedComponentService.ValidateAndNormalize(new PageBuilderSharedComponent
            {
                StoreId = "store",
                Name = new string('x', ModuleConstants.SharedComponents.NameMaxLength + 1),
            }));
    }

    [Fact]
    public void ValidateStoreImmutability_RejectsMovingExistingComponent()
    {
        var component = new PageBuilderSharedComponent
        {
            Id = "component",
            StoreId = "new-store",
            Name = "Shared header",
        };

        var exception = Assert.Throws<InvalidDataException>(() =>
            PageBuilderSharedComponentService.ValidateStoreImmutability(
                [component],
                new Dictionary<string, string> { [component.Id] = "original-store" }));

        Assert.Contains("cannot be moved", exception.Message);
    }
}
