using VirtoCommerce.ContentModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Data.Extensions;

public static class BlobContentStorageProviderExtensions
{
    /// <summary>Reads the text of a blob; <c>null</c> when there is no such blob.</summary>
    public static async Task<string> ReadTextAsync(this IBlobContentStorageProvider storageProvider, string path)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);

        if (await storageProvider.GetBlobInfoAsync(path) == null)
        {
            return null;
        }

        await using var stream = await storageProvider.OpenReadAsync(path);
        using var reader = new StreamReader(stream);

        return await reader.ReadToEndAsync();
    }
}
