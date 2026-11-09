using System.Linq;
using FluentValidation;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Validation;
using VirtoCommerce.StoreModule.Core.Model;

namespace VirtoCommerce.PageBuilderModule.Tests;

internal static class RequestValidators
{
    public static IValidator<GitHubAppSetupRequest> AppSetup { get; } = new GitHubAppSetupRequestValidator();

    public static IValidator<PageBuilderStoreRequest> Stores(params string[] storeIds) =>
        new PageBuilderStoreRequestValidator(new FakeStoreService(storeIds.Select(id => new Store { Id = id, Name = id }).ToArray()));

    public static IValidator<ContentHistoryRequest> History(params string[] storeIds) =>
        new ContentHistoryRequestValidator(Stores(storeIds));

    public static IValidator<ContentHistoryRestoreRequest> Restore(params string[] storeIds) =>
        new ContentHistoryRestoreRequestValidator(History(storeIds));

    public static IValidator<PageBuilderContentRequest> Content(params string[] storeIds) =>
        new PageBuilderContentRequestValidator(Stores(storeIds), new PageBuilderContentFileValidator());

    public static IValidator<DesignerSaveRequest> DesignerSave { get; } = new DesignerSaveRequestValidator(new DesignerPageValidator());

    public static IValidator<DesignerDiscardRequest> DesignerDiscard { get; } = new DesignerDiscardRequestValidator(new DesignerPageValidator());
}
