using Dapper;
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
    Task<Data.User?> GetUserThatHasPlayerAsync(Data.FNFPlayer player);
    Task<string> DraftPlayerAsync(FNFPlayer player, User user);
    Task<(int, User?)> GetCurrentDraftPickTeamAsync();
    Task UpdateTeamNameAsync(Data.User user, string newName);
    Task<Nhl.Api.Models.Schedule.LeagueSchedule> GetTournamentScheduleAsync();
    Task<(int, int)> GetPlayerGameGuessAsync(int gameId, int userId);
    Task AddPlayerGameGuess(int gameId, int userId, int homeScore, int awayScore);
    Task SimulateDraft();
    Task ResetRosters();
    Task<IEnumerable<FNFPlayer>> GetRosterByCountryAsync(string abbrev);
    Task<Nhl.Api.Models.Game.PlayerSeasonGameLog> GetPlayerTournamentGameLogAsync(FNFPlayer player);
    Task<Nhl.Api.Models.Game.GoalieSeasonGameLog> GetGoalieTournamentGameLogAsync(FNFPlayer player);
}

public class FNFData : QueryDapperBase, IFNFData
{
    private readonly DateOnly FirstDate = new DateOnly(2025, 02, 12);
    private readonly DateOnly LastDate = new DateOnly(2025, 02, 17);

    private const int MAX_FORWARDS = 8;
    private const int MAX_DEFENSE = 4;
    private const int MAX_GOALIES = 2;
    private const int MAX_UTIL = 1;
    
    private readonly Nhl.Api.INhlApi _nhlApi;
    public FNFData(string connectionString, ICacheService cacheService, Nhl.Api.INhlApi nhlApi) : base(connectionString, cacheService)
    {
        _nhlApi = nhlApi;
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        string sql = @"SELECT *
                        FROM
                          accounts
                        WHERE id = @UserId";
        return await QueryDbSingleAsync<User>(sql, new { UserId = userId });
    }    
    
    public async Task<User?> GetUserByEmailAsync(string email)
    {
        string sql = @"SELECT *
                        FROM
                          accounts
                        WHERE email = @Email";
        return await QueryDbSingleAsync<User>(sql, new { Email = email });
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        string sql = @"SELECT *
                        FROM
                          accounts";
        return await QueryDbAsync<User>(sql);
    }
    
    public async Task<IEnumerable<FNFPlayer>> GetAllPlayersAsync()
    {
        string sql = @"SELECT *
                        FROM
                          players";
        return await QueryDbAsync<FNFPlayer>(sql);
    }

    public async Task<IEnumerable<FNFPlayer>> GetRosterAsync(int userId)
    {
        string sql = @"SELECT *
                      FROM
                        players P
                      WHERE
                        P.user_id = @UserId";
        
        return await QueryDbAsync<FNFPlayer>(sql, new { UserId = userId });
    }

    public async Task<Data.User?> GetUserThatHasPlayerAsync(Data.FNFPlayer player)
    {
        string sql = @"SELECT
                          a.*
                        FROM
                          accounts a
                          INNER JOIN players p ON p.user_id = a.id
                          AND nhl_id = @NhlId";
        return await QueryDbSingleAsync<Data.User>(sql, new { NhlId = player.nhl_id });
    }


    /*
        1. Set a constant for # of forwards/defense/goalies/flex allowed per team (ex. MAX_FORWARDS = 8)
            a. 15 players total = 8F, 4D, 2G, 1UTIL 

        2. When user selects a player in the draft (ex. forward):
            a. Check the users roster & count # of forwards
                i. If # forwards >= MAX_FORWARDS, check #UTIL on roster
                       If #UTIL >= 1, throw error
                ii. else, add forward to roster

        obv do this for all positions
    */
    
