using System.IO;
using VirtoCommerce.PageBuilderModule.Data.Services;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

public class PageBuilderLinkedComponentReferenceMatcherTests
{
    [Fact]
    public void HasReferences_DistinguishesOrdinaryAndLinkedPageContent()
    {
        Assert.False(PageBuilderLinkedComponentReferenceMatcher.HasReferences(
            "{ \"settings\": {}, \"content\": [{ \"id\": \"hero\", \"type\": \"hero\" }] }"));
        Assert.True(PageBuilderLinkedComponentReferenceMatcher.HasReferences(
            "{ \"settings\": {}, \"content\": [{ \"id\": \"placement\", \"type\": \"componentRef\", \"componentRef\": \"component\" }] }"));
    }

    [Fact]
    public void ExtractReferences_ReturnsDistinctTopLevelComponentReferences()
    {
        var content = """
            {
              "settings": {},
              "content": [
                { "id": "placement-1", "type": "componentRef", "componentRef": "component-1" },
                { "id": "ordinary", "type": "hero", "blocks": [] },
                { "id": "placement-2", "type": "componentRef", "componentRef": "component-1" }
              ]
            }
            """;

        var references = PageBuilderLinkedComponentReferenceMatcher.ExtractReferences(content);

        Assert.Equal("component-1", Assert.Single(references));
    }

    [Theory]
    [InlineData("{ \"settings\": {}, \"content\": [{ \"id\": \"x\", \"componentRef\": \"c\" }] }")]
    [InlineData("{ \"settings\": {}, \"content\": [{ \"id\": \"x\", \"type\": \"componentRef\" }] }")]
    [InlineData("{ \"settings\": {}, \"content\": [{ \"id\": \"x\", \"type\": \"componentRef\", \"componentRef\": \"c\", \"blocks\": [] }] }")]
    [InlineData("{ \"settings\": {}, \"content\": [{ \"id\": \"section\", \"type\": \"hero\", \"blocks\": [{ \"id\": \"x\", \"type\": \"componentRef\", \"componentRef\": \"c\" }] }] }")]
    public void ExtractReferences_RejectsMalformedOrNestedReferences(string content)
    {
        Assert.Throws<InvalidDataException>(() =>
            PageBuilderLinkedComponentReferenceMatcher.ExtractReferences(content));
    }

    [Fact]
    public void ExtractReferences_RejectsDuplicatePlacementIds()
    {
        var content = """
            {
              "settings": {},
              "content": [
                { "id": "same", "type": "componentRef", "componentRef": "component-1" },
                { "id": "same", "type": "componentRef", "componentRef": "component-2" }
              ]
            }
            """;

        Assert.Throws<InvalidDataException>(() =>
            PageBuilderLinkedComponentReferenceMatcher.ExtractReferences(content));
    }

    [Theory]
    [InlineData("{ \"settings\": {}, \"content\": [{ \"id\": \"same\", \"type\": \"hero\" }, { \"id\": \"same\", \"type\": \"componentRef\", \"componentRef\": \"component\" }] }")]
    [InlineData("{ \"settings\": {}, \"content\": [{ \"id\": \"same\", \"type\": \"componentRef\", \"componentRef\": \"component\" }, { \"id\": \"same\", \"type\": \"hero\" }] }")]
    public void ExtractReferences_RejectsPlacementIdSharedWithOrdinaryTopLevelSection(string content)
    {
        Assert.Throws<InvalidDataException>(() =>
            PageBuilderLinkedComponentReferenceMatcher.ExtractReferences(content));
    }

    [Fact]
    public void ExtractReferences_AllowsDuplicateOrdinaryLegacySectionIds()
    {
        var references = PageBuilderLinkedComponentReferenceMatcher.ExtractReferences(
            "{ \"settings\": {}, \"content\": [{ \"id\": \"same\", \"type\": \"hero\" }, { \"id\": \"same\", \"type\": \"text\" }] }");

        Assert.Empty(references);
    }

    [Fact]
    public void ExtractReferences_PreservesLegacyOrInvalidPayloadCompatibility()
    {
        Assert.Empty(PageBuilderLinkedComponentReferenceMatcher.ExtractReferences("not-json"));
        Assert.Empty(PageBuilderLinkedComponentReferenceMatcher.ExtractReferences("[{\"type\":\"settings\"}]"));
    }

    [Fact]
    public void ValidateComponentContent_RequiresTemplateModelAndRejectsNesting()
    {
        PageBuilderLinkedComponentReferenceMatcher.ValidateComponentContent(
            "{ \"settings\": {}, \"content\": [{ \"id\": \"hero\", \"type\": \"hero\" }] }");

        Assert.Throws<InvalidDataException>(() =>
            PageBuilderLinkedComponentReferenceMatcher.ValidateComponentContent("[]"));
        Assert.Throws<InvalidDataException>(() =>
            PageBuilderLinkedComponentReferenceMatcher.ValidateComponentContent(
                "{ \"settings\": {}, \"content\": [{ \"id\": \"x\", \"type\": \"componentRef\", \"componentRef\": \"nested\" }] }"));
    }

    [Theory]
    [InlineData("{ \"settings\": {}, \"content\": [{ \"id\": \"hero\", \"type\": \"hero\", \"blocks\": {} }] }")]
    [InlineData("{ \"settings\": {}, \"content\": [{ \"id\": 1, \"type\": \"hero\" }] }")]
    [InlineData("{ \"settings\": {}, \"content\": [{ \"type\": \"hero\" }] }")]
    [InlineData("{ \"settings\": {}, \"content\": [{ \"id\": \"hero\" }] }")]
    [InlineData("{ \"settings\": {}, \"content\": [{ \"id\": \"hero\", \"type\": 1 }] }")]
    [InlineData("{ \"settings\": {}, \"content\": [42] }")]
    [InlineData("{ \"settings\": {}, \"content\": [{ \"id\": \"hero\", \"type\": \"hero\", \"blocks\": [42] }] }")]
    public void ValidateComponentContent_RejectsSectionsThatDesignerCannotResolve(string content)
    {
        Assert.Throws<InvalidDataException>(() =>
            PageBuilderLinkedComponentReferenceMatcher.ValidateComponentContent(content));
    }

    [Theory]
    [InlineData("{ \"settings\": {}, \"content\": [{ \"id\": \"same\", \"type\": \"hero\" }, { \"id\": \"same\", \"type\": \"text\" }] }")]
    [InlineData("{ \"settings\": {}, \"content\": [{ \"id\": \"hero\", \"type\": \"hero\", \"blocks\": [{ \"id\": \"same\", \"type\": \"text\" }, { \"id\": \"same\", \"type\": \"image\" }] }] }")]
    public void ValidateComponentContent_RejectsDuplicateSiblingIds(string content)
    {
        Assert.Throws<InvalidDataException>(() =>
            PageBuilderLinkedComponentReferenceMatcher.ValidateComponentContent(content));
    }

    [Fact]
    public void ValidateComponentContent_AllowsSameBlockIdUnderDifferentParents()
    {
        PageBuilderLinkedComponentReferenceMatcher.ValidateComponentContent(
            "{ \"settings\": {}, \"content\": [" +
            "{ \"id\": \"left\", \"type\": \"column\", \"blocks\": [{ \"id\": \"shared\", \"type\": \"text\" }] }," +
            "{ \"id\": \"right\", \"type\": \"column\", \"blocks\": [{ \"id\": \"shared\", \"type\": \"text\" }] }" +
            "] }");
    }
}
