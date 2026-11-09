namespace VirtoCommerce.PageBuilderModule.Core.Theme;

/// <summary>A theme file as the designer edits it: its draft, else what is live, and its version.</summary>
public class ThemeFileContent : ThemeFileVersion
{
    /// <summary>Null when the file has neither a draft nor a live version.</summary>
    public string Content { get; set; }
}
