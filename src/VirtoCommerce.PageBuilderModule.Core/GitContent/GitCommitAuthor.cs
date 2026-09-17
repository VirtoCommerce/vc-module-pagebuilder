namespace VirtoCommerce.PageBuilderModule.Core.GitContent
{
    /// <summary>
    /// Maps the VC user to the git commit author (the audit trail of who edited what on the site).
    /// </summary>
    public class GitCommitAuthor
    {
        public string Name { get; set; }
        public string Email { get; set; }
    }
}
