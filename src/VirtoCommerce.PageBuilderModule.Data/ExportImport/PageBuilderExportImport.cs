using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.ExportImport;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;


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
        criteria.Statuses = $"{Draft},{Published}";

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

            await writer.FlushAsync();

            progressInfo.Description = $"{processedCount} of {searchResult.TotalCount} page builder pages have been exported";
            progressInfo.ProcessedCount = processedCount;
            progressInfo.TotalCount = searchResult.TotalCount;
            progressCallback(progressInfo);

            if (criteria.Skip + BatchSize >= searchResult.TotalCount)
            {
                break;
            }
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
        if (!await reader.ReadAsync() || reader.TokenType != JsonToken.StartArray)
        {
            return;
        }

        var processedCount = 0;

        while (await reader.ReadAsync() && reader.TokenType != JsonToken.EndArray)
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
        }
    }

    private async Task ImportPageAsync(PageBuilderExportPage exportPage)
    {
        GroupedPageBuilderPage existingGroup = null;

        if (!string.IsNullOrEmpty(exportPage.GroupId))
        {
            existingGroup = await groupedPageService.GetByIdAsync(exportPage.GroupId);
        }

        if (existingGroup != null)
        {
            await UpdateExistingGroupAsync(existingGroup, exportPage);
        }
        else
        {
            await CreateNewGroupAsync(exportPage);
        }
    }

    private async Task UpdateExistingGroupAsync(GroupedPageBuilderPage group, PageBuilderExportPage exportPage)
    {
        group.Name = exportPage.Name;
        group.Visibility = exportPage.Visibility;
        group.UserGroups = exportPage.UserGroups;
        group.OrganizationId = exportPage.OrganizationId;
        group.StartDate = exportPage.StartDate;
        group.EndDate = exportPage.EndDate;

        // Add missing variants
        var existingStatuses = group.Pages.Select(p => p.Status).ToHashSet();
        foreach (var variant in exportPage.Variants.Where(v => !existingStatuses.Contains(v.Status)))
        {
            var page = AbstractTypeFactory<PageBuilderPage>.TryCreateInstance();
            page.Status = variant.Status;
            page.StoreId = exportPage.StoreId;
            group.Pages.Add(page);
        }

        await groupedPageService.SaveChangesAsync([group]);

        // Reload to get IDs for newly created pages
        group = await groupedPageService.GetByIdAsync(group.Id);

        await SaveVariantsContentAsync(group.Pages, exportPage.Variants);
    }

    private async Task CreateNewGroupAsync(PageBuilderExportPage exportPage)
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

        // Reload to get IDs for newly created pages
        newGroup = await groupedPageService.GetByIdAsync(newGroup.Id);

        await SaveVariantsContentAsync(newGroup.Pages, exportPage.Variants);
    }

    private async Task SaveVariantsContentAsync(IList<PageBuilderPage> pages, IList<PageBuilderExportPageVariant> variants)
    {
        var usedPageIds = new HashSet<string>();

        foreach (var variant in variants)
        {
            var matchedPage = pages.FirstOrDefault(p => p.Status == variant.Status && !usedPageIds.Contains(p.Id));
            if (matchedPage != null && !string.IsNullOrEmpty(variant.Content))
            {
                await groupedPageService.SaveContent(matchedPage.Id, variant.Content);
                usedPageIds.Add(matchedPage.Id);
            }
        }
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

        foreach (var page in group.Pages.Where(p => p.Status != Archived))
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
