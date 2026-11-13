using FluentValidation;

namespace VirtoCommerce.PageBuilderModule.Data.Validation;

/// <summary>Shared, so that a path is checked the same way wherever a request names one.</summary>
public static class PageBuilderContentRules
{
    public static IRuleBuilderOptions<T, string> ContentPath<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty()
            .WithMessage("A path is required.")
            .Must(PageBuilderContentPaths.HasPathCharactersOnly)
            .WithMessage("Path \"{PropertyValue}\" contains \"\\\", \":\" or a control character.")
            .Must(PageBuilderContentPaths.StaysInsideTheFolder)
            .WithMessage("Path \"{PropertyValue}\" must stay inside the store's content folder.")
            .Must(PageBuilderContentPaths.HasPortableNames)
            .WithMessage("Path \"{PropertyValue}\" has a name ending in a dot or a space, which Windows storage reads as another name.");

    public static IRuleBuilderOptions<T, string> PageDocument<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .Must(PageBuilderContentPaths.IsPageDocument)
            .WithMessage("\"{PropertyValue}\" is not a page document: pages and blog articles are .page or .page-draft files.");
}
