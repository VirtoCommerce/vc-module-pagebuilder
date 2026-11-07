namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public sealed class ThemeValidationException(IReadOnlyList<string> errors)
    : Exception($"The theme is not valid: {string.Join("; ", errors)}")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
