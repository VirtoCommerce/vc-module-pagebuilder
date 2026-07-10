using System.ComponentModel.DataAnnotations;
using VirtoCommerce.Platform.Core.Common;
using static VirtoCommerce.Platform.Data.Infrastructure.DbContextBase;

namespace VirtoCommerce.PageBuilderModule.Data.Models;

public class PageBuilderAssetReferenceEntity : Entity
{
    [StringLength(IdLength)]
    [Required]
    public string PageId { get; set; }

    [StringLength(2048)]
    [Required]
    public string NormalizedAssetUrl { get; set; }

    [StringLength(64)]
    [Required]
    public string NormalizedAssetUrlHash { get; set; }
}
