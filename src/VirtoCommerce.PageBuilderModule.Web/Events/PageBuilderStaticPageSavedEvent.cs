using System.Collections.Generic;
using VirtoCommerce.PageBuilderModule.Web.Models;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Web.Events
{
    public class PageBuilderStaticPageSavedEvent : GenericChangedEntryEvent<FileEntity>
    {
        public PageBuilderStaticPageSavedEvent(IEnumerable<GenericChangedEntry<FileEntity>> changedEntries): base(changedEntries)
        {
        }
    }
}
