using System.ComponentModel.DataAnnotations;
using VirtoCommerce.Platform.Core.Common;
using static VirtoCommerce.Platform.Data.Infrastructure.DbContextBase;

namespace VirtoCommerce.PageBuilderModule.Data.Models;

public class PageBuilderSharedComponentReferenceEntity : Entity
{
    [StringLength(IdLength)]
    [Required]
    public string PageId { get; set; }

    [StringLength(IdLength)]
    [Required]
    public string SharedComponentId { get; set; }
}
