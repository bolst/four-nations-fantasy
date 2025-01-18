using Dapper;
using Nhl.Api.Enumerations.Game;
using Nhl.Api.Models.Player;
using Npgsql;

namespace FourNationsFantasy.Data;

public abstract class QueryDapperBase
{
    private readonly string _connectionString;
    protected readonly ICacheService CacheService;
    protected readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(20);

    public QueryDapperBase(string connectionString, ICacheService cacheService)
    {
        _connectionString = connectionString;
        CacheService = cacheService;
    }

    protected async Task<IEnumerable<T>> QueryDbAsync<T>(string query, object? param = null)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        return await connection.QueryAsync<T>(query, param);
    }

    protected async Task<IEnumerable<T>> QueryDbWithCacheAsync<T>(string cacheKey, string query, object? param = null)
    {
        return await CacheService.GetOrAddAsync(cacheKey, async () => await QueryDbAsync<T>(query, param), CacheDuration);
    }
    
    protected async Task<T?> QueryDbSingleAsync<T>(string query, object? param = null)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<T>(query, param);
    }

    protected async Task<T?> QueryDbSingleWithCacheAsync<T>(string cacheKey, string query, object? param = null)
    {
        return await CacheService.GetOrAddAsync(cacheKey, async () => await QueryDbSingleAsync<T>(query, param), CacheDuration);
    }
    
    protected async Task ExecuteSqlAsync(string query, object? param = null)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync(query, param);
    }
}

public interface IFNFData
{
    Task<User?> GetUserByIdAsync(int userId);
    Task<User?> GetUserByEmailAsync(string email);
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task<IEnumerable<FNFPlayer>> GetAllPlayersAsync();
    Task<IEnumerable<FNFPlayer>> GetRosterAsync(int userId);
    Task<IEnumerable<(Data.FNFPlayer, List<Nhl.Api.Models.Game.PlayerGameLog>)>> GetRosterPlayerTournamentGameLogsAsync(int userId);
    Task<IEnumerable<(Data.FNFPlayer, List<Nhl.Api.Models.Game.GoalieGameLog>)>> GetRosterGoalieTournamentGameLogsAsync(int userId);
    Task DraftPlayerAsync(FNFPlayer player, User user);
    Task<(int, User?)> GetCurrentDraftPickTeamAsync();
    Task<Nhl.Api.Models.Player.PlayerProfile?> GetPlayerProfileByIdAsync(int nhlId);
    Task<Nhl.Api.Models.Player.GoalieProfile?> GetGoalieProfileByIdAsync(int nhlId);
    Task UpdateTeamNameAsync(Data.User user, string newName);
    Task<Nhl.Api.Models.Schedule.LeagueSchedule> GetTournamentScheduleAsync();
}

public class FNFData : QueryDapperBase, IFNFData
{
    private readonly DateOnly FirstDate = new DateOnly(2025, 02, 12);
    private readonly DateOnly LastDate = new DateOnly(2025, 02, 17);
    
    private readonly Nhl.Api.INhlApi _nhlApi;
    public FNFData(string connectionString, ICacheService cacheService, Nhl.Api.INhlApi nhlApi) : base(connectionString, cacheService)
    {
        _nhlApi = nhlApi;
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        string sql = @"SELECT
                          id AS Id,
                          email AS Email,
                          firstname AS FirstName,
                          lastname AS LastName,
                          teamname AS TeamName,
                          role AS Role
                        FROM
                          accounts
                        WHERE id = @UserId";
        return await QueryDbSingleAsync<User>(sql, new { UserId = userId });
    }    
    
    public async Task<User?> GetUserByEmailAsync(string email)
    {
        string sql = @"SELECT
                          id AS Id,
                          email AS Email,
                          firstname AS FirstName,
                          lastname AS LastName,
                          teamname AS TeamName,
                          role AS Role
                        FROM
                          accounts
                        WHERE email = @Email";
        return await QueryDbSingleAsync<User>(sql, new { Email = email });
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        string sql = @"SELECT
                          id AS Id,
                          email AS Email,
                          firstname AS FirstName,
                          lastname AS LastName,
                          teamname AS TeamName,
                          role AS Role
                        FROM
                          accounts";
        return await QueryDbAsync<User>(sql);
    }
    
    public async Task<IEnumerable<FNFPlayer>> GetAllPlayersAsync()
    {
        string cacheKey = "all_players";
        
        string sql = @"SELECT
                          nhl_id AS NhlId,
                          firstname AS FirstName,
                          lastname AS LastName,
                          position AS Position,
                          nationality AS Nationality,
                          user_id AS UserId,
                          draft_number AS DraftNumber
                        FROM
                          players";
        return await QueryDbWithCacheAsync<FNFPlayer>(cacheKey, sql);
    }

    public async Task<IEnumerable<FNFPlayer>> GetRosterAsync(int userId)
    {
        string sql = @"SELECT
                        P.nhl_id AS NhlId,
                        P.firstname AS FirstName,
                        P.lastname AS LastName,
                        P.position AS Position,
                        P.nationality AS Nationality,
                        P.user_id AS UserId,
                        P.draft_number AS DraftNumber
                      FROM
                        players P
                      WHERE
                        P.user_id = @UserId";
        
        return await QueryDbAsync<FNFPlayer>(sql, new { UserId = userId });
    }

