namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IPageBuilderLinkedComponentResolver
{
    Task<string> ResolveAsync(string content, CancellationToken cancellationToken = default);
}
