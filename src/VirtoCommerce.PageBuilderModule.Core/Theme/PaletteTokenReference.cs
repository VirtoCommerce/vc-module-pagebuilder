namespace VirtoCommerce.PageBuilderModule.Core.Theme;

/// <summary>A palette token and the token the storefront uses in its place when its palette does not have it.</summary>
public class PaletteTokenReference
{
    public string Token { get; set; }

    public string Fallback { get; set; }
}
