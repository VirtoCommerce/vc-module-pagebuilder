namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>A store in the Sites panel: its repository and whether git content is on.</summary>
public class GitContentSite
{
    public string StoreId { get; set; }

    public string Name { get; set; }

    /// <summary>Repository in "owner/name" form.</summary>
    public string Repository { get; set; }

    public string Url { get; set; }

    public bool Enabled { get; set; }
}
