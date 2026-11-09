namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public class ThemeSnapshotOptions
{
    public const string SectionName = "PageBuilder:ThemeSnapshot";

    /// <summary>A shorter platform cache expiration is kept.</summary>
    public TimeSpan CacheAbsoluteExpiration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>In gzip bytes.</summary>
    public int SizeWarningThreshold { get; set; } = 30 * 1024;
}
