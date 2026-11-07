using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtoCommerce.ContentModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.History;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.PageBuilderModule.Data.Services;
using VirtoCommerce.PageBuilderModule.Data.Theme;
using VirtoCommerce.PageBuilderModule.Web.Services;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.StoreModule.Core.Services;
using Fakes = VirtoCommerce.PageBuilderModule.Tests.PublishedRenameContentPreservationTests;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>The services the controllers delegate to, built over the fakes a test already has.</summary>
internal static class TestServices
{
    public static ContentPermissionService Permissions(IAuthorizationService authorizationService) => new(authorizationService);

    public static FilePageDraftService FilePageDrafts(
        IOptions<GitContentOptions> options,
        IGitHubConnectionService connection,
        IGitContentPolicy policy,
        IGitContentRepository repository = null,
        IGitContentHistory history = null,
        IGitContentProvisioner provisioner = null,
        IStoreService storeService = null,
        IContentPathResolver pathResolver = null,
        IBlobContentStorageProviderFactory blobs = null,
        IPublishingService publishing = null,
        IEventPublisher events = null) =>
        new(policy, repository, history, provisioner ?? new FakeGitContentProvisioner(), connection, options,
            new ThemeStorageFactory(storeService, pathResolver, blobs, publishing), pathResolver, blobs, publishing, events);

    public static FilePageDraftService FilePageDrafts(FakeBlobStorage live, IOptions<GitContentOptions> options, IGitHubConnectionService connection,
        IGitContentPolicy policy, IGitContentRepository repository = null, IGitContentHistory history = null, IGitContentProvisioner provisioner = null) =>
        FilePageDrafts(options, connection, policy, repository, history, provisioner, live?.StoreService, live, live, live, live);

    public static DesignerContentService Designer(
        IContentHistory contentHistory,
        IFilePageDraftService filePageDrafts = null,
        IThemeDraftService themeDrafts = null,
        Fakes.FakeGroupedPageService pages = null,
        IEventPublisher events = null,
        IThemePublisher themePublisher = null,
        IThemeSnapshotService themeSnapshots = null)
    {
        var contentWriter = pages == null
            ? null
            : new GroupedPageContentWriter(new PageBuilderPageContentService(new Fakes.FakePageBuilderPageService(pages), pages,
                events ?? new Fakes.NoopEventPublisher(), NullLogger<PageBuilderPageContentService>.Instance));

        return new DesignerContentService(themeDrafts, themePublisher, themeSnapshots, pages == null ? null : new Fakes.FakePageBuilderPageService(pages),
            pages, pages, contentWriter, filePageDrafts, contentHistory);
    }
}
