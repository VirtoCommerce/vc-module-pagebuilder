using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.ContentModule.Core.Model;
using VirtoCommerce.ContentModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>
/// A store's theme storage in a dictionary, by path from the store's theme root ("default/templates/product.json"),
/// with the order files were written in. Only the calls the theme publication makes are supported.
/// </summary>
internal sealed class FakeThemeStorage : IBlobContentStorageProvider, IBlobContentStorageProviderFactory, IContentPathResolver, IPublishingService
{
    public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Written { get; } = [];

    public IBlobContentStorageProvider CreateProvider(string basePath) => this;
    public string GetContentBasePath(string contentType, string storeId, string themeName = null) => $"Themes/{storeId}/";

    public string GetRelativeDraftUrl(string source, bool draft) =>
        draft ? source + "-draft" : source.EndsWith("-draft", StringComparison.OrdinalIgnoreCase) ? source[..^"-draft".Length] : source;

    public Task<Stream> OpenWriteAsync(string blobUrl) => Task.FromResult<Stream>(new CapturingStream(content =>
    {
        Files[blobUrl] = content;
        Written.Add(blobUrl);
    }));

    public Task RemoveAsync(string[] urls)
    {
        foreach (var url in urls)
        {
            Files.Remove(url);
        }

        return Task.CompletedTask;
    }

    public Task<BlobInfo> GetBlobInfoAsync(string blobUrl) =>
        Task.FromResult(Files.ContainsKey(blobUrl) ? new BlobInfo { Name = blobUrl, RelativeUrl = blobUrl } : null);

    public Task<Stream> OpenReadAsync(string blobUrl) => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(Files[blobUrl])));

    public Task<BlobEntrySearchResult> SearchAsync(string folderUrl, string keyword)
    {
        var prefix = folderUrl.TrimEnd('/') + "/";
        var results = new List<BlobEntry>();
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in Files.Keys.Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            var rest = path[prefix.Length..];
            var slash = rest.IndexOf('/', StringComparison.Ordinal);
            if (slash < 0)
            {
                results.Add(new BlobInfo { Name = rest, RelativeUrl = path });
            }
            else if (folders.Add(rest[..slash]))
            {
                results.Add(new BlobFolder { Name = rest[..slash], RelativeUrl = prefix + rest[..slash] });
            }
        }

        return Task.FromResult(new BlobEntrySearchResult { Results = results, TotalCount = results.Count });
    }

    public Task PublishingAsync(string contentType, string storeId, string relativeUrl, bool publish) => throw new NotSupportedException();
    public Task<IEnumerable<ContentFile>> SetFilesStatuses(IEnumerable<ContentFile> files) => throw new NotSupportedException();
    public Task<FilePublishStatus> PublishStatusAsync(string contentType, string storeId, string relativeUrl) => throw new NotSupportedException();
    public bool IsDraft(string relativeUrl) => throw new NotSupportedException();
    public Task CreateFolderAsync(BlobFolder folder) => throw new NotSupportedException();
    public Stream OpenRead(string blobUrl) => throw new NotSupportedException();
    public Stream OpenWrite(string blobUrl) => throw new NotSupportedException();
    public void Move(string srcUrl, string destUrl) => throw new NotSupportedException();
    public Task MoveAsyncPublic(string srcUrl, string destUrl) => throw new NotSupportedException();
    public void Copy(string srcUrl, string destUrl) => throw new NotSupportedException();
    public Task CopyAsync(string srcUrl, string destUrl) => throw new NotSupportedException();
    public string GetAbsoluteUrl(string blobKey) => throw new NotSupportedException();

    // the writer and its owner both dispose it: the content is handed over once
    private sealed class CapturingStream(Action<string> onClose) : MemoryStream
    {
        private bool _closed;

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_closed)
            {
                _closed = true;
                onClose(Encoding.UTF8.GetString(ToArray()));
            }

            base.Dispose(disposing);
        }
    }
}
