namespace VirtoCommerce.PageBuilderModule.Data.Validation;

public static class DesignerThemeFiles
{
    public const string NamingError = "Name each theme file once, by its path.";

    public static bool AreNamedOnce(IList<string> paths) =>
        paths == null ||
        (!paths.Any(string.IsNullOrEmpty) && paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() == paths.Count);
}
