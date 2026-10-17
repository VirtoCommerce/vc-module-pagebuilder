using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.ContentModule.Core.Model;
using VirtoCommerce.ContentModule.Core.Services;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.StoreModule.Core.Model;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>
    /// The live storage a git publish writes to once its merge is done, with what it took part in: the
    /// blob writes and removals, and the content-changed events. Only those calls are supported.
    /// </summary>
    internal sealed class FakeLiveStorage : IBlobContentStorageProvider, IBlobContentStorageProviderFactory, IContentPathResolver, IPublishingService, IEventPublisher
    {
        public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Removed { get; } = [];
        public List<IEvent> Events { get; } = [];

        /// <summary>A store service that knows the one store the tests use, with no theme of its own.</summary>
        public FakeStoreService StoreService { get; } = new(new Store { Id = "s1", DynamicProperties = [] });

        // the factory and the path resolver: every base path is this one storage
        public IBlobContentStorageProvider CreateProvider(string basePath) => this;
        public string GetContentBasePath(string contentType, string storeId, string themeName = null) => $"Pages/{storeId}";

        // the publishing service: only the draft naming takes part
        public string GetRelativeDraftUrl(string source, bool draft) =>
            draft ? source + "-draft" : source.EndsWith("-draft", StringComparison.OrdinalIgnoreCase) ? source[..^"-draft".Length] : source;

        public Task Publish<T>(T @event, CancellationToken cancellationToken = default) where T : IEvent
        {
            Events.Add(@event);
            return Task.CompletedTask;
        }

        public Task<Stream> OpenWriteAsync(string blobUrl) => Task.FromResult<Stream>(new CapturingStream(content => Files[blobUrl] = content));

        public Task RemoveAsync(string[] urls)
        {
            Removed.AddRange(urls);
            foreach (var url in urls)
            {
                Files.Remove(url);
            }

            return Task.CompletedTask;
        }

        public Task<BlobInfo> GetBlobInfoAsync(string blobUrl) =>
            Task.FromResult(Files.ContainsKey(blobUrl) ? new BlobInfo { Name = blobUrl, RelativeUrl = blobUrl } : null);

        public Task<Stream> OpenReadAsync(string blobUrl) => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(Files[blobUrl])));

        public Task PublishingAsync(string contentType, string storeId, string relativeUrl, bool publish) => throw new NotSupportedException();
        public Task<IEnumerable<ContentFile>> SetFilesStatuses(IEnumerable<ContentFile> files) => throw new NotSupportedException();
        public Task<FilePublishStatus> PublishStatusAsync(string contentType, string storeId, string relativeUrl) => throw new NotSupportedException();
        public bool IsDraft(string relativeUrl) => throw new NotSupportedException();
        public Task<BlobEntrySearchResult> SearchAsync(string folderUrl, string keyword) => throw new NotSupportedException();
        public Task CreateFolderAsync(BlobFolder folder) => throw new NotSupportedException();
        public Stream OpenRead(string blobUrl) => throw new NotSupportedException();
        public Stream OpenWrite(string blobUrl) => throw new NotSupportedException();
        public void Move(string srcUrl, string destUrl) => throw new NotSupportedException();
        public Task MoveAsyncPublic(string srcUrl, string destUrl) => throw new NotSupportedException();
        public void Copy(string srcUrl, string destUrl) => throw new NotSupportedException();
        public Task CopyAsync(string srcUrl, string destUrl) => throw new NotSupportedException();
        public string GetAbsoluteUrl(string blobKey) => throw new NotSupportedException();

        /// <summary>Hands over what was written when the writer closes it.</summary>
        private sealed class CapturingStream(Action<string> onClose) : MemoryStream
        {
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    onClose(Encoding.UTF8.GetString(ToArray()));
                }

                base.Dispose(disposing);
            }
        }
    }
}
