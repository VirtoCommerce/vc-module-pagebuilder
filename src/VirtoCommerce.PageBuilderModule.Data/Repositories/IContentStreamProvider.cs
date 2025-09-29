namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

public interface IContentStreamRepository
{
    Task SaveBinaryAsync(string pageId, TextReader reader, CancellationToken cancellationToken = default);
    Task LoadBinaryAsync(string pageId, TextWriter writer, CancellationToken cancellationToken = default);
}
