using System.ComponentModel.DataAnnotations;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using static VirtoCommerce.Platform.Data.Infrastructure.DbContextBase;

namespace VirtoCommerce.PageBuilderModule.Data.Models;

public class PageBuilderLinkedComponentEntity
    : AuditableEntity, IDataEntity<PageBuilderLinkedComponentEntity, PageBuilderLinkedComponent>
{
    [StringLength(IdLength)]
    [Required]
    public string StoreId { get; set; }

    [StringLength(ModuleConstants.LinkedComponents.NameMaxLength)]
    [Required]
    public string Name { get; set; }

    public virtual PageBuilderLinkedComponentContentEntity Content { get; set; }

    public PageBuilderLinkedComponent ToModel(PageBuilderLinkedComponent model)
    {
        model.Id = Id;
        model.CreatedBy = CreatedBy;
        model.CreatedDate = CreatedDate;
        model.ModifiedBy = ModifiedBy;
        model.ModifiedDate = ModifiedDate;
        model.StoreId = StoreId;
        model.Name = Name;

        return model;
    }

    public PageBuilderLinkedComponentEntity FromModel(
        PageBuilderLinkedComponent model,
        PrimaryKeyResolvingMap pkMap)
    {
        pkMap.AddPair(model, this);

        Id = model.Id;
        CreatedBy = model.CreatedBy;
        CreatedDate = model.CreatedDate;
        ModifiedBy = model.ModifiedBy;
        ModifiedDate = model.ModifiedDate;
        StoreId = model.StoreId;
        Name = model.Name;

        return this;
    }

    public void Patch(PageBuilderLinkedComponentEntity target)
    {
        if (!string.Equals(target.StoreId, StoreId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Linked Component '{target.Id}' cannot be moved from store '{target.StoreId}' to '{StoreId}'.");
        }

        target.Name = Name;
    }
}
