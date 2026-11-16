using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.Pages.Core.Search;
using VirtoCommerce.Platform.Core.Events;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderPageControllerUpdateGroupTests
{
    private const string StoreA = "store-a";
    private const string StoreB = "store-b";

    [Fact]
    public async Task UpdateGroup_InTheCallersStore_IsSaved()
    {
        var groups = new GroupStore(Group("group", StoreA));

        var result = await Controller(groups, StoreA).UpdateGroup(new GroupedPageBuilderPage { Id = "group", StoreId = StoreA, Name = "Renamed" }, TestContext.Current.CancellationToken);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("Renamed", groups.Get("group").Name);
        Assert.Equal(1, groups.Saves);
    }

    [Fact]
    public async Task UpdateGroup_OfAnotherStore_NamingTheCallersStore_IsForbidden()
    {
        var groups = new GroupStore(Group("group", StoreB));

        var result = await Controller(groups, StoreA).UpdateGroup(new GroupedPageBuilderPage { Id = "group", StoreId = StoreA, Name = "Taken over" }, TestContext.Current.CancellationToken);

        AssertForbidden(result);
        Assert.Equal(StoreB, groups.Get("group").StoreId);
        Assert.Equal("Original", groups.Get("group").Name);
        Assert.Equal(0, groups.Saves);
    }

    [Fact]
    public async Task UpdateGroup_MovingToAStoreTheCallerCannotReach_IsForbidden()
    {
        var groups = new GroupStore(Group("group", StoreA));

        var result = await Controller(groups, StoreA).UpdateGroup(new GroupedPageBuilderPage { Id = "group", StoreId = StoreB, Name = "Moved" }, TestContext.Current.CancellationToken);

        AssertForbidden(result);
        Assert.Equal(StoreA, groups.Get("group").StoreId);
        Assert.Equal(0, groups.Saves);
    }

    [Fact]
    public async Task UpdateGroup_MovingBetweenStoresTheCallerReaches_IsSaved()
    {
        var groups = new GroupStore(Group("group", StoreA));

        var result = await Controller(groups, StoreA, StoreB).UpdateGroup(new GroupedPageBuilderPage { Id = "group", StoreId = StoreB, Name = "Moved" }, TestContext.Current.CancellationToken);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StoreB, groups.Get("group").StoreId);
    }

    [Fact]
    public async Task UpdateGroup_WithoutAnExistingGroup_IsCheckedAgainstTheRequestedStore()
    {
        var allowed = await Controller(new GroupStore(), StoreA).UpdateGroup(new GroupedPageBuilderPage { StoreId = StoreA, Name = "New" }, TestContext.Current.CancellationToken);
        var forbidden = await Controller(new GroupStore(), StoreA).UpdateGroup(new GroupedPageBuilderPage { StoreId = StoreB, Name = "New" }, TestContext.Current.CancellationToken);

        Assert.IsType<OkObjectResult>(allowed.Result);
        AssertForbidden(forbidden);
    }

    private static GroupedPageBuilderPage Group(string id, string storeId) => new()
    {
        Id = id,
        StoreId = storeId,
        Name = "Original",
        Pages = [new PageBuilderPage { Id = id + "-draft", StoreId = storeId, Status = "Draft" }],
    };

    private static void AssertForbidden(ActionResult<GroupedPageBuilderPage> result)
    {
        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    private static PageBuilderPageController Controller(GroupStore groups, params string[] allowedStores)
    {
        var controller = new PageBuilderPageController(
            Fake<IPageBuilderPageService>.Create((method, _) => method.Name == "GetAsync" ? Task.FromResult<IList<PageBuilderPage>>([]) : null),
            groups.Service,
            Fake<IGroupedPageSearchService>.Create((_, _) => null),
            new StoreScopedAuthorizationService(allowedStores),
            Fake<IPageDocumentSearchService>.Create((_, _) => null),
            Fake<IEventPublisher>.Create((_, _) => null),
            NullLogger<PageBuilderPageController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) },
        };

        return controller;
    }

    private sealed class GroupStore
    {
        private readonly Dictionary<string, GroupedPageBuilderPage> _groups;

        public GroupStore(params GroupedPageBuilderPage[] groups)
        {
            _groups = groups.ToDictionary(x => x.Id);
            Service = Fake<IGroupedPageService>.Create(Handle);
        }

        public IGroupedPageService Service { get; }

        public int Saves { get; private set; }

        public GroupedPageBuilderPage Get(string id) => _groups[id];

        private object Handle(MethodInfo method, object[] args)
        {
            switch (method.Name)
            {
                case "GetAsync":
                    var ids = (IEnumerable<string>)args[0];
                    IList<GroupedPageBuilderPage> found = ids.Where(id => id != null && _groups.ContainsKey(id)).Select(id => _groups[id]).ToList();
                    return Task.FromResult(found);
                case "SaveChangesAsync":
                    Saves++;
                    foreach (var group in (IEnumerable<GroupedPageBuilderPage>)args[0])
                    {
                        group.Id ??= Guid.NewGuid().ToString();
                        foreach (var page in group.Pages.Where(x => x.Id == null))
                        {
                            page.Id = Guid.NewGuid().ToString();
                        }
                        _groups[group.Id] = group;
                    }
                    return Task.CompletedTask;
                default:
                    return null;
            }
        }
    }

    private sealed class StoreScopedAuthorizationService(string[] allowedStores) : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            var allowed = resource is GroupedPageBuilderPage page && allowedStores.Contains(page.StoreId, StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName) =>
            Task.FromResult(AuthorizationResult.Success());
    }

    // Answers what a test handles and a completed task or a default value for everything else.
    public class Fake<T> : DispatchProxy where T : class
    {
        private Func<MethodInfo, object[], object> _handler;

        public static T Create(Func<MethodInfo, object[], object> handler)
        {
            var proxy = Create<T, Fake<T>>();
            ((Fake<T>)(object)proxy)._handler = handler;
            return proxy;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args) =>
            _handler(targetMethod, args) ?? DefaultFor(targetMethod.ReturnType);

        private static object DefaultFor(Type type)
        {
            if (type == typeof(Task))
            {
                return Task.CompletedTask;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var result = type.GetGenericArguments()[0];
                return typeof(Task).GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(result)
                    .Invoke(null, [result.IsValueType ? Activator.CreateInstance(result) : null]);
            }

            return type.IsValueType && type != typeof(void) ? Activator.CreateInstance(type) : null;
        }
    }
}
