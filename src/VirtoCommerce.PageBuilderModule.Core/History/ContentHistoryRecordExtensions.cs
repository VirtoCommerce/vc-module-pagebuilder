using System.Security.Claims;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Core.History;

public static class ContentHistoryRecordExtensions
{
    /// <summary>Sets the user who made the save.</summary>
    public static ContentHistoryRecord WithUser(this ContentHistoryRecord record, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(record);

        record.UserName = user?.Identity?.Name;
        record.UserEmail = user?.FindFirstValue(ClaimTypes.Email);

        return record;
    }

    public static ContentHistoryRecord AddEntry(this ContentHistoryRecord record, HistoryItem item, string content)
    {
        ArgumentNullException.ThrowIfNull(record);

        var entry = AbstractTypeFactory<ContentHistoryEntry>.TryCreateInstance();
        entry.Item = item;
        entry.Content = content;
        record.Entries.Add(entry);

        return record;
    }
}
