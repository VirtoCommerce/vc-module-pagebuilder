using System.ComponentModel.DataAnnotations;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using static VirtoCommerce.Platform.Data.Infrastructure.DbContextBase;

namespace VirtoCommerce.PageBuilderModule.Data.Models;

public class PageBuilderPageEntity : AuditableEntity, IDataEntity<PageBuilderPageEntity, PageBuilderPage>
{
    [StringLength(IdLength)]
    public string GroupId { get; set; }

    [StringLength(IdLength)]
    public string StoreId { get; set; }

    [StringLength(128)]
    public string Status { get; set; } // Draft | Published | Archived

    public virtual PageBuilderContentEntity Content { get; set; }

    public GroupedPageBuilderPageEntity Group { get; set; }

    public virtual PageBuilderPage ToModel(PageBuilderPage model)
    {
        model.Id = Id;
        model.CreatedBy = CreatedBy;
        model.CreatedDate = CreatedDate;
        model.ModifiedBy = ModifiedBy;
        model.ModifiedDate = ModifiedDate;

        model.GroupId = GroupId;
        model.StoreId = StoreId;
        model.Status = Status;

        return model;
    }

    public virtual PageBuilderPageEntity FromModel(PageBuilderPage model, PrimaryKeyResolvingMap pkMap)
    {
        pkMap.AddPair(model, this);

        Id = model.Id;
        CreatedBy = model.CreatedBy;
        CreatedDate = model.CreatedDate;
        ModifiedBy = model.ModifiedBy;
        ModifiedDate = model.ModifiedDate;

        GroupId = model.GroupId;
        StoreId = model.StoreId;
        Status = model.Status;

        return this;
    }

    public virtual void Patch(PageBuilderPageEntity target)
    {
        target.GroupId = GroupId;
        target.StoreId = StoreId;
        target.Status = Status;
    }
}
