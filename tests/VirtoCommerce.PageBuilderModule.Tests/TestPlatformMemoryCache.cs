using System;
using Microsoft.Extensions.Caching.Memory;
using VirtoCommerce.Platform.Core.Caching;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <param name="defaults">The entry options the platform is configured with; none by default.</param>
internal sealed class TestPlatformMemoryCache(Func<MemoryCacheEntryOptions> defaults = null) : IPlatformMemoryCache
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public ICacheEntry CreateEntry(object key) => _cache.CreateEntry(key);
    public void Remove(object key) => _cache.Remove(key);
    public bool TryGetValue(object key, out object value) => _cache.TryGetValue(key, out value);
    public MemoryCacheEntryOptions GetDefaultCacheEntryOptions() => defaults?.Invoke() ?? new();
    public void Dispose() => _cache.Dispose();
}
