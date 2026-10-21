using System.ComponentModel.DataAnnotations;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using static VirtoCommerce.Platform.Data.Infrastructure.DbContextBase;

namespace VirtoCommerce.PageBuilderModule.Data.Models;

public class MultilanguagePageEntity : AuditableEntity, IDataEntity<MultilanguagePageEntity, MultilanguagePage>
{
    [StringLength(IdLength)]
    [Required]
    public string StoreId { get; set; }

    [StringLength(1024)]
    public string Name { get; set; }

    [StringLength(2048)]
    public string Permalink { get; set; }

    [StringLength(CultureNameLength)]
    public string DefaultCultureName { get; set; }

    [StringLength(IdLength)]
    public string OrganizationId { get; set; }

    public bool Visibility { get; set; }

    [StringLength(Length1024)]
    public string UserGroups { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public MultilanguagePage ToModel(MultilanguagePage model)
    {
        model.Id = Id;
        model.CreatedBy = CreatedBy;
        model.CreatedDate = CreatedDate;
        model.ModifiedBy = ModifiedBy;
        model.ModifiedDate = ModifiedDate;

        model.StoreId = StoreId;
        model.Name = Name;
        model.Permalink = Permalink;
        model.DefaultCultureName = DefaultCultureName;
        model.OrganizationId = OrganizationId;
        model.Visibility = Visibility;
        model.UserGroups = UserGroups;
        model.StartDate = StartDate;
        model.EndDate = EndDate;

        return model;
    }

    public MultilanguagePageEntity FromModel(MultilanguagePage model, PrimaryKeyResolvingMap pkMap)
    {
        pkMap.AddPair(model, this);

        Id = model.Id;
        CreatedBy = model.CreatedBy;
        CreatedDate = model.CreatedDate;
        ModifiedBy = model.ModifiedBy;
        ModifiedDate = model.ModifiedDate;

        StoreId = model.StoreId;
        Name = model.Name;
        Permalink = model.Permalink;
        DefaultCultureName = model.DefaultCultureName;
        OrganizationId = model.OrganizationId;
        Visibility = model.Visibility;
        UserGroups = model.UserGroups;
        StartDate = model.StartDate;
        EndDate = model.EndDate;

        return this;
    }

    public void Patch(MultilanguagePageEntity target)
    {
        target.StoreId = StoreId;
        target.Name = Name;
        target.Permalink = Permalink;
        target.DefaultCultureName = DefaultCultureName;
        target.OrganizationId = OrganizationId;
        target.Visibility = Visibility;
        target.UserGroups = UserGroups;
        target.StartDate = StartDate;
        target.EndDate = EndDate;
    }
}
