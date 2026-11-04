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

        RuleFor(x => x.Theme)
            .Must(PageBuilderContentPaths.IsFolderName)
            .When(x => !string.IsNullOrEmpty(x.Theme))
            .WithMessage("Theme \"{PropertyValue}\" must be one folder name.");

        RuleForEach(x => x.Paths)
            .Must(PageBuilderContentPaths.IsRelativePath)
            .WithMessage("Path \"{PropertyValue}\" must stay inside the store's content folder.");

        RuleForEach(x => x.Files)
            .NotNull()
            .SetValidator(fileValidator);

        RuleFor(x => x.StoreId)
            .NotEmpty()
            .MustAsync(async (storeId, _) => await storeService.GetNoCloneAsync(storeId) != null)
            .WithMessage("Store \"{PropertyValue}\" does not exist.");
    }
}
