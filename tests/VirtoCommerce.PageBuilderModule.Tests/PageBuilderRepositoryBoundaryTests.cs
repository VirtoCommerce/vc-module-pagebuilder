using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.ContentProviders;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.PageBuilderModule.Data.Services;
using VirtoCommerce.PageBuilderModule.Web.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderRepositoryBoundaryTests
{
    [Fact]
    public void ContentStreamRepository_HasSingleRepositoryContract()
    {
        var repositoryContracts = typeof(ContentStreamRepository).GetInterfaces()
            .Where(type => type != typeof(IAsyncDisposable))
            .ToArray();

        Assert.Equal([typeof(IContentStreamRepository)], repositoryContracts);
    }

    [Fact]
    public void ContentStreamRepository_DoesNotExposeRawWriteOperations()
    {
        var methodNames = typeof(IContentStreamRepository).GetMethods()
            .Select(method => method.Name)
            .ToArray();

        Assert.DoesNotContain("SaveBinaryAsync", methodNames);
        Assert.DoesNotContain("SaveRawContentAsync", methodNames);
        Assert.DoesNotContain("CopyContentAsync", methodNames);
    }

    [Fact]
    public void ServiceCompatibilityMethods_DoNotHaveDefaultImplementations()
    {
        var methods = new[]
        {
            typeof(IGroupedPageService).GetMethod(nameof(IGroupedPageService.TryDeleteEmptyDraftAsync)),
            typeof(IPageBuilderSharedComponentContentService).GetMethod(nameof(IPageBuilderSharedComponentContentService.TryLoadContentAsync)),
            typeof(IPageBuilderSharedComponentContentService).GetMethod(nameof(IPageBuilderSharedComponentContentService.TrySaveContentAsync)),
            typeof(IPageBuilderSharedComponentService).GetMethod(nameof(IPageBuilderSharedComponentService.UpdateMetadataAsync)),
            typeof(IPageBuilderSharedComponentService).GetMethod(nameof(IPageBuilderSharedComponentService.TryDeleteAsync)),
        };

        Assert.All(methods, method => Assert.True(method?.IsAbstract));
    }

    [Fact]
    public void NonRepositoryTypes_DoNotExposePageBuilderDbContext()
    {
        var dataAssembly = typeof(GroupedPageService).Assembly;
        var repositoryNamespace = typeof(PageBuilderModuleRepository).Namespace;

        var leakingMembers = dataAssembly.GetTypes()
            .Where(type => type.Namespace != repositoryNamespace)
            .SelectMany(GetDeclaredMembers)
            .Where(ReferencesPageBuilderDbContext)
            .Select(member => $"{member.DeclaringType?.FullName}.{member.Name}")
            .Order()
            .ToArray();

        Assert.Empty(leakingMembers);
    }

    [Fact]
    public void ContentProviders_DoNotDependOnPageBuilderRepositories()
    {
        var dataAssembly = typeof(PageBuilderContentProvider).Assembly;
        var contentProviderNamespace = typeof(PageBuilderContentProvider).Namespace;

        var leakingParameters = dataAssembly.GetTypes()
            .Where(type => type.Namespace == contentProviderNamespace)
            .SelectMany(type => type.GetConstructors())
            .SelectMany(constructor => constructor.GetParameters())
            .Where(parameter => ReferencesPageBuilderRepository(parameter.ParameterType))
            .Select(parameter => $"{parameter.Member.DeclaringType?.FullName}.{parameter.Name}")
            .Order()
            .ToArray();

        Assert.Empty(leakingParameters);
    }

    [Fact]
    public void PageBuilderModuleRepository_DisposesItsDbContext()
    {
        var options = new DbContextOptionsBuilder<PageBuilderModuleDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var dbContext = new PageBuilderModuleDbContext(options);
        var repository = new PageBuilderModuleRepository(dbContext);

        repository.Dispose();

        Assert.Throws<ObjectDisposedException>(() => dbContext.Database.EnsureCreated());
    }

    [Fact]
    public async Task ContentStreamRepository_DisposesItsDbContext()
    {
        var options = new DbContextOptionsBuilder<PageBuilderModuleDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var dbContext = new PageBuilderModuleDbContext(options);
        var repository = new ContentStreamRepositoryProbe(dbContext);

        await repository.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => dbContext.Database.EnsureCreated());
    }

    [Fact]
    public void ServiceTypes_ArePublic()
    {
        var assemblies = new[]
        {
            typeof(GroupedPageService).Assembly,
            typeof(PageBuilderPageContentService).Assembly,
        };

        var nonPublicServices = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Namespace?.Contains(".Services", StringComparison.Ordinal) == true)
            .Where(type => type.Name.EndsWith("Service", StringComparison.Ordinal))
            .Where(type => !type.IsPublic)
            .Select(type => type.FullName)
            .Order()
            .ToArray();

        Assert.Empty(nonPublicServices);
    }

    private static IEnumerable<MemberInfo> GetDeclaredMembers(Type type)
    {
        const BindingFlags flags = BindingFlags.DeclaredOnly |
                                   BindingFlags.Instance |
                                   BindingFlags.Static |
                                   BindingFlags.Public |
                                   BindingFlags.NonPublic;

        return type.GetMembers(flags)
            .Where(member => member is MethodBase or FieldInfo or PropertyInfo);
    }

    private static bool ReferencesPageBuilderDbContext(MemberInfo member)
    {
        return member switch
        {
            MethodInfo method => ReferencesPageBuilderDbContext(method.ReturnType) ||
                                 method.GetParameters().Any(parameter =>
                                     ReferencesPageBuilderDbContext(parameter.ParameterType)),
            ConstructorInfo constructor => constructor.GetParameters().Any(parameter =>
                ReferencesPageBuilderDbContext(parameter.ParameterType)),
            FieldInfo field => ReferencesPageBuilderDbContext(field.FieldType),
            PropertyInfo property => ReferencesPageBuilderDbContext(property.PropertyType),
            _ => false,
        };
    }

    private static bool ReferencesPageBuilderDbContext(Type type)
    {
        return type == typeof(PageBuilderModuleDbContext) ||
               type.HasElementType && ReferencesPageBuilderDbContext(type.GetElementType()!) ||
               type.IsGenericType && type.GetGenericArguments().Any(ReferencesPageBuilderDbContext);
    }

    private static bool ReferencesPageBuilderRepository(Type type)
    {
        return type == typeof(IPageBuilderModuleRepository) ||
               type == typeof(IContentStreamRepository) ||
               type.HasElementType && ReferencesPageBuilderRepository(type.GetElementType()!) ||
               type.IsGenericType && type.GetGenericArguments().Any(ReferencesPageBuilderRepository);
    }

    private sealed class ContentStreamRepositoryProbe(PageBuilderModuleDbContext dbContext)
        : ContentStreamRepository(dbContext)
    {
        protected override string QuoteOpen => "\"";
        protected override string QuoteClose => "\"";
        protected override string AppendContentChunkSql => string.Empty;

        protected override void SetIdParameter(DbCommand cmd, string value)
        {
        }

        protected override void SetContentChunk(DbCommand cmd, string chunk)
        {
        }
    }

}
