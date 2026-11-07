using FluentValidation;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.Theme;

namespace VirtoCommerce.PageBuilderModule.Data.Validation;

public class ThemePaletteGenerationRequestValidator : AbstractValidator<ThemePaletteGenerationRequest>
{
    public ThemePaletteGenerationRequestValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Primary)
            .Must(IsColor)
            .WithMessage("The primary color \"{PropertyValue}\" is not #rrggbb or #rgb.");

        RuleFor(x => x.Secondary)
            .Must(IsColor)
            .When(x => !string.IsNullOrEmpty(x.Secondary))
            .WithMessage("The secondary color \"{PropertyValue}\" is not #rrggbb or #rgb.");

        RuleFor(x => x.Tokens)
            .NotNull()
            .WithMessage("Name the palette tokens the storefront announced.")
            .SetValidator(new ThemePaletteTokensValidator());

        RuleFor(x => x.Tokens.Light)
            .NotEmpty()
            .When(x => x.Tokens?.Light != null)
            .WithMessage("Name the palette tokens the storefront announced.");
    }

    private static bool IsColor(string value) => PaletteColor.TryParse(value, out _);
}
