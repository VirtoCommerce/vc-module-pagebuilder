namespace VirtoCommerce.PageBuilderModule.Core.Theme;

/// <summary>
/// A store's published theme in one language, as the storefront reads it (docs/contracts/theme-snapshot.md).
/// </summary>
/// <param name="Content">The snapshot JSON.</param>
/// <param name="Menus">The link list names the header and footer hold in their <c>menu</c> settings.</param>
public sealed record ThemeSnapshot(string StoreId, string CultureName, int Publication, string Content, IReadOnlyList<string> Menus);
