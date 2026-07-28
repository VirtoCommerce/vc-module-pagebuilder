using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.ExportImport;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;


namespace VirtoCommerce.PageBuilderModule.Data.ExportImport;

public sealed class PageBuilderExportImport(
    IGroupedPageService groupedPageService,
    IGroupedPageSearchService groupedPageSearchService,
    IPageBuilderPageService pageBuilderPageService,
    PageBuilderLinkedComponentExportImportDependencies linkedComponents,
    IEventPublisher eventPublisher,
    JsonSerializer jsonSerializer)
{
    private const int BatchSize = 50;

    public async Task DoExportAsync(Stream outStream, Action<ExportImportProgressInfo> progressCallback, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var progressInfo = new ExportImportProgressInfo { Description = "Page Builder pages are loading" };
        progressCallback(progressInfo);

        using var sw = new StreamWriter(outStream, leaveOpen: true);
        using var writer = new JsonTextWriter(sw);

        await writer.WriteStartObjectAsync(cancellationToken);

        await ExportLinkedComponentsAsync(writer, progressInfo, progressCallback, cancellationToken);

        progressInfo.Description = "Page Builder pages are started to export";
        progressCallback(progressInfo);

        await writer.WritePropertyNameAsync("PageBuilderPages", cancellationToken);
        await writer.WriteStartArrayAsync(cancellationToken);

        var criteria = AbstractTypeFactory<PageBuilderPageSearchCriteria>.TryCreateInstance();
        criteria.Take = BatchSize;
        criteria.Statuses = $"{Draft},{Published},{Archived}";

        var processedCount = 0;

        for (criteria.Skip = 0; ; criteria.Skip += BatchSize)
        {
            var searchResult = await groupedPageSearchService.SearchNoCloneAsync(criteria);

            foreach (var group in searchResult.Results)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var exportPage = await ConvertToExportPageAsync(group);
                jsonSerializer.Serialize(writer, exportPage);
                processedCount++;
            }

            await writer.FlushAsync(cancellationToken);

            progressInfo.Description = $"{processedCount} of {searchResult.TotalCount} page builder pages have been exported";
            progressInfo.ProcessedCount = processedCount;
            progressInfo.TotalCount = searchResult.TotalCount;
            progressCallback(progressInfo);

            if (criteria.Skip + BatchSize >= searchResult.TotalCount)
            {
                break;
            }
        }

        await writer.WriteEndArrayAsync(cancellationToken);
        await writer.WriteEndObjectAsync(cancellationToken);
        await writer.FlushAsync(cancellationToken);
    }

    public async Task DoImportAsync(Stream inputStream, Action<ExportImportProgressInfo> progressCallback, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var progressInfo = new ExportImportProgressInfo();
        var pageBufferPath = Path.Combine(
            Path.GetTempPath(),
            $"page-builder-import-{Guid.NewGuid():N}.json");
        await using var pageBuffer = new FileStream(
            pageBufferPath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);

        using (var pageBufferStreamWriter = new StreamWriter(pageBuffer, leaveOpen: true))
        using (var pageBufferWriter = new JsonTextWriter(pageBufferStreamWriter))
        {
            await pageBufferWriter.WriteStartArrayAsync(cancellationToken);

            using var streamReader = new StreamReader(inputStream, leaveOpen: true);
            using var reader = new JsonTextReader(streamReader);

            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.TokenType != JsonToken.PropertyName)
                {
                    continue;
                }

                if (reader.Value?.ToString() == "PageBuilderLinkedComponents")
                {
                    await ImportLinkedComponentsArrayAsync(reader, progressInfo, progressCallback, cancellationToken);
                }
                else if (reader.Value?.ToString() == "PageBuilderPages")
                {
                    await BufferPagesArrayAsync(reader, pageBufferWriter, cancellationToken);
                }
            }

            await pageBufferWriter.WriteEndArrayAsync(cancellationToken);
            await pageBufferWriter.FlushAsync(cancellationToken);
        }

        pageBuffer.Position = 0;
        using var pageBufferStreamReader = new StreamReader(pageBuffer, leaveOpen: true);
        using var pageBufferReader = new JsonTextReader(pageBufferStreamReader);
        await ImportPagesArrayAsync(pageBufferReader, progressInfo, progressCallback, cancellationToken);
    }

    private async Task ExportLinkedComponentsAsync(
        JsonTextWriter writer,
        ExportImportProgressInfo progressInfo,
        Action<ExportImportProgressInfo> progressCallback,
        CancellationToken cancellationToken)
    {
        progressInfo.Description = "Page Builder Linked Components are started to export";
        progressCallback(progressInfo);

        await writer.WritePropertyNameAsync("PageBuilderLinkedComponents", cancellationToken);
        await writer.WriteStartArrayAsync(cancellationToken);

        var criteria = AbstractTypeFactory<PageBuilderLinkedComponentSearchCriteria>.TryCreateInstance();
        criteria.Take = BatchSize;
        var processedCount = 0;

        for (criteria.Skip = 0; ; criteria.Skip += BatchSize)
        {
            var searchResult = await linkedComponents.SearchService.SearchAsync(criteria);

            foreach (var component in searchResult.Results)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var exportComponent = new PageBuilderExportLinkedComponent
                {
                    Id = component.Id,
                    StoreId = component.StoreId,
                    Name = component.Name,
                    Content = await linkedComponents.ContentService.LoadContentAsync(component.Id, cancellationToken),
                };
                jsonSerializer.Serialize(writer, exportComponent);
                processedCount++;
            }

            await writer.FlushAsync(cancellationToken);
            progressInfo.Description = $"{processedCount} of {searchResult.TotalCount} Page Builder Linked Components have been exported";
            progressInfo.ProcessedCount = processedCount;
            progressInfo.TotalCount = searchResult.TotalCount;
            progressCallback(progressInfo);

            if (criteria.Skip + BatchSize >= searchResult.TotalCount)
            {
                break;
            }
        }

        await writer.WriteEndArrayAsync(cancellationToken);
    }

    private async Task ImportLinkedComponentsArrayAsync(
        JsonTextReader reader,
        ExportImportProgressInfo progressInfo,
        Action<ExportImportProgressInfo> progressCallback,
        CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken) || reader.TokenType != JsonToken.StartArray)
        {
            return;
        }

        var processedCount = 0;
        while (await reader.ReadAsync(cancellationToken) && reader.TokenType != JsonToken.EndArray)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var exportComponent = jsonSerializer.Deserialize<PageBuilderExportLinkedComponent>(reader);
            if (exportComponent == null)
            {
                continue;
            }

            PageBuilderLinkedComponentReferenceMatcher.ValidateComponentContent(exportComponent.Content);

            var component = string.IsNullOrWhiteSpace(exportComponent.Id)
                ? null
                : await linkedComponents.ComponentService.GetByIdAsync(exportComponent.Id);
            component ??= AbstractTypeFactory<PageBuilderLinkedComponent>.TryCreateInstance();
            component.Id = exportComponent.Id;
            component.StoreId = exportComponent.StoreId;
            component.Name = exportComponent.Name;

            await linkedComponents.ComponentService.SaveWithContentAsync(
                component,
                exportComponent.Content,
                cancellationToken);

            processedCount++;
            progressInfo.Description = $"{processedCount} Page Builder Linked Components have been imported";
            progressInfo.ProcessedCount = processedCount;
            progressCallback(progressInfo);
        }
    }

    private async Task ImportPagesArrayAsync(JsonTextReader reader, ExportImportProgressInfo progressInfo, Action<ExportImportProgressInfo> progressCallback, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken) || reader.TokenType != JsonToken.StartArray)
        {
            return;
        }

        var processedCount = 0;

        while (await reader.ReadAsync(cancellationToken) && reader.TokenType != JsonToken.EndArray)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var exportPage = jsonSerializer.Deserialize<PageBuilderExportPage>(reader);
            if (exportPage != null)
            {
                await ImportPageAsync(exportPage, cancellationToken);
                processedCount++;

                progressInfo.Description = $"{processedCount} page builder pages have been imported";
                progressInfo.ProcessedCount = processedCount;
                progressCallback(progressInfo);
            }
        }
    }

    private async Task ImportPageAsync(PageBuilderExportPage exportPage, CancellationToken cancellationToken)
    {
        GroupedPageBuilderPage existingGroup = null;

        if (!string.IsNullOrEmpty(exportPage.GroupId))
        {
            existingGroup = await groupedPageService.GetByIdAsync(exportPage.GroupId);
        }

        if (existingGroup != null)
        {
            await UpdateExistingGroupAsync(existingGroup, exportPage, cancellationToken);
        }
        else
        {
            await CreateNewGroupAsync(exportPage, cancellationToken);
        }
    }

    private async Task UpdateExistingGroupAsync(
        GroupedPageBuilderPage group,
        PageBuilderExportPage exportPage,
        CancellationToken cancellationToken)
    {
        ApplyMetadata(group, exportPage);

        // Replace pages with exported snapshot
        group.Pages = [.. exportPage.Variants.Select(v =>
        {
            var page = AbstractTypeFactory<PageBuilderPage>.TryCreateInstance();
            page.Id = v.PageId;
            page.Status = v.Status;
            page.StoreId = exportPage.StoreId;
            page.GroupId = group.Id;
            return page;
        })];

        await groupedPageService.SaveChangesAsync([group]);

        await SaveVariantsContentAsync(exportPage.Variants, cancellationToken);
    }

    private async Task CreateNewGroupAsync(PageBuilderExportPage exportPage, CancellationToken cancellationToken)
    {
        var newGroup = AbstractTypeFactory<GroupedPageBuilderPage>.TryCreateInstance();
        newGroup.Id = exportPage.GroupId;
        ApplyMetadata(newGroup, exportPage);

        foreach (var variant in exportPage.Variants)
        {
            var page = AbstractTypeFactory<PageBuilderPage>.TryCreateInstance();
            page.Id = variant.PageId;
            page.GroupId = newGroup.Id;
            page.Status = variant.Status;
            page.StoreId = exportPage.StoreId;
            newGroup.Pages.Add(page);
        }

        await groupedPageService.SaveChangesAsync([newGroup]);

        await SaveVariantsContentAsync(exportPage.Variants, cancellationToken);
    }

    private static void ApplyMetadata(GroupedPageBuilderPage group, PageBuilderExportPage exportPage)
    {
        group.Name = exportPage.Name;
        group.Permalink = exportPage.Permalink;
        group.StoreId = exportPage.StoreId;
        group.CultureName = exportPage.CultureName;
        group.Visibility = exportPage.Visibility;
        group.UserGroups = exportPage.UserGroups;
        group.OrganizationId = exportPage.OrganizationId;
        group.StartDate = exportPage.StartDate;
        group.EndDate = exportPage.EndDate;
    }

    private async Task SaveVariantsContentAsync(
        IList<PageBuilderExportPageVariant> variants,
        CancellationToken cancellationToken)
    {
        var variantsWithContent = variants
            .Where(v => !string.IsNullOrEmpty(v.PageId) && !string.IsNullOrEmpty(v.Content))
            .ToArray();

        foreach (var variant in variantsWithContent)
        {
            await groupedPageService.SaveContent(variant.PageId, variant.Content, cancellationToken);
        }

        if (variantsWithContent.Length > 0)
        {
            var pages = await pageBuilderPageService.GetAsync(variantsWithContent.Select(x => x.PageId).ToArray());
            var changedEntries = pages
                .Select(x => new GenericChangedEntry<PageBuilderPage>(x, EntryState.Modified))
                .ToArray();

            if (changedEntries.Length > 0)
            {
                await eventPublisher.Publish(new PageBuilderPageChangedEvent(changedEntries), cancellationToken);
            }
        }
    }

    private async Task BufferPagesArrayAsync(
        JsonTextReader reader,
        JsonTextWriter writer,
        CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken) || reader.TokenType != JsonToken.StartArray)
        {
            return;
        }

        while (await reader.ReadAsync(cancellationToken) && reader.TokenType != JsonToken.EndArray)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var exportPage = jsonSerializer.Deserialize<PageBuilderExportPage>(reader);
            if (exportPage != null)
            {
                jsonSerializer.Serialize(writer, exportPage);
            }
        }

        await writer.FlushAsync(cancellationToken);
    }

    private async Task<PageBuilderExportPage> ConvertToExportPageAsync(GroupedPageBuilderPage group)
    {
        var exportPage = new PageBuilderExportPage
        {
            GroupId = group.Id,
            Name = group.Name,
            Permalink = group.Permalink,
            StoreId = group.StoreId,
            CultureName = group.CultureName,
            Visibility = group.Visibility,
            UserGroups = group.UserGroups,
            OrganizationId = group.OrganizationId,
            StartDate = group.StartDate,
            EndDate = group.EndDate,
        };

        foreach (var page in group.Pages)
        {
            var content = await groupedPageService.LoadContent(page.Id);
            exportPage.Variants.Add(new PageBuilderExportPageVariant
            {
                PageId = page.Id,
                Status = page.Status,
                Content = content,
            });
        }

        return exportPage;
    }
}