    public async Task<string> DraftPlayerAsync(FNFPlayer player, User user)
    {
        string draftNumberSql = @"SELECT
                                      COALESCE(MAX(draft_number), 0)
                                    FROM
                                      players p";
        int currentDraftNumber = await QueryDbSingleAsync<Int16>(draftNumberSql);
        int newDraftNumber = currentDraftNumber + 1;
        
        string positionsDraftedSql = @$"WITH
                                          num_forwards AS (
                                            SELECT
                                              COUNT(*) AS nf
                                            FROM
                                              players
                                            WHERE
                                              user_id = @UserId
                                              AND position = 'F'
                                          ),
                                          num_defense AS (
                                            SELECT
                                              COUNT(*) AS nd
                                            FROM
                                              players
                                            WHERE
                                              user_id = @UserId
                                              AND position = 'D'
                                          ),
                                          num_goalies AS (
                                            SELECT
                                              COUNT(*) AS ng
                                            FROM
                                              players
                                            WHERE
                                              user_id = @UserId
                                              AND position = 'G'
                                          )
                                        SELECT
                                          nf, nd, ng
                                        FROM
                                          num_forwards, num_defense, num_goalies";
        (int nF, int nD, int nG) = await QueryDbSingleAsync<(int, int, int)>(positionsDraftedSql,  new { UserId = user.id });

        // if someone already has 2 goalies
        if (player.position == "G" && nG >= MAX_GOALIES)
        {
            return "You cannot draft more than 2 goalies";
        }
        
        // if someone has exceeded total # of players
        if (player.position == "F" || player.position == "D")
        {
            if (nF + nD >= MAX_FORWARDS + MAX_DEFENSE + MAX_UTIL)
            {
                return "You must draft 8 forwards, 4 defensemen and 1 utility";
            }

            if (nF >= MAX_FORWARDS + MAX_UTIL && player.position == "F")
            {
                return "You cannot draft more than 9 forwards";
            }

            if (nD >= MAX_DEFENSE + MAX_UTIL && player.position == "D")
            {
                return "You cannot draft more than 5 defensemen";
            }
        }
        
        string sql = @"UPDATE players
                            SET
                              user_id = @UserId,
                              draft_number = @DraftNumber
                            WHERE
                              nhl_id = @NhlId";
        await ExecuteSqlAsync(sql, new { UserId = user.id, DraftNumber = newDraftNumber, NhlId = player.nhl_id });

        return string.Empty;
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
        
        string sql = @"SELECT *
                        FROM
                          accounts
                        WHERE id = @TeamId";
        Data.User? currentUser = await QueryDbSingleAsync<User>(sql, new { TeamId = currentTeamId });
        
        return (currentDraftNumber, currentUser);
    }

