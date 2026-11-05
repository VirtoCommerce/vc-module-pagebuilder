using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.Theme;

namespace VirtoCommerce.PageBuilderModule.Data.Theme;

/// <summary>
/// The catalogs kept in the store's active theme: one per hash, never changed once written, and which one was
/// uploaded last.
/// </summary>
public class SchemaCatalogService(ThemeStorageFactory storageFactory) : ISchemaCatalogService
{
    private const string Current = ThemeStorage.CatalogsFolder + "/current.json";

    // the hash names a file: no path characters
    private static readonly Regex HashPattern = new("^[A-Za-z0-9_-]{8,128}$", RegexOptions.None, TimeSpan.FromSeconds(1));

    // the parts a publication looks schemas up in, by name
    private static readonly string[] SchemaMaps = ["sections", "blocks", "shared"];

    public virtual async Task<string> SaveAsync(string storeId, string catalogJson, CancellationToken cancellationToken = default)
    {
        var catalog = Parse(catalogJson);
        var hash = HashOf(catalog);

        var storage = await storageFactory.OpenAsync(storeId);
        if (await storage.ReadAsync(FileOf(hash)) == null)
        {
            await storage.WriteAsync(FileOf(hash), catalog.ToString(Formatting.None));
        }
        await storage.WriteAsync(Current, new JObject { ["hash"] = hash }.ToString(Formatting.None));

        return hash;
    }

    public virtual async Task<string> GetAsync(string storeId, string hash, CancellationToken cancellationToken = default)
    {
        var storage = await storageFactory.OpenAsync(storeId);
        if (string.IsNullOrEmpty(hash))
        {
            var current = await storage.ReadAsync(Current);
            try
            {
                hash = current == null ? null : HashOf(JObject.Parse(current));
            }
            catch (JsonReaderException)
            {
                hash = null;
            }
        }

        return IsHash(hash) ? await storage.ReadAsync(FileOf(hash)) : null;
    }

    public static bool IsHash(string hash) => !string.IsNullOrEmpty(hash) && HashPattern.IsMatch(hash);

    /// <summary>
    /// The catalog, refused with <see cref="ArgumentException"/> unless it is an object with a valid hash and every
    /// part a publication reads schemas from maps names to schema objects: a hash keeps the content it is first
    /// saved with, so a catalog no publication can read must not be kept.
    /// </summary>
    private static JObject Parse(string catalogJson)
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

        if (!IsHash(HashOf(catalog)))
        {
            throw new ArgumentException("The catalog has no valid hash.", nameof(catalogJson));
        }

        var malformed = SchemaMaps.FirstOrDefault(name =>
            catalog[name] is { Type: not JTokenType.Null } schemas && (schemas is not JObject map || map.Properties().Any(x => x.Value is not JObject)));
        if (malformed != null)
        {
            throw new ArgumentException($"The catalog's \"{malformed}\" must name a schema object for each key.", nameof(catalogJson));
        }

        return catalog;
    }

    private static string HashOf(JObject catalog) => catalog["hash"] is JValue { Type: JTokenType.String } hash ? (string)hash : null;

    private static string FileOf(string hash) => $"{ThemeStorage.CatalogsFolder}/{hash}.json";
}
