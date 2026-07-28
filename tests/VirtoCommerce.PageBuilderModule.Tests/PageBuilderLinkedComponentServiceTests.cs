using System.Collections.Generic;
using System.IO;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderLinkedComponentServiceTests
{
    [Fact]
    public void ValidateAndNormalize_TrimsNameAndRejectsNamesOverSharedLimit()
    {
        var valid = new PageBuilderLinkedComponent
        {
            StoreId = "store",
            Name = $"  {new string('x', ModuleConstants.LinkedComponents.NameMaxLength)}  ",
        };

        PageBuilderLinkedComponentService.ValidateAndNormalize(valid);

        Assert.Equal(ModuleConstants.LinkedComponents.NameMaxLength, valid.Name.Length);
        Assert.Throws<InvalidDataException>(() =>
            PageBuilderLinkedComponentService.ValidateAndNormalize(new PageBuilderLinkedComponent
            {
                StoreId = "store",
                Name = new string('x', ModuleConstants.LinkedComponents.NameMaxLength + 1),
            }));
    }

    [Fact]
    public void ValidateStoreImmutability_RejectsMovingExistingComponent()
    {
        var component = new PageBuilderLinkedComponent
        {
            Id = "component",
            StoreId = "new-store",
            Name = "Shared header",
        };

        var exception = Assert.Throws<InvalidDataException>(() =>
            PageBuilderLinkedComponentService.ValidateStoreImmutability(
                [component],
                new Dictionary<string, string> { [component.Id] = "original-store" }));

        Assert.Contains("cannot be moved", exception.Message);
    }
}
