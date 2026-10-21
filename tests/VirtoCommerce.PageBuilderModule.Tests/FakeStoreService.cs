using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Tests;

// Only store lookup is supported: any new use of the store service must fail here
// instead of silently passing a test with an incomplete fake.
internal sealed class FakeStoreService(Store store) : IStoreService
{
    public string LastRequestedId { get; private set; }
    public bool? LastClone { get; private set; }

    public Task<IList<Store>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true)
    {
        LastRequestedId = ids.Single();
        LastClone = clone;
        return Task.FromResult<IList<Store>>(store != null && ids.Contains(store.Id) ? [store] : []);
    }

    public Task<IList<Store>> GetByOuterIdsAsync(IList<string> outerIds, string responseGroup = null, bool clone = true) =>
        throw new NotSupportedException();
    public Task SaveChangesAsync(IList<Store> models) => throw new NotSupportedException();
    public Task DeleteAsync(IList<string> ids, bool softDelete = false) => throw new NotSupportedException();
    public Task<IList<string>> GetUserAllowedStoreIdsAsync(ApplicationUser user) => throw new NotSupportedException();
}
