using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Pages.Core.Events;
using VirtoCommerce.Pages.Core.Models;
using VirtoCommerce.Platform.Core.Common;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Data.Extensions;

public static class PagesExtensions
{
    public static PageOperation ToPageOperation(this EntryState state, PageBuilderPage page)
    {
        if (state == EntryState.Deleted)
        {
            return PageOperation.Archive;
        }

        return page.Status switch
        {
            Published => PageOperation.Publish,
            Archived => PageOperation.Archive,
            Draft => PageOperation.Unpublish,
            _ => PageOperation.Unknown,
        };
    }

    public static PageDocument ToPageDocument(this PageBuilderPage page, GroupedPageBuilderPage group, string content)
    {
        var pageDocument = AbstractTypeFactory<PageDocument>.TryCreateInstance();
        pageDocument.Id = page.Id;
        pageDocument.OuterId = page.Id;
        pageDocument.StoreId = page.StoreId;
        pageDocument.CultureName = group.CultureName;
        pageDocument.Permalink = group.Permalink;
        pageDocument.OrganizationId = group.OrganizationId;

        pageDocument.CreatedBy = page.CreatedBy;
        pageDocument.CreatedDate = page.CreatedDate;
        pageDocument.ModifiedBy = page.ModifiedBy;
        pageDocument.ModifiedDate = page.ModifiedDate;

        pageDocument.Source = "page-builder";
        pageDocument.MimeType = "application/json";
        pageDocument.Title = group.Name; // may be overridden by settings
        pageDocument.Visibility = group.Visibility ? PageDocumentVisibility.Public : PageDocumentVisibility.Private;
        pageDocument.UserGroups = group.UserGroups
            ?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .ToArray();
        pageDocument.StartDate = group.StartDate;
        pageDocument.EndDate = group.EndDate;

        pageDocument.Content = content;
        GetDataFromSettings(content, pageDocument);
        return pageDocument;
    }

    private static void GetDataFromSettings(string contentAsString, PageDocument pageDocument)
    {
        try
        {
            var content = JObject.Parse(contentAsString.IsNullOrEmpty() ? "{}" : contentAsString);
            var settings = content["settings"];
            pageDocument.Title = settings?.Value<string>("title") ?? pageDocument.Title;
            pageDocument.Description = settings?.Value<string>("description") ?? pageDocument.Title;
        }
        catch
        {
            // ignored
        }
    }
}
