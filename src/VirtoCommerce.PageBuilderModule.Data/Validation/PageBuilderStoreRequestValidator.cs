using FluentValidation;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Data.Validation;

public class PageBuilderStoreRequestValidator : AbstractValidator<PageBuilderStoreRequest>
{
    public PageBuilderStoreRequestValidator(IStoreService storeService)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        // A store that is not there would still get folders, settings and a GitHub repository of its own.
        RuleFor(x => x.StoreId)
            .NotEmpty()
            .WithMessage("storeId is required.")
            .MustAsync(async (storeId, _) => await storeService.GetNoCloneAsync(storeId) != null)
            .WithMessage("Store \"{PropertyValue}\" does not exist.");
    }
}
