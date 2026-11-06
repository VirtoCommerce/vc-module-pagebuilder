using FluentValidation;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.GitContent;

namespace VirtoCommerce.PageBuilderModule.Data.Validation;

public class ThemeFileRequestValidator : AbstractValidator<ThemeFileRequest>
{
    public ThemeFileRequestValidator(IValidator<PageBuilderStoreRequest> storeRequestValidator)
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Path)
            .Must(ThemeFileHistoryService.IsThemePath)
            .WithMessage("\"{PropertyValue}\" is not a theme file: the theme's folder, then the file's path in it.");

        Include(storeRequestValidator);
    }
}
