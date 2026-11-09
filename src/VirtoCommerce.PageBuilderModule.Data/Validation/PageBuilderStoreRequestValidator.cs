using FluentValidation;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Data.Validation;

public class PageBuilderStoreRequestValidator : AbstractValidator<PageBuilderStoreRequest>
{
    public PageBuilderStoreRequestValidator(IStoreService storeService)
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CatalogHash)
            .Must(PageBuilderContentPaths.IsCatalogHash)
            .When(x => x.CatalogHash != null)
            .WithMessage("\"{PropertyValue}\" is not a catalog hash.");

        RuleFor(x => x.StoreId)
            .NotEmpty()
            .MustAsync(async (storeId, _) => await storeService.GetNoCloneAsync(storeId) != null)
            .WithMessage("Store \"{PropertyValue}\" does not exist.");
    }
}
