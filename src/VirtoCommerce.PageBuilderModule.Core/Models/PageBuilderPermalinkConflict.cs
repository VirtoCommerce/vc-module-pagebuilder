namespace VirtoCommerce.PageBuilderModule.Core.Models;

public enum PageBuilderPermalinkConflict
{
    /// <summary>
    /// No storefront visitor can get both pages at the same time.
    /// </summary>
    None,

    /// <summary>
    /// Only a visitor who belongs to user groups of both pages can get both of them.
    /// </summary>
    Possible,

    /// <summary>
    /// Some storefront visitors get both pages at the same time, and which one is shown is not defined.
    /// </summary>
    Certain,
}
