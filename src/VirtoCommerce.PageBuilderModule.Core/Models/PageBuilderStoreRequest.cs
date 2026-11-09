namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class PageBuilderStoreRequest : IHasStoreId
{
    public string StoreId { get; set; }

    public string CatalogHash { get; set; }
}
