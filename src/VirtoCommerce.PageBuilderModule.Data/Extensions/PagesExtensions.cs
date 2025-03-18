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
                return PageOperation.Delete;
            case EntryState.Added:
                return PageOperation.Publish;
            case EntryState.Modified:
                // todo: use constants for status
                return page.Status == "Published" ? PageOperation.Publish : PageOperation.Unpublish;
        }

        return PageOperation.Unknown;
    }

    public static PageDocument ToPageDocument(this PageBuilderPage page)
    {
        var pageDocument = AbstractTypeFactory<PageDocument>.TryCreateInstance();
        pageDocument.Content = page.PageContent;
        pageDocument.Id = page.Id;
        pageDocument.OuterId = page.Id;
        pageDocument.Permalink = page.Permalink;
        pageDocument.MimeType = "application/json";
        pageDocument.Source = "page-builder";

        pageDocument.CreatedBy = page.CreatedBy;
        pageDocument.CreatedDate = page.CreatedDate;
        pageDocument.ModifiedBy = page.ModifiedBy;
        pageDocument.ModifiedDate = page.ModifiedDate;

        // todo: implement the rest properties
        // almost every property can be in page settings

        //pageDocument.UserGroups = GetQueryProperty("groupName")
        //     ?.Split(',', StringSplitOptions.RemoveEmptyEntries)
        //     .Select(x => x.Trim())
        //     .ToArray();
        //pageDocument.Title = GetDataProperty("title");
        //pageDocument.Description = GetDataProperty("description");
        //pageDocument.Visibility = string.Equals(GetQueryProperty("isAuthenticated"), "true", StringComparison.InvariantCultureIgnoreCase)
        //    ? PageDocumentVisibility.Private
        //    : PageDocumentVisibility.Public;
        //pageDocument.StartDate = StartDate;
        //pageDocument.EndDate = EndDate == DateTime.MinValue ? DateTime.MaxValue : EndDate;

        return pageDocument;

    }
}
