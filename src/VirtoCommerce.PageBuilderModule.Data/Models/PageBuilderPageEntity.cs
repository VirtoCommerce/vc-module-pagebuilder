using System.ComponentModel.DataAnnotations;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;

namespace VirtoCommerce.PageBuilderModule.Data.Models;

public class PageBuilderPageEntity : AuditableEntity, IDataEntity<PageBuilderPageEntity, PageBuilderPage>
{
    [StringLength(128)]
    public string StoreId { get; set; }

    [StringLength(128)]
    public string CultureName { get; set; }

    [StringLength(1024)]
    public string Name { get; set; }

    [StringLength(2048)]
    public string Permalink { get; set; }

    [StringLength(128)]
    public string Status { get; set; } // Draft | Published | Archived

    public string PageContent { get; set; }

    public virtual PageBuilderPage ToModel(PageBuilderPage model)
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
        model.PageContent = PageContent;

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

        StoreId = model.StoreId;
        CultureName = model.CultureName;
        Name = model.Name;
        Permalink = model.Permalink;
        Status = model.Status;
        PageContent = model.PageContent;

        return this;
    }

    public virtual void Patch(PageBuilderPageEntity target)
    {
        target.StoreId = StoreId;
        target.CultureName = CultureName;
        target.Name = Name;
        target.Permalink = Permalink;
        target.Status = Status;
        target.PageContent = PageContent;
    }
}
