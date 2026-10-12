using Microsoft.Extensions.Caching.Memory;
using VirtoCommerce.Platform.Core.Caching;

namespace VirtoCommerce.PageBuilderModule.Tests;

internal sealed class TestPlatformMemoryCache : IPlatformMemoryCache
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public ICacheEntry CreateEntry(object key) => _cache.CreateEntry(key);
    public void Remove(object key) => _cache.Remove(key);
    public bool TryGetValue(object key, out object value) => _cache.TryGetValue(key, out value);
    public MemoryCacheEntryOptions GetDefaultCacheEntryOptions() => new();
    public void Dispose() => _cache.Dispose();
}
