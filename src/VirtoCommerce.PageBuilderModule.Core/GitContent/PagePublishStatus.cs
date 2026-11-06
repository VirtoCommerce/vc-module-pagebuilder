namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>Publish status of a page.</summary>
public class PagePublishStatus
{
    /// <summary>The page is on the base branch.</summary>
    public bool Published { get; set; }

    /// <summary>The editor's draft differs from the published page.</summary>
    public bool HasChanges { get; set; }

    /// <summary>A pull request of the page is open.</summary>
    public bool Pending { get; set; }

    /// <summary>The open pull request does not merge itself.</summary>
    public bool AwaitingMerge { get; set; }

    /// <summary>"git" or "blob".</summary>
    public string Flow { get; set; }

    /// <summary>Status on the release branch; <c>null</c> without one.</summary>
    public PageProductionStatus Production { get; set; }

    /// <summary>The live page differs from the base branch.</summary>
    public bool LiveBehind { get; set; }
}
