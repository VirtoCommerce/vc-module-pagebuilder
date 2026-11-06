namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public sealed record ThemeSnapshot(string StoreId, string CultureName, int Publication, string Content, IReadOnlyList<string> Menus);
