namespace VirtoCommerce.PageBuilderModule.Core.Models;

public static class PageBuilderPageName
{
    // The length of the name column: a longer name fails in the database with its SQL error text.
    public const int MaxLength = 1024;

    public static readonly string InvalidMessage = "Name must be at most " + MaxLength + " characters.";

    public static bool IsValid(string name)
    {
        return name == null || name.Length <= MaxLength;
    }
}
