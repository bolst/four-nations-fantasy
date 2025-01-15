using Dapper;
using Npgsql;

namespace FourNationsFantasy.Data;

public abstract class QueryDapperBase
{
    protected readonly string _connectionString;
    private readonly IServiceProvider _serviceProvider;

    public QueryDapperBase(string connectionString, IServiceProvider serviceProvider)
    {
        _connectionString = connectionString;
        _serviceProvider = serviceProvider;
    }

    protected async Task<IEnumerable<T>> QueryDbAsync<T>(string query, object? param = null)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        return await connection.QueryAsync<T>(query, param);
    }

    protected async Task<T?> QueryDbSingleAsync<T>(string query, object? param = null)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<T>(query, param);
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
    Task<IEnumerable<FNFPlayer>> GetDraftAvailablePlayersAsync();
    Task DraftPlayerAsync(FNFPlayer player, User user);
    Task<(int, User?)> GetCurrentDraftPickTeamAsync();
}

public class FNFData : QueryDapperBase, IFNFData
{
    public FNFData(string connectionString, IServiceProvider serviceProvider) : base(connectionString, serviceProvider) {}

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        string sql = @"SELECT
                          id AS Id,
                          email AS Email,
                          firstname AS FirstName,
                          lastname AS LastName,
                          teamname AS TeamName
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
                          teamname AS TeamName
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
                          teamname AS TeamName
                        FROM
                          accounts";
        return await QueryDbAsync<User>(sql);
    }
    
    public async Task<IEnumerable<FNFPlayer>> GetAllPlayersAsync()
    {
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
        return await QueryDbAsync<FNFPlayer>(sql);
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

    public async Task<IEnumerable<FNFPlayer>> GetDraftAvailablePlayersAsync()
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
                        P.user_id IS NULL";
        return await QueryDbAsync<FNFPlayer>(sql);
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
}

