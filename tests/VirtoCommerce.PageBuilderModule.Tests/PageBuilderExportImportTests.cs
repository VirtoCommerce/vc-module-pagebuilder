using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        var referenceIndex = new RecordingReferenceIndexService(calls);
        var jsonSerializer = JsonSerializer.CreateDefault();
        var sharedComponentExportImport = new PageBuilderSharedComponentExportImport(
            referenceIndex,
            new RecordingSharedComponentService(calls),
            null!,
            new RecordingSharedComponentContentService(calls),
            jsonSerializer);
        var exportImport = new PageBuilderExportImport(
            new RecordingGroupedPageService(calls),
            null!,
            null!,
            sharedComponentExportImport,
            null!,
            jsonSerializer);
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
            PageBuilderSharedComponents = new[]
            {
                new PageBuilderExportSharedComponent
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

        Assert.Equal(["component-aggregate", "preflight", "page-group"], calls);
        Assert.Equal(1, referenceIndex.CallCount);
    }

    [Fact]
    public async Task DoImportAsync_PreflightsAllVariantsOnceBeforeMutatingGroup()
    {
        var calls = new List<string>();
        var referenceIndex = new RecordingReferenceIndexService(calls, throwOnValidation: true);
        var jsonSerializer = JsonSerializer.CreateDefault();
        var sharedComponentExportImport = new PageBuilderSharedComponentExportImport(
            referenceIndex,
            null!,
            null!,
            null!,
            jsonSerializer);
        var exportImport = new PageBuilderExportImport(
            new RecordingGroupedPageService(calls),
            null!,
            null!,
            sharedComponentExportImport,
            null!,
            jsonSerializer);
        var variants = new[]
        {
            new PageBuilderExportPageVariant { PageId = "draft", Content = "variant-a" },
            new PageBuilderExportPageVariant { PageId = "published", Content = "variant-b" },
            new PageBuilderExportPageVariant { PageId = "archived", Content = "variant-c" },
        };
        var payload = JsonConvert.SerializeObject(new
        {
            PageBuilderPages = new[]
            {
                new PageBuilderExportPage
                {
                    GroupId = "group",
                    StoreId = "store",
                    Name = "Page",
                    Variants = variants,
                },
            },
        });
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        await Assert.ThrowsAsync<InvalidDataException>(() => exportImport.DoImportAsync(
            stream,
            _ => { },
            TestContext.Current.CancellationToken));

        Assert.Equal(["preflight"], calls);
        Assert.Equal(1, referenceIndex.CallCount);
        Assert.Equal(variants.Select(x => x.Content), referenceIndex.Contents);
    }

    private sealed class RecordingReferenceIndexService(
        IList<string> calls,
        bool throwOnValidation = false) : NoopSharedComponentReferenceIndexService
    {
        public int CallCount { get; private set; }

        public string[] Contents { get; private set; } = [];

        public override Task ValidateReferencesForStoreAsync(
            string storeId,
            IEnumerable<string> contents,
            CancellationToken cancellationToken = default)
        {
            calls.Add("preflight");
            CallCount++;
            Contents = contents.ToArray();
            if (throwOnValidation)
            {
                throw new InvalidDataException("Invalid shared component reference.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSharedComponentService(IList<string> calls)
        : IPageBuilderSharedComponentService
    {
        public Task<IList<PageBuilderSharedComponent>> GetAsync(
            IList<string> ids,
            string responseGroup = null,
            bool clone = true)
        {
            return Task.FromResult<IList<PageBuilderSharedComponent>>([]);
        }

        public Task SaveChangesAsync(IList<PageBuilderSharedComponent> models)
        {
            throw new NotSupportedException();
        }

        public Task<PageBuilderSharedComponent> UpdateMetadataAsync(
            PageBuilderSharedComponent model,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> TryDeleteAsync(
            PageBuilderSharedComponent expectedComponent,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SaveWithContentAsync(
            PageBuilderSharedComponent model,
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

    private sealed class RecordingSharedComponentContentService(IList<string> calls)
        : IPageBuilderSharedComponentContentService
    {
        public Task<string> LoadContentAsync(
            string sharedComponentId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<string> TryLoadContentAsync(
            PageBuilderSharedComponent expectedComponent,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyDictionary<string, string>> LoadContentsAsync(
            IEnumerable<string> sharedComponentIds,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SaveContentAsync(
            string sharedComponentId,
            string content,
            CancellationToken cancellationToken = default)
        {
            calls.Add("component-content");
            return Task.CompletedTask;
        }

        public Task<bool> TrySaveContentAsync(
            PageBuilderSharedComponent expectedComponent,
            string content,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
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

        public Task<bool> TryDeleteEmptyDraftAsync(
            string pageId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
