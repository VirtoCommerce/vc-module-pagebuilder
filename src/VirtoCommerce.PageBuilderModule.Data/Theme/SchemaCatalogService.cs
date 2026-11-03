using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.Theme;

namespace VirtoCommerce.PageBuilderModule.Data.Theme;

/// <summary>
/// Catalogs in the active theme's <c>catalogs/</c> folder: <c>{hash}.json</c>, never changed once written, and
/// <c>current.json</c> naming the latest upload.
/// </summary>
public class SchemaCatalogService(ThemeStorageFactory storageFactory) : ISchemaCatalogService
{
    private const string Current = ThemeStorage.CatalogsFolder + "/current.json";

    // the hash names a file: no path characters
    private static readonly Regex HashPattern = new("^[A-Za-z0-9_-]{8,128}$", RegexOptions.None, TimeSpan.FromSeconds(1));

    public virtual async Task<string> SaveAsync(string storeId, string catalogJson, CancellationToken cancellationToken = default)
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

        var hash = catalog.Value<string>("hash");
        if (!IsHash(hash))
        {
            throw new ArgumentException("The catalog has no valid hash.", nameof(catalogJson));
        }

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
                hash = current == null ? null : JObject.Parse(current).Value<string>("hash");
            }
            catch (JsonReaderException)
            {
                hash = null;
            }
        }

        return IsHash(hash) ? await storage.ReadAsync(FileOf(hash)) : null;
    }

    public static bool IsHash(string hash) => !string.IsNullOrEmpty(hash) && HashPattern.IsMatch(hash);

    private static string FileOf(string hash) => $"{ThemeStorage.CatalogsFolder}/{hash}.json";
}
