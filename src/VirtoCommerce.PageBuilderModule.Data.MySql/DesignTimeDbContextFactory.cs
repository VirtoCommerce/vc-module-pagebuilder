using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using VirtoCommerce.PageBuilderModule.Data.Repositories;

namespace VirtoCommerce.PageBuilderModule.Data.MySql;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PageBuilderModuleDbContext>
{
    public PageBuilderModuleDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<PageBuilderModuleDbContext>();
        var connectionString = args.Length != 0 ? args[0] : "Server=localhost;User=virto;Password=virto;Database=VirtoCommerce3;";

        builder.UseMySql(
            connectionString,
            ResolveServerVersion(args, connectionString),
            options => options.MigrationsAssembly(typeof(MySqlDataAssemblyMarker).Assembly.GetName().Name));

        return new PageBuilderModuleDbContext(builder.Options);
    }

    private static ServerVersion ResolveServerVersion(string[] args, string connectionString)
    {
        var serverVersion = args.Length >= 2 ? args[1] : null;

        if (serverVersion == "AutoDetect")
        {
            return ServerVersion.AutoDetect(connectionString);
        }

        if (serverVersion != null)
        {
            return ServerVersion.Parse(serverVersion);
        }

        return new MySqlServerVersion(new Version(5, 7));
    }
}
