namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderSharedComponentResolver
{
    Task<string> ResolveAsync(string content, CancellationToken cancellationToken = default);
}
