using VirtoCommerce.Platform.Core.Caching;

namespace VirtoCommerce.PageBuilderModule.Data.Theme;

/// <summary>The cached snapshots of a store, keyed by its id: a publication resets them on every node Redis reaches.</summary>
public sealed class ThemeSnapshotCacheRegion : CancellableCacheRegion<ThemeSnapshotCacheRegion>;
