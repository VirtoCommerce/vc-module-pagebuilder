namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public class ThemePublishResult
{
    public int Publication { get; set; }

    public IList<PublishedThemeFile> PublishedFiles { get; set; }
}
