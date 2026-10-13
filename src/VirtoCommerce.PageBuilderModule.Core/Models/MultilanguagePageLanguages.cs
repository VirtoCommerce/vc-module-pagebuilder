namespace VirtoCommerce.PageBuilderModule.Core.Models;

public static class MultilanguagePageLanguages
{
    /// <summary>
    /// Language table: the default language first, then the other store languages, then versions whose language
    /// was removed from the store.
    /// </summary>
    public static IList<MultilanguagePageLanguage> Build(
        MultilanguagePage root,
        IEnumerable<string> storeLanguages,
        IEnumerable<GroupedPageBuilderPage> versions)
    {
        var versionsByCulture = versions
            .Where(x => !string.IsNullOrEmpty(x.CultureName))
            .GroupBy(x => x.CultureName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var languages = new[] { root.DefaultCultureName }
            .Concat(storeLanguages ?? [])
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var removedLanguages = versionsByCulture.Keys
            .Where(x => !languages.Contains(x, StringComparer.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase);

        return languages.Select(x => Create(root, x, versionsByCulture.GetValueOrDefault(x), isStoreLanguage: true))
            .Concat(removedLanguages.Select(x => Create(root, x, versionsByCulture[x], isStoreLanguage: false)))
            .ToList();
    }

    private static MultilanguagePageLanguage Create(MultilanguagePage root, string cultureName, GroupedPageBuilderPage page, bool isStoreLanguage)
    {
        return new MultilanguagePageLanguage
        {
            CultureName = page?.CultureName ?? cultureName,
            IsDefault = string.Equals(cultureName, root.DefaultCultureName, StringComparison.OrdinalIgnoreCase),
            IsStoreLanguage = isStoreLanguage,
            Status = page?.Status ?? MultilanguagePageLanguage.Missing,
            HasChanges = page != null && page.Status == ModuleConstants.PageStatuses.Published && page.HasChanges,
            Page = page,
        };
    }
}
