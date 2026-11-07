using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.StoreModule.Core.Model;

namespace VirtoCommerce.PageBuilderModule.Data.Extensions;

public static class StoreExtensions
{
    public static string GetThemeDefaultLanguage(this Store store)
    {
        return store.DefaultLanguage ?? store.Languages?.FirstOrDefault() ?? ModuleConstants.Theme.FallbackLanguage;
    }
}
