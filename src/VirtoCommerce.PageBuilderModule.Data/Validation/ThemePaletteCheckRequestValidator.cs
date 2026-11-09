using FluentValidation;
using VirtoCommerce.PageBuilderModule.Core.Theme;

namespace VirtoCommerce.PageBuilderModule.Data.Validation;

public class ThemePaletteCheckRequestValidator : AbstractValidator<ThemePaletteCheckRequest>
{
    public ThemePaletteCheckRequestValidator(IValidator<ThemePaletteTokens> tokensValidator)
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Palette)
            .NotNull()
            .WithMessage("Send the palette to check.")
            .Must(x => (x.Light?.Count ?? 0) <= ThemePaletteTokensValidator.MaxTokens && (x.Dark?.Count ?? 0) <= ThemePaletteTokensValidator.MaxTokens)
            .WithMessage($"A palette has at most {ThemePaletteTokensValidator.MaxTokens} colors per color scheme.");

        RuleFor(x => x.Tokens)
            .NotNull()
            .WithMessage("Name the palette tokens the storefront announced.")
            .SetValidator(tokensValidator);
    }
}
