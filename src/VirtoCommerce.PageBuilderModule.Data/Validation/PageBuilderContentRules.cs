using FluentValidation;
using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Data.Validation;

/// <summary>Shared, so that a path is checked the same way wherever a request names one.</summary>
public static class PageBuilderContentRules
{
    private const string EnvelopeError = nameof(EnvelopeError);

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

    public static IRuleBuilderOptions<T, string> PageEnvelope<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty()
            .WithMessage("Content is required.")
            .Must((_, content, context) =>
            {
                if (string.IsNullOrWhiteSpace(content) || PageContentEnvelope.TryValidate(content, out var error))
                {
                    return true;
                }

                context.MessageFormatter.AppendArgument(EnvelopeError, error);
                return false;
            })
            .WithMessage($"{{{EnvelopeError}}}");

    public static IRuleBuilderOptions<T, string> ReadVersion<T>(this IRuleBuilder<T, string> rule, bool trim = false) =>
        rule
            .Must(eTag => string.IsNullOrWhiteSpace(eTag) || PageBuilderContentVersion.IsWellFormed(trim ? eTag.Trim() : eTag))
            .WithMessage("The ETag must be the single strong ETag its read returned.");
}
