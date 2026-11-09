namespace VirtoCommerce.PageBuilderModule.Core.History;

/// <summary>What a history version can change: <c>page:{groupId}</c> or <c>theme:{path}</c>.</summary>
public sealed record HistoryItem(HistoryItemType Type, string Key)
{
    public const string PagePrefix = "page:";
    public const string ThemePrefix = "theme:";

    /// <summary>A theme file by its theme folder and path, without the leading "/" the admin may send: the history names it once.</summary>
    public static HistoryItem ForThemeFile(string path) => new(HistoryItemType.Theme, path?.TrimStart('/'));

    public static bool TryParse(string value, out HistoryItem item)
    {
        item = null;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (value.StartsWith(PagePrefix, StringComparison.Ordinal) && value.Length > PagePrefix.Length)
        {
            item = new HistoryItem(HistoryItemType.Page, value[PagePrefix.Length..]);
        }
        else if (value.StartsWith(ThemePrefix, StringComparison.Ordinal) && value.Length > ThemePrefix.Length)
        {
            item = new HistoryItem(HistoryItemType.Theme, value[ThemePrefix.Length..]);
        }

        return item != null;
    }

    public override string ToString() => $"{(Type == HistoryItemType.Page ? PagePrefix : ThemePrefix)}{Key}";
}
