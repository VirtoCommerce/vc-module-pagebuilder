namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

public class PageProductionStatus
{
    public bool Published { get; set; }

    public bool Behind { get; set; }

    public bool Pending { get; set; }

    /// <summary>The open pull request does not merge itself.</summary>
    public bool AwaitingMerge { get; set; }
}
