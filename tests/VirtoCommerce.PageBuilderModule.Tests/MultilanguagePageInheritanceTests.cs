using System;
using VirtoCommerce.PageBuilderModule.Core.Models;
using Xunit;
using static VirtoCommerce.PageBuilderModule.Core.Models.MultilanguagePageInheritance;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class MultilanguagePageInheritanceTests
{
    [Fact]
    public void Apply_CopiesNameAndInheritedFields_KeepsOverriddenFields()
    {
        var root = CreateRoot();
        var group = new GroupedPageBuilderPage
        {
            Name = "Old name",
            Permalink = "/over-ons",
            Visibility = false,
            StartDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            OverriddenFields = "Permalink",
        };

        var changed = Apply(root, group);

        Assert.True(changed);
        Assert.Equal(root.Name, group.Name);
        Assert.Equal("/over-ons", group.Permalink);
        Assert.Equal(root.Visibility, group.Visibility);
        Assert.Equal(root.UserGroups, group.UserGroups);
        Assert.Equal(root.OrganizationId, group.OrganizationId);
        Assert.Equal(root.StartDate, group.StartDate);
        Assert.Equal(root.EndDate, group.EndDate);
    }

    [Fact]
    public void Apply_OverriddenEmptyValue_IsKeptEmpty()
    {
        var root = CreateRoot();
        var group = new GroupedPageBuilderPage { EndDate = null, OverriddenFields = "EndDate" };

        Apply(root, group);

        Assert.Null(group.EndDate);
    }

    [Fact]
    public void Apply_NothingDiffers_ReturnsFalse()
    {
        var root = CreateRoot();
        var group = new GroupedPageBuilderPage();
        Apply(root, group);

        Assert.False(Apply(root, group));
    }

    [Fact]
    public void SetOverriddenFields_NormalizesCaseOrderAndUnknownFields()
    {
        var group = new GroupedPageBuilderPage();

        SetOverriddenFields(group, ["enddate", "Unknown", "permalink", "Permalink"]);

        Assert.Equal("Permalink,EndDate", group.OverriddenFields);
        Assert.True(IsOverridden(group, EndDate));
        Assert.False(IsOverridden(group, StartDate));
    }

    [Fact]
    public void SetOverriddenFields_Empty_StoresEmptyStringNotNull()
    {
        var group = new GroupedPageBuilderPage { OverriddenFields = "Permalink" };

        SetOverriddenFields(group, []);

        Assert.Equal(string.Empty, group.OverriddenFields);
    }

    [Fact]
    public void GetDifferingFields_ReturnsOnlyFieldsThatDiffer()
    {
        var root = CreateRoot();
        var group = new GroupedPageBuilderPage();
        Apply(root, group);
        group.Permalink = "/faq";
        group.UserGroups = "b2b";

        Assert.Equal([Permalink, UserGroups], GetDifferingFields(root, group));
    }

    [Fact]
    public void EmptyAndMissingText_AreTheSameValue()
    {
        var root = CreateRoot();
        root.UserGroups = null;
        root.OrganizationId = string.Empty;
        var group = new GroupedPageBuilderPage();
        Apply(root, group);
        // The page blade sends an empty string for a cleared selector.
        group.UserGroups = string.Empty;
        group.OrganizationId = null;

        Assert.Empty(GetDifferingFields(root, group));
        Assert.False(Apply(root, group));
    }

    private static MultilanguagePage CreateRoot()
    {
        return new MultilanguagePage
        {
            Name = "About us",
            Permalink = "/about-us",
            Visibility = true,
            UserGroups = "vip",
            OrganizationId = "org",
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        };
    }
}
