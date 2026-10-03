using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public static class PageBuilderContentVersion
{
    // Bind the content to the authorized aggregate, including its lifetime and store.
    // NULL (never seeded) and an intentionally empty document must have distinct versions.
    public static string Create(GroupedPageBuilderPage group, string content)
    {
        if (content == null)
        {
            return CreateFromContentHash(group, null);
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var encoder = Encoding.UTF8.GetEncoder();
        Span<byte> buffer = stackalloc byte[4096];
        var remaining = content.AsSpan();
        bool completed;
        do
        {
            encoder.Convert(remaining, buffer, flush: true, out var charsUsed, out var bytesUsed, out completed);
            hash.AppendData(buffer[..bytesUsed]);
            remaining = remaining[charsUsed..];
        } while (!completed);

        return CreateFromContentHash(group, Convert.ToHexString(hash.GetHashAndReset()));
    }

    public static string CreateFromUtf8(GroupedPageBuilderPage group, ReadOnlySpan<byte> content)
    {
        return CreateFromContentHash(group, Convert.ToHexString(SHA256.HashData(content)));
    }

    private static string CreateFromContentHash(GroupedPageBuilderPage group, string contentHash)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            group.Id,
            group.StoreId,
            CreatedDateTicks = group.CreatedDate.Ticks,
            ContentHash = contentHash,
        });
        return $"\"{Convert.ToHexString(SHA256.HashData(bytes))}\"";
    }
}

public sealed class PageBuilderContentConflictException() : Exception("This page changed while you were editing. Your changes have not been saved. Copy your changes before reloading the page.");

public sealed record PageBuilderConditionalContentWriteResult(string PageId, string ETag);
