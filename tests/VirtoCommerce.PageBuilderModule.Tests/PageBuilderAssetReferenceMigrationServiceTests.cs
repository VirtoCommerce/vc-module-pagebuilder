using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderAssetReferenceMigrationServiceTests
{
    [Fact]
    public async Task RebuildResolvedPageIndexAsync_IndexesResolvedLinkedComponentContent()
    {
        var resolver = new RecordingResolver();
        var index = new RecordingAssetReferenceIndexService();

        await PageBuilderAssetReferenceMigrationService.RebuildResolvedPageIndexAsync(
            "page",
            "raw-component-reference",
            resolver,
            index,
            TestContext.Current.CancellationToken);

        Assert.Equal("raw-component-reference", resolver.Input);
        Assert.Equal("page", index.PageId);
        Assert.Equal("resolved-component-assets", index.Content);
    }

    [Fact]
    public async Task RebuildLinkedComponentIndexAsync_BackfillsUnusedComponentContent()
    {
        var index = new RecordingLinkedComponentAssetReferenceIndexService();

        await PageBuilderAssetReferenceMigrationService.RebuildLinkedComponentIndexAsync(
            "unused-component",
            "component-assets",
            index,
            TestContext.Current.CancellationToken);

        Assert.Equal("unused-component", index.LinkedComponentId);
        Assert.Equal("component-assets", index.Content);
    }

    private sealed class RecordingResolver : IPageBuilderLinkedComponentResolver
    {
        public string Input { get; private set; }

        public Task<string> ResolveAsync(string content, CancellationToken cancellationToken = default)
        {
            Input = content;
            return Task.FromResult("resolved-component-assets");
        }
    }

    private sealed class RecordingAssetReferenceIndexService : IPageBuilderAssetReferenceIndexService
    {
        public string PageId { get; private set; }
        public string Content { get; private set; }

        public Task RebuildPageIndexAsync(
            string pageId,
            string content,
            CancellationToken cancellationToken = default)
        {
            PageId = pageId;
            Content = content;
            return Task.CompletedTask;
        }

        public Task DeletePageIndexAsync(
            IEnumerable<string> pageIds,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteGroupIndexAsync(
            IEnumerable<string> groupIds,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingLinkedComponentAssetReferenceIndexService
        : IPageBuilderLinkedComponentAssetReferenceIndexService
    {
        public string LinkedComponentId { get; private set; }

        public string Content { get; private set; }

        public Task RebuildIndexAsync(
            string linkedComponentId,
            string content,
            CancellationToken cancellationToken = default)
        {
            LinkedComponentId = linkedComponentId;
            Content = content;
            return Task.CompletedTask;
        }
    }
}
