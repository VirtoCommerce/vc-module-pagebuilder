using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Core.Theme;

/// <summary>A theme draft asked to be discarded, and what became of it.</summary>
public class DiscardedThemeDraft
{
    /// <summary>Path in the store's theme storage, e.g. "default/templates/product.json".</summary>
    public string Path { get; set; }

    public DraftDiscardStatus Status { get; set; }

    /// <summary>The live content, current again once the draft is gone.</summary>
    public string PublishedContent { get; set; }
}
