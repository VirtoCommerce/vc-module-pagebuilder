namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

public sealed class GroupedPageHistoryResult
{
    public IList<GroupedPageVersion> Versions { get; init; } = [];

    public bool HasMore { get; init; }

    /// <summary>Pass back as <c>after</c> for the next changes.</summary>
    public string EndCursor { get; init; }
}
