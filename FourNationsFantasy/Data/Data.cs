using System.Security.Claims;
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
}

public interface IFNFData
{
    Task<User?> GetUserByIdAsync(int userId);
    Task<User?> GetUserByEmailAsync(string email);
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task<IEnumerable<FNFPlayer>> GetAllPlayersAsync();
    Task<IEnumerable<FNFPlayer>> GetRosterAsync(int userId);
    Task<IEnumerable<FNFPlayer>> GetDraftAvailablePlayersAsync();
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
                          nationality AS Nationality
                        FROM
                          players";
        return await QueryDbAsync<FNFPlayer>(sql);
    }

    public async Task<IEnumerable<FNFPlayer>> GetRosterAsync(int userId)
    {
        string sql = @"SELECT
                          R.nhl_id AS NhlId,
                          P.firstname AS FirstName,
                          P.lastname AS LastName,
                          P.position AS Position,
                          P.nationality AS Nationality
                        FROM
                          roster R
                        JOIN
                        players P ON R.nhl_id = P.nhl_id
                        WHERE
                          R.account_id = @UserId";
        return await QueryDbAsync<FNFPlayer>(sql, new { UserId = userId });
    }

    public async Task<IEnumerable<FNFPlayer>> GetDraftAvailablePlayersAsync()
    {
        string sql = @"SELECT
                          P.nhl_id AS NhlId,
                          P.firstname AS FirstName,
                          P.lastname AS LastName,
                          P.position AS Position,
                          P.nationality AS Nationality
                        FROM
                          players P
                        WHERE
                          P.nhl_id NOT IN (
                            SELECT
                              nhl_id
                            FROM
                              roster)";
        return await QueryDbAsync<FNFPlayer>(sql);
    }
}


public class FNFPlayer : IEquatable<FNFPlayer>
{
    public string NhlId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Position { get; set; }
    public string Nationality { get; set; }

    public int NhlIdInt => int.Parse(NhlId);
    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        FNFPlayer player = obj as FNFPlayer;
        if (player is null) return false;
        else return Equals(player);
    }

    public override int GetHashCode()
    {
        return int.Parse(NhlId);
    }

    public bool Equals(FNFPlayer other)
    {
        if (other is null) return false;
        return (this.NhlId == other.NhlId);
    }
}

public class User
{
    public int Id { get; set; }
    public string Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? TeamName { get; set; }

    public ClaimsPrincipal ToClaimsPrincipal()
    {
        return new(new ClaimsIdentity(new Claim[]
        {
            new (ClaimTypes.Sid, Id.ToString()),
            new (ClaimTypes.Name, Email),
            new (nameof(FirstName), FirstName),
            new (nameof(LastName), LastName),
            new (nameof(TeamName), TeamName)
        }, "FNF"));
    }

    public static User FromClaimsPrincipal(ClaimsPrincipal principal) => new()
    {
        Id = int.Parse(principal.FindFirstValue(ClaimTypes.Sid)),
        Email = principal.FindFirstValue(ClaimTypes.Email),
        FirstName = principal.FindFirstValue(nameof(FirstName)),
        LastName = principal.FindFirstValue(nameof(LastName)),
        TeamName = principal.FindFirstValue(nameof(TeamName))
    };
}

