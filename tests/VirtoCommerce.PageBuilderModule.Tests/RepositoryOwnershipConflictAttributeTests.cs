using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    public class RepositoryOwnershipConflictAttributeTests
    {
        [Fact]
        public void A_store_working_in_another_stores_repository_is_told_so_with_409()
        {
            var context = Context(new GitRepositoryOwnershipException("acme/site-a", "a"));

            new RepositoryOwnershipConflictAttribute().OnException(context);

            Assert.True(context.ExceptionHandled);
            var conflict = Assert.IsType<ConflictObjectResult>(context.Result);
            Assert.Contains("belongs to store \"a\"", conflict.Value!.GetType().GetProperty("error")!.GetValue(conflict.Value) as string);
        }

        [Fact]
        public void Any_other_failure_goes_on_as_it_was()
        {
            var context = Context(new InvalidOperationException("something else"));

            new RepositoryOwnershipConflictAttribute().OnException(context);

            Assert.False(context.ExceptionHandled);
            Assert.Null(context.Result);
        }

        private static ExceptionContext Context(Exception exception) =>
            new(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()), []) { Exception = exception };
    }
}
