using System.Reflection;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Core;
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

        modelBuilder.Entity<PageBuilderAssetReferenceEntity>().ToTable("PageBuilderAssetReference").HasKey(x => x.Id);
        modelBuilder.Entity<PageBuilderAssetReferenceEntity>().Property(x => x.Id).HasMaxLength(IdLength).ValueGeneratedOnAdd();
        modelBuilder.Entity<PageBuilderAssetReferenceEntity>().HasOne<PageBuilderPageEntity>().WithMany()
            .HasForeignKey(x => x.PageId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        modelBuilder.Entity<PageBuilderAssetReferenceEntity>().HasIndex(x => x.PageId);
        modelBuilder.Entity<PageBuilderAssetReferenceEntity>().HasIndex(x => x.NormalizedAssetUrlHash);
        modelBuilder.Entity<PageBuilderAssetReferenceEntity>().HasIndex(x => new { x.PageId, x.NormalizedAssetUrlHash }).IsUnique();

        modelBuilder.Entity<PageBuilderSharedComponentEntity>().ToTable("PageBuilderSharedComponent").HasKey(x => x.Id);
        modelBuilder.Entity<PageBuilderSharedComponentEntity>().Property(x => x.Id).HasMaxLength(IdLength).ValueGeneratedOnAdd();
        modelBuilder.Entity<PageBuilderSharedComponentEntity>().Property(x => x.Name)
            .HasMaxLength(ModuleConstants.SharedComponents.NameMaxLength).IsRequired();
        modelBuilder.Entity<PageBuilderSharedComponentEntity>().HasIndex(x => x.StoreId);
        modelBuilder.Entity<PageBuilderSharedComponentEntity>().HasOne(x => x.Content).WithOne(x => x.Component)
            .HasForeignKey<PageBuilderSharedComponentContentEntity>(x => x.Id)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PageBuilderSharedComponentEntity>().Navigation(x => x.Content).AutoInclude(false);

        modelBuilder.Entity<PageBuilderSharedComponentContentEntity>().ToTable("PageBuilderSharedComponentContent").HasKey(x => x.Id);
        modelBuilder.Entity<PageBuilderSharedComponentContentEntity>().Property(x => x.Id).HasMaxLength(IdLength).ValueGeneratedNever();
        modelBuilder.Entity<PageBuilderSharedComponentContentEntity>().Property(x => x.ComponentContent).IsRequired();

        modelBuilder.Entity<PageBuilderSharedComponentReferenceEntity>().ToTable("PageBuilderSharedComponentReference").HasKey(x => x.Id);
        modelBuilder.Entity<PageBuilderSharedComponentReferenceEntity>().Property(x => x.Id).HasMaxLength(IdLength).ValueGeneratedOnAdd();
        modelBuilder.Entity<PageBuilderSharedComponentReferenceEntity>().HasOne<PageBuilderPageEntity>().WithMany()
            .HasForeignKey(x => x.PageId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        modelBuilder.Entity<PageBuilderSharedComponentReferenceEntity>().HasOne<PageBuilderSharedComponentEntity>().WithMany()
            .HasForeignKey(x => x.SharedComponentId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        modelBuilder.Entity<PageBuilderSharedComponentReferenceEntity>().HasIndex(x => x.PageId);
        modelBuilder.Entity<PageBuilderSharedComponentReferenceEntity>().HasIndex(x => x.SharedComponentId);
        modelBuilder.Entity<PageBuilderSharedComponentReferenceEntity>()
            .HasIndex(x => new { x.PageId, x.SharedComponentId }).IsUnique();

        modelBuilder.Entity<PageBuilderSharedComponentAssetReferenceEntity>()
            .ToTable("PageBuilderSharedComponentAssetReference").HasKey(x => x.Id);
        modelBuilder.Entity<PageBuilderSharedComponentAssetReferenceEntity>()
            .Property(x => x.Id).HasMaxLength(IdLength).ValueGeneratedOnAdd();
        modelBuilder.Entity<PageBuilderSharedComponentAssetReferenceEntity>()
            .HasOne<PageBuilderSharedComponentEntity>().WithMany()
            .HasForeignKey(x => x.SharedComponentId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        modelBuilder.Entity<PageBuilderSharedComponentAssetReferenceEntity>().HasIndex(x => x.SharedComponentId);
        modelBuilder.Entity<PageBuilderSharedComponentAssetReferenceEntity>().HasIndex(x => x.NormalizedAssetUrlHash);
        modelBuilder.Entity<PageBuilderSharedComponentAssetReferenceEntity>()
            .HasIndex(x => new { x.SharedComponentId, x.NormalizedAssetUrlHash }).IsUnique();

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
