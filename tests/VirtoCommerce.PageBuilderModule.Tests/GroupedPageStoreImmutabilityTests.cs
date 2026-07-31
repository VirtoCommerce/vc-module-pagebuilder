using System;
using System.Collections.Generic;
using System.IO;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class GroupedPageStoreImmutabilityTests
{
    [Fact]
    public void ValidateStoreImmutability_RejectsReferencedGroupStoreChange()
    {
        var group = new GroupedPageBuilderPage { Id = "group", StoreId = "store-b" };

        var exception = Assert.Throws<InvalidDataException>(() =>
            GroupedPageService.ValidateStoreImmutability(
                [group],
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["group"] = "store-a",
                },
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "group" }));

        Assert.Contains("cannot be moved", exception.Message);
    }

    [Fact]
    public void ValidateStoreImmutability_AllowsLegacyGroupWithoutLinkedComponentsToMove()
    {
        GroupedPageService.ValidateStoreImmutability(
            [new GroupedPageBuilderPage { Id = "group", StoreId = "store-b" }],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["group"] = "store-a",
            },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void SynchronizeMovedPageStores_UpdatesEveryVariant()
    {
        var group = new GroupedPageBuilderPage
        {
            Id = "group",
            StoreId = "store-b",
            Pages =
            [
                new PageBuilderPage { Id = "draft", StoreId = "store-a" },
                new PageBuilderPage { Id = "published", StoreId = "store-a" },
            ],
        };

        GroupedPageService.SynchronizeMovedPageStores(
            [group],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["group"] = "store-a",
            });

        Assert.All(group.Pages, page => Assert.Equal("store-b", page.StoreId));
    }

    [Fact]
    public void ValidateStoreImmutability_AllowsSameStoreCaseInsensitively()
    {
        GroupedPageService.ValidateStoreImmutability(
            [new GroupedPageBuilderPage { Id = "group", StoreId = "STORE" }],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["group"] = "store",
            },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "group" });
    }
}
