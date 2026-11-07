using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Data.Theme;

public class SchemaCatalogService(IThemeStorageFactory themeStorageFactory) : ISchemaCatalogService
{
    private const string HashProperty = "hash";

    // The hash is used as a file name.
    private static readonly Regex HashPattern = new("^[A-Za-z0-9_-]{8,128}$", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly string[] SchemaKinds =
    [
        ModuleConstants.SchemaKinds.Sections,
        ModuleConstants.SchemaKinds.Blocks,
        ModuleConstants.SchemaKinds.Shared,
    ];

    public virtual async Task<string> SaveAsync(string storeId, string catalogJson, CancellationToken cancellationToken = default)
    {
        var catalog = ParseCatalog(catalogJson);
        var hash = GetHash(catalog);

        var storage = await themeStorageFactory.OpenAsync(storeId);
        if (await storage.ReadAsync(GetCatalogPath(hash)) == null)
        {
            await storage.WriteAsync(GetCatalogPath(hash), catalog.ToString(Formatting.None));
        }

        var pointer = AbstractTypeFactory<SchemaCatalogPointer>.TryCreateInstance();
        pointer.Hash = hash;
        await storage.WriteJsonAsync(ThemeStorage.CurrentCatalogFile, pointer);

        return hash;
    }

    public virtual async Task<string> GetAsync(string storeId, string hash, CancellationToken cancellationToken = default)
    {
        var storage = await themeStorageFactory.OpenAsync(storeId);
        if (string.IsNullOrEmpty(hash))
        {
            hash = (await storage.ReadJsonAsync<SchemaCatalogPointer>(ThemeStorage.CurrentCatalogFile))?.Hash;
        }

        return IsValidHash(hash) ? await storage.ReadAsync(GetCatalogPath(hash)) : null;
    }

    protected virtual bool IsValidHash(string hash) => !string.IsNullOrEmpty(hash) && HashPattern.IsMatch(hash);

    /// <summary>Throws <see cref="ArgumentException"/> for a catalog without a valid hash or with a malformed schema map.</summary>
    protected virtual JObject ParseCatalog(string catalogJson)
    {
        JObject catalog;
        try
        {
            catalog = JObject.Parse(catalogJson ?? string.Empty);
        }
        catch (JsonReaderException)
        {
            throw new ArgumentException("The catalog is not a JSON object.", nameof(catalogJson));
        }

        if (!IsValidHash(GetHash(catalog)))
        {
            throw new ArgumentException("The catalog has no valid hash.", nameof(catalogJson));
        }

        var malformed = SchemaKinds.FirstOrDefault(kind =>
            catalog[kind] is { Type: not JTokenType.Null } schemas && (schemas is not JObject map || map.Properties().Any(x => x.Value is not JObject)));
        if (malformed != null)
        {
            throw new ArgumentException($"The catalog's \"{malformed}\" must name a schema object for each key.", nameof(catalogJson));
        }

        return catalog;
    }

    protected virtual string GetHash(JObject catalog) => catalog[HashProperty] is JValue { Type: JTokenType.String } hash ? (string)hash : null;

    protected virtual string GetCatalogPath(string hash) => $"{ThemeStorage.CatalogsFolder}/{hash}.json";
}
