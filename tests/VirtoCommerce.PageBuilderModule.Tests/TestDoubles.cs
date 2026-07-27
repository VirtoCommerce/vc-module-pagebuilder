using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.Pages.Core.Models;
using VirtoCommerce.Pages.Core.Search;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    // Shared in-memory doubles for tests that drive the real PageBuilderPageController.
    internal static class TestDoubles
    {
        // Faithful in-memory IGroupedPageService. The one behaviour it must get exactly right is the
        // difference between "no content was ever written for this page" and "content was written and is
        // empty" — that is the distinction the content endpoint's fall-through rests on.
        internal sealed class FakeGroupedPageService : IGroupedPageService
        {
            private readonly Dictionary<string, GroupedPageBuilderPage> _groups = new();
            private readonly Dictionary<string, string> _content = new();
            private int _idSeq;

            public void SeedGroup(GroupedPageBuilderPage group) => _groups[group.Id] = DeepClone(group);
            public void SeedContent(string pageId, string content) => _content[pageId] = content;

            public Task<IList<GroupedPageBuilderPage>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true)
            {
                IList<GroupedPageBuilderPage> result = ids
                    .Where(id => id != null && _groups.ContainsKey(id))
                    .Select(id => clone ? DeepClone(_groups[id]) : _groups[id])
                    .ToList();
                return Task.FromResult(result);
            }

            public Task SaveChangesAsync(IList<GroupedPageBuilderPage> models)
            {
                foreach (var model in models)
                {
                    if (string.IsNullOrEmpty(model.Id))
                    {
                        model.Id = $"group-new-{++_idSeq}";
                    }

                    foreach (var page in model.Pages)
                    {
                        if (string.IsNullOrEmpty(page.Id))
                        {
                            page.Id = $"page-new-{++_idSeq}";
                        }
                        page.GroupId = model.Id;
                        page.ModifiedDate = DateTime.UtcNow.AddTicks(++_idSeq);
                    }

                    _groups[model.Id] = DeepClone(model);
                }

                return Task.CompletedTask;
            }

            public Task DeleteAsync(IList<string> ids, bool softDelete = false)
            {
                foreach (var id in ids)
                {
                    _content.Remove(id);
                    foreach (var group in _groups.Values)
                    {
                        var page = group.Pages.FirstOrDefault(p => p.Id == id);
                        if (page != null)
                        {
                            group.Pages.Remove(page);
                        }
                    }
                }
                return Task.CompletedTask;
            }

            public Task<string> LoadContent(string pageId, CancellationToken cancellationToken = default)
                => Task.FromResult(_content.TryGetValue(pageId, out var c) ? c : string.Empty);

            public Task SaveContent(string pageId, string content, CancellationToken cancellationToken = default)
            {
                _content[pageId] = content;
                return Task.CompletedTask;
            }

            // Absence from _content mirrors a NULL PageContent column — content was never written for this page.
            // A present empty string mirrors '' — content was written and is deliberately empty. Only the former
            // reports false, and only the former writes nothing to the stream.
            public async Task<bool> LoadContentToStreamAsync(string pageId, Stream stream, CancellationToken cancellationToken = default)
            {
                if (!_content.TryGetValue(pageId, out var content))
                {
                    return false;
                }

                var bytes = Encoding.UTF8.GetBytes(content);
                await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
                return true;
            }

            public async Task SaveStreamAsContentAsync(string pageId, Stream stream, CancellationToken cancellationToken = default)
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
                _content[pageId] = await reader.ReadToEndAsync();
            }

            public Task CopyPageContentAsync(string sourcePageId, string targetPageId, CancellationToken cancellationToken = default)
            {
                // Mirrors the server-side copy: the target takes on exactly the source's state. A source that was
                // never seeded leaves the target unseeded too, rather than "deliberately empty".
                if (_content.TryGetValue(sourcePageId, out var c))
                {
                    _content[targetPageId] = c;
                }
                else
                {
                    _content.Remove(targetPageId);
                }

                return Task.CompletedTask;
            }

            internal PageBuilderPage GetStoredPage(string pageId)
                => _groups.Values.SelectMany(g => g.Pages).FirstOrDefault(p => p.Id == pageId);

            private static GroupedPageBuilderPage DeepClone(GroupedPageBuilderPage group)
            {
                return new GroupedPageBuilderPage
                {
                    Id = group.Id,
                    Name = group.Name,
                    Permalink = group.Permalink,
                    CultureName = group.CultureName,
                    StoreId = group.StoreId,
                    Visibility = group.Visibility,
                    UserGroups = group.UserGroups,
                    StartDate = group.StartDate,
                    EndDate = group.EndDate,
                    OrganizationId = group.OrganizationId,
                    CreatedDate = group.CreatedDate,
                    ModifiedDate = group.ModifiedDate,
                    Pages = group.Pages.Select(p => new PageBuilderPage
                    {
                        Id = p.Id,
                        GroupId = p.GroupId,
                        StoreId = p.StoreId,
                        Status = p.Status,
                        CreatedDate = p.CreatedDate,
                        ModifiedDate = p.ModifiedDate,
                    }).ToList(),
                };
            }
        }

        internal sealed class FakePageBuilderPageService(FakeGroupedPageService groupedService) : IPageBuilderPageService
        {
            public Task<IList<PageBuilderPage>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true)
            {
                IList<PageBuilderPage> result = ids
                    .Select(groupedService.GetStoredPage)
                    .Where(p => p != null)
                    .ToList();
                return Task.FromResult(result);
            }

            public Task SaveChangesAsync(IList<PageBuilderPage> models) => Task.CompletedTask;

            public Task DeleteAsync(IList<string> ids, bool softDelete = false) => groupedService.DeleteAsync(ids, softDelete);
        }

        internal sealed class FakeGroupedPageSearchService : IGroupedPageSearchService
        {
            public Task<GroupedPageBuilderPageSearchResult> SearchAsync(PageBuilderPageSearchCriteria criteria, bool clone = true)
                => Task.FromResult(new GroupedPageBuilderPageSearchResult());
        }

        internal sealed class NoopPageDocumentSearchService : IPageDocumentSearchService
        {
            public Task<IndexingResult> IndexDocuments(PageDocument[] documents) => Task.FromResult(new IndexingResult());
            public Task<IndexingResult> RemoveDocuments(string[] documentIds) => Task.FromResult(new IndexingResult());
            public Task<PageDocumentSearchResult> SearchAsync(PageDocumentSearchCriteria criteria, bool clone = true)
                => Task.FromResult(new PageDocumentSearchResult());
        }

        internal sealed class AllowAllAuthorizationService : IAuthorizationService
        {
            public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
                => Task.FromResult(AuthorizationResult.Success());

            public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
                => Task.FromResult(AuthorizationResult.Success());
        }
    }
}
