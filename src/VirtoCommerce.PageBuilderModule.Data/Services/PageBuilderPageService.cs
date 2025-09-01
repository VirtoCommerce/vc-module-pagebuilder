using System.Text;
using VirtoCommerce.PageBuilderModule.Core.Events;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Data.Models;
using VirtoCommerce.PageBuilderModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Data.GenericCrud;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PageBuilderPageService(
    Func<IPageBuilderModuleRepository> repositoryFactory,
    Func<IContentStreamRepository> contentStreamRepositoryFactory,
    IPlatformMemoryCache platformMemoryCache,
    IEventPublisher eventPublisher)
    : CrudService<PageBuilderPage, PageBuilderPageEntity, PageBuilderPageChangingEvent, PageBuilderPageChangedEvent>(
        repositoryFactory, platformMemoryCache, eventPublisher), IPageBuilderPageService
{
    protected override Task<IList<PageBuilderPageEntity>> LoadEntities(IRepository repository, IList<string> ids, string responseGroup)
    {
        return ((IPageBuilderModuleRepository)repository).GetPageBuilderPagesByIdsAsync(ids, responseGroup);
    }

    public async Task<string> GetPageContentAsync(string pageId, CancellationToken cancellationToken = default)
    {
        var repository = contentStreamRepositoryFactory();
        using var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: ContentStreamRepository.ContentBufferSize, leaveOpen: true);
        await repository.LoadBinaryAsync(pageId, writer, cancellationToken);
        await writer.FlushAsync(cancellationToken);
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
