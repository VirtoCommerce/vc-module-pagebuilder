namespace VirtoCommerce.PageBuilderModule.Core.Models;

public enum PageBuilderPermalinkConflict
{
    /// <summary>
    /// The storefront tells the pages apart: they are for different visitors or dates, or one is more specific.
    /// </summary>
    None,

    /// <summary>
    /// Equally specific pages that meet for some visitors or dates only; there the page published last is shown,
    /// elsewhere the other page still is.
    /// </summary>
    Possible,

    /// <summary>
    /// The page is shown to every visitor and on every date the other page is, so the other page is never shown again.
    /// </summary>
    Certain,
}