    public async Task UpdateTeamNameAsync(Data.User user, string newName)
    {
        string sql = @"UPDATE accounts
                        SET teamname = @TeamName
                        WHERE id = @UserId";
        await ExecuteSqlAsync(sql, new { UserId = user.id, TeamName = newName });
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

    public async Task<(int, int)> GetPlayerGameGuessAsync(int gameId, int userId)
    {
        string sql = @"SELECT
                          home_score_guess,
                          away_score_guess
                        FROM
                          game_guesses
                        WHERE
                          nhl_game_id = @GameId
                          AND user_id = @UserId";
        return await QueryDbSingleAsync<(int, int)>(sql, new { GameId = gameId, UserId = userId });
    }
    
    public async Task AddPlayerGameGuess(int gameId, int userId, int homeScore, int awayScore)
    {
        string sql = @"INSERT INTO game_guesses (user_id, nhl_game_id, home_score_guess, away_score_guess)
                        VALUES (@UserId, @GameId, @HomeScore, @AwayScore)
                        ON CONFLICT (user_id, nhl_game_id)
                        DO UPDATE SET user_id = @UserId, nhl_game_id = @GameId, home_score_guess = @HomeScore, away_score_guess = @AwayScore";
        await ExecuteSqlAsync(sql, new { GameId = gameId, UserId = userId, HomeScore = homeScore, AwayScore = awayScore });
    }

    public async Task SimulateDraft()
    {
        await ResetRosters();
        
        // get # of players playing in tournament
        var allPlayers = (await GetAllPlayersAsync()).ToList();
        int numPlayers = allPlayers.Count();
        
        var allUsers = (await GetAllUsersAsync()).ToList();
        int numUsers = allUsers.Count();
        
        // generate random list from 1 to numPlayers for the draft number, and partition the user ids uniformly random
        // each entry will also have the drafted player's nhl id
        // list will have entries in the form (nhl id, user id, draft number)
        List<(string, int, int)> usersAndDraftNumbers = new List<(string, int, int)>();
        
        // shuffle players and add each to list
        Random rng = new Random();
        int draftNumber = 1;
        foreach (string nhlId in allPlayers.Select(x => x.nhl_id).OrderBy(_ => rng.Next()))
        {
            // if a round is entered where not everyone gets a selection stop populating
            // e.g., if 92 players are available and 6 users drafting, stop after pick 90
            if (draftNumber > (numPlayers / numUsers) * numUsers)
            {
                break;
            }
            
            usersAndDraftNumbers.Add( (nhlId, allUsers[draftNumber % numUsers].id, draftNumber++) );
        }

        // supposedly this is the only way to do a bulk update w/ Dapper
        // consider Dapper.Contrib to simplify
        foreach (var entry in usersAndDraftNumbers)
        {
            string sql = @"UPDATE players
                               SET user_id = @UserId,
                                   draft_number = @DraftNumber
                               WHERE
                                   nhl_id = @NhlId";
            await ExecuteSqlAsync(sql, new
            {
                NhlId = entry.Item1,
                UserId = entry.Item2,
                DraftNumber = entry.Item3,
            });
        }
    }

    public async Task ResetRosters()
    {
        string sql = @"UPDATE players
                        SET
                          user_id = null,
                          draft_number = null";
        await ExecuteSqlAsync(sql);
    }

    public async Task<IEnumerable<FNFPlayer>> GetRosterByCountryAsync(string abbrev)
    {
        string sql = @"SELECT
                          *
                        FROM
                          players
                        WHERE
                          nationality = @Abbrev";
        return await QueryDbAsync<FNFPlayer>(sql, new { Abbrev = abbrev });
    }

    public async Task<Nhl.Api.Models.Game.PlayerSeasonGameLog> GetPlayerTournamentGameLogAsync(FNFPlayer player)
    {
        var fnGameType = (Nhl.Api.Enumerations.Game.GameType)19;
        var playerGameLog = await _nhlApi.GetPlayerSeasonGameLogsBySeasonAndGameTypeAsync(player.NhlIdInt, "20242025", fnGameType);
        
        if (playerGameLog.PlayerGameLogs.Count != 0)
        {
            return playerGameLog;
        }

        var schedule = await GetTournamentScheduleAsync();
        
        var gameLogs = new List<Nhl.Api.Models.Game.PlayerGameLog>();
        foreach (var week in schedule.GameWeek)
        {
            foreach (var game in week.Games)
            {
                if (game.HomeTeam.Abbrev == player.nationality)
                {
                    gameLogs.Add(new()
                    {
                        GameId = game.Id,
                        TeamAbbrev = game.HomeTeam.Abbrev,
                        HomeRoadFlag = "H",
                        GameDate = game.StartTimeUTC.ToString("yyyy-MM-dd"),
                        OpponentAbbrev = game.AwayTeam.Abbrev,
                        Toi = "-"
                    });
                }
                else if (game.AwayTeam.Abbrev == player.nationality)
                {
                    gameLogs.Add(new()
                    {
                        GameId = game.Id,
                        TeamAbbrev = game.AwayTeam.Abbrev,
                        HomeRoadFlag = "R",
                        GameDate = game.StartTimeUTC.ToString("yyyy-MM-dd"),
                        OpponentAbbrev = game.HomeTeam.Abbrev,
                        Toi = "-"
                    });
                }
            }
        }
        
        Nhl.Api.Models.Game.PlayerSeasonGameLog emptyGameLog = new()
        {
            SeasonId = 20242025,
            GameTypeId = 19,
            PlayerGameLogs = gameLogs
        };

        return emptyGameLog;
    }    
    
    public async Task<Nhl.Api.Models.Game.GoalieSeasonGameLog> GetGoalieTournamentGameLogAsync(FNFPlayer player)
    {
        var fnGameType = (Nhl.Api.Enumerations.Game.GameType)19;
        var playerGameLog = await _nhlApi.GetGoalieSeasonGameLogsBySeasonAndGameTypeAsync(player.NhlIdInt, "20242025", fnGameType);
        
        if (playerGameLog.GoalieGameLogs.Count != 0)
        {
            return playerGameLog;
        }

        var schedule = await GetTournamentScheduleAsync();
        
        var gameLogs = new List<Nhl.Api.Models.Game.GoalieGameLog>();
        foreach (var week in schedule.GameWeek)
        {
            foreach (var game in week.Games)
            {
                if (game.HomeTeam.Abbrev == player.nationality)
                {
                    gameLogs.Add(new()
                    {
                        GameId = game.Id,
                        TeamAbbrev = game.HomeTeam.Abbrev,
                        HomeRoadFlag = "H",
                        GameDate = game.StartTimeUTC.ToString("yyyy-MM-dd"),
                        OpponentAbbrev = game.AwayTeam.Abbrev,
                        Decision = "-",
                        Toi = "-",
                    });
                }
                else if (game.AwayTeam.Abbrev == player.nationality)
                {
                    gameLogs.Add(new()
                    {
                        GameId = game.Id,
                        TeamAbbrev = game.AwayTeam.Abbrev,
                        HomeRoadFlag = "R",
                        GameDate = game.StartTimeUTC.ToString("yyyy-MM-dd"),
                        OpponentAbbrev = game.HomeTeam.Abbrev,
                        Decision = "-",
                        Toi = "-",
                    });
                }
            }
        }
        
        Nhl.Api.Models.Game.GoalieSeasonGameLog emptyGameLog = new()
        {
            SeasonId = 20242025,
            GameTypeId = 19,
            GoalieGameLogs = gameLogs
        };

        return emptyGameLog;
    }
}

