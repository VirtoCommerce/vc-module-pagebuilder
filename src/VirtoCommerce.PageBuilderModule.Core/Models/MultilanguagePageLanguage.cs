namespace VirtoCommerce.PageBuilderModule.Core.Models;

/// <summary>
/// A row of the language table of a <see cref="MultilanguagePage"/>: one per store language, plus languages
/// that have a version but were removed from the store.
/// </summary>
public class MultilanguagePageLanguage
{
    /// <summary>The language has no version yet.</summary>
    public const string Missing = "Missing";

    public string CultureName { get; set; }

    /// <summary>Source language of the multilanguage page.</summary>
    public bool IsDefault { get; set; }

    /// <summary><c>false</c> when the store no longer has this language.</summary>
    public bool IsStoreLanguage { get; set; }

    /// <summary>Draft, Published, Archived (see <see cref="GroupedPageBuilderPage.Status"/>) or <see cref="Missing"/>.</summary>
    public string Status { get; set; }

    /// <summary>A published version also has unpublished changes.</summary>
    public bool HasChanges { get; set; }

    /// <summary>The source language version was published after this translation was.</summary>
    public bool IsOutdated { get; set; }

    /// <summary>The language version; null when <see cref="Status"/> is <see cref="Missing"/>.</summary>
    public GroupedPageBuilderPage Page { get; set; }
}
