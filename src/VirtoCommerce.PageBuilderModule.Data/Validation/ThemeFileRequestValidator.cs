using FluentValidation;
using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Data.Validation;

public class ThemeFileRequestValidator : AbstractValidator<ThemeFileRequest>
{
    public ThemeFileRequestValidator(IValidator<PageBuilderStoreRequest> storeRequestValidator)
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Path)
            .Must(PageBuilderContentPaths.IsThemeFilePath)
            .WithMessage("Path \"{PropertyValue}\" must be a theme folder followed by a file path.");

        Include(storeRequestValidator);
    }
}
