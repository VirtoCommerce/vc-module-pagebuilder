using System.Reflection;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.Platform.Data.Infrastructure;

namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

public class PageBuilderModuleDbContext : DbContextBase
{
    public const string PageBuilderPageTableName = "PageBuilderPage";

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

        modelBuilder.Entity<GroupedPageBuilderPageEntity>().ToTable("GroupedPageBuilderPage").HasKey(x => x.Id);
        modelBuilder.Entity<GroupedPageBuilderPageEntity>().Property(x => x.Id).HasMaxLength(IdLength).ValueGeneratedOnAdd();

        modelBuilder.Entity<PageBuilderPageEntity>().ToTable(PageBuilderPageTableName).HasKey(x => x.Id);
        modelBuilder.Entity<PageBuilderPageEntity>().Property(x => x.Id).HasMaxLength(IdLength).ValueGeneratedOnAdd();
        modelBuilder.Entity<PageBuilderPageEntity>().HasOne(x => x.Group).WithMany(x => x.Pages)
            .HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        modelBuilder.Entity<PageBuilderPageEntity>().HasOne(x => x.Content).WithOne(x => x.Page)
            .HasForeignKey<PageBuilderContentEntity>(x => x.Id);
        modelBuilder.Entity<PageBuilderPageEntity>().Navigation(x => x.Content).AutoInclude(false);

        modelBuilder.Entity<PageBuilderContentEntity>().ToTable(PageBuilderPageTableName).HasKey(x => x.Id);

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
