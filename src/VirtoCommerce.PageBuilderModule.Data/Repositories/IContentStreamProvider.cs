namespace VirtoCommerce.PageBuilderModule.Data.Repositories;

public interface IContentStreamRepository
{
    //public Task StreamContentAsTextAsync(string pageId, StreamWriter writer);
    Task SaveBinaryAsync(string pageId, TextReader reader, CancellationToken cancellationToken = default);
    Task LoadBinaryAsync(string pageId, TextWriter writer, CancellationToken cancellationToken = default);
}
