namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public class ThemeSnapshotSet
{
    /// <summary>Snapshot JSON by language.</summary>
    public IDictionary<string, string> Files { get; set; }

    public IList<string> Menus { get; set; }
}
