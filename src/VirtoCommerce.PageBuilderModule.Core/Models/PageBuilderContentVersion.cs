using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public static class PageBuilderContentVersion
{
    private const int HashLength = 64;
    private const int SurroundingQuoteCount = 2;
    private static readonly SearchValues<char> HexCharacters = SearchValues.Create("0123456789ABCDEF");

    public static bool IsWellFormed(string eTag)
    {
        return eTag is { Length: HashLength + SurroundingQuoteCount } && eTag[0] == '"' && eTag[^1] == '"'
            && !eTag.AsSpan(1, HashLength).ContainsAnyExcept(HexCharacters);
    }

    // Bind the content to the authorized aggregate, including its lifetime and store.
    // NULL (never seeded) and an intentionally empty document must have distinct versions.
    public static string Create(GroupedPageBuilderPage group, string content)
    {
        return content == null ? CreateFromContentHash(group, null) : CreateFromUtf8(group, Encoding.UTF8.GetBytes(content));
    }

    public static string CreateFromUtf8(GroupedPageBuilderPage group, ReadOnlySpan<byte> content)
    {
        return CreateFromContentHash(group, HashContent(content));
    }

    /// <summary>A theme file's version: of its content in the store's theme file at the path; a file that does not exist (null) has its own.</summary>
    public static string CreateForThemeFile(string storeId, string path, string content)
    {
        return Hash(new
        {
            StoreId = storeId,
            ThemeFile = path,
            ContentHash = content == null ? null : HashContent(Encoding.UTF8.GetBytes(content)),
        });
    }

    private static string CreateFromContentHash(GroupedPageBuilderPage group, string contentHash)
    {
        return Hash(new
        {
            group.Id,
            group.StoreId,
            CreatedDateTicks = group.CreatedDate.Ticks,
            ContentHash = contentHash,
        });
    }

    private static string HashContent(ReadOnlySpan<byte> content) => Convert.ToHexString(SHA256.HashData(content));

    private static string Hash<T>(T identity) => $"\"{HashContent(JsonSerializer.SerializeToUtf8Bytes(identity))}\"";
}
