namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class CreateGroupedPageRequest : IHasStoreId
{
    public string StoreId { get; set; }

    public string Name { get; set; }

    public string Permalink { get; set; }

    public string CultureName { get; set; }

    public string OrganizationId { get; set; }

    public bool Visibility { get; set; }

    public string UserGroups { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string Content { get; set; }
}
