using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.PageBuilderModule.Data.MySql;
using VirtoCommerce.PageBuilderModule.Data.PostgreSql;
using VirtoCommerce.PageBuilderModule.Data.SqlServer;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Data.MySql.Extensions;
using VirtoCommerce.Platform.Data.PostgreSql.Extensions;
using VirtoCommerce.Platform.Data.SqlServer.Extensions;

namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

internal static class PageBuilderRepositoryRegistration
{
    internal static void AddPageBuilderRepositories(
        this IServiceCollection serviceCollection,
        IConfiguration configuration,
        ManifestModuleInfo moduleInfo)
    {
        var databaseProvider = configuration.GetValue("DatabaseProvider", "SqlServer");
        serviceCollection.AddDbContextFactory<PageBuilderModuleDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(moduleInfo.Id) ??
                                   configuration.GetConnectionString("VirtoCommerce");

            switch (databaseProvider)
            {
                case "MySql":
                    options.UseMySqlDatabase(connectionString, typeof(MySqlDataAssemblyMarker), configuration);
                    break;
                case "PostgreSql":
                    options.UsePostgreSqlDatabase(connectionString, typeof(PostgreSqlDataAssemblyMarker), configuration);
                    break;
                default:
                    options.UseSqlServerDatabase(connectionString, typeof(SqlServerDataAssemblyMarker), configuration);
                    break;
            }
        });

        serviceCollection.AddTransient<IPageBuilderModuleRepository, PageBuilderModuleRepository>();
        serviceCollection.AddSingleton<Func<IPageBuilderModuleRepository>>(provider =>
        {
            var dbContextFactory = provider.GetRequiredService<IDbContextFactory<PageBuilderModuleDbContext>>();
            return () => new PageBuilderModuleRepository(dbContextFactory.CreateDbContext());
        });

        serviceCollection.AddSingleton<Func<IContentStreamRepository>>(provider =>
        {
            var dbContextFactory = provider.GetRequiredService<IDbContextFactory<PageBuilderModuleDbContext>>();
            return () =>
            {
                var dbContext = dbContextFactory.CreateDbContext();
                return databaseProvider switch
                {
                    "MySql" => new MySqlContentStreamRepository(dbContext),
                    "PostgreSql" => new PostgreSqlContentStreamRepository(dbContext),
                    _ => new SqlServerContentStreamRepository(dbContext),
                };
            };
        });
    }

    internal static void MigratePageBuilderDatabase(this IServiceProvider serviceProvider)
    {
        using var dbContext = serviceProvider
            .GetRequiredService<IDbContextFactory<PageBuilderModuleDbContext>>()
            .CreateDbContext();
        dbContext.Database.Migrate();
    }
}
