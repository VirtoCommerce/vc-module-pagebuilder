using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderAssetSearchServiceTests
{
    [Theory]
    [InlineData("image/*")]
    [InlineData("image/png")]
    [InlineData(".PNG")]
    public async Task SearchAsync_AcceptedTypesFilterBeforeTotalAndPagingAndKeepFolders(string acceptedType)
    {
        var (service, provider) = CreateService();
        provider.Listing.Results = Enumerable.Range(0, 20).Select(index => (BlobEntry)new BlobInfo
        {
            Name = $"a-{index:D3}.pdf", ContentType = "application/pdf",
        }).Concat(Enumerable.Range(0, 25).Select(index => (BlobEntry)new BlobInfo
        {
            Name = $"z-{index:D3}.png", ContentType = "image/png",
        })).Append(new BlobFolder { Name = "folder" }).ToList();
        var result = await service.SearchAsync(new PageBuilderAssetSearchCriteria
        {
            FolderUrl = "/folder", AcceptedTypes = [acceptedType], Skip = 20, Take = 20,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(26, result.TotalCount);
        Assert.Equal(6, result.Results.Count);
        Assert.Equal("z-019.png", result.Results[0].Name);
        var first = await service.SearchAsync(new PageBuilderAssetSearchCriteria
        {
            FolderUrl = "/folder", AcceptedTypes = [acceptedType], Take = 20,
        }, TestContext.Current.CancellationToken);
        Assert.Equal("folder", first.Results[0].Name);
        Assert.Equal("z-000.png", first.Results[1].Name);
    }

    [Fact]
    public async Task SearchAsync_AcceptedTypesResolveMissingMimeAndCombineExtensions()
    {
        var (service, provider) = CreateService();
        provider.Listing.Results = [
            new BlobInfo { Name = "photo.png" },
            new BlobInfo { Name = "document.PDF", ContentType = "application/pdf" },
            new BlobInfo { Name = "other.zip", ContentType = "application/zip" },
        ];
        var result = await service.SearchAsync(new PageBuilderAssetSearchCriteria
        {
            FolderUrl = "/folder", AcceptedTypes = [" IMAGE/* ", ".pdf"], Take = 20,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(["document.PDF", "photo.png"], result.Results.Select(x => x.Name));
        Assert.Equal("image/png", Assert.IsType<BlobInfo>(result.Results[1]).ContentType);
    }

    [Theory]
    [InlineData("photo.png")]
    [InlineData("photo.avif")]
    [InlineData("photo.apng")]
    public async Task SearchAsync_ImageWildcardPreservesPickerFilenameFallback(string name)
    {
        var (service, provider) = CreateService();
        provider.Listing.Results = [new BlobInfo { Name = name, ContentType = "application/octet-stream" }];
        var result = await service.SearchAsync(new PageBuilderAssetSearchCriteria
        {
            FolderUrl = "/folder", AcceptedTypes = ["image/*"], Take = 20,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(name, Assert.Single(result.Results).Name);
    }

    [Fact]
    public async Task SearchAsync_500Assets_ReturnsOnlyRequestedPageAndRealTotal()
    {
        var (service, provider) = CreateService();
        var result = await service.SearchAsync(new PageBuilderAssetSearchCriteria
        {
            FolderUrl = "/folder", Skip = 40, Take = 20, Sort = "name",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(500, result.TotalCount);
        Assert.Equal(20, result.Results.Count);
        Assert.Equal("asset-040.png", result.Results[0].Name);
        Assert.Equal("asset-059.png", result.Results[^1].Name);
        Assert.Equal(1, provider.ListCalls);
        Assert.Null(provider.Keyword);
    }

    [Fact]
    public async Task SearchAsync_SearchesAndSortsWholeLocationBeforePaging()
    {
        var (service, _) = CreateService();
        var result = await service.SearchAsync(new PageBuilderAssetSearchCriteria
        {
            FolderUrl = "/folder", Keyword = "asset-4", Skip = 20, Take = 20, Sort = "size:desc",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(100, result.TotalCount);
        Assert.Equal("asset-479.png", result.Results[0].Name);
        Assert.Equal("asset-460.png", result.Results[^1].Name);
    }

    [Fact]
    public async Task SearchAsync_ExactNameFindsConflictOutsideCurrentPage()
    {
        var (service, _) = CreateService();
        var result = await service.SearchAsync(new PageBuilderAssetSearchCriteria
        {
            FolderUrl = "/folder", ExactName = "ASSET-499.PNG", Take = 1,
        }, TestContext.Current.CancellationToken);

        Assert.Equal("asset-499.png", Assert.Single(result.Results).Name);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_BeyondLastPage_ReturnsEmptyPageWithRealTotal()
    {
        var (service, _) = CreateService();
        var result = await service.SearchAsync(new PageBuilderAssetSearchCriteria
        {
            FolderUrl = "/folder", Skip = 500, Take = 20,
        }, TestContext.Current.CancellationToken);

        Assert.Empty(result.Results);
        Assert.Equal(500, result.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_ExactNameUsesTheSameUnicodeAndWhitespaceNormalizationAsUploadChecks()
    {
        var (service, provider) = CreateService();
        provider.Listing.Results.Add(new BlobInfo { Name = " e\u0301.png ", RelativeUrl = "/folder/e\u0301.png" });
        var result = await service.SearchAsync(new PageBuilderAssetSearchCriteria
        {
            FolderUrl = "/folder", ExactName = "É.png", Take = 1,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(" e\u0301.png ", Assert.Single(result.Results).Name);
    }

    private static (PageBuilderAssetSearchService Service, BlobProviderProxy Provider) CreateService()
    {
        var provider = DispatchProxy.Create<IBlobStorageProvider, BlobProviderProxy>();
        var proxy = (BlobProviderProxy)provider;
        proxy.Listing = new BlobEntrySearchResult
        {
            TotalCount = 500,
            Results = Enumerable.Range(0, 500).Reverse().Select(index => (BlobEntry)new BlobInfo
            {
                Name = $"asset-{index:D3}.png", RelativeUrl = $"/folder/asset-{index:D3}.png", Size = index,
            }).ToList(),
        };
        return (new PageBuilderAssetSearchService(provider), proxy);
    }

    public class BlobProviderProxy : DispatchProxy
    {
        public BlobEntrySearchResult Listing { get; set; }
        public int ListCalls { get; private set; }
        public string Keyword { get; private set; }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            Assert.Equal(nameof(IBlobStorageProvider.SearchAsync), targetMethod.Name);
            Assert.Equal("/folder", args[0]);
            ListCalls++;
            Keyword = (string)args[1];
            return Task.FromResult(Listing);
        }
    }
}
