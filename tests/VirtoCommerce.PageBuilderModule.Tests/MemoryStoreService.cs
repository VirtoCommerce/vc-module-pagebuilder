using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>Stores kept in memory: what is saved is recorded and read back. Nothing else is supported.</summary>
internal sealed class MemoryStoreService(params Store[] stores) : IStoreService
{
    public List<Store> Saved { get; } = [];

    public Task<IList<Store>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true) =>
        Task.FromResult<IList<Store>>(stores.Where(x => ids.Contains(x.Id)).ToList());

    public Task SaveChangesAsync(IList<Store> models)
    {
        Saved.AddRange(models);
        return Task.CompletedTask;
    }

    public Task<IList<Store>> GetByOuterIdsAsync(IList<string> outerIds, string responseGroup = null, bool clone = true) =>
        throw new NotSupportedException();
    public Task DeleteAsync(IList<string> ids, bool softDelete = false) => throw new NotSupportedException();
    public Task<IList<string>> GetUserAllowedStoreIdsAsync(ApplicationUser user) => throw new NotSupportedException();
}
