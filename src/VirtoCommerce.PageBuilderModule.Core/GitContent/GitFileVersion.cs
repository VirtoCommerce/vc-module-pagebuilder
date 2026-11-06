using System;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>A change of a site file.</summary>
public class GitFileVersion
{
    public string Sha { get; set; }

    public string ShortSha => Sha?.Length > GitCommitSha.ShortShaLength ? Sha[..GitCommitSha.ShortShaLength] : Sha;

    public DateTime? Date { get; set; }

    public string Message { get; set; }

    public string VcUser { get; set; }

    /// <summary>Id of the designer save the change belongs to.</summary>
    public string SaveId { get; set; }

    public string AuthorName { get; set; }

    public string AuthorEmail { get; set; }

    public string AuthorLogin { get; set; }

    public bool Published { get; set; }
}
