namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public class ThemeSnapshotOptions
{
    public const string SectionName = "PageBuilder:ThemeSnapshot";

    /// <summary>Absolute lifetime of a cached snapshot. A shorter platform cache expiration is kept.</summary>
    public TimeSpan CacheAbsoluteExpiration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Snapshot size, in gzip bytes, above which a warning is logged.</summary>
    public int SizeWarningThreshold { get; set; } = 30 * 1024;
}
