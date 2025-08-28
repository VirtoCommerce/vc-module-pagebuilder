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

    [StringLength(CultureNameLength)]
    public string CultureName { get; set; }

    [StringLength(1024)]
    public string Name { get; set; }

    [StringLength(2048)]
    public string Permalink { get; set; }

    [StringLength(128)]
    public string Status { get; set; } // Draft | Published | Archived

    public bool Visibility { get; set; }

    [StringLength(Length1024)]
    public string UserGroups { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

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
        model.CultureName = CultureName;
        model.Name = Name;
        model.Permalink = Permalink;
        model.Status = Status;

        model.Visibility = Visibility;
        model.UserGroups = UserGroups;
        model.StartDate = StartDate;
        model.EndDate = EndDate;

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
        CultureName = model.CultureName;
        Name = model.Name;
        Permalink = model.Permalink;
        Status = model.Status;

        Visibility = model.Visibility;
        UserGroups = model.UserGroups;
        StartDate = model.StartDate;
        EndDate = model.EndDate;

        return this;
    }

    public virtual void Patch(PageBuilderPageEntity target)
    {
        target.GroupId = GroupId;
        target.StoreId = StoreId;
        target.CultureName = CultureName;
        target.Name = Name;
        target.Permalink = Permalink;
        target.Status = Status;

        target.Visibility = Visibility;
        target.UserGroups = UserGroups;
        target.StartDate = StartDate;
        target.EndDate = EndDate;
    }
}
