using System.Text.RegularExpressions;
using FluentValidation;
using VirtoCommerce.PageBuilderModule.Core.Theme;

namespace VirtoCommerce.PageBuilderModule.Data.Validation;

public class ThemePaletteTokensValidator : AbstractValidator<ThemePaletteTokens>
{
    // a few times the storefront's presets
    public const int MaxTokens = 1000;

    private static readonly Regex TokenPattern = new("^[a-z0-9_]{1,100}$", RegexOptions.None, TimeSpan.FromSeconds(1));

    public ThemePaletteTokensValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Light)
            .NotNull()
            .Must(x => x.Count <= MaxTokens)
            .WithMessage($"Name at most {MaxTokens} light tokens.");

        RuleFor(x => x.Dark)
            .NotNull()
            .Must(x => x.Count <= MaxTokens)
            .WithMessage($"Name at most {MaxTokens} dark tokens.");

        RuleForEach(x => x.Light)
            .Must(IsToken)
            .WithMessage("\"{PropertyValue}\" is not a palette token.");

        RuleForEach(x => x.Dark)
            .Must(IsToken)
            .WithMessage("\"{PropertyValue}\" is not a palette token.");
    }

    private static bool IsToken(string token) => token != null && TokenPattern.IsMatch(token);
}
