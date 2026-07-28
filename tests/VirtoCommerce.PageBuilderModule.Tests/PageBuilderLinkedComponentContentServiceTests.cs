using System;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.PageBuilderModule.Data.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderLinkedComponentContentServiceTests
{
    [Fact]
    public void TouchMetadata_MarksOnlyModifiedDateAndCannotOverwriteConcurrentRename()
    {
        var options = new DbContextOptionsBuilder<PageBuilderModuleDbContext>()
            .UseSqlServer("Server=(local);Database=PageBuilderTouchDesignTest;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new PageBuilderModuleDbContext(options);
        var component = new PageBuilderLinkedComponentEntity
        {
            Id = "component",
            StoreId = "store",
            Name = "name-loaded-before-concurrent-rename",
            CreatedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ModifiedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        context.Attach(component);

        PageBuilderLinkedComponentContentService.TouchMetadata(component);
        context.ChangeTracker.DetectChanges();

        var modifiedProperty = Assert.Single(context.Entry(component).Properties, x => x.IsModified);
        Assert.Equal(nameof(PageBuilderLinkedComponentEntity.ModifiedDate), modifiedProperty.Metadata.Name);
        Assert.False(context.Entry(component).Property(x => x.Name).IsModified);
        Assert.False(context.Entry(component).Property(x => x.StoreId).IsModified);
    }
}
