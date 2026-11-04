using FluentValidation;
using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Data.Validation;

public class PageBuilderContentFileValidator : AbstractValidator<PageBuilderContentFile>
{
    public PageBuilderContentFileValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.ContentType)
            .Must(PageBuilderContentPaths.IsContentType)
            .WithMessage("The content type of a file must be themes, pages or blogs.");

        RuleFor(x => x.Path)
            .Must(PageBuilderContentPaths.IsRelativePath)
            .WithMessage("Path \"{PropertyValue}\" must stay inside the store's content folder.");

        RuleFor(x => x.Path)
            .Must((file, path) => PageBuilderContentPaths.IsWritable(file.ContentType, path))
            .WithMessage("\"{PropertyValue}\" is not a file the designer writes: a .page document, a theme JSON file outside the theme's config folder, or the theme's config/settings_data.json.");
    }
}
