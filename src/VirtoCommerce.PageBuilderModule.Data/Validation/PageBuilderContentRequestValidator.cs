using FluentValidation;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Data.Validation;

public class PageBuilderContentRequestValidator : AbstractValidator<PageBuilderContentRequest>
{
    // GitHub's GraphQL connections return at most this many nodes.
    private const int MaxHistoryTake = 100;

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
            .When(x => x.Pages is { Count: > 0 } || x.LegacyDrafts is { Count: > 0 })
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

        RuleForEach(x => x.LegacyDrafts)
            .ContentPath()
            .Must(PageBuilderContentPaths.IsLegacyDraft)
            .WithMessage("Only \"-draft\" files can be deleted here, by their exact path: \"{PropertyValue}\" is not one.");

        RuleForEach(x => x.Files)
            .NotNull()
            .WithMessage("The list of files has an empty entry.")
            .SetValidator(fileValidator);

        RuleFor(x => x.Ref)
            .Must(GitCommitSha.IsSha)
            .When(x => x.Ref != null)
            .WithMessage("\"{PropertyValue}\" is not a commit sha.");

        RuleFor(x => x.Keyword)
            .Must(PageBuilderContentPaths.IsSearchKeyword)
            .When(x => !string.IsNullOrEmpty(x.Keyword))
            .WithMessage("Keyword \"{PropertyValue}\" may not contain \\ / : * ? \" < > | or a control character.");

        RuleFor(x => x.HistoryTake)
            .InclusiveBetween(1, MaxHistoryTake)
            .When(x => x.HistoryTake.HasValue)
            .WithMessage($"take must be between 1 and {MaxHistoryTake}.");

        RuleFor(x => x.HistoryAfter)
            .Must(PageBuilderContentPaths.IsHistoryCursor)
            .When(x => x.HistoryAfter != null)
            .WithMessage("\"{PropertyValue}\" is not a history cursor.");

        RuleFor(x => x.SchemaKind)
            .Must(PageBuilderContentPaths.IsSchemaKind)
            .When(x => x.SchemaKind != null || x.SchemaKey != null)
            .WithMessage(x => $"Unknown kind '{x.SchemaKind}'. Expected one of: {string.Join(", ", ModuleConstants.SchemaKinds.All)}.");

        RuleFor(x => x.SchemaKey)
            .Must(PageBuilderContentPaths.IsFolderName)
            .When(x => x.SchemaKind != null || x.SchemaKey != null)
            .WithMessage("\"{PropertyValue}\" is not a schema name.");

        RuleFor(x => x.StoreId)
            .NotEmpty()
            .MustAsync(async (storeId, _) => await storeService.GetNoCloneAsync(storeId) != null)
            .WithMessage("Store \"{PropertyValue}\" does not exist.");
    }
}
