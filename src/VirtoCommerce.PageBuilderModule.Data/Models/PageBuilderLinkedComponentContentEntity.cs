using System.ComponentModel.DataAnnotations;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Data.Models;

public class PageBuilderLinkedComponentContentEntity : Entity
{
    [Required]
    public string ComponentContent { get; set; }

    public virtual PageBuilderLinkedComponentEntity Component { get; set; }
}
