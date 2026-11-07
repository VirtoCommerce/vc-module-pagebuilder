namespace VirtoCommerce.PageBuilderModule.Core.History;

public enum HistoryItemType
{
    /// <summary>A database page, by its group id.</summary>
    Page,

    /// <summary>A theme file, by its path with the theme folder: "default/templates/product.json".</summary>
    Theme,
}
