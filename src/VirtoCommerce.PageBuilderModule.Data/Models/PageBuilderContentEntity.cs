using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Data.Models;

public class PageBuilderContentEntity : Entity
{
    public string PageContent { get; set; }

    public virtual PageBuilderPageEntity Page { get; set; }
}
