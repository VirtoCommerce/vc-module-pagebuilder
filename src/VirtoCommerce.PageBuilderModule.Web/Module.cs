using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using VirtoCommerce.ContentModule.Core.Extensions;
using VirtoCommerce.ContentModule.Core.Search;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.History;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.PageBuilderModule.Data.BackgroundJobs;
using VirtoCommerce.PageBuilderModule.Data.ContentProviders;
using VirtoCommerce.PageBuilderModule.Data.ExportImport;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.PageBuilderModule.Data.Handlers;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.PageBuilderModule.Data.Search;
using VirtoCommerce.PageBuilderModule.Data.Services;
using VirtoCommerce.PageBuilderModule.Data.Theme;
using VirtoCommerce.PageBuilderModule.Data.Validation;
using VirtoCommerce.PageBuilderModule.Web.Services;
using VirtoCommerce.Pages.Core.ContentProviders;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.ExportImport;
using VirtoCommerce.Platform.Core.Jobs;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Events;
using VirtoCommerce.StoreModule.Core.Security;

namespace VirtoCommerce.PageBuilderModule.Web
{
    public class Module : IModule, IExportSupport, IImportSupport, IHasConfiguration
    {
        private IApplicationBuilder _appBuilder;

        public ManifestModuleInfo ModuleInfo { get; set; }
        public IConfiguration Configuration { get; set; }

