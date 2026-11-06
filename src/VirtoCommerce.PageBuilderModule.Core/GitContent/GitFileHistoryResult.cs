using System.Collections.Generic;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>A page of a site file's changes, newest first.</summary>
public class GitFileHistoryResult
{
    public IList<GitFileVersion> Versions { get; set; } = [];

    public bool HasMore { get; set; }

    /// <summary>Cursor for the next page.</summary>
    public string EndCursor { get; set; }
}
