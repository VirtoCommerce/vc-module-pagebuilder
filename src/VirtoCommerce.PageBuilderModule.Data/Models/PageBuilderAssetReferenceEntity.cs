using System.ComponentModel.DataAnnotations;
using VirtoCommerce.Platform.Core.Common;
using static VirtoCommerce.Platform.Data.Infrastructure.DbContextBase;

namespace VirtoCommerce.PageBuilderModule.Data.Models;

public class PageBuilderAssetReferenceEntity : Entity
{
    [StringLength(IdLength)]
    [Required]
    public string PageId { get; set; }

    [StringLength(IdLength)]
    [Required]
    public string GroupId { get; set; }

    [StringLength(IdLength)]
    [Required]
    public string StoreId { get; set; }

    [StringLength(CultureNameLength)]
    public string CultureName { get; set; }

    [Required]
    public PageBuilderPageStatus Status { get; set; }

    [StringLength(2048)]
    [Required]
    public string NormalizedAssetUrl { get; set; }

    [StringLength(64)]
    [Required]
    public string NormalizedAssetUrlHash { get; set; }
}
