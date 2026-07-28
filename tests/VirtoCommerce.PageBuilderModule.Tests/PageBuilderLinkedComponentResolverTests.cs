using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderLinkedComponentResolverTests
{
    [Fact]
    public async Task ResolveAsync_FlattensComponentSectionsAndRemapsIdsPerPlacement()
    {
        const string componentContent = """
            {
              "settings": { "name": "must-not-leak" },
              "content": [
                {
                  "id": "hero",
                  "__id": "hero",
                  "type": "hero",
                  "title": "Shared title",
                  "blocks": [
                    { "id": "text", "__id": "text", "type": "text" },
                    { "id": "text", "__id": "text", "type": "text" }
                  ]
                }
              ]
            }
            """;
        var contentService = new StubContentService(new Dictionary<string, string>
        {
            ["component-1"] = componentContent,
        });
        var resolver = new PageBuilderLinkedComponentResolver(
            contentService,
            NullLogger<PageBuilderLinkedComponentResolver>.Instance);
        const string pageContent = """
            {
              "settings": { "name": "Page" },
              "content": [
                { "id": "before", "type": "text" },
                { "id": "placement-a", "type": "componentRef", "componentRef": "component-1" },
                { "id": "placement-b", "type": "componentRef", "componentRef": "component-1" }
              ]
            }
            """;

        var resolved = await resolver.ResolveAsync(pageContent, TestContext.Current.CancellationToken);
        var root = JsonNode.Parse(resolved).AsObject();
        var sections = root["content"].AsArray();

        Assert.Equal(3, sections.Count);
        Assert.Equal("Page", root["settings"]["name"].GetValue<string>());
        var firstPlacementId = sections[1]["id"].GetValue<string>();
        var secondPlacementId = sections[2]["id"].GetValue<string>();
        Assert.Equal("lc706c6163656d656e742d61section0", firstPlacementId);
        Assert.Equal(firstPlacementId, sections[1]["__id"].GetValue<string>());
        Assert.Equal("lc706c6163656d656e742d61section0block0", sections[1]["blocks"][0]["id"].GetValue<string>());
        Assert.Equal("lc706c6163656d656e742d61section0block1", sections[1]["blocks"][1]["id"].GetValue<string>());
        Assert.Equal("lc706c6163656d656e742d62section0", secondPlacementId);
        Assert.Equal("Shared title", sections[2]["title"].GetValue<string>());
        Assert.DoesNotContain("must-not-leak", resolved);
        Assert.Contains("componentRef", pageContent);
    }

    [Theory]
    [InlineData("placement-1", "lc706c6163656d656e742d31section0")]
    [InlineData("a-b", "lc612d62section0")]
    [InlineData("Компонент-🙂", "lcd09ad0bed0bcd0bfd0bed0bdd0b5d0bdd1822df09f9982section0")]
    public async Task ResolveAsync_UsesDesignerUtf8HexIdContract(string placementId, string expectedId)
    {
        var resolver = new PageBuilderLinkedComponentResolver(
            new StubContentService(new Dictionary<string, string>
            {
                ["component"] = "{ \"settings\": {}, \"content\": [{ \"id\": \"hero\", \"type\": \"hero\" }] }",
            }),
            NullLogger<PageBuilderLinkedComponentResolver>.Instance);
        var pageContent = new JsonObject
        {
            ["settings"] = new JsonObject(),
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = placementId,
                    ["type"] = "componentRef",
                    ["componentRef"] = "component",
                },
            },
        }.ToJsonString();

        var resolved = await resolver.ResolveAsync(pageContent, TestContext.Current.CancellationToken);
        var resolvedId = JsonNode.Parse(resolved)["content"][0]["id"].GetValue<string>();

        Assert.Equal(expectedId, resolvedId);
    }

    [Fact]
    public async Task ResolveAsync_KeepsPunctuationDistinctPlacementIdsCollisionSafe()
    {
        var resolver = new PageBuilderLinkedComponentResolver(
            new StubContentService(new Dictionary<string, string>
            {
                ["component"] = "{ \"settings\": {}, \"content\": [{ \"id\": \"hero\", \"type\": \"hero\" }] }",
            }),
            NullLogger<PageBuilderLinkedComponentResolver>.Instance);
        const string pageContent = """
            {
              "settings": {},
              "content": [
                { "id": "a-b", "type": "componentRef", "componentRef": "component" },
                { "id": "ab", "type": "componentRef", "componentRef": "component" }
              ]
            }
            """;

        var resolved = await resolver.ResolveAsync(pageContent, TestContext.Current.CancellationToken);
        var sections = JsonNode.Parse(resolved)["content"].AsArray();

        Assert.NotEqual(
            sections[0]["id"].GetValue<string>(),
            sections[1]["id"].GetValue<string>());
    }

    [Fact]
    public async Task ResolveAsync_RemovesUnresolvablePlacementWithoutChangingOrdinarySections()
    {
        var resolver = new PageBuilderLinkedComponentResolver(
            new StubContentService(new Dictionary<string, string>()),
            NullLogger<PageBuilderLinkedComponentResolver>.Instance);
        const string pageContent = """
            {
              "settings": {},
              "content": [
                { "id": "ordinary", "type": "hero" },
                { "id": "missing", "type": "componentRef", "componentRef": "does-not-exist" }
              ]
            }
            """;

        var resolved = await resolver.ResolveAsync(pageContent, TestContext.Current.CancellationToken);
        var sections = JsonNode.Parse(resolved)["content"].AsArray();

        Assert.Equal("ordinary", Assert.Single(sections)["id"].GetValue<string>());
    }

    private sealed class StubContentService(IReadOnlyDictionary<string, string> contents)
        : IPageBuilderLinkedComponentContentService
    {
        public Task<string> LoadContentAsync(string linkedComponentId, CancellationToken cancellationToken = default)
        {
            contents.TryGetValue(linkedComponentId, out var content);
            return Task.FromResult(content);
        }

        public Task<IReadOnlyDictionary<string, string>> LoadContentsAsync(
            IEnumerable<string> linkedComponentIds,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<string, string> result = linkedComponentIds
                .Where(contents.ContainsKey)
                .ToDictionary(x => x, x => contents[x], StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(result);
        }

        public Task SaveContentAsync(
            string linkedComponentId,
            string content,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
