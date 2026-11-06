using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Validation;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>The request validators the controllers get, over a platform that has these stores.</summary>
internal static class RequestValidators
{
    public static IValidator<PageBuilderStoreRequest> Stores(params string[] storeIds) =>
        new PageBuilderStoreRequestValidator(new KnownStores(storeIds));

    public static IValidator<ThemeFileRequest> ThemeFiles(params string[] storeIds) =>
        new ThemeFileRequestValidator(Stores(storeIds));

    public static IValidator<GitHubAppSetupRequest> AppSetup { get; } = new GitHubAppSetupRequestValidator();

    public static IValidator<PageBuilderContentRequest> Content(params string[] storeIds) =>
        new PageBuilderContentRequestValidator(new KnownStores(storeIds), new PageBuilderContentFileValidator());

    // Only store lookup is supported: any other use of the store service must fail here.
    private sealed class KnownStores(string[] storeIds) : IStoreService
    {
        public Task<IList<Store>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true) =>
            Task.FromResult<IList<Store>>(ids.Where(storeIds.Contains).Select(id => new Store { Id = id, Name = id }).ToList());

        public Task<IList<Store>> GetByOuterIdsAsync(IList<string> outerIds, string responseGroup = null, bool clone = true) =>
            throw new NotSupportedException();
        public Task SaveChangesAsync(IList<Store> models) => throw new NotSupportedException();
        public Task DeleteAsync(IList<string> ids, bool softDelete = false) => throw new NotSupportedException();
        public Task<IList<string>> GetUserAllowedStoreIdsAsync(ApplicationUser user) => throw new NotSupportedException();
    }
}
