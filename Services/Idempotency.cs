using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics.CodeAnalysis;

namespace Choosr.Web.Services;

public interface IIdempotencyStore
{
    // Issues a token bound to a user and a purpose, with a TTL window
    string CreateToken(string userKey, string purpose, TimeSpan? ttl = null);

    // If the token has a completed result, returns it; ensures token belongs to userKey and purpose matches (if provided)
    bool TryGetResult<T>(string token, string userKey, [MaybeNullWhen(false)] out T result, string? purpose = null);

    // Stores the result for the token; only succeeds if token exists and bound to the same user and (optional) purpose
    bool TrySetResult<T>(string token, string userKey, T result, string? purpose = null);
}

internal class InMemoryIdempotencyStore(IMemoryCache cache) : IIdempotencyStore
{
    private class Entry
    {
        public required string UserKey { get; init; }
        public required string Purpose { get; init; }
        public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
        public bool Completed { get; set; }
        public object? Result { get; set; }
    }

    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(15);

    public string CreateToken(string userKey, string purpose, TimeSpan? ttl = null)
    {
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
        var entry = new Entry { UserKey = userKey, Purpose = purpose, Completed = false, Result = null };
        cache.Set(GetKey(token), entry, ttl ?? DefaultTtl);
        return token;
    }

    public bool TryGetResult<T>(string token, string userKey, [MaybeNullWhen(false)] out T result, string? purpose = null)
    {
        result = default!;
        if (!cache.TryGetValue(GetKey(token), out Entry? entry) || entry == null)
            return false;
        if (!IsOwner(entry, userKey, purpose))
            return false;
        if (!entry.Completed)
            return false;
        if (entry.Result is T t)
        {
            result = t;
            return true;
        }
        try
        {
            // last-resort conversion for simple types
            if (entry.Result is not null)
            {
                result = (T)Convert.ChangeType(entry.Result, typeof(T));
                return true;
            }
        }
        catch { }
        return false;
    }

    public bool TrySetResult<T>(string token, string userKey, T result, string? purpose = null)
    {
        if (!cache.TryGetValue(GetKey(token), out Entry? entry) || entry == null)
            return false;
        if (!IsOwner(entry, userKey, purpose))
            return false;
        entry.Result = result!;
        entry.Completed = true;
        // Refresh TTL a bit to allow duplicates to reuse the result shortly after
        cache.Set(GetKey(token), entry, DefaultTtl);
        return true;
    }

    private static string GetKey(string token) => $"idem:{token}";
    private static bool IsOwner(Entry entry, string userKey, string? purpose)
        => string.Equals(entry.UserKey, userKey, StringComparison.Ordinal) &&
           (purpose == null || string.Equals(entry.Purpose, purpose, StringComparison.Ordinal));
}
