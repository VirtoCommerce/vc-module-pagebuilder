using System;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class GitContentFailureAttributeTests
{
    [Fact]
    public void OnException_GitContentException_ReturnsBadGatewayWithTheReason()
    {
        var context = Context(new GitContentException("\"https://github.com/acme/site\" is not a GitHub repository in \"owner/name\" form."));

        new GitContentFailureAttribute().OnException(context);

        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        Assert.Contains("owner/name", result.Value!.GetType().GetProperty("error")!.GetValue(result.Value) as string);
    }

    [Fact]
    public void OnException_OtherException_LeavesItUnhandled()
    {
        var context = Context(new GitRepositoryOwnershipException("acme/site-a", "a"));

        new GitContentFailureAttribute().OnException(context);

        Assert.False(context.ExceptionHandled);
        Assert.Null(context.Result);
    }

    [Fact]
    public void PageBuilderController_HasTheAttribute()
    {
        Assert.NotNull(typeof(PageBuilderController).GetCustomAttribute<GitContentFailureAttribute>());
    }

    private static ExceptionContext Context(Exception exception) =>
        new(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()), []) { Exception = exception };
}
