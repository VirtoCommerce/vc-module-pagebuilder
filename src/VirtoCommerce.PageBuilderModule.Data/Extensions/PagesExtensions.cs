using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Pages.Core.Events;
using VirtoCommerce.Pages.Core.Models;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Data.Extensions;

static class PagesExtensions
{
    public static PageOperation ToPageOperation(this EntryState state, PageBuilderPage page)
    {
        switch (state)
        {
            case EntryState.Deleted:
                return PageOperation.Archive;
            case EntryState.Added:
            case EntryState.Modified:
            case EntryState.Detached:
            case EntryState.Unchanged:
                // todo: use constants for status
                return page.Status == "Published" ? PageOperation.Publish : PageOperation.Unpublish;
        }

        return PageOperation.Unknown;
    }

    public static PageDocument ToPageDocument(this PageBuilderPage page)
    {
        var pageDocument = AbstractTypeFactory<PageDocument>.TryCreateInstance();
        pageDocument.Id = page.Id;
        pageDocument.OuterId = page.Id;
        pageDocument.StoreId = page.StoreId;
        pageDocument.CultureName = page.CultureName;
        pageDocument.Permalink = page.Permalink;

        pageDocument.CreatedBy = page.CreatedBy;
        pageDocument.CreatedDate = page.CreatedDate;
        pageDocument.ModifiedBy = page.ModifiedBy;
        pageDocument.ModifiedDate = page.ModifiedDate;

        pageDocument.Source = "page-builder";
        pageDocument.MimeType = "application/json";
        pageDocument.Content = page.PageContent;
        pageDocument.Title = page.Name; // may be overridden by settings
        GetDataFromSettings(page.PageContent, pageDocument);
        return pageDocument;
    }

    private static void GetDataFromSettings(string contentAsString, PageDocument pageDocument)
    {
        try
        {
            var content = JObject.Parse(contentAsString.IsNullOrEmpty() ? "{}" : contentAsString);
            var settings = content["settings"];
            pageDocument.Title = settings?.Value<string>("title") ?? pageDocument.Title;
            pageDocument.Visibility = (settings?.Value<bool>("visibility") ?? true)
                ? PageDocumentVisibility.Public
                : PageDocumentVisibility.Private;
            pageDocument.Title = settings?.Value<string>("description") ?? pageDocument.Title;

            pageDocument.UserGroups = settings?.Value<string>("groupName")
                 ?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                 .Select(x => x.Trim())
                 .ToArray();
            // todo: check the type for data in page builder. it can be string or date
            pageDocument.StartDate = settings?.Value<DateTime>("startDate");
            pageDocument.EndDate = settings?.Value<DateTime>("endDate");
        }
        catch
        {
            // ignored
        }
    }
}
