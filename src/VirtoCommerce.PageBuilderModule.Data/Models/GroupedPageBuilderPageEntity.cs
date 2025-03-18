using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;

namespace VirtoCommerce.PageBuilderModule.Data.Models
{
    public class GroupedPageBuilderPageEntity : AuditableEntity, IDataEntity<GroupedPageBuilderPageEntity, GroupedPageBuilderPage>
    {
        public string StoreId { get; set; }

        public string CultureName { get; set; }

        public string Name { get; set; }

        public string Permalink { get; set; }

        public bool HasChanges { get; set; }

        public string Status { get; set; } // Draft | Published | Archived

        public IList<string> PagesIds { get; set; } = [];

        public GroupedPageBuilderPage ToModel(GroupedPageBuilderPage model)
        {
            model.Id = Id;
            model.CreatedBy = CreatedBy;
            model.CreatedDate = CreatedDate;
            model.ModifiedBy = ModifiedBy;
            model.ModifiedDate = ModifiedDate;

            model.StoreId = StoreId;
            model.CultureName = CultureName;
            model.Name = Name;
            model.Permalink = Permalink;
            model.Status = Status;
            model.HasChanges = HasChanges;
            model.PageIds = PagesIds;

            return model;
        }

        public GroupedPageBuilderPageEntity FromModel(GroupedPageBuilderPage model, PrimaryKeyResolvingMap pkMap)
        {
            throw new NotSupportedException("This entity is read-only");
        }

        public void Patch(GroupedPageBuilderPageEntity target)
        {
            throw new NotSupportedException("This entity is read-only");
        }
    }
}
