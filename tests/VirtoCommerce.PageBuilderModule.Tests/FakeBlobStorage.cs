using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.ContentModule.Core.Model;
using VirtoCommerce.ContentModule.Core.Services;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.StoreModule.Core.Model;

namespace VirtoCommerce.PageBuilderModule.Tests;

// One blob container, keyed by full path as "{contentType}/{storeId}/{path}": each content folder is a view of its own part of it.
internal sealed class FakeBlobStorage : IBlobContentStorageProviderFactory, IContentPathResolver, IPublishingService, IEventPublisher
{
    public Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);

    public List<string> Written { get; } = [];

    public List<string> Removed { get; } = [];

    public List<IEvent> Events { get; } = [];

    public FakeStoreService StoreService { get; } = new(new Store { Id = "s1", DynamicProperties = [] });

    public Action<string> BeforeWrite { get; set; }

    public Action<string> BeforeRemove { get; set; }

    public IBlobContentStorageProvider CreateProvider(string basePath) => new Folder(this, basePath.TrimEnd('/') + "/");

    public string GetContentBasePath(string contentType, string storeId, string themeName = null) => $"{contentType}/{storeId}";

    public string GetRelativeDraftUrl(string source, bool draft) =>
        draft ? source + "-draft" : source.EndsWith("-draft", StringComparison.OrdinalIgnoreCase) ? source[..^"-draft".Length] : source;

    public Task Publish<T>(T @event, CancellationToken cancellationToken = default) where T : IEvent
    {
        Events.Add(@event);
        return Task.CompletedTask;
    }

    public Task PublishingAsync(string contentType, string storeId, string relativeUrl, bool publish) => throw new NotSupportedException();
    public Task<IEnumerable<ContentFile>> SetFilesStatuses(IEnumerable<ContentFile> files) => throw new NotSupportedException();
    public Task<FilePublishStatus> PublishStatusAsync(string contentType, string storeId, string relativeUrl) => throw new NotSupportedException();
    public bool IsDraft(string relativeUrl) => throw new NotSupportedException();

    private sealed class Folder(FakeBlobStorage storage, string prefix) : IBlobContentStorageProvider
    {
        public Task<Stream> OpenWriteAsync(string blobUrl)
        {
            var path = Key(blobUrl);
            storage.BeforeWrite?.Invoke(path);

            return Task.FromResult<Stream>(new CapturingStream(content =>
            {
                storage.Files[path] = content;
                storage.Written.Add(path);
            }));
        }

        public Task RemoveAsync(string[] urls)
        {
            foreach (var path in urls.Select(Key))
            {
                storage.BeforeRemove?.Invoke(path);
                storage.Removed.Add(path);
                storage.Files.Remove(path);
            }

            return Task.CompletedTask;
        }

        public Task<BlobInfo> GetBlobInfoAsync(string blobUrl) =>
            Task.FromResult(storage.Files.ContainsKey(Key(blobUrl)) ? new BlobInfo { Name = blobUrl, RelativeUrl = blobUrl } : null);

        public Task<Stream> OpenReadAsync(string blobUrl) => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(storage.Files[Key(blobUrl)])));

        public Task<BlobEntrySearchResult> SearchAsync(string folderUrl, string keyword)
        {
            var folder = folderUrl.Trim('/') + "/";
            var results = new List<BlobEntry>();
            var folders = new HashSet<string>(StringComparer.Ordinal);

            foreach (var path in storage.Files.Keys.Where(x => x.StartsWith(Key(folder), StringComparison.Ordinal)))
            {
                var rest = path[Key(folder).Length..];
                var slash = rest.IndexOf('/', StringComparison.Ordinal);
                if (slash < 0)
                {
                    results.Add(new BlobInfo { Name = rest, RelativeUrl = folder + rest });
                }
                else if (folders.Add(rest[..slash]))
                {
                    results.Add(new BlobFolder { Name = rest[..slash], RelativeUrl = folder + rest[..slash] });
                }
            }

            return Task.FromResult(new BlobEntrySearchResult { Results = results, TotalCount = results.Count });
        }

        // Blob providers combine the base path and a url that starts with a slash into one path.
        private string Key(string blobUrl) => prefix + blobUrl.TrimStart('/');

        public Task CreateFolderAsync(BlobFolder folder) => throw new NotSupportedException();
        public Stream OpenRead(string blobUrl) => throw new NotSupportedException();
        public Stream OpenWrite(string blobUrl) => throw new NotSupportedException();
        public void Move(string srcUrl, string destUrl) => throw new NotSupportedException();
        public Task MoveAsyncPublic(string srcUrl, string destUrl) => throw new NotSupportedException();
        public void Copy(string srcUrl, string destUrl) => throw new NotSupportedException();
        public Task CopyAsync(string srcUrl, string destUrl) => throw new NotSupportedException();
        public string GetAbsoluteUrl(string blobKey) => throw new NotSupportedException();
    }

    // The writer and its owner both dispose this stream, so the content must be handed over only once.
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
