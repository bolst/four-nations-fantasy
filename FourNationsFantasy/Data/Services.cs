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
    private readonly DateOnly FirstDate = new DateOnly(2025, 02, 12);
    private readonly DateOnly LastDate = new DateOnly(2025, 02, 17);
    private Dictionary<string, double> FantasyPointCache = new();
    
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

    public double CalculatePlayerGameScore(Nhl.Api.Models.Game.PlayerGameLog gameLog)
    {
        return gameLog.Goals * Goal
               + gameLog.Assists * Assist
               + gameLog.PowerPlayPoints * PowerplayPoint
               + gameLog.ShorthandedPoints * ShorthandedPoint
               + gameLog.Shots * ShotOnGoal;
    }    
    
    public double CalculateGoalieGameScore(Nhl.Api.Models.Game.GoalieGameLog gameLog)
    {
        return (gameLog.Decision is not null && gameLog.Decision.Equals("W") ? 1 : 0) * GoalieWin
            + gameLog.GoalsAgainst * GoalieGoalAgainst
            + (gameLog.ShotsAgainst - gameLog.GoalsAgainst) * GoalieSave
            + gameLog.Shutouts * GoalieShutout;
    }

    public double CalculatePlayerSeasonScore(FNFPlayer player)
    {
        if (FantasyPointCache.TryGetValue(player.NhlId, out double score))
        {
            return score;
        }
        
        if (player.Position == "G")
        {
            score = CalculateGoalieSeasonScore(player);
        }
        else
        {
            score = CalculateSkaterSeasonScore(player);
        }
        
        FantasyPointCache.Add(player.NhlId, score);

        return score;
    }

    private double CalculateSkaterSeasonScore(FNFPlayer player)
    {
        return player.Goals * Goal
               + player.Assists * Assist
               + player.PowerplayPoints * PowerplayPoint
               + player.ShorthandedPoints * ShorthandedPoint
               + player.SOG * ShotOnGoal
               + player.Hits * Hit
               + player.Blocks * Block; 
    }
    
    private double CalculateGoalieSeasonScore(FNFPlayer player)
    {
        return player.GoalieWins * GoalieWin
               + player.GoalieGoalsAgainst * GoalieGoalAgainst
               + player.GoalieSaves * GoalieSave
               + player.GoalieShutouts * GoalieShutout;
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