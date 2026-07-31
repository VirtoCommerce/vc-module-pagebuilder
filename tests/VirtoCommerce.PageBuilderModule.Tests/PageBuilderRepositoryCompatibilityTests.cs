using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Domain;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderRepositoryCompatibilityTests
{
    [Fact]
    public void SharedComponentCapabilities_LegacyImplementationDoesNotOptIn()
    {
        IPageBuilderModuleRepository repository = new LegacyRepository();
        Assert.False(repository is IPageBuilderLinkedComponentRepository);
        Assert.False(repository is IPageBuilderWriteLockRepository);
    }

    private sealed class LegacyRepository : IPageBuilderModuleRepository
    {
        public IQueryable<PageBuilderPageEntity> PageBuilderPages => Empty<PageBuilderPageEntity>();
        public IQueryable<GroupedPageBuilderPageEntity> GroupedPageBuilderPages => Empty<GroupedPageBuilderPageEntity>();
        public IQueryable<PageBuilderAssetReferenceEntity> PageBuilderAssetReferences => Empty<PageBuilderAssetReferenceEntity>();
        public IUnitOfWork UnitOfWork => null;

        public Task<IList<PageBuilderPageEntity>> GetPageBuilderPagesByIdsAsync(IList<string> ids, string responseGroup)
        {
            return Task.FromResult<IList<PageBuilderPageEntity>>([]);
        }

        public Task<IList<GroupedPageBuilderPageEntity>> GetGroupedPageBuilderPagesByIdsAsync(IList<string> ids, string responseGroup)
        {
            return Task.FromResult<IList<GroupedPageBuilderPageEntity>>([]);
        }

        public void Attach<T>(T item) where T : class
        {
        }

        public void Add<T>(T item) where T : class
        {
        }

        public void Update<T>(T item) where T : class
        {
        }

        public void Remove<T>(T item) where T : class
        {
        }

        public void Dispose()
        {
        }

        private static IQueryable<T> Empty<T>()
        {
            return Array.Empty<T>().AsQueryable();
        }
    }
}
