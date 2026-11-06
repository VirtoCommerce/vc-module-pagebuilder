using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.ContentModule.Core;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.Extensions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Data.Theme;

public class ThemePublisher(
    IThemeStorageFactory themeStorageFactory,
    IThemeSnapshotBuilder themeSnapshotBuilder,
    IStoreService storeService,
    ISchemaCatalogService schemaCatalogService,
    IDistributedLock distributedLock,
    IEventPublisher eventPublisher,
    IThemeFileHistoryService themeFileHistory,
    IOptions<ThemeSnapshotOptions> options,
    ILogger<ThemePublisher> logger) : IThemePublisher
{
    private const string JsonExtension = ".json";

    private static readonly Dictionary<string, Action<ThemeSource, JToken>> ThemeParts = new(StringComparer.OrdinalIgnoreCase)
    {
        [ThemeStorage.TemplateRulesFile] = (source, content) => source.Rules = content,
        [ThemeStorage.HeaderGroupFile] = (source, content) => source.Header = content,
        [ThemeStorage.FooterGroupFile] = (source, content) => source.Footer = content,
        [ThemeStorage.BrandFile] = (source, content) => source.Brand = content,
        [ThemeStorage.SettingsDataFile] = (source, content) => source.SettingsData = content,
        [ThemeStorage.PaletteFile] = (source, content) => source.Palette = content,
        [ThemeStorage.DarkPaletteFile] = (source, content) => source.PaletteDark = content,
    };

    public virtual async Task<int> PublishAsync(string storeId, string catalogHash, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var catalog = await GetCatalogAsync(storeId, catalogHash, cancellationToken);
        var result = await distributedLock.ExecuteAsync(
            ThemeStorage.GetLockResource(storeId),
            _ => PublishUnderLockAsync(storeId, catalog, catalogHash, includeDrafts: true),
            cancellationToken: cancellationToken);

        await AfterPublishAsync(storeId, result.PublishedFiles, user, cancellationToken);

        return result.Publication;
    }

    public virtual Task<int> PublishFirstAsync(string storeId, CancellationToken cancellationToken = default)
    {
        return distributedLock.ExecuteAsync(
            ThemeStorage.GetLockResource(storeId),
            async _ =>
            {
                var storage = await themeStorageFactory.OpenAsync(storeId);
                if (await storage.GetCurrentPublicationAsync() is { } publication)
                {
                    return publication;
                }

                // Live files may predate the storefront schemas, so only their shape is validated.
                var currentCatalog = await schemaCatalogService.GetAsync(storeId, hash: null, cancellationToken);
                var catalog = currentCatalog == null ? null : JObject.Parse(currentCatalog);
                var result = await PublishUnderLockAsync(storeId, catalog, catalogHash: null, includeDrafts: false);

                return result.Publication;
            },
            cancellationToken: cancellationToken);
    }

    public virtual async Task<ThemeStatus> GetStatusAsync(string storeId, CancellationToken cancellationToken = default)
    {
        var storage = await themeStorageFactory.OpenAsync(storeId);

        var result = AbstractTypeFactory<ThemeStatus>.TryCreateInstance();
        result.Publication = await storage.GetCurrentPublicationAsync();
        result.Drafts = await GetDraftsAsync(storage);

        return result;
    }

    public virtual async Task ValidateFilesAsync(string storeId, string catalogHash, IDictionary<string, JToken> files, CancellationToken cancellationToken = default)
    {
        var catalog = await GetCatalogAsync(storeId, catalogHash, cancellationToken);

        var source = AbstractTypeFactory<ThemeSource>.TryCreateInstance();
        foreach (var (file, content) in files)
        {
            AddThemeFile(source, file, content);
        }

        var errors = themeSnapshotBuilder.Validate(source, catalog);
        if (errors.Count > 0)
        {
            throw new ThemeValidationException([.. errors]);
        }
    }

    protected virtual async Task<ThemePublishResult> PublishUnderLockAsync(string storeId, JObject catalog, string catalogHash, bool includeDrafts)
    {
        var store = await storeService.GetNoCloneAsync(storeId) ?? throw new KeyNotFoundException($"Store '{storeId}' not found.");
        var storage = await themeStorageFactory.OpenAsync(storeId);

        var source = await GetThemeSourceAsync(storage, includeDrafts);
        var errors = themeSnapshotBuilder.Validate(source, catalogHash == null ? null : catalog);
        if (errors.Count > 0)
        {
            throw new ThemeValidationException([.. errors]);
        }

        var languages = store.Languages?.ToList() ?? [];
        var defaultLanguage = store.GetThemeDefaultLanguage();
        var publication = await storage.GetNextPublicationAsync();
        var snapshots = themeSnapshotBuilder.Build(source, catalog, catalogHash, languages, defaultLanguage, publication, DateTime.UtcNow);

        foreach (var (language, content) in snapshots.Files)
        {
            await storage.WriteSnapshotAsync(publication, language, content);
            LogSnapshotSize(storeId, publication, language, content);
        }

        var record = AbstractTypeFactory<ThemePublicationRecord>.TryCreateInstance();
        record.DefaultLanguage = defaultLanguage;
        await storage.WritePublicationRecordAsync(publication, record);
        await storage.SetCurrentPublicationAsync(publication);
        ThemeSnapshotCacheRegion.ExpireTokenForKey(storeId, propagate: true);

        var result = AbstractTypeFactory<ThemePublishResult>.TryCreateInstance();
        result.Publication = publication;
        result.PublishedFiles = await PublishDraftsAsync(storage, source.Drafts);

        return result;
    }

    protected virtual async Task<JObject> GetCatalogAsync(string storeId, string catalogHash, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(catalogHash))
        {
            throw new ThemeValidationException(["the publication names no schema catalog: preview the storefront first"]);
        }

        var catalog = await schemaCatalogService.GetAsync(storeId, catalogHash, cancellationToken)
            ?? throw new ThemeValidationException([$"there is no schema catalog '{catalogHash}': preview the storefront first"]);

        return JObject.Parse(catalog);
    }

    protected virtual async Task<ThemeSource> GetThemeSourceAsync(IThemeStorage storage, bool includeDrafts)
    {
        var source = AbstractTypeFactory<ThemeSource>.TryCreateInstance();
        source.ThemeName = storage.ThemeName;

        foreach (var file in await GetThemeFilesAsync(storage))
        {
            AddThemeFile(source, file, await ReadThemeFileAsync(storage, source, file, includeDrafts));
        }

        return source;
    }

    /// <summary>Gets the files a publication reads: the theme's own files and every template.</summary>
    protected virtual async Task<IList<string>> GetThemeFilesAsync(IThemeStorage storage)
    {
        var templateKeys = await GetTemplateKeysAsync(storage);

        return ThemeParts.Keys.Concat(templateKeys.Select(x => $"{ThemeStorage.TemplatesFolder}/{x}{JsonExtension}")).ToList();
    }

    /// <summary>Puts a file into the source by its path in the theme; a file that is no part of the theme is left out.</summary>
    protected virtual void AddThemeFile(ThemeSource source, string file, JToken content)
    {
        if (ThemeParts.TryGetValue(file, out var setPart))
        {
            setPart(source, content);
        }
        else if (content != null && GetTemplateKey(file) is { } key)
        {
            source.Templates[key] = content;
        }
    }

    /// <summary>Gets the template key of a file in the templates folder; null for any other file.</summary>
    protected virtual string GetTemplateKey(string file)
    {
        var folder = ThemeStorage.TemplatesFolder + "/";

        return file.StartsWith(folder, StringComparison.OrdinalIgnoreCase) ? themeSnapshotBuilder.GetTemplateKey(file[folder.Length..]) : null;
    }

    protected virtual async Task<IList<string>> GetDraftsAsync(IThemeStorage storage)
    {
        var drafts = new List<string>();
        foreach (var file in await GetThemeFilesAsync(storage))
        {
            if (await storage.ExistsAsync(storage.GetDraftPath(file)))
            {
                drafts.Add(file);
            }
        }

        return drafts;
    }

    /// <summary>Reads the draft when there is one, otherwise the live file.</summary>
    protected virtual async Task<JToken> ReadThemeFileAsync(IThemeStorage storage, ThemeSource source, string file, bool includeDrafts)
    {
        var draft = includeDrafts ? await storage.ReadAsync(storage.GetDraftPath(file)) : null;
        if (draft != null)
        {
            source.Drafts[file] = draft;
        }

        var content = draft ?? await storage.ReadAsync(file);
        if (content == null)
        {
            return null;
        }

        try
        {
            return JToken.Parse(content);
        }
        catch (JsonReaderException)
        {
            source.InvalidFiles.Add(file);
            return null;
        }
    }

    protected virtual async Task<IList<string>> GetTemplateKeysAsync(IThemeStorage storage)
    {
        var fileNames = await storage.ListFilesAsync(ThemeStorage.TemplatesFolder);

        return fileNames
            .Select(x => themeSnapshotBuilder.GetTemplateKey(storage.GetLivePath(x)))
            .Where(x => x != null)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    protected virtual async Task<IList<PublishedThemeFile>> PublishDraftsAsync(IThemeStorage storage, IDictionary<string, string> drafts)
    {
        if (drafts.Count == 0)
        {
            return [];
        }

        foreach (var (file, content) in drafts)
        {
            await storage.WriteAsync(file, content);
        }

        await storage.RemoveAsync(drafts.Keys.Select(storage.GetDraftPath));

        return drafts
            .Select(x =>
            {
                var file = AbstractTypeFactory<PublishedThemeFile>.TryCreateInstance();
                file.Path = storage.GetPath(x.Key);
                file.Content = x.Value;

                return file;
            })
            .ToList();
    }

    protected virtual async Task AfterPublishAsync(string storeId, IList<PublishedThemeFile> files, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            return;
        }

        var changedEntries = files.Select(x => CreateChangedEntry(x.Path, EntryState.Modified)).ToList();
        await eventPublisher.Publish(new PageBuilderContentChangedEvent(ContentConstants.ContentTypes.Themes, changedEntries), cancellationToken);

        var saveId = Guid.NewGuid().ToString("N");
        foreach (var file in files)
        {
            await themeFileHistory.RecordAsync(storeId, file.Path, file.Content, ModuleConstants.GitHistory.PublishedSummary, user, saveId, cancellationToken);
        }
    }

    protected virtual GenericChangedEntry<FileEntity> CreateChangedEntry(string path, EntryState state)
    {
        var file = AbstractTypeFactory<FileEntity>.TryCreateInstance();
        file.Path = path;
        file.Type = ContentConstants.ContentTypes.Themes;

        return new GenericChangedEntry<FileEntity>(file, state);
    }

    protected virtual void LogSnapshotSize(string storeId, int publication, string language, string content)
    {
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            gzip.Write(bytes, 0, bytes.Length);
        }

        var threshold = options.Value.SizeWarningThreshold;
        if (compressed.Length > threshold)
        {
            logger.LogWarning(
                "The theme snapshot {Publication} of store {StoreId} in {Language} is {Size} bytes gzipped, more than {Limit}.",
                publication, storeId, language, compressed.Length, threshold);
        }
    }
}
