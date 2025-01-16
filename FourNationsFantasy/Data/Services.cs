using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace FourNationsFantasy.Data;

public interface ICacheService
{
    Task<T> GetOrAddAsync<T>(string cacheKey, Func<Task<T>> factory, TimeSpan cacheDuration);
    void Clear();
}

public class CacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private CancellationTokenSource _resetCacheToken = new();
    
    public CacheService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public async Task<T> GetOrAddAsync<T>(string cacheKey, Func<Task<T>> factory, TimeSpan cacheDuration)
    {
        if (!_memoryCache.TryGetValue(cacheKey, out T? cacheEntry))
        {
            cacheEntry = await factory();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = cacheDuration,
            };
            cacheOptions.AddExpirationToken(new CancellationChangeToken(_resetCacheToken.Token));

            _memoryCache.Set(cacheKey, cacheEntry, cacheOptions);
        }

        return cacheEntry!;
    }

    public void Clear()
    {
        _resetCacheToken.Cancel();
        _resetCacheToken.Dispose();
        _resetCacheToken = new CancellationTokenSource();
    }
}
