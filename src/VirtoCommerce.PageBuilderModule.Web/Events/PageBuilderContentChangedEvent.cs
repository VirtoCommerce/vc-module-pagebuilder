using System.Collections.Generic;
using Newtonsoft.Json;
using VirtoCommerce.PageBuilderModule.Web.Models;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.PageBuilderModule.Web.Events
{
    public class PageBuilderContentChangedEvent : GenericChangedEntryEvent<FileEntity>
    {
        [JsonConstructor]
        public PageBuilderContentChangedEvent(string contentType, IEnumerable<GenericChangedEntry<FileEntity>> changedEntries) : base(changedEntries)
        {
            ContentType = contentType;
        }

        public string ContentType { get; }
    }
}
