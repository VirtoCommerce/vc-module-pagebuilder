using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    // IContentStreamRepository gained TryLoadBinaryAsync and CopyContentAsync as default interface methods so the
    // published contract keeps working for anyone who implemented only the original two members. These tests pin
    // that compatibility path: the fake below implements nothing but SaveBinaryAsync and the obsolete
    // LoadBinaryAsync, exactly like a pre-existing external implementation.
    public class ContentStreamRepositoryContractTests
    {
        [Fact]
        public async Task Default_TryLoadBinaryAsync_writes_content_and_reports_found()
        {
            IContentStreamRepository repository = new LegacyOnlyRepository { ["page-1"] = "payload" };

            var writer = new StringWriter();
            var found = await repository.TryLoadBinaryAsync("page-1", writer, CancellationToken.None);

            Assert.True(found);
            Assert.Equal("payload", writer.ToString());
        }

        [Fact]
        public async Task Default_TryLoadBinaryAsync_reports_found_even_when_nothing_is_there()
        {
            IContentStreamRepository repository = new LegacyOnlyRepository();

            var writer = new StringWriter();
            var found = await repository.TryLoadBinaryAsync("missing", writer, CancellationToken.None);

            // The legacy overload already discarded the distinction, so the compatibility path cannot invent it.
            // It must stay optimistic rather than start reporting false and silently changing caller behaviour.
            Assert.True(found);
            Assert.Equal(string.Empty, writer.ToString());
        }

        [Fact]
        public async Task Default_CopyContentAsync_round_trips_content_to_the_target()
        {
            var repository = new LegacyOnlyRepository { ["source"] = "{ \"blocks\": [1,2,3] }" };

            await ((IContentStreamRepository)repository).CopyContentAsync(
                "source", "target", CancellationToken.None);

            Assert.Equal("{ \"blocks\": [1,2,3] }", repository["target"]);
        }

        [Fact]
        public async Task Default_CopyContentAsync_from_an_absent_source_leaves_the_target_empty()
        {
            var repository = new LegacyOnlyRepository { ["target"] = "stale" };

            await ((IContentStreamRepository)repository).CopyContentAsync(
                "missing", "target", CancellationToken.None);

            // A copy carries the source's state, so stale content must not survive it.
            Assert.Equal(string.Empty, repository["target"]);
        }

        // An implementation as it could have existed before TryLoadBinaryAsync/CopyContentAsync were added:
        // only the two original members, everything else inherited from the interface.
        private sealed class LegacyOnlyRepository : IContentStreamRepository
        {
            private readonly Dictionary<string, string> _content = new();

            public string this[string pageId]
            {
                get => _content.TryGetValue(pageId, out var value) ? value : null;
                init => _content[pageId] = value;
            }

            public async Task SaveBinaryAsync(string pageId, TextReader reader, CancellationToken cancellationToken = default)
            {
                _content[pageId] = await reader.ReadToEndAsync(cancellationToken);
            }

            [Obsolete("Mirrors the legacy member this fake deliberately still implements.")]
            public async Task LoadBinaryAsync(string pageId, TextWriter writer, CancellationToken cancellationToken = default)
            {
                if (_content.TryGetValue(pageId, out var value))
                {
                    await writer.WriteAsync(value);
                }
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
