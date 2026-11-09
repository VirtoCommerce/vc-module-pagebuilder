using FluentValidation;
using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Data.Validation;

/// <summary>
/// The shape of a designer save. The paths are checked as every content request's are, once the files are read; a version a
/// read returned is required (428) where the save is made.
/// </summary>
public class DesignerSaveRequestValidator : AbstractValidator<DesignerSaveRequest>
{
    public DesignerSaveRequestValidator(IValidator<DesignerSavePage> pageValidator)
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x)
            .Must(x => x.Page != null || x.ThemeFiles?.Count > 0)
            .WithMessage("Name the page or a theme file.");

        RuleFor(x => x.ThemeFiles)
            .Must(files => DesignerThemeFiles.AreNamedOnce(files?.Select(x => x?.Path).ToList()))
            .WithMessage(DesignerThemeFiles.NamingError);

        RuleFor(x => x.Page)
            .SetValidator(pageValidator)
            .When(x => x.Page != null);

        RuleFor(x => x.Page.Content)
            .PageEnvelope()
            .When(x => !string.IsNullOrEmpty(x.Page?.GroupId));

        // the page's version is read trimmed, as a page content save reads its If-Match
        RuleFor(x => x.Page.ETag)
            .ReadVersion(trim: true)
            .When(x => x.Page != null);

        RuleForEach(x => x.ThemeFiles)
            .ChildRules(file => file.RuleFor(x => x.ETag).ReadVersion());
    }
}
