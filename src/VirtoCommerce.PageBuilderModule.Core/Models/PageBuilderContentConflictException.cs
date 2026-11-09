namespace VirtoCommerce.PageBuilderModule.Core.Models;

public sealed class PageBuilderContentConflictException(string message) : Exception(message)
{
    public const string ThemeFileMessage = "A theme file was saved by someone else since it was read.";

    public PageBuilderContentConflictException() : this("The page content version does not match the current version.")
    {
    }
}
