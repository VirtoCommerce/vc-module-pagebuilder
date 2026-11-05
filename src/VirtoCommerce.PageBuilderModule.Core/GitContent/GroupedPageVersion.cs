namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>One change of a site's file: who, when, what, and whether it is the one the storefront shows.</summary>
public sealed class GroupedPageVersion
{
    public string Sha { get; init; }

    public string ShortSha => Sha?.Length > 7 ? Sha[..7] : Sha;

    public DateTime? Date { get; init; }

    public string Message { get; init; }

    public string VcUser { get; init; }

    /// <summary>The designer action this change is part of; the changes of one Save share it.</summary>
    public string SaveId { get; init; }

    public string AuthorName { get; init; }

    public string AuthorEmail { get; init; }

    public string AuthorLogin { get; init; }

    public bool Published { get; init; }
}
