namespace VirtoCommerce.PageBuilderModule.Core.Models;

/// <summary>A request about one store: what the store check is asked about, and then validated.</summary>
public class PageBuilderStoreRequest : IHasStoreId
{
    public string StoreId { get; set; }
}
