using FluentValidation;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Data.Validation;

public class PageBuilderContentRequestValidator : AbstractValidator<PageBuilderContentRequest>
{
    public PageBuilderContentRequestValidator(IStoreService storeService, IValidator<PageBuilderContentFile> fileValidator)
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        // A save has no type of its own: each file names one.
        RuleFor(x => x.ContentType)
            .Must(PageBuilderContentPaths.IsContentType)
            .Unless(x => x.ContentType == null && x.Files != null)
            .WithMessage("The content type must be themes, pages or blogs.");

        RuleFor(x => x.ContentType)
            .Must(PageBuilderContentPaths.IsPageContentType)
            .When(x => x.Pages is { Count: > 0 })
            .WithMessage("Only pages and blog articles are addressed here: the content type must be pages or blogs.");

        RuleFor(x => x.Theme)
            .Must(PageBuilderContentPaths.IsFolderName)
            .When(x => !string.IsNullOrEmpty(x.Theme))
            .WithMessage("Theme \"{PropertyValue}\" must be one folder name.");

        RuleForEach(x => x.Paths)
            .ContentPath();

        RuleForEach(x => x.Pages)
            .ContentPath()
            .PageDocument();

        RuleForEach(x => x.Files)
            .NotNull()
            .WithMessage("The list of files has an empty entry.")
            .SetValidator(fileValidator);

        RuleFor(x => x.Keyword)
            .Must(PageBuilderContentPaths.IsSearchKeyword)
            .When(x => !string.IsNullOrEmpty(x.Keyword))
            .WithMessage("Keyword \"{PropertyValue}\" may not contain \\ / : * ? \" < > | or a control character.");

        RuleFor(x => x.StoreId)
            .NotEmpty()
            .MustAsync(async (storeId, _) => await storeService.GetNoCloneAsync(storeId) != null)
            .WithMessage("Store \"{PropertyValue}\" does not exist.");
    }
}
