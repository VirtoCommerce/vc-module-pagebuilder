using System;
using System.Text;
using VirtoCommerce.PageBuilderModule.Core.Models;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderContentVersionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(4095)]
    [InlineData(8191)]
    [InlineData(100000)]
    public void StreamedGetAndStringSaveUseTheSameVersionForUnicodeContent(int prefixLength)
    {
        var group = new GroupedPageBuilderPage { Id = "group", StoreId = "store", CreatedDate = new DateTime(2026, 1, 1) };
        var content = new string('a', prefixLength) + "😀 Кириллица 日本語 \"quoted\"\r\n\\";

        Assert.Equal(PageBuilderContentVersion.Create(group, content),
            PageBuilderContentVersion.CreateFromUtf8(group, Encoding.UTF8.GetBytes(content)));
    }

    [Fact]
    public void EmptyDocumentHasTheSameVersionInBothPathsAndDiffersFromAnUnseededGroup()
    {
        var group = new GroupedPageBuilderPage { Id = "group", StoreId = "store" };

        Assert.Equal(PageBuilderContentVersion.Create(group, ""), PageBuilderContentVersion.CreateFromUtf8(group, []));
        Assert.NotEqual(PageBuilderContentVersion.Create(group, null), PageBuilderContentVersion.CreateFromUtf8(group, []));
    }
}
