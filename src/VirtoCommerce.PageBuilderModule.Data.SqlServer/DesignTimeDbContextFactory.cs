using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using VirtoCommerce.PageBuilderModule.Data.Repositories;

namespace VirtoCommerce.PageBuilderModule.Data.SqlServer;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PageBuilderModuleDbContext>
{
    public PageBuilderModuleDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<PageBuilderModuleDbContext>();
        var connectionString = args.Length != 0 ? args[0] : "Server=(local);User=virto;Password=virto;Database=VirtoCommerce3;";

        builder.UseSqlServer(
            connectionString,
            options => options.MigrationsAssembly(typeof(SqlServerDataAssemblyMarker).Assembly.GetName().Name));

        return new PageBuilderModuleDbContext(builder.Options);
    }
}
