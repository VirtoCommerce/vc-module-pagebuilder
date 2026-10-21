using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.ExportImport;

namespace VirtoCommerce.PageBuilderModule.Data.ExportImport;

/// <summary>
/// Export and import of <see cref="MultilanguagePage"/> roots and of the links of their language versions.
/// </summary>
public sealed class MultilanguagePageExportImport(
    IMultilanguagePageService multilanguagePageService,
    IMultilanguagePageSearchService multilanguagePageSearchService,
    JsonSerializer jsonSerializer)
{
    public const string PropertyName = "PageBuilderMultilanguagePages";

    private const int BatchSize = 50;

    public async Task ExportAsync(JsonTextWriter writer, ExportImportProgressInfo progressInfo,
        Action<ExportImportProgressInfo> progressCallback, CancellationToken cancellationToken)
    {
        if (multilanguagePageSearchService == null)
        {
            return;
        }

        progressInfo.Description = "Page Builder multilanguage pages are started to export";
        progressCallback(progressInfo);

        await writer.WritePropertyNameAsync(PropertyName, cancellationToken);
        await writer.WriteStartArrayAsync(cancellationToken);

        var criteria = AbstractTypeFactory<MultilanguagePageSearchCriteria>.TryCreateInstance();
        foreach (var page in await multilanguagePageSearchService.SearchAllNoCloneAsync(criteria))
        {
            cancellationToken.ThrowIfCancellationRequested();
            jsonSerializer.Serialize(writer, page);
        }

        await writer.WriteEndArrayAsync(cancellationToken);
        await writer.FlushAsync(cancellationToken);
    }

    public async Task ImportAsync(JsonTextReader reader, ExportImportProgressInfo progressInfo,
        Action<ExportImportProgressInfo> progressCallback, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken) || reader.TokenType != JsonToken.StartArray)
        {
            return;
        }

        var pages = new List<MultilanguagePage>();
        while (await reader.ReadAsync(cancellationToken) && reader.TokenType != JsonToken.EndArray)
        {
            var page = jsonSerializer.Deserialize<MultilanguagePage>(reader);
            if (page != null)
            {
                pages.Add(page);
            }
        }

        if (multilanguagePageService == null || pages.Count == 0)
        {
            return;
        }

        foreach (var batch in pages.Chunk(BatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await multilanguagePageService.SaveChangesAsync(batch);
        }

        progressInfo.Description = $"{pages.Count} page builder multilanguage pages have been imported";
        progressCallback(progressInfo);
    }

    /// <summary>
    /// A regular save never changes the link, so an existing page follows the exported one through link/unlink.
    /// </summary>
    public async Task SyncLinkAsync(GroupedPageBuilderPage group, string multilanguagePageId,
        ExportImportProgressInfo progressInfo, CancellationToken cancellationToken)
    {
        if (multilanguagePageService == null || group.MultilanguagePageId == multilanguagePageId)
        {
            return;
        }

        try
        {
            if (!string.IsNullOrEmpty(group.MultilanguagePageId))
            {
                await multilanguagePageService.UnlinkAsync(group.Id, cancellationToken);
            }

            if (!string.IsNullOrEmpty(multilanguagePageId))
            {
                await multilanguagePageService.LinkAsync(multilanguagePageId, group.Id, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            progressInfo.Errors.Add($"Page '{group.Id}' was imported without its multilanguage page link: {ex.Message}");
        }
    }

    /// <summary>
    /// The id of the multilanguage page when the target has it; a page referencing a missing one is imported as a Landing Page.
    /// </summary>
    public async Task<string> GetExistingIdAsync(string multilanguagePageId)
    {
        if (multilanguagePageService == null || string.IsNullOrEmpty(multilanguagePageId))
        {
            return null;
        }

        return (await multilanguagePageService.GetByIdAsync(multilanguagePageId))?.Id;
    }
}
