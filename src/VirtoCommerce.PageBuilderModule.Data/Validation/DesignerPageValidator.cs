using FluentValidation;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Data.Validation;

/// <summary>The page of a designer save or discard: a database page by its group, or a file page by its path and type.</summary>
public class DesignerPageValidator : AbstractValidator<DesignerSavePage>
{
    public DesignerPageValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x)
            .Must(x => string.IsNullOrEmpty(x.GroupId) != string.IsNullOrEmpty(x.Path))
            .WithMessage("Name the page by its groupId (a database page) or by its path and type (a file page).");

        RuleFor(x => x.GroupId)
            .Must(PageBuilderContentPaths.IsFolderName)
            .When(x => !string.IsNullOrEmpty(x.GroupId))
            .WithMessage("\"{PropertyValue}\" is not a page group id.");

        RuleFor(x => x.Type)
            .Must(GitPageLocation.IsPageContent)
            .When(x => !string.IsNullOrEmpty(x.Path))
            .WithMessage("A file page is a page or a blog article: a theme file goes with the theme files.");
    }
}
