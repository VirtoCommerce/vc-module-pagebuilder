namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class PageBuilderStoreRequest : IHasStoreId
{
    public string StoreId { get; set; }

    /// <summary>The storefront's schema catalog the request is checked against, when it names one.</summary>
    public string CatalogHash { get; set; }
}
