using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

namespace VirtoCommerce.PageBuilderModule.Tests;

internal static class MvcRouting
{
    private static readonly Lazy<IServiceProvider> Services = new(() =>
    {
        var services = new ServiceCollection();
        services.AddSingleton(new DiagnosticListener("Microsoft.AspNetCore"));
        services.AddLogging();
        services.AddRouting();
        services.AddMvcCore().AddApplicationPart(typeof(GitContentSetupController).Assembly);
        var provider = services.BuildServiceProvider();

        var app = new ApplicationBuilder(provider);
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapControllers());

        return provider;
    });

    public static ControllerContext CreateContext(HttpContext httpContext, string controller)
    {
        httpContext.RequestServices = Services.Value;
        httpContext.SetEndpoint(new Endpoint(null, EndpointMetadataCollection.Empty, controller));

        return new ControllerContext
        {
            HttpContext = httpContext,
            RouteData = new RouteData(new RouteValueDictionary { ["controller"] = controller }),
        };
    }
}
