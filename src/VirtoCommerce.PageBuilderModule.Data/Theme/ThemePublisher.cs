using System.Globalization;
using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Data.Theme;

/// <summary>
/// A publication, one store at a time: validate, write the snapshots, move the pointer, then put the drafts live
/// (docs/contracts/theme-snapshot.md, Publishing).
/// </summary>
public class ThemePublisher(
    ThemeStorageFactory storageFactory,
    IStoreService storeService,
    ISchemaCatalogService catalogService,
    IDistributedLock distributedLock,
    IEventPublisher eventPublisher,
    ILogger<ThemePublisher> logger,
    ThemeFileHistory themeFileHistory = null) : IThemePublisher
{
    private const string FallbackLanguage = "en-US";
    private const string Published = "Published";

    private static readonly string[] FixedFiles =
    [
        ThemeStorage.Rules,
        ThemeStorage.HeaderGroup,
        ThemeStorage.FooterGroup,
        ThemeStorage.Brand,
        ThemeStorage.SettingsData,
        ThemeStorage.Palette,
        ThemeStorage.PaletteDark,
    ];

    /// <summary>The lock theme saves and publications of a store share.</summary>
    public static string LockResource(string storeId) => $"PageBuilder:Theme:{storeId}";

    public virtual async Task<int> PublishAsync(string storeId, string catalogHash, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var catalog = await ReadCatalogAsync(storeId, catalogHash, cancellationToken);
        return await distributedLock.ExecuteAsync(
            LockResource(storeId),
            _ => PublishUnderLockAsync(storeId, catalog, catalogHash, user, drafts: true, cancellationToken),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// The first publication of a store that has none, from its live files: no draft is published, so an anonymous
    /// read may start it. The live files may predate the storefront's schemas, so only their shape is checked; the
    /// store's current catalog, when there is one, still tells which settings name menus. Returns the current
    /// publication instead when there is one, which another node may have written meanwhile.
    /// </summary>
    public virtual Task<int> PublishFirstAsync(string storeId, CancellationToken cancellationToken = default)
    {
        return distributedLock.ExecuteAsync(
            LockResource(storeId),
            async _ =>
            {
                var storage = await storageFactory.OpenAsync(storeId);
                if (await CurrentPublicationAsync(storage) is { } existing)
                {
                    return existing;
                }

                var current = await catalogService.GetAsync(storeId, hash: null, cancellationToken);
                var catalog = current == null ? null : JObject.Parse(current);
                return await PublishUnderLockAsync(storeId, catalog, catalogHash: null, user: null, drafts: false, cancellationToken);
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// The publication storefronts are served: the one the pointer names; when the pointer cannot be read (a write cut
    /// off on a file system), the highest complete one. <c>null</c> when there is none, and the first publication is due.
    /// </summary>
    public static async Task<int?> CurrentPublicationAsync(ThemeStorage storage)
    {
        var pointer = await storage.ReadAsync(ThemeStorage.Pointer);
        if (pointer == null)
        {
            return null;
        }

        if (ParsePointer(pointer) is { } publication)
        {
            return publication;
        }

        var publications = (await storage.ListFoldersAsync(ThemeStorage.SnapshotsFolder))
            .Select(x => int.TryParse(x, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .Where(x => x > 0)
            .OrderDescending();
        foreach (var candidate in publications)
        {
            if (await ReadDefaultLanguageAsync(storage, candidate) != null)
            {
                return candidate;
            }
        }
        return null;
    }

    /// <summary>
    /// The store default language a publication was written with, from its record; <c>null</c> when the publication
    /// is not complete: the record is written after every snapshot of the publication.
    /// </summary>
    public static async Task<string> ReadDefaultLanguageAsync(ThemeStorage storage, int publication)
    {
        var record = await storage.ReadAsync(RecordFile(publication));
        if (record == null)
        {
            return null;
        }

        try
        {
            return JObject.Parse(record).Value<string>("defaultLanguage") is { Length: > 0 } language ? language : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string SnapshotFile(int publication, string language) => $"{PublicationFolder(publication)}/{language}.json";

    private static string RecordFile(int publication) => $"{PublicationFolder(publication)}/publication.json";

    private static string PublicationFolder(int publication) =>
        $"{ThemeStorage.SnapshotsFolder}/{publication.ToString(CultureInfo.InvariantCulture)}";

    private static int? ParsePointer(string pointer)
    {
        if (pointer == null)
        {
            return null;
        }

        try
        {
            return JObject.Parse(pointer).Value<int?>("publication") is > 0 and var publication ? publication : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <param name="catalog">The storefront's schemas: what the theme is checked against, and where its menus are found.</param>
    /// <param name="catalogHash">The catalog the publication is checked against; <c>null</c> checks the shape only.</param>
    private async Task<int> PublishUnderLockAsync(string storeId, JObject catalog, string catalogHash, ClaimsPrincipal user, bool drafts, CancellationToken cancellationToken)
    {
        var store = await storeService.GetNoCloneAsync(storeId) ?? throw new KeyNotFoundException($"Store '{storeId}' not found.");
        var storage = await storageFactory.OpenAsync(storeId);

        var (source, draftFiles, errors) = await ReadSourceAsync(storage, drafts);
        errors.AddRange(ThemeSnapshotBuilder.Validate(source, catalogHash == null ? null : catalog));
        if (errors.Count > 0)
        {
            throw new ThemeValidationException(errors);
        }

        var languages = store.Languages?.ToList() ?? [];
        var defaultLanguage = store.DefaultLanguage ?? languages.FirstOrDefault() ?? FallbackLanguage;
        var publication = await NextPublicationAsync(storage);
        var set = ThemeSnapshotBuilder.Build(source, catalog, catalogHash, languages, defaultLanguage, publication, DateTime.UtcNow);

        foreach (var (language, json) in set.Files)
        {
            await storage.WriteAsync(SnapshotFile(publication, language), json);
            WarnWhenLarge(storeId, publication, language, json);
        }
        await storage.WriteAsync(RecordFile(publication), new JObject { ["defaultLanguage"] = defaultLanguage }.ToString(Formatting.None));
        await storage.WriteAsync(ThemeStorage.Pointer, new JObject { ["publication"] = publication }.ToString(Formatting.None));
        ThemeSnapshotCacheRegion.ExpireTokenForKey(storeId, propagate: true);

        await PutDraftsLiveAsync(storeId, storage, draftFiles, user, cancellationToken);
        return publication;
    }

    private async Task<JObject> ReadCatalogAsync(string storeId, string catalogHash, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(catalogHash))
        {
            throw new ThemeValidationException(["the publication names no schema catalog: preview the storefront first"]);
        }

        var catalog = await catalogService.GetAsync(storeId, catalogHash, cancellationToken)
            ?? throw new ThemeValidationException([$"there is no schema catalog '{catalogHash}': preview the storefront first"]);
        return JObject.Parse(catalog);
    }

    private static async Task<(ThemeSource Source, Dictionary<string, string> Drafts, List<string> Errors)> ReadSourceAsync(ThemeStorage storage, bool drafts)
    {
        var errors = new List<string>();
        var draftFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        async Task<JToken> ReadAsync(string file)
        {
            var draft = drafts ? await storage.ReadAsync(storage.DraftOf(file)) : null;
            if (draft != null)
            {
                draftFiles[file] = draft;
            }

            var text = draft ?? await storage.ReadAsync(file);
            if (text == null)
            {
                return null;
            }

            try
            {
                return JToken.Parse(text);
            }
            catch (JsonReaderException)
            {
                errors.Add($"{file}: not valid JSON");
                return null;
            }
        }

        var fixedTokens = new Dictionary<string, JToken>(StringComparer.Ordinal);
        foreach (var file in FixedFiles)
        {
            fixedTokens[file] = await ReadAsync(file);
        }

        var templates = new Dictionary<string, JToken>(StringComparer.Ordinal);
        var names = await storage.ListFilesAsync(ThemeStorage.TemplatesFolder);
        var keys = names
            .Select(x => ThemeSnapshotBuilder.TemplateKeyOf(x.EndsWith("-draft", StringComparison.Ordinal) ? x[..^"-draft".Length] : x))
            .Where(x => x != null)
            .Distinct(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            // a template saved only as a draft is not live yet: the first publication leaves it out
            if (await ReadAsync($"{ThemeStorage.TemplatesFolder}/{key}.json") is { } document)
            {
                templates[key] = document;
            }
        }

        var source = new ThemeSource
        {
            Theme = storage.Theme,
            Templates = templates,
            Rules = fixedTokens[ThemeStorage.Rules],
            Header = fixedTokens[ThemeStorage.HeaderGroup],
            Footer = fixedTokens[ThemeStorage.FooterGroup],
            Brand = fixedTokens[ThemeStorage.Brand],
            SettingsData = fixedTokens[ThemeStorage.SettingsData],
            Palette = fixedTokens[ThemeStorage.Palette],
            PaletteDark = fixedTokens[ThemeStorage.PaletteDark],
        };
        return (source, draftFiles, errors);
    }

    private static async Task<int> NextPublicationAsync(ThemeStorage storage)
    {
        var numbers = (await storage.ListFoldersAsync(ThemeStorage.SnapshotsFolder))
            .Select(x => int.TryParse(x, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .Append(ParsePointer(await storage.ReadAsync(ThemeStorage.Pointer)) ?? 0);
        return numbers.Max() + 1;
    }

    private async Task PutDraftsLiveAsync(string storeId, ThemeStorage storage, Dictionary<string, string> drafts, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (drafts.Count == 0)
        {
            return;
        }

        foreach (var (file, content) in drafts)
        {
            await storage.WriteAsync(file, content);
        }
        await storage.RemoveAsync(drafts.Keys.Select(storage.DraftOf));

        var changes = drafts.Keys
            .Select(file => new GenericChangedEntry<FileEntity>(new FileEntity { Path = storage.PathOf(file), Type = ThemeStorageFactory.ContentType }, EntryState.Modified))
            .ToList();
        await eventPublisher.Publish(new PageBuilderContentChangedEvent(ThemeStorageFactory.ContentType, changes), cancellationToken);

        if (themeFileHistory != null)
        {
            var save = Guid.NewGuid().ToString("N");
            foreach (var (file, content) in drafts)
            {
                await themeFileHistory.RecordAsync(storeId, storage.PathOf(file), content, Published, user, save, cancellationToken);
            }
        }
    }

    private void WarnWhenLarge(string storeId, int publication, string language, string json)
    {
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            gzip.Write(bytes, 0, bytes.Length);
        }

        if (compressed.Length > ModuleConstants.Theme.ThemeSnapshotSizeWarning)
        {
            logger.LogWarning(
                "The theme snapshot {Publication} of store {StoreId} in {Language} is {Size} bytes gzipped, more than {Limit}.",
                publication, storeId, language, compressed.Length, ModuleConstants.Theme.ThemeSnapshotSizeWarning);
        }
    }
}
