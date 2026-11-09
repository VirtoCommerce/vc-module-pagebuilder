using System.Security.Claims;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Core.History;

public class ContentHistoryRecord
{
    public string Description { get; set; }

    public string SaveId { get; set; }

    public string UserName { get; set; }

    public string UserEmail { get; set; }

    public IList<ContentHistoryEntry> Entries { get; set; } = [];

    public static ContentHistoryRecord Create(string description, string saveId, ClaimsPrincipal user)
    {
        var record = AbstractTypeFactory<ContentHistoryRecord>.TryCreateInstance().WithUser(user);
        record.Description = description;
        record.SaveId = saveId;

        return record;
    }
}