    public async Task<IEnumerable<(Data.FNFPlayer, List<Nhl.Api.Models.Game.PlayerGameLog>)>> GetRosterPlayerTournamentGameLogsAsync(int userId)
    {
        var roster = (await GetRosterAsync(userId)).ToList();

        List<(Data.FNFPlayer, List<Nhl.Api.Models.Game.PlayerGameLog>)> gameLogs = new();

        foreach (var player in roster.Where(x => x.Position != "G"))
        {
            var gameLog = (await _nhlApi.GetPlayerSeasonGameLogsBySeasonAndGameTypeAsync(player.NhlIdInt, "20242025",
                Nhl.Api.Enumerations.Game.GameType.RegularSeason)).PlayerGameLogs;

            gameLog = gameLog.Where(g =>
            {
                var gameDate = DateOnly.Parse(g.GameDate);
                return gameDate > FirstDate && gameDate < LastDate;
            }).ToList();
            
            gameLogs.Add((player, gameLog));
        }

        return gameLogs;
    }
    
    public async Task<IEnumerable<(Data.FNFPlayer, List<Nhl.Api.Models.Game.GoalieGameLog>)>> GetRosterGoalieTournamentGameLogsAsync(int userId)
    {
        var roster = (await GetRosterAsync(userId)).ToList();

        List<(Data.FNFPlayer, List<Nhl.Api.Models.Game.GoalieGameLog>)> gameLogs = new();

        foreach (var player in roster.Where(x => x.Position == "G"))
        {
            var gameLog = (await _nhlApi.GetGoalieSeasonGameLogsBySeasonAndGameTypeAsync(player.NhlIdInt, "20242025",
                Nhl.Api.Enumerations.Game.GameType.RegularSeason)).GoalieGameLogs;
            gameLogs.Add((player, gameLog));
        }

        return gameLogs;
    }

    public async Task DraftPlayerAsync(FNFPlayer player, User user)
    {
        string draftNumberSql = @"SELECT
                                      COALESCE(MAX(draft_number), 0)
                                    FROM
                                      players p";
        int currentDraftNumber = await QueryDbSingleAsync<Int16>(draftNumberSql);
        int newDraftNumber = currentDraftNumber + 1;
        
        string sql = @"UPDATE players
                            SET
                              user_id = @UserId,
                              draft_number = @DraftNumber
                            WHERE
                              nhl_id = @NhlId";
        await ExecuteSqlAsync(sql, new { UserId = user.Id, DraftNumber = newDraftNumber, NhlId = player.NhlId });
    }

    public async Task<(int, User?)> GetCurrentDraftPickTeamAsync()
    {
        string draftNumberSql = @"SELECT
                                      COALESCE(MAX(draft_number), 0)
                                    FROM
                                      players p";
        int currentDraftNumber = (await QueryDbSingleAsync<Int16>(draftNumberSql)) + 1;

        // https://stackoverflow.com/questions/43957015/creating-a-snake-counter
        const int teams = 6;
        int currentTeamId = teams - (int)Math.Abs((currentDraftNumber - 1) % (2 * teams) + 1 - teams - 0.5);
        
        string sql = @"SELECT
                          id AS Id,
                          email AS Email,
                          firstname AS FirstName,
                          lastname AS LastName,
                          teamname AS TeamName
                        FROM
                          accounts
                        WHERE id = @TeamId";
        Data.User? currentUser = await QueryDbSingleAsync<User>(sql, new { TeamId = currentTeamId });
        
        return (currentDraftNumber, currentUser);
    }

    public async Task<Nhl.Api.Models.Player.PlayerProfile?> GetPlayerProfileByIdAsync(int nhlId)
    {
        string cacheKey = $"player_profile_{nhlId}";
        return await CacheService.GetOrAddAsync(cacheKey, async () => await _nhlApi.GetPlayerInformationAsync(nhlId), CacheDuration);
    }    
    
    public async Task<Nhl.Api.Models.Player.GoalieProfile?> GetGoalieProfileByIdAsync(int nhlId)
    {
        string cacheKey = $"goalie_profile_{nhlId}";
        return await CacheService.GetOrAddAsync(cacheKey, async () => await _nhlApi.GetGoalieInformationAsync(nhlId), CacheDuration);
    }

    public async Task UpdateTeamNameAsync(Data.User user, string newName)
    {
        string sql = @"UPDATE accounts
                        SET teamname = @TeamName
                        WHERE id = @UserId";
        await ExecuteSqlAsync(sql, new { UserId = user.Id, TeamName = newName });
    }

    public async Task<Nhl.Api.Models.Schedule.LeagueSchedule> GetTournamentScheduleAsync()
    {
        string cacheKey = $"schedule";

        return await CacheService.GetOrAddAsync(cacheKey, async () =>
        {
            var schedule = await _nhlApi.GetLeagueWeekScheduleByDateAsync(new DateOnly(2025, 02, 12));

            // add rest of tournament to fnSchedule
            var secondWeek = await _nhlApi.GetLeagueWeekScheduleByDateAsync(new DateOnly(2025, 02, 19));
            schedule.GameWeek.AddRange(secondWeek.GameWeek.Take(2));
            
            return schedule;
        }, CacheDuration);
    }

}

