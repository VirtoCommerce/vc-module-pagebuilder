using FluentValidation;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Data.Validation;

public class PageBuilderContentFileValidator : AbstractValidator<PageBuilderContentFile>
{
    public PageBuilderContentFileValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.ContentType)
            .Must(PageBuilderContentPaths.IsContentType)
            .WithMessage("The content type of a file must be themes, pages or blogs.");

        RuleFor(x => x.Path)
            .ContentPath();

        When(x => PageBuilderContentPaths.IsPageContentType(x.ContentType), () =>
        {
            RuleFor(x => x.Path)
                .PageDocument();

            RuleFor(x => x.Content)
                .Custom((content, context) =>
                {
                    foreach (var error in PageEnvelopeValidator.Validate(content))
                    {
                        context.AddFailure(nameof(PageBuilderContentFile.Content), $"{context.InstanceToValidate.Path}: {error}");
                    }
                });
        }).Otherwise(() =>
        {
            RuleFor(x => x.Path)
                .Must(PageBuilderContentPaths.IsJsonFile)
                .WithMessage("\"{PropertyValue}\" is not a theme JSON file.")
                .Must(PageBuilderContentPaths.IsInsideAThemeFolder)
                .WithMessage("\"{PropertyValue}\" is not inside a theme folder.")
                .Must(PageBuilderContentPaths.IsOutsideTheThemeConfig)
                .WithMessage("\"{PropertyValue}\" is in the theme's config folder, which is deployed with the theme: only config/settings_data.json is saved through the builder.");
        });
    }
}
