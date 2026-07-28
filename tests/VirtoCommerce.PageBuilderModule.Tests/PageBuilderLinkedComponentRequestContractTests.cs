using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.PageBuilderModule.Web.Models;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderLinkedComponentRequestContractTests
{
    [Fact]
    public void CreateModel_NewtonsoftBindingPreservesObjectContent()
    {
        const string json = """
            {
              "storeId": "store",
              "name": "Shared hero",
              "content": {
                "settings": { "image": "/stores/store/hero.png" },
                "content": []
              }
            }
            """;

        var model = JsonConvert.DeserializeObject<PageBuilderLinkedComponentCreateModel>(json);

        Assert.NotNull(model);
        Assert.Equal("store", model.StoreId);
        Assert.Equal("Shared hero", model.Name);
        Assert.IsType<JObject>(model.Content);
        Assert.Equal(
            "{\"settings\":{\"image\":\"/stores/store/hero.png\"},\"content\":[]}",
            model.Content.ToString(Formatting.None));
    }

    [Fact]
    public void SaveContentAction_UsesNewtonsoftObjectBodyContract()
    {
        var action = typeof(PageBuilderLinkedComponentsController)
            .GetMethods()
            .Single(x => x.Name == nameof(PageBuilderLinkedComponentsController.SaveContent));
        var contentParameter = action.GetParameters().Single(x => x.Name == "content");

        Assert.Equal(typeof(JObject), contentParameter.ParameterType);
        Assert.NotNull(contentParameter.GetCustomAttributes(typeof(FromBodyAttribute), inherit: true).SingleOrDefault());
    }

    [Fact]
    public void Actions_DescribeTheirActualSuccessResponsesForGeneratedClients()
    {
        AssertResponseType(
            nameof(PageBuilderLinkedComponentsController.Create),
            StatusCodes.Status201Created,
            typeof(PageBuilderLinkedComponent));
        AssertResponseType(
            nameof(PageBuilderLinkedComponentsController.GetContent),
            StatusCodes.Status200OK,
            typeof(JObject));
        AssertResponseType(
            nameof(PageBuilderLinkedComponentsController.SaveContent),
            StatusCodes.Status204NoContent,
            expectedType: null);
    }

    private static void AssertResponseType(
        string actionName,
        int statusCode,
        Type expectedType)
    {
        var action = typeof(PageBuilderLinkedComponentsController)
            .GetMethods()
            .Single(x => x.Name == actionName);
        var response = action
            .GetCustomAttributes(typeof(ProducesResponseTypeAttribute), inherit: true)
            .Cast<ProducesResponseTypeAttribute>()
            .Single(x => x.StatusCode == statusCode);

        if (expectedType != null)
        {
            Assert.Equal(expectedType, response.Type);
        }
    }
}
