namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

public class PagePublishStatus
{
    public bool Published { get; set; }

    public bool HasChanges { get; set; }

    public bool Pending { get; set; }

    /// <summary>The open pull request does not merge itself.</summary>
    public bool AwaitingMerge { get; set; }

    public string Flow { get; set; }

    /// <summary><c>null</c> without a release branch.</summary>
    public PageProductionStatus Production { get; set; }

    public bool LiveBehind { get; set; }
}
