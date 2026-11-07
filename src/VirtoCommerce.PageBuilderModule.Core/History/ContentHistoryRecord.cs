using System.Security.Claims;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Core.History;

/// <summary>One save to record: what it changed, by whom and why.</summary>
public class ContentHistoryRecord
{
    public string Description { get; set; }

    /// <summary>The designer save the record belongs to.</summary>
    public string SaveId { get; set; }

    public string UserName { get; set; }

    public string UserEmail { get; set; }

    public IList<ContentHistoryEntry> Entries { get; set; } = [];

    /// <summary>Creates a record of a save by the user.</summary>
    public static ContentHistoryRecord Create(string description, string saveId, ClaimsPrincipal user)
    {
        var record = AbstractTypeFactory<ContentHistoryRecord>.TryCreateInstance().WithUser(user);
        record.Description = description;
        record.SaveId = saveId;

        return record;
    }
}
