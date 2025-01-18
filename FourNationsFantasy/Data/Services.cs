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

public class ScoreCalculationService
{

    public const double Goal = 3;
    public const double Assist = 1;
    public const double PowerplayPoint = 0.5;
    public const double ShorthandedPoint = 0.5;
    public const double ShotOnGoal = 0.1;
    public const double Hit = 0.1;
    public const double Block = 0.5;
    public const double GoalieWin = 4;
    public const double GoalieGoalAgainst = -2;
    public const double GoalieSave = 0.2;
    public const double GoalieShutout = 5;
    
    public double CalculatePlayerScore(Nhl.Api.Models.Player.PlayerProfile profile)
    {
        // TODO
        return 0.0;
    }    
    
    public double CalculateGoalieScore(Nhl.Api.Models.Player.PlayerProfile profile)
    {
        // TODO
        return 0.0;
    }

    public IEnumerable<(string, double)> Categories()
    {
        yield return ("Goal", Goal);
        yield return ("Assist", Assist);
        yield return ("Powerplay Point", PowerplayPoint);
        yield return ("Shorthanded Point", ShorthandedPoint);
        yield return ("Shot on Goal", ShotOnGoal);
        yield return ("Hit", Hit);
        yield return ("Block", Block);
        yield return ("Goalie Win", GoalieWin);
        yield return ("Goalie Goal Against", GoalieGoalAgainst);
        yield return ("Goalie Save", GoalieSave);
        yield return ("Goalie Shutout", GoalieShutout);
    }
}