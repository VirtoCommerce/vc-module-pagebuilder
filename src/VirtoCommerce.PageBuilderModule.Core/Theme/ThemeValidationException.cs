namespace VirtoCommerce.PageBuilderModule.Core.Theme;

public sealed class ThemeValidationException(IReadOnlyList<string> errors)
    : Exception($"The theme cannot be published: {string.Join("; ", errors)}")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
