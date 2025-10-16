using Microsoft.Extensions.Caching.Memory;

namespace Choosr.Web.Services;

public interface ITagStatsInvalidator
{
    void InvalidateAll();
}

public class TagStatsInvalidator : ITagStatsInvalidator
{
    private readonly IMemoryCache _cache;
    private const string VER_KEY = "tagstats:ver";
    private static readonly object _lock = new();

    public TagStatsInvalidator(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void InvalidateAll()
    {
        lock(_lock)
        {
            var ver = _cache.TryGetValue<long>(VER_KEY, out var current) ? current : 1L;
            _cache.Set(VER_KEY, ver + 1L, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) });
        }
    }

    internal static long GetVersion(IMemoryCache cache)
    {
        // Long-lived version key; bumped on invalidations
        if(!cache.TryGetValue<long>(VER_KEY, out var ver))
        {
            ver = 1L;
            cache.Set(VER_KEY, ver, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) });
        }
        return ver;
    }
}
