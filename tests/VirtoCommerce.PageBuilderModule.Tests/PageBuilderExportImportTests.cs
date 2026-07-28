using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.ExportImport;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderExportImportTests
{
    [Fact]
    public async Task DoImportAsync_AppliesComponentsBeforePagesRegardlessOfJsonPropertyOrder()
    {
        var calls = new List<string>();
        var exportImport = new PageBuilderExportImport(
            new RecordingGroupedPageService(calls),
            null!,
            null!,
            new PageBuilderLinkedComponentExportImportDependencies(
                new RecordingLinkedComponentService(calls),
                null!,
                new RecordingLinkedComponentContentService(calls)),
            null!,
            JsonSerializer.CreateDefault());
        var payload = JsonConvert.SerializeObject(new
        {
            PageBuilderPages = new[]
            {
                new PageBuilderExportPage
                {
                    GroupId = "group",
                    StoreId = "store",
                    Name = "Page",
                    Variants = [],
                },
            },
            PageBuilderLinkedComponents = new[]
            {
                new PageBuilderExportLinkedComponent
                {
                    Id = "component",
                    StoreId = "store",
                    Name = "Shared header",
                    Content = "{ \"settings\": {}, \"content\": [] }",
                },
            },
        });
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        await exportImport.DoImportAsync(
            stream,
            _ => { },
            TestContext.Current.CancellationToken);

        Assert.Equal(["component-aggregate", "page-group"], calls);
    }

    private sealed class RecordingLinkedComponentService(IList<string> calls)
        : IPageBuilderLinkedComponentService
    {
        public Task<IList<PageBuilderLinkedComponent>> GetAsync(
            IList<string> ids,
            string responseGroup = null,
            bool clone = true)
        {
            return Task.FromResult<IList<PageBuilderLinkedComponent>>([]);
        }

        public Task SaveChangesAsync(IList<PageBuilderLinkedComponent> models)
        {
            throw new NotSupportedException();
        }

        public Task SaveWithContentAsync(
            PageBuilderLinkedComponent model,
            string content,
            CancellationToken cancellationToken = default)
        {
            calls.Add("component-aggregate");
            return Task.CompletedTask;
        }

        public Task DeleteAsync(IList<string> ids, bool softDelete = false)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingLinkedComponentContentService(IList<string> calls)
        : IPageBuilderLinkedComponentContentService
    {
        public Task<string> LoadContentAsync(
            string linkedComponentId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyDictionary<string, string>> LoadContentsAsync(
            IEnumerable<string> linkedComponentIds,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SaveContentAsync(
            string linkedComponentId,
            string content,
            CancellationToken cancellationToken = default)
        {
            calls.Add("component-content");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingGroupedPageService(IList<string> calls) : IGroupedPageService
    {
        public Task<IList<GroupedPageBuilderPage>> GetAsync(
            IList<string> ids,
            string responseGroup = null,
            bool clone = true)
        {
            return Task.FromResult<IList<GroupedPageBuilderPage>>([]);
        }

        public Task SaveChangesAsync(IList<GroupedPageBuilderPage> models)
        {
            calls.Add("page-group");
            return Task.CompletedTask;
        }

        public Task DeleteAsync(IList<string> ids, bool softDelete = false)
        {
            throw new NotSupportedException();
        }

        public Task<string> LoadContent(string pageId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SaveContent(
            string pageId,
            string content,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> LoadContentToStreamAsync(
            string pageId,
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SaveStreamAsContentAsync(
            string pageId,
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task CopyPageContentAsync(
            string sourcePageId,
            string targetPageId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
