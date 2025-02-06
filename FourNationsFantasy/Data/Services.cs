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

    public double CalculatePlayerSeasonGameScore(Nhl.Api.Models.Game.PlayerGameLog gameLog)
    {
        return gameLog.Goals * Goal
               + gameLog.Assists * Assist
               + gameLog.PowerPlayPoints * PowerplayPoint
               + gameLog.ShorthandedPoints * ShorthandedPoint
               + gameLog.Shots * ShotOnGoal;
    }    
    
    public double CalculateGoalieSeasonGameScore(Nhl.Api.Models.Game.GoalieGameLog gameLog)
    {
        return (gameLog.Decision is not null && gameLog.Decision.Equals("W") ? 1 : 0) * GoalieWin
            + gameLog.GoalsAgainst * GoalieGoalAgainst
            + (gameLog.ShotsAgainst - gameLog.GoalsAgainst) * GoalieSave
            + gameLog.Shutouts * GoalieShutout;
    }

    public double CalculatePlayerSeasonScore(FNFPlayer player)
    {
        return player.position.Equals("G") ? CalculateGoalieSeasonScore(player) : CalculateSkaterSeasonScore(player);
    }

    private double CalculateSkaterSeasonScore(FNFPlayer player)
    {
        return player.goals * Goal
               + player.assists * Assist
               + player.pp_points * PowerplayPoint
               + player.sh_points * ShorthandedPoint
               + player.shots_on_goal * ShotOnGoal
               + player.hits * Hit
               + player.blocks * Block; 
    }
    
    private double CalculateGoalieSeasonScore(FNFPlayer player)
    {
        return player.goalie_wins * GoalieWin
               + player.goalie_goals_against * GoalieGoalAgainst
               + player.goalie_saves * GoalieSave
               + player.goalie_shutouts * GoalieShutout;
    }
    
    public double CalculatePlayerTournamentScore(FNFPlayer player)
    {
        return player.position.Equals("G") ? CalculateGoalieTournamentScore(player) : CalculateSkaterTournamentScore(player);
    }

    private double CalculateSkaterTournamentScore(FNFPlayer player)
    {
        return 0;
    }
    
    private double CalculateGoalieTournamentScore(FNFPlayer player)
    {
        return 0;
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