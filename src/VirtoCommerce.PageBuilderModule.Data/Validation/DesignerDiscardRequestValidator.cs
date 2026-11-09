using FluentValidation;
using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Data.Validation;

/// <summary>The shape of a designer discard. The paths are checked as every content request's are.</summary>
public class DesignerDiscardRequestValidator : AbstractValidator<DesignerDiscardRequest>
{
    public DesignerDiscardRequestValidator(IValidator<DesignerSavePage> pageValidator)
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x)
            .Must(x => x.Page != null || x.ThemeFiles?.Count > 0)
            .WithMessage("Name the page or a theme file.");

        RuleFor(x => x.ThemeFiles)
            .Must(DesignerThemeFiles.AreNamedOnce)
            .WithMessage(DesignerThemeFiles.NamingError);

        RuleFor(x => x.Page)
            .SetValidator(pageValidator)
            .When(x => x.Page != null);
    }
}
