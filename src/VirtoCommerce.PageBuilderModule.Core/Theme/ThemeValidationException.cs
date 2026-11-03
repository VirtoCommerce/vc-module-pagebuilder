namespace VirtoCommerce.PageBuilderModule.Core.Theme;

/// <summary>Theme files a publication refused, one message per problem.</summary>
public sealed class ThemeValidationException(IReadOnlyList<string> errors)
    : Exception($"The theme cannot be published: {string.Join("; ", errors)}")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
