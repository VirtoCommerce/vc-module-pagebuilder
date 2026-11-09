using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.Validation;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Data.Theme;

public class SchemaCatalogService(IThemeStorageFactory themeStorageFactory) : ISchemaCatalogService
{
    private const string HashProperty = "hash";

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
        if (await ReadCatalogAsync(storage, hash) == null)
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

        return IsValidHash(hash) ? await ReadCatalogAsync(storage, hash) : null;
    }

    /// <summary>Null for none and for a file cut off mid-write, which the next upload of the hash then replaces.</summary>
    protected virtual async Task<string> ReadCatalogAsync(IThemeStorage storage, string hash)
    {
        var catalog = await storage.ReadAsync(GetCatalogPath(hash));
        if (catalog == null)
        {
            return null;
        }

        try
        {
            ThemeJson.ParseObject(catalog);
            return catalog;
        }
        catch (JsonReaderException)
        {
            return null;
        }
    }

    protected virtual bool IsValidHash(string hash) => PageBuilderContentPaths.IsCatalogHash(hash);

    protected virtual JObject ParseCatalog(string catalogJson)
    {
        JObject catalog;
        try
        {
            catalog = ThemeJson.ParseObject(catalogJson ?? string.Empty);
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
