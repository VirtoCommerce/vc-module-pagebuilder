using VirtoCommerce.PageBuilderModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Data.ExportImport;

public sealed class PageBuilderLinkedComponentExportImportDependencies(
    IPageBuilderLinkedComponentService service,
    IPageBuilderLinkedComponentSearchService searchService,
    IPageBuilderLinkedComponentContentService contentService)
{
    public IPageBuilderLinkedComponentService ComponentService { get; } = service;

    public IPageBuilderLinkedComponentSearchService SearchService { get; } = searchService;

    public IPageBuilderLinkedComponentContentService ContentService { get; } = contentService;
}
