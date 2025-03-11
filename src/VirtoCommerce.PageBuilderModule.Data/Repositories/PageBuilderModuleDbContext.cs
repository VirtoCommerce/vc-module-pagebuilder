using System.Reflection;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.Platform.Data.Infrastructure;
using VirtoCommerce.PageBuilderModule.Data.Models;

namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

public class PageBuilderModuleDbContext : DbContextBase
{
    public PageBuilderModuleDbContext(DbContextOptions<PageBuilderModuleDbContext> options)
        : base(options)
    {
    }

    protected PageBuilderModuleDbContext(DbContextOptions options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PageBuilderPageEntity>().ToTable("PageBuilderPage").HasKey(x => x.Id);
        modelBuilder.Entity<PageBuilderPageEntity>().Property(x => x.Id).HasMaxLength(IdLength).ValueGeneratedOnAdd();

        switch (Database.ProviderName)
        {
            case "Pomelo.EntityFrameworkCore.MySql":
                modelBuilder.ApplyConfigurationsFromAssembly(Assembly.Load("VirtoCommerce.PageBuilderModule.Data.MySql"));
                break;
            case "Npgsql.EntityFrameworkCore.PostgreSQL":
                modelBuilder.ApplyConfigurationsFromAssembly(Assembly.Load("VirtoCommerce.PageBuilderModule.Data.PostgreSql"));
                break;
            case "Microsoft.EntityFrameworkCore.SqlServer":
                modelBuilder.ApplyConfigurationsFromAssembly(Assembly.Load("VirtoCommerce.PageBuilderModule.Data.SqlServer"));
                break;
        }
    }
}
