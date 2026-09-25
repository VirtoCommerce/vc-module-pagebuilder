using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Authorization;
using VirtoCommerce.PageBuilderModule.Data.ContentProviders;
using VirtoCommerce.PageBuilderModule.Data.ExportImport;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.PageBuilderModule.Data.Handlers;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.PageBuilderModule.Data.Search;
using VirtoCommerce.PageBuilderModule.Data.Services;
using VirtoCommerce.PageBuilderModule.Web.Services;
using VirtoCommerce.Pages.Core.ContentProviders;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.ExportImport;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Platform.Data.MySql.Extensions;
using VirtoCommerce.Platform.Data.PostgreSql.Extensions;
using VirtoCommerce.Platform.Data.SqlServer.Extensions;
using VirtoCommerce.StoreModule.Core.Model;

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
            serviceCollection.AddTransient<IGroupedPageSearchService, GroupedPageSearchService>();
            serviceCollection.AddTransient<IPageBuilderAssetReferenceService, PageBuilderAssetReferenceService>();
            serviceCollection.AddTransient<IPageBuilderAssetReferenceIndexService, PageBuilderAssetReferenceIndexService>();
            serviceCollection.AddTransient<IPageBuilderSharedComponentService, PageBuilderSharedComponentService>();
            serviceCollection.AddTransient<IPageBuilderSharedComponentSearchService, PageBuilderSharedComponentSearchService>();
            serviceCollection.AddTransient<IPageBuilderSharedComponentContentService, PageBuilderSharedComponentContentService>();
            serviceCollection.AddTransient<IPageBuilderSharedComponentResolver, PageBuilderSharedComponentResolver>();
            serviceCollection.AddTransient<IPageBuilderSharedComponentReferenceIndexService, PageBuilderSharedComponentReferenceIndexService>();
            serviceCollection.AddTransient<IPageBuilderSharedComponentUsageService, PageBuilderSharedComponentUsageService>();
            serviceCollection.AddTransient<
                IPageBuilderSharedComponentAssetReferenceIndexService,
                PageBuilderSharedComponentAssetReferenceIndexService>();
            serviceCollection.AddTransient<PageBuilderPageContentService>();

            serviceCollection.AddTransient<GroupedPageBuilderPageChangedEventHandler>();
            serviceCollection.AddTransient<PageBuilderSharedComponentContentChangedEventHandler>();
            serviceCollection.AddTransient<PageBuilderSharedComponentContentPropagationJob>();

            serviceCollection.AddTransient<IAuthorizationHandler, PageBuilderAuthorizationHandler>();
            serviceCollection.AddTransient<IPageContentProvider, PageBuilderContentProvider>();
            serviceCollection.AddTransient<IPagesMigrationService, PagesMigrationService>();
            serviceCollection.AddTransient<IPageBuilderAssetReferenceMigrationService, PageBuilderAssetReferenceMigrationService>();
            serviceCollection.AddTransient<PageBuilderSharedComponentExportImport>();
            serviceCollection.AddTransient<PageBuilderExportImport>();

            // Git content flow (disabled until PageBuilder:GitContent:Enabled is set AND a store opts in
            // — see IGitContentPolicy, GitContentOptions and vc-content/.github/git-flow-setup.md).
            var gitContentSection = Configuration.GetSection(GitContentOptions.SectionName);
            serviceCollection.Configure<GitContentOptions>(gitContentSection);
            // Fail fast: a half-configured flow must not boot, or publishing would silently keep writing
            // straight to production on an installation whose operator expects pull requests.
            gitContentSection.Get<GitContentOptions>()?.Validate();
            serviceCollection.AddHttpClient(GitHubContentRepository.HttpClientName)
                .ConfigureHttpClient((provider, httpClient) =>
                {
                    var gitContentOptions = provider.GetRequiredService<IOptions<GitContentOptions>>().Value;
                    // Relative request uris are combined against BaseAddress, so it must end with '/'.
                    httpClient.BaseAddress = new Uri(gitContentOptions.ApiUrl.AbsoluteUri.TrimEnd('/') + "/");
                    httpClient.Timeout = gitContentOptions.RequestTimeout;
                    httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
                    httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
                    // GitHub rejects requests without a User-Agent.
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "vc-pagebuilder-module");
                    if (!string.IsNullOrEmpty(gitContentOptions.Token))
                    {
                        httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + gitContentOptions.Token);
                    }
                });
            serviceCollection.AddTransient<IGitContentRepository, GitHubContentRepository>();
            serviceCollection.AddTransient<IGitContentHistory, GitHubContentHistory>();
            serviceCollection.AddTransient<IGitContentPublisher, GitHubContentPublisher>();
            serviceCollection.AddTransient<IGitContentPolicy, GitContentPolicy>();

            var isFullTextSearchEnabled = Configuration.IsContentFullTextSearchEnabled();

            if (isFullTextSearchEnabled)
            {
                serviceCollection.AddTransient<PageBuilderContentItemBuilder>();
            }

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

            var isFullTextSearchEnabled = Configuration.IsContentFullTextSearchEnabled();

            if (isFullTextSearchEnabled)
            {
                var contentItemTypeRegistrar = serviceProvider.GetService<IContentItemTypeRegistrar>();
                contentItemTypeRegistrar.RegisterContentItemType(".page", serviceProvider.GetService<PageBuilderContentItemBuilder>);
            }

            appBuilder.RegisterEventHandler<PageBuilderPageChangedEvent, PageBuilderPageChangedEventHandler>();
            appBuilder.RegisterEventHandler<GroupedPageBuilderPageChangedEvent, GroupedPageBuilderPageChangedEventHandler>();
            appBuilder.RegisterEventHandler<PageBuilderSharedComponentContentChangedEvent, PageBuilderSharedComponentContentChangedEventHandler>();

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