        public void Initialize(IServiceCollection serviceCollection)
        {
            serviceCollection.AddPageBuilderRepositories(Configuration, ModuleInfo);

            // Register services
            serviceCollection.AddTransient<IPageBuilderPageService, PageBuilderPageService>();
            serviceCollection.AddTransient<IPageBuilderPageSearchService, PageBuilderPageSearchService>();
            serviceCollection.AddTransient<IPageBuilderPageChangeService, PageBuilderPageChangeService>();

            serviceCollection.AddTransient<PageBuilderPageChangedEventHandler>();

            serviceCollection.AddTransient<IGroupedPageService, GroupedPageService>();
            serviceCollection.AddTransient<IGroupedPageContentService>(provider =>
                provider.GetRequiredService<IGroupedPageService>() as IGroupedPageContentService
                ?? ActivatorUtilities.CreateInstance<GroupedPageService>(provider));
            serviceCollection.AddTransient<IGroupedPageSearchService, GroupedPageSearchService>();
            serviceCollection.AddTransient<IMultilanguagePageService, MultilanguagePageService>();
            serviceCollection.AddTransient<IMultilanguagePageSearchService, MultilanguagePageSearchService>();
            serviceCollection.AddTransient<IPageBuilderPageListService, PageBuilderPageListService>();
            serviceCollection.AddTransient<IPageBuilderAssetReferenceService, PageBuilderAssetReferenceService>();
            serviceCollection.AddTransient<IPageBuilderAssetSearchService, PageBuilderAssetSearchService>();
            serviceCollection.AddTransient<IPageBuilderAssetReferenceIndexService, PageBuilderAssetReferenceIndexService>();
            serviceCollection.AddTransient<IPageBuilderSharedComponentService, PageBuilderSharedComponentService>();
            serviceCollection.AddTransient<IPageBuilderSharedComponentSearchService, PageBuilderSharedComponentSearchService>();
            serviceCollection.AddTransient<IPageBuilderSharedComponentContentService, PageBuilderSharedComponentContentService>();
            serviceCollection.AddTransient<IPageBuilderSharedComponentResolver, PageBuilderSharedComponentResolver>();
            serviceCollection.AddTransient<IPageBuilderSharedComponentReferenceIndexService, PageBuilderSharedComponentReferenceIndexService>();
            serviceCollection.AddTransient<IPageBuilderSharedComponentPlacementValidator, PageBuilderSharedComponentPlacementValidator>();
            serviceCollection.AddTransient<IPageBuilderSharedComponentUsageService, PageBuilderSharedComponentUsageService>();
            serviceCollection.AddTransient<
                IPageBuilderSharedComponentAssetReferenceIndexService,
                PageBuilderSharedComponentAssetReferenceIndexService>();
            serviceCollection.AddTransient<PageBuilderPageContentService>();
            serviceCollection.AddTransient<IGroupedPageContentWriter, GroupedPageContentWriter>();

            serviceCollection.AddTransient<GroupedPageBuilderPageChangedEventHandler>();
            serviceCollection.AddTransient<PageBuilderSharedComponentContentChangedEventHandler>();
            serviceCollection.AddBackgroundJob<PageBuilderSharedComponentContentPropagationJob, PageBuilderSharedComponentContentPropagationJobPayload>(triggerable: false);

            serviceCollection.AddTransient<IAuthorizationHandler, PageBuilderAuthorizationHandler>();
            serviceCollection.AddTransient<IValidator<PageBuilderContentFile>, PageBuilderContentFileValidator>();
            serviceCollection.AddTransient<IValidator<PageBuilderContentRequest>, PageBuilderContentRequestValidator>();
            serviceCollection.AddTransient<IValidator<DesignerSavePage>, DesignerPageValidator>();
            serviceCollection.AddTransient<IValidator<DesignerSaveRequest>, DesignerSaveRequestValidator>();
            serviceCollection.AddTransient<IValidator<DesignerDiscardRequest>, DesignerDiscardRequestValidator>();
            serviceCollection.AddTransient<IPageContentProvider, PageBuilderContentProvider>();
            serviceCollection.AddTransient<IPagesMigrationService, PagesMigrationService>();
            serviceCollection.AddTransient<IPageBuilderAssetReferenceMigrationService, PageBuilderAssetReferenceMigrationService>();
            // The one-time startup migrations run as background jobs; their handlers call the concrete services.
            serviceCollection.AddTransient<PagesMigrationService>();
            serviceCollection.AddTransient<PageBuilderAssetReferenceMigrationService>();
            serviceCollection.AddBackgroundJob<PagesMigrationJob, PagesMigrationJobPayload>(triggerable: false);
            serviceCollection.AddBackgroundJob<AssetReferenceIndexRebuildJob, AssetReferenceIndexRebuildJobPayload>(triggerable: false);
            serviceCollection.AddTransient<PageBuilderSharedComponentExportImport>();
            serviceCollection.AddTransient<MultilanguagePageExportImport>();
            serviceCollection.AddTransient<PageBuilderExportImport>();

            // Git content flow: off until a connection is configured and a store opts in
            var gitContentSection = Configuration.GetSection(GitContentOptions.SectionName);
            serviceCollection.Configure<GitContentOptions>(gitContentSection);
            // Fail fast: a half-configured flow must not boot, or publishing would silently keep writing
            // straight to production on an installation whose operator expects pull requests.
            gitContentSection.Get<GitContentOptions>()?.Validate();
            serviceCollection.AddTransient<IGitHubConnectionService, GitHubConnectionService>();
            serviceCollection.AddTransient<IGitHubApiClient, GitHubApiClient>();
            serviceCollection.AddTransient<GitHubAuthHandler>();
            serviceCollection.AddHttpClient(GitHubApiClient.HttpClientName)
                .ConfigureHttpClient(ConfigureGitHubClient)
                .AddHttpMessageHandler<GitHubAuthHandler>();
            serviceCollection.AddHttpClient(GitHubApiClient.AnonymousHttpClientName).ConfigureHttpClient(ConfigureGitHubClient);
            serviceCollection.AddTransient<IGitContentRepository, GitHubContentRepository>();
            serviceCollection.AddTransient<IGitContentHistory, GitHubContentHistory>();
            serviceCollection.AddTransient<IGitContentPublisher, GitHubContentPublisher>();
            serviceCollection.AddTransient<IGitContentOwnership, GitHubContentOwnership>();
            serviceCollection.AddTransient<IGitContentPolicy, GitContentPolicy>();
            serviceCollection.AddTransient<IGitContentProvisioner, GitHubContentProvisioner>();
            serviceCollection.AddTransient<IGitContentSetupService, GitContentSetupService>();
            serviceCollection.AddTransient<IGitLivePageService, GitLivePageService>();
            serviceCollection.AddTransient<IContentHistory, GitHubSiteHistory>();

            serviceCollection.Configure<ThemeSnapshotOptions>(Configuration.GetSection(ThemeSnapshotOptions.SectionName));
            serviceCollection.AddTransient<IThemeStorageFactory, ThemeStorageFactory>();
            serviceCollection.AddTransient<IThemeSnapshotBuilder, ThemeSnapshotBuilder>();
            serviceCollection.AddTransient<ISchemaCatalogService, SchemaCatalogService>();
            serviceCollection.AddTransient<IThemePublisher, ThemePublisher>();
            serviceCollection.AddTransient<IThemeDraftService, ThemeDraftService>();
            serviceCollection.AddTransient<IFilePageDraftService, FilePageDraftService>();
            serviceCollection.AddTransient<IDesignerContentService, DesignerContentService>();
            serviceCollection.AddTransient<IContentPermissionService, ContentPermissionService>();
            serviceCollection.AddTransient<IThemeSnapshotService, ThemeSnapshotService>();
            serviceCollection.AddTransient<IThemePaletteService, ThemePaletteService>();
            serviceCollection.AddTransient<IValidator<PageBuilderStoreRequest>, PageBuilderStoreRequestValidator>();
            serviceCollection.AddTransient<IValidator<ContentHistoryRequest>, ContentHistoryRequestValidator>();
            serviceCollection.AddTransient<IValidator<ContentHistoryRestoreRequest>, ContentHistoryRestoreRequestValidator>();
            serviceCollection.AddTransient<IValidator<GitHubAppSetupRequest>, GitHubAppSetupRequestValidator>();
            serviceCollection.AddTransient<IValidator<ThemePaletteGenerationRequest>, ThemePaletteGenerationRequestValidator>();
            serviceCollection.AddTransient<IValidator<ThemePaletteCheckRequest>, ThemePaletteCheckRequestValidator>();
            serviceCollection.AddTransient<GitContentStoreChangedEventHandler>();
            serviceCollection.AddBackgroundJob<GitContentRepositoryJob, GitContentRepositoryJobPayload>(triggerable: false);

            var isFullTextSearchEnabled = Configuration.IsContentFullTextSearchEnabled();

            if (isFullTextSearchEnabled)
            {
                serviceCollection.AddTransient<PageBuilderContentItemBuilder>();
            }

        }

