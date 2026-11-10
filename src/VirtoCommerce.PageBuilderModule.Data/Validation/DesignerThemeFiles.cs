namespace VirtoCommerce.PageBuilderModule.Data.Validation;

public static class DesignerThemeFiles
{
    public const string NamingError = "Name each theme file once, by its path.";

    // a leading '/' names the same theme file, as PageBuilderContentPaths.GetPathInTheme reads it
    public static bool AreNamedOnce(IList<string> paths) =>
        paths == null ||
        (!paths.Any(string.IsNullOrEmpty) && paths.Select(x => x.TrimStart('/')).Distinct(StringComparer.OrdinalIgnoreCase).Count() == paths.Count);
}
