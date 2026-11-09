using FluentValidation;
using VirtoCommerce.PageBuilderModule.Core.History;
using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Data.Validation;

public class ContentHistoryRestoreRequestValidator : AbstractValidator<ContentHistoryRestoreRequest>
{
    public ContentHistoryRestoreRequestValidator(IValidator<ContentHistoryRequest> requestValidator)
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        Include(requestValidator);

        RuleForEach(x => x.Items)
            .Must(IsSavedItem)
            .WithMessage("\"{PropertyValue}\" is a theme file the builder does not save.");
    }

    private static bool IsSavedItem(string value) =>
        HistoryItem.TryParse(value, out var item) && (item.Type == HistoryItemType.Page || PageBuilderContentPaths.IsWritableThemeFile(item.Key));
}