        private static void ConfigureGitHubClient(IServiceProvider provider, HttpClient httpClient)
        {
            GitHubApiClient.ConfigureHttpClient(httpClient, provider.GetRequiredService<IOptions<GitContentOptions>>().Value);
        }

        public void PostInitialize(IApplicationBuilder appBuilder)
        {
            _appBuilder = appBuilder;
            var serviceProvider = appBuilder.ApplicationServices;

            var settingsRegistrar = serviceProvider.GetRequiredService<ISettingsRegistrar>();
            settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);
            settingsRegistrar.RegisterSettingsForType(ModuleConstants.Settings.StoreLevelSettings.AllStoreLevelSettings, ModuleConstants.Settings.StoreSettingsObjectType);

            var permissionsProvider = serviceProvider.GetRequiredService<IPermissionsRegistrar>();
            permissionsProvider.RegisterPermissions(ModuleInfo.Id, "Page builder", ModuleConstants.Security.Permissions.AllPermissions);
            // the read permissions decide which stores a role works in; PageBuilderAuthorizationRequirement checks them in each action
            permissionsProvider.WithAvailabeScopesForPermissions(
                [ModuleConstants.Security.Permissions.Read, ModuleConstants.Security.Permissions.SharedComponentsRead],
                new StoreSelectedScope());

            var isFullTextSearchEnabled = Configuration.IsContentFullTextSearchEnabled();

            if (isFullTextSearchEnabled)
            {
                var contentItemTypeRegistrar = serviceProvider.GetService<IContentItemTypeRegistrar>();
                contentItemTypeRegistrar.RegisterContentItemType(".page", serviceProvider.GetService<PageBuilderContentItemBuilder>);
            }

            appBuilder.RegisterEventHandler<PageBuilderPageChangedEvent, PageBuilderPageChangedEventHandler>();
            appBuilder.RegisterEventHandler<GroupedPageBuilderPageChangedEvent, GroupedPageBuilderPageChangedEventHandler>();
            appBuilder.RegisterEventHandler<PageBuilderSharedComponentContentChangedEvent, PageBuilderSharedComponentContentChangedEventHandler>();
            appBuilder.RegisterEventHandler<StoreChangedEvent, GitContentStoreChangedEventHandler>();

            // Apply migrations
            using var serviceScope = serviceProvider.CreateScope();
            serviceScope.ServiceProvider.MigratePageBuilderDatabase();

            // Run pages migration
            var pagesMigrationService = serviceScope.ServiceProvider.GetRequiredService<IPagesMigrationService>();
            pagesMigrationService.StartMigration();

            // Build asset reference index for existing pages
            var assetReferenceMigrationService = serviceScope.ServiceProvider.GetRequiredService<IPageBuilderAssetReferenceMigrationService>();
            assetReferenceMigrationService.StartMigration();

            // page-builder
            var pageBuilderAppPath = Path.Combine(ModuleInfo.FullPhysicalPath, "page-builder", "dist");
            if (Directory.Exists(pageBuilderAppPath))
            {
                appBuilder.UseDefaultFiles(new DefaultFilesOptions
                {
                    FileProvider = new PhysicalFileProvider(pageBuilderAppPath),
                    RequestPath = new PathString($"/apps/page-builder")
                });
                appBuilder.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(pageBuilderAppPath),
                    RequestPath = new PathString($"/apps/page-builder")
                });
            }
        }

        public void Uninstall()
        {
            // Method intentionally left empty.
        }

        public Task ExportAsync(Stream outStream, ExportImportOptions options, Action<ExportImportProgressInfo> progressCallback,
            CancellationToken cancellationToken)
        {
            return _appBuilder.ApplicationServices.GetRequiredService<PageBuilderExportImport>().DoExportAsync(outStream,
                progressCallback, cancellationToken);
        }

        public Task ImportAsync(Stream inputStream, ExportImportOptions options, Action<ExportImportProgressInfo> progressCallback,
            CancellationToken cancellationToken)
        {
            return _appBuilder.ApplicationServices.GetRequiredService<PageBuilderExportImport>().DoImportAsync(inputStream,
                progressCallback, cancellationToken);
        }
    }
}
