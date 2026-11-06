namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>Publish status of a page on the release branch.</summary>
public class GitProductionStatus
{
    public bool Published { get; set; }

    /// <summary>The release branch differs from the base branch.</summary>
    public bool Behind { get; set; }

    /// <summary>A promotion pull request of the page is open.</summary>
    public bool Pending { get; set; }

    /// <summary>The open pull request does not merge itself.</summary>
    public bool AwaitingMerge { get; set; }
}
