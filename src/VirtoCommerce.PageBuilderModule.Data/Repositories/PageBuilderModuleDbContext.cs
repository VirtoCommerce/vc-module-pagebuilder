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

        modelBuilder.Entity<PageBuilderLinkedComponentEntity>().ToTable("PageBuilderLinkedComponent").HasKey(x => x.Id);
        modelBuilder.Entity<PageBuilderLinkedComponentEntity>().Property(x => x.Id).HasMaxLength(IdLength).ValueGeneratedOnAdd();
        modelBuilder.Entity<PageBuilderLinkedComponentEntity>().Property(x => x.Name)
            .HasMaxLength(ModuleConstants.LinkedComponents.NameMaxLength).IsRequired();
        modelBuilder.Entity<PageBuilderLinkedComponentEntity>().HasIndex(x => x.StoreId);
        modelBuilder.Entity<PageBuilderLinkedComponentEntity>().HasOne(x => x.Content).WithOne(x => x.Component)
            .HasForeignKey<PageBuilderLinkedComponentContentEntity>(x => x.Id)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PageBuilderLinkedComponentEntity>().Navigation(x => x.Content).AutoInclude(false);

        modelBuilder.Entity<PageBuilderLinkedComponentContentEntity>().ToTable("PageBuilderLinkedComponentContent").HasKey(x => x.Id);
        modelBuilder.Entity<PageBuilderLinkedComponentContentEntity>().Property(x => x.Id).HasMaxLength(IdLength).ValueGeneratedNever();
        modelBuilder.Entity<PageBuilderLinkedComponentContentEntity>().Property(x => x.ComponentContent).IsRequired();

        modelBuilder.Entity<PageBuilderLinkedComponentReferenceEntity>().ToTable("PageBuilderLinkedComponentReference").HasKey(x => x.Id);
        modelBuilder.Entity<PageBuilderLinkedComponentReferenceEntity>().Property(x => x.Id).HasMaxLength(IdLength).ValueGeneratedOnAdd();
        modelBuilder.Entity<PageBuilderLinkedComponentReferenceEntity>().HasOne<PageBuilderPageEntity>().WithMany()
            .HasForeignKey(x => x.PageId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        modelBuilder.Entity<PageBuilderLinkedComponentReferenceEntity>().HasOne<PageBuilderLinkedComponentEntity>().WithMany()
            .HasForeignKey(x => x.LinkedComponentId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        modelBuilder.Entity<PageBuilderLinkedComponentReferenceEntity>().HasIndex(x => x.PageId);
        modelBuilder.Entity<PageBuilderLinkedComponentReferenceEntity>().HasIndex(x => x.LinkedComponentId);
        modelBuilder.Entity<PageBuilderLinkedComponentReferenceEntity>()
            .HasIndex(x => new { x.PageId, x.LinkedComponentId }).IsUnique();

        modelBuilder.Entity<PageBuilderLinkedComponentAssetReferenceEntity>()
            .ToTable("PageBuilderLinkedComponentAssetReference").HasKey(x => x.Id);
        modelBuilder.Entity<PageBuilderLinkedComponentAssetReferenceEntity>()
            .Property(x => x.Id).HasMaxLength(IdLength).ValueGeneratedOnAdd();
        modelBuilder.Entity<PageBuilderLinkedComponentAssetReferenceEntity>()
            .HasOne<PageBuilderLinkedComponentEntity>().WithMany()
            .HasForeignKey(x => x.LinkedComponentId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        modelBuilder.Entity<PageBuilderLinkedComponentAssetReferenceEntity>().HasIndex(x => x.LinkedComponentId);
        modelBuilder.Entity<PageBuilderLinkedComponentAssetReferenceEntity>().HasIndex(x => x.NormalizedAssetUrlHash);
        modelBuilder.Entity<PageBuilderLinkedComponentAssetReferenceEntity>()
            .HasIndex(x => new { x.LinkedComponentId, x.NormalizedAssetUrlHash }).IsUnique();

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
