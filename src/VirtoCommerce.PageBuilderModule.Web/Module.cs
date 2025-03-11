using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.ContentModule.Core.Extensions;
using VirtoCommerce.ContentModule.Core.Search;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.MySql;
using VirtoCommerce.PageBuilderModule.Data.PostgreSql;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.PageBuilderModule.Data.Search;
using VirtoCommerce.PageBuilderModule.Data.Services;
using VirtoCommerce.PageBuilderModule.Data.SqlServer;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Platform.Data.MySql.Extensions;
using VirtoCommerce.Platform.Data.PostgreSql.Extensions;
using VirtoCommerce.Platform.Data.SqlServer.Extensions;

namespace VirtoCommerce.PageBuilderModule.Web
{
    public class Module : IModule, IHasConfiguration
    {
        public ManifestModuleInfo ModuleInfo { get; set; }
        public IConfiguration Configuration { get; set; }

        public void Initialize(IServiceCollection serviceCollection)
        {
            serviceCollection.AddDbContext<PageBuilderModuleDbContext>(options =>
            {
                var databaseProvider = Configuration.GetValue("DatabaseProvider", "SqlServer");
                var connectionString = Configuration.GetConnectionString(ModuleInfo.Id) ?? Configuration.GetConnectionString("VirtoCommerce");

                switch (databaseProvider)
                {
                    case "MySql":
                        options.UseMySqlDatabase(connectionString, typeof(MySqlDataAssemblyMarker), Configuration);
                        break;
                    case "PostgreSql":
                        options.UsePostgreSqlDatabase(connectionString, typeof(PostgreSqlDataAssemblyMarker), Configuration);
                        break;
                    default:
                        options.UseSqlServerDatabase(connectionString, typeof(SqlServerDataAssemblyMarker), Configuration);
                        break;
                }
            });

            // Register services
            serviceCollection.AddTransient<IPageBuilderModuleRepository, PageBuilderModuleRepository>();
            serviceCollection.AddSingleton<Func<IPageBuilderModuleRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetRequiredService<IPageBuilderModuleRepository>());

            serviceCollection.AddTransient<IPageBuilderPageService, PageBuilderPageService>();
            serviceCollection.AddTransient<IPageBuilderPageSearchService, PageBuilderPageSearchService>();

            var isFullTextSearchEnabled = Configuration.IsContentFullTextSearchEnabled();

            if (isFullTextSearchEnabled)
            {
                serviceCollection.AddTransient<PageBuilderContentItemBuilder>();
            }
        }

        public void PostInitialize(IApplicationBuilder appBuilder)
        {
            var serviceProvider = appBuilder.ApplicationServices;

            var settingsRegistrar = serviceProvider.GetRequiredService<ISettingsRegistrar>();
            settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);

            var permissionsProvider = serviceProvider.GetRequiredService<IPermissionsRegistrar>();
            permissionsProvider.RegisterPermissions(ModuleInfo.Id, "Page builder", ModuleConstants.Security.Permissions.AllPermissions);

            var isFullTextSearchEnabled = Configuration.IsContentFullTextSearchEnabled();

            if (isFullTextSearchEnabled)
            {
                var contentItemTypeRegistrar = serviceProvider.GetService<IContentItemTypeRegistrar>();
                contentItemTypeRegistrar.RegisterContentItemType(".page", serviceProvider.GetService<PageBuilderContentItemBuilder>);
            }

            // Apply migrations
            using var serviceScope = serviceProvider.CreateScope();
            using var dbContext = serviceScope.ServiceProvider.GetRequiredService<PageBuilderModuleDbContext>();
            dbContext.Database.Migrate();
        }

        public void Uninstall()
        {
            // Method intentionally left empty.
        }
    }
}
