using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Tests;

// Only store lookup and save are supported: any new use of the store service must fail here
// instead of silently passing a test with an incomplete fake.
internal sealed class FakeStoreService(params Store[] stores) : IStoreService
{
    public string LastRequestedId { get; private set; }
    public bool? LastClone { get; private set; }
    public List<Store> Saved { get; } = [];

    public Task<IList<Store>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true)
    {
        LastRequestedId = ids.Single();
        LastClone = clone;
        return Task.FromResult<IList<Store>>((stores ?? []).Where(store => store != null && ids.Contains(store.Id)).ToList());
    }

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
