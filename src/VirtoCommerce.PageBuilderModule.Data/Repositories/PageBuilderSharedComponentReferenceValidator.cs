namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

internal static class PageBuilderSharedComponentReferenceValidator
{
    internal static void Validate(
        IEnumerable<string> sharedComponentIds,
        string pageStoreId,
        IReadOnlyDictionary<string, string> componentStores,
        ISet<string> contentIds)
    {
        foreach (var sharedComponentId in sharedComponentIds)
        {
            if (!componentStores.TryGetValue(sharedComponentId, out var componentStoreId))
            {
                throw new InvalidDataException($"Shared Component '{sharedComponentId}' was not found.");
            }

            if (!contentIds.Contains(sharedComponentId))
            {
                throw new InvalidDataException($"Shared Component '{sharedComponentId}' has no content.");
            }

            if (!string.Equals(componentStoreId, pageStoreId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Shared Component '{sharedComponentId}' belongs to a different store and cannot be inserted into this page.");
            }
        }
    }
}
