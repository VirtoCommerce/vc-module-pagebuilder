using System.ComponentModel.DataAnnotations;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Data.Models;

public class PageBuilderSharedComponentContentEntity : Entity
{
    [Required]
    public string ComponentContent { get; set; }

    public virtual PageBuilderSharedComponentEntity Component { get; set; }
}
