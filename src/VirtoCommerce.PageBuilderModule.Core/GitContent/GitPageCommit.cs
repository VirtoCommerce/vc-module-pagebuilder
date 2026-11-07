namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>A page committed to the editor's work branch.</summary>
public class GitPageCommit
{
    public string Path { get; set; }

    public string Branch { get; set; }

    public string CommitSha { get; set; }
}
