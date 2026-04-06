using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.ExportImport;


namespace VirtoCommerce.PageBuilderModule.Data.ExportImport;

public sealed class PageBuilderExportImport(
    IGroupedPageService groupedPageService,
    IGroupedPageSearchService groupedPageSearchService,
    JsonSerializer jsonSerializer)
{
    private const int BatchSize = 50;

    public async Task DoExportAsync(Stream outStream, Action<ExportImportProgressInfo> progressCallback, ICancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var progressInfo = new ExportImportProgressInfo { Description = "Page Builder pages are loading" };
        progressCallback(progressInfo);

        using var sw = new StreamWriter(outStream);
        using var writer = new JsonTextWriter(sw);

        await writer.WriteStartObjectAsync();

        progressInfo.Description = "Page Builder pages are started to export";
        progressCallback(progressInfo);

        await writer.WritePropertyNameAsync("PageBuilderPages");
        await writer.WriteStartArrayAsync();

        var criteria = AbstractTypeFactory<PageBuilderPageSearchCriteria>.TryCreateInstance();
        criteria.Take = BatchSize;
        criteria.Skip = 0;

        // First, get total count
        var initialResult = await groupedPageSearchService.SearchNoCloneAsync(criteria);
        var totalCount = initialResult.TotalCount;

        var processedCount = 0;

        for (var i = 0; i < totalCount; i += BatchSize)
        {
            criteria.Skip = i;
            criteria.Take = BatchSize;
            var searchResult = await groupedPageSearchService.SearchNoCloneAsync(criteria);

            foreach (var group in searchResult.Results)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var exportPage = await ConvertToExportPageAsync(group);
                jsonSerializer.Serialize(writer, exportPage);
                processedCount++;
            }

            await writer.FlushAsync();

            progressInfo.Description = $"{processedCount} of {totalCount} page builder pages have been exported";
            progressInfo.ProcessedCount = processedCount;
            progressInfo.TotalCount = totalCount;
            progressCallback(progressInfo);
        }

        await writer.WriteEndArrayAsync();
        await writer.WriteEndObjectAsync();
        await writer.FlushAsync();
    }

    public async Task DoImportAsync(Stream inputStream, Action<ExportImportProgressInfo> progressCallback, ICancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var progressInfo = new ExportImportProgressInfo();

        using var streamReader = new StreamReader(inputStream);
        using var reader = new JsonTextReader(streamReader);

        while (await reader.ReadAsync())
        {
            if (reader.TokenType == JsonToken.PropertyName && reader.Value?.ToString() == "PageBuilderPages")
            {
                await ImportPagesArrayAsync(reader, progressInfo, progressCallback, cancellationToken);
            }
        }
    }

    private async Task ImportPagesArrayAsync(JsonTextReader reader, ExportImportProgressInfo progressInfo, Action<ExportImportProgressInfo> progressCallback, ICancellationToken cancellationToken)
    {
        await reader.ReadAsync();
        if (reader.TokenType != JsonToken.StartArray)
        {
            return;
        }

        await reader.ReadAsync();
        var processedCount = 0;

        while (reader.TokenType != JsonToken.EndArray)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var exportPage = jsonSerializer.Deserialize<PageBuilderExportPage>(reader);
            if (exportPage != null)
            {
                await ImportPageAsync(exportPage);
                processedCount++;

                progressInfo.Description = $"{processedCount} page builder pages have been imported";
                progressInfo.ProcessedCount = processedCount;
                progressCallback(progressInfo);
            }

            await reader.ReadAsync();
        }
    }

    private async Task ImportPageAsync(PageBuilderExportPage exportPage)
    {
        var newGroup = AbstractTypeFactory<GroupedPageBuilderPage>.TryCreateInstance();
        newGroup.Name = exportPage.Name;
        newGroup.Permalink = exportPage.Permalink;
        newGroup.StoreId = exportPage.StoreId;
        newGroup.CultureName = exportPage.CultureName;
        newGroup.Visibility = exportPage.Visibility;
        newGroup.UserGroups = exportPage.UserGroups;
        newGroup.OrganizationId = exportPage.OrganizationId;
        newGroup.StartDate = exportPage.StartDate;
        newGroup.EndDate = exportPage.EndDate;

        foreach (var variant in exportPage.Variants)
        {
            var page = AbstractTypeFactory<PageBuilderPage>.TryCreateInstance();
            page.Status = variant.Status;
            page.StoreId = exportPage.StoreId;
            newGroup.Pages.Add(page);
        }

        await groupedPageService.SaveChangesAsync([newGroup]);

        // Save content for each variant
        for (var i = 0; i < exportPage.Variants.Count; i++)
        {
            var variant = exportPage.Variants[i];
            var savedPage = newGroup.Pages.FirstOrDefault(p => p.Status == variant.Status);
            if (savedPage != null && !string.IsNullOrEmpty(variant.Content))
            {
                await groupedPageService.SaveContent(savedPage.Id, variant.Content);
            }
        }
    }

    private async Task<PageBuilderExportPage> ConvertToExportPageAsync(GroupedPageBuilderPage group)
    {
        var exportPage = new PageBuilderExportPage
        {
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
                Status = page.Status,
                Content = content,
            });
        }

        return exportPage;
    }
}
