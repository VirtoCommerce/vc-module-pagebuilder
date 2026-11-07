namespace VirtoCommerce.PageBuilderModule.Core.Models;

public enum DraftDiscardStatus
{
    Discarded,
    NoDraft,

    /// <summary>Nothing is published: the draft is the only copy and stays.</summary>
    OnlyCopy,
}
